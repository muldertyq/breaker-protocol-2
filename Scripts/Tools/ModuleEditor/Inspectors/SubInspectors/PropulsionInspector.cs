using System;
using System.Collections.Generic;
using System.Text.Json;
using Godot;
using BreakerProtocol.Data.Models;
using BreakerProtocol.Data.Models.Properties;

namespace BreakerProtocol.Tools.ModuleEditor.Inspectors.SubInspectors
{
	public partial class PropulsionInspector : VBoxContainer
	{
		private SpinBox _thrust = null!;
		private SpinBox _torque = null!;
		private SpinBox _boost = null!;

		// 喷口列表与配置
		private ItemList _exhaustList = null!;
		private VBoxContainer _exhaustDetailBox = null!;
		private SpinBox _angleInput = null!;
		private SpinBox _flameLengthInput = null!;
		private SpinBox _flameWidthInput = null!;
		private ColorPickerButton _flameColorPicker = null!;
		private Button _deleteExhaustBtn = null!;

		public event Action? OnValuesChanged;
		public event Action<int>? OnExhaustSelectedInInspector;

		private ModuleDataDefinition? _boundData;
		private int _selectedExhaustIndex = -1;
		private bool _isUpdating = false;

		public override void _Ready()
		{
			BuildUI();
		}

		private void BuildUI()
		{
			AddThemeConstantOverride("separation", 10);

			// 1. 推进物理动力参数卡片
			var (thrustCard, thrustContent) = CreateCard("🚀 推进物理与动力参数", new Color(0.38f, 0.75f, 0.98f));
			AddChild(thrustCard);

			_thrust = CreateNumberRowToParent(thrustContent, "主推力 (N):", 0, 100000, 100);
			_torque = CreateNumberRowToParent(thrustContent, "姿态扭矩 (N·m):", 0, 50000, 50);
			_boost = CreateNumberRowToParent(thrustContent, "加力倍率:", 1.0, 10.0, 0.1);

			// 2. 推进喷口与尾焰等离子卡片
			var (exhaustCard, exhaustContent) = CreateCard("🔥 推进喷口与尾焰 (Exhausts)", new Color(1.0f, 0.65f, 0.3f));
			AddChild(exhaustCard);

			_exhaustList = new ItemList
			{
				CustomMinimumSize = new Vector2(0, 85),
				SizeFlagsHorizontal = SizeFlags.ExpandFill,
				SelectMode = ItemList.SelectModeEnum.Single
			};
			var listStyle = new StyleBoxFlat
			{
				BgColor = new Color(0.08f, 0.09f, 0.12f, 0.85f),
				BorderColor = new Color(0.20f, 0.24f, 0.32f, 0.7f),
				BorderWidthBottom = 1,
				BorderWidthLeft = 1,
				BorderWidthRight = 1,
				BorderWidthTop = 1,
				CornerRadiusBottomLeft = 4,
				CornerRadiusBottomRight = 4,
				CornerRadiusTopLeft = 4,
				CornerRadiusTopRight = 4,
				ContentMarginBottom = 4,
				ContentMarginLeft = 6,
				ContentMarginRight = 6,
				ContentMarginTop = 4
			};
			_exhaustList.AddThemeStyleboxOverride("panel", listStyle);
			_exhaustList.ItemSelected += OnExhaustListItemSelected;
			exhaustContent.AddChild(_exhaustList);

			_exhaustDetailBox = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
			_exhaustDetailBox.AddThemeConstantOverride("separation", 6);
			exhaustContent.AddChild(_exhaustDetailBox);

			_angleInput = CreateNumberRowToParent(_exhaustDetailBox, "喷射角度 (°):", 0, 360, 5);
			_flameLengthInput = CreateNumberRowToParent(_exhaustDetailBox, "尾焰长度 (px):", 10, 400, 5);
			_flameWidthInput = CreateNumberRowToParent(_exhaustDetailBox, "喷口宽度 (px):", 4, 120, 2);

			var colorHBox = new HBoxContainer();
			colorHBox.AddChild(new Label { Text = "尾焰等离子色:", CustomMinimumSize = new Vector2(100, 0) });
			_flameColorPicker = new ColorPickerButton 
			{ 
				SizeFlagsHorizontal = SizeFlags.ExpandFill, 
				CustomMinimumSize = new Vector2(0, 26) 
			};
			_flameColorPicker.ColorChanged += _ => EmitChange();
			colorHBox.AddChild(_flameColorPicker);
			_exhaustDetailBox.AddChild(colorHBox);

			_deleteExhaustBtn = new Button 
			{ 
				Text = "🗑️ 删除当前喷口 (或画布右键)", 
				CustomMinimumSize = new Vector2(0, 28) 
			};
			_deleteExhaustBtn.Pressed += DeleteCurrentExhaust;
			_exhaustDetailBox.AddChild(_deleteExhaustBtn);

			_exhaustDetailBox.Visible = false;
		}

		public void BindData(ModuleDataDefinition data, int selectExhaustIndex = -1)
		{
			_boundData = data;
			_isUpdating = true;

			var prop = data.GetProperties<PropulsionProperties>() ?? new PropulsionProperties();
			_thrust.Value = prop.ThrustForce;
			_torque.Value = prop.TorquePower;
			_boost.Value = prop.BoostMultiplier;

			RefreshExhaustList();

			if (data.ExhaustPoints != null && data.ExhaustPoints.Length > 0)
			{
				if (selectExhaustIndex >= 0 && selectExhaustIndex < data.ExhaustPoints.Length)
				{
					_selectedExhaustIndex = selectExhaustIndex;
				}
				else if (_selectedExhaustIndex < 0 || _selectedExhaustIndex >= data.ExhaustPoints.Length)
				{
					_selectedExhaustIndex = 0;
				}

				if (_selectedExhaustIndex < _exhaustList.ItemCount)
				{
					_exhaustList.Select(_selectedExhaustIndex);
				}
				BindSelectedExhaustDetail();
				_exhaustDetailBox.Visible = true;
			}
			else
			{
				_selectedExhaustIndex = -1;
				_exhaustDetailBox.Visible = false;
			}

			_isUpdating = false;
		}

		public void SelectExhaustExternal(int index)
		{
			if (_boundData?.ExhaustPoints == null || index < 0 || index >= _boundData.ExhaustPoints.Length)
			{
				_selectedExhaustIndex = -1;
				_exhaustDetailBox.Visible = false;
				_exhaustList.DeselectAll();
				return;
			}

			_isUpdating = true;
			_selectedExhaustIndex = index;
			RefreshExhaustList();
			if (_selectedExhaustIndex < _exhaustList.ItemCount)
			{
				_exhaustList.Select(_selectedExhaustIndex);
				_exhaustList.EnsureCurrentIsVisible();
			}
			BindSelectedExhaustDetail();
			_exhaustDetailBox.Visible = true;
			_isUpdating = false;
		}

		private void RefreshExhaustList()
		{
			_exhaustList.Clear();
			if (_boundData?.ExhaustPoints == null) return;

			for (int i = 0; i < _boundData.ExhaustPoints.Length; i++)
			{
				var ep = _boundData.ExhaustPoints[i];
				float angleDeg = Mathf.PosMod(Mathf.RadToDeg(Mathf.Atan2(ep.DirY, ep.DirX)) + 90.0f, 360.0f);
				_exhaustList.AddItem($"🚀 #{i} {ep.Id} ({angleDeg:F0}° | L:{ep.FlameLength:F0} W:{ep.FlameWidth:F0})");
			}
		}

		private void BindSelectedExhaustDetail()
		{
			if (_boundData?.ExhaustPoints == null || _selectedExhaustIndex < 0 || _selectedExhaustIndex >= _boundData.ExhaustPoints.Length) return;
			var ep = _boundData.ExhaustPoints[_selectedExhaustIndex];

			float angleDeg = Mathf.PosMod(Mathf.RadToDeg(Mathf.Atan2(ep.DirY, ep.DirX)) + 90.0f, 360.0f);
			_angleInput.Value = angleDeg;
			_flameLengthInput.Value = ep.FlameLength;
			_flameWidthInput.Value = ep.FlameWidth;
			_flameColorPicker.Color = Color.FromHtml(string.IsNullOrEmpty(ep.FlameColorHex) ? "#38bdf8" : ep.FlameColorHex);
		}

		private void OnExhaustListItemSelected(long index)
		{
			_selectedExhaustIndex = (int)index;
			BindSelectedExhaustDetail();
			_exhaustDetailBox.Visible = true;
			OnExhaustSelectedInInspector?.Invoke(_selectedExhaustIndex);
		}

		private void EmitChange()
		{
			if (_isUpdating || _boundData == null) return;

			var prop = _boundData.GetProperties<PropulsionProperties>() ?? new PropulsionProperties();
			prop.ThrustForce = (float)_thrust.Value;
			prop.TorquePower = (float)_torque.Value;
			prop.BoostMultiplier = (float)_boost.Value;
			_boundData.Properties = JsonSerializer.SerializeToElement(prop);

			if (_selectedExhaustIndex >= 0 && _boundData.ExhaustPoints != null && _selectedExhaustIndex < _boundData.ExhaustPoints.Length)
			{
				var ep = _boundData.ExhaustPoints[_selectedExhaustIndex];
				float rad = Mathf.DegToRad((float)_angleInput.Value - 90.0f);
				ep.DirX = Mathf.Cos(rad);
				ep.DirY = Mathf.Sin(rad);
				ep.FlameLength = (float)_flameLengthInput.Value;
				ep.FlameWidth = (float)_flameWidthInput.Value;
				ep.FlameColorHex = $"#{_flameColorPicker.Color.ToHtml(false)}";

				RefreshExhaustList();
				if (_selectedExhaustIndex < _exhaustList.ItemCount)
				{
					_exhaustList.Select(_selectedExhaustIndex);
				}
			}

			OnValuesChanged?.Invoke();
		}

		private void DeleteCurrentExhaust()
		{
			if (_boundData?.ExhaustPoints == null || _selectedExhaustIndex < 0 || _selectedExhaustIndex >= _boundData.ExhaustPoints.Length) return;

			var list = new List<ExhaustPointDefinition>(_boundData.ExhaustPoints);
			list.RemoveAt(_selectedExhaustIndex);
			_boundData.ExhaustPoints = list.ToArray();

			_selectedExhaustIndex = list.Count > 0 ? Mathf.Clamp(_selectedExhaustIndex - 1, 0, list.Count - 1) : -1;
			BindData(_boundData, _selectedExhaustIndex);
			OnValuesChanged?.Invoke();
		}

		private (PanelContainer card, VBoxContainer content) CreateCard(string title, Color accentColor)
		{
			var panel = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
			var style = new StyleBoxFlat
			{
				BgColor = new Color(0.11f, 0.13f, 0.17f, 0.95f),
				BorderColor = new Color(0.22f, 0.26f, 0.35f, 0.8f),
				BorderWidthBottom = 1,
				BorderWidthLeft = 1,
				BorderWidthRight = 1,
				BorderWidthTop = 1,
				CornerRadiusBottomLeft = 6,
				CornerRadiusBottomRight = 6,
				CornerRadiusTopLeft = 6,
				CornerRadiusTopRight = 6,
				ContentMarginBottom = 8,
				ContentMarginLeft = 10,
				ContentMarginRight = 10,
				ContentMarginTop = 8
			};
			panel.AddThemeStyleboxOverride("panel", style);

			var vbox = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
			vbox.AddThemeConstantOverride("separation", 6);

			var header = new Label { Text = title, HorizontalAlignment = HorizontalAlignment.Left };
			header.AddThemeColorOverride("font_color", accentColor);
			header.AddThemeFontSizeOverride("font_size", 13);
			vbox.AddChild(header);
			vbox.AddChild(new HSeparator());

			panel.AddChild(vbox);
			return (panel, vbox);
		}

		private SpinBox CreateNumberRowToParent(Control parent, string labelText, double min, double max, double step)
		{
			var hbox = new HBoxContainer();
			hbox.AddChild(new Label { Text = labelText, CustomMinimumSize = new Vector2(100, 0) });
			var spin = new SpinBox { MinValue = min, MaxValue = max, Step = step, SizeFlagsHorizontal = SizeFlags.ExpandFill };
			spin.ValueChanged += _ => EmitChange();
			hbox.AddChild(spin);
			parent.AddChild(hbox);
			return spin;
		}
	}
}
