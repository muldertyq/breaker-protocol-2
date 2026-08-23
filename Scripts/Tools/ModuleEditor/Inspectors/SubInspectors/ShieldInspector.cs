using System;
using System.Text.Json;
using Godot;
using BreakerProtocol.Data.Models;
using BreakerProtocol.Data.Models.Properties;

namespace BreakerProtocol.Tools.ModuleEditor.Inspectors.SubInspectors
{
	public partial class ShieldInspector : VBoxContainer
	{
		private OptionButton _shieldTypeSelect = null!;
		private SpinBox _emitterX = null!;
		private SpinBox _emitterY = null!;
		private SpinBox _shieldCapacityInput = null!;
		private SpinBox _shieldArcInput = null!;
		private SpinBox _shieldRadiusInput = null!;
		private SpinBox _rechargeRateInput = null!;

		public event Action? OnValuesChanged;
		private ModuleDataDefinition? _boundData;
		private bool _isUpdating = false;

		private static readonly string[] ShieldTypes = { "DirectionalArc", "OmniBubble" };
		private static readonly string[] ShieldTypeNames = { "🛡️ 定向偏导弧 (DirectionalArc)", "🌐 全向力场球 (OmniBubble)" };

		public override void _Ready()
		{
			BuildUI();
		}

		private void BuildUI()
		{
			AddThemeConstantOverride("separation", 10);

			// 护盾发生器力场卡片
			var (card, content) = CreateCard("🛡️ 护盾发生器与力场参数", new Color(0.35f, 0.85f, 0.98f));
			AddChild(card);

			_shieldTypeSelect = CreateOptionRowToParent(content, "护盾形态:", ShieldTypeNames);
			_shieldTypeSelect.ItemSelected += _ =>
			{
				bool isArc = _shieldTypeSelect.Selected == 0;
				_shieldArcInput.GetParent<Control>().Visible = isArc;
				EmitChange();
			};

			CreateDualNumberRowToParent(content, "发射中心 (px):", 0, 640, 1, out _emitterX, out _emitterY);

			_shieldCapacityInput = CreateNumberRowToParent(content, "护盾容量 (HP):", 50, 20000, 50);
			_rechargeRateInput = CreateNumberRowToParent(content, "充能速率 (HP/s):", 1, 1000, 5);
			_shieldArcInput = CreateNumberRowToParent(content, "偏导弧度 (°):", 30, 360, 15);
			_shieldRadiusInput = CreateNumberRowToParent(content, "投影半径 (px):", 40, 1000, 10);
		}

		public void BindData(ModuleDataDefinition data)
		{
			_boundData = data;
			_isUpdating = true;

			_emitterX.Value = data.PivotPixelX;
			_emitterY.Value = data.PivotPixelY;

			var sp = data.GetProperties<ShieldProperties>() ?? new ShieldProperties();
			SelectOptionByValue(_shieldTypeSelect, ShieldTypes, sp.ShieldType);
			_shieldCapacityInput.Value = sp.ShieldCapacity;
			_rechargeRateInput.Value = sp.RechargeRate;
			_shieldArcInput.Value = sp.ShieldArc;
			_shieldRadiusInput.Value = sp.ShieldRadius;

			bool isArc = sp.ShieldType != "OmniBubble";
			_shieldArcInput.GetParent<Control>().Visible = isArc;

			_isUpdating = false;
		}

		public void ApplyToData(ModuleDataDefinition data)
		{
			if (_isUpdating) return;

			data.PivotPixelX = (float)_emitterX.Value;
			data.PivotPixelY = (float)_emitterY.Value;

			var sp = data.GetProperties<ShieldProperties>() ?? new ShieldProperties();
			sp.ShieldType = ShieldTypes[Mathf.Clamp(_shieldTypeSelect.Selected, 0, ShieldTypes.Length - 1)];
			sp.ShieldCapacity = (float)_shieldCapacityInput.Value;
			sp.RechargeRate = (float)_rechargeRateInput.Value;
			sp.ShieldArc = (float)_shieldArcInput.Value;
			sp.ShieldRadius = (float)_shieldRadiusInput.Value;
			data.Properties = JsonSerializer.SerializeToElement(sp);
		}

		private void EmitChange()
		{
			if (_isUpdating || _boundData == null) return;
			ApplyToData(_boundData);
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

		private OptionButton CreateOptionRowToParent(Control parent, string labelText, string[] displayItems)
		{
			var hbox = new HBoxContainer();
			hbox.AddChild(new Label { Text = labelText, CustomMinimumSize = new Vector2(110, 0) });
			var opt = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
			for (int i = 0; i < displayItems.Length; i++) opt.AddItem(displayItems[i], i);
			opt.ItemSelected += _ => EmitChange();
			hbox.AddChild(opt);
			parent.AddChild(hbox);
			return opt;
		}

		private SpinBox CreateNumberRowToParent(Control parent, string labelText, double min, double max, double step)
		{
			var hbox = new HBoxContainer();
			hbox.AddChild(new Label { Text = labelText, CustomMinimumSize = new Vector2(110, 0) });
			var spin = new SpinBox { MinValue = min, MaxValue = max, Step = step, SizeFlagsHorizontal = SizeFlags.ExpandFill };
			spin.ValueChanged += _ => EmitChange();
			hbox.AddChild(spin);
			parent.AddChild(hbox);
			return spin;
		}

		private void CreateDualNumberRowToParent(Control parent, string labelText, double min, double max, double step, out SpinBox s1, out SpinBox s2)
		{
			var hbox = new HBoxContainer();
			hbox.AddChild(new Label { Text = labelText, CustomMinimumSize = new Vector2(110, 0) });
			s1 = new SpinBox { MinValue = min, MaxValue = max, Step = step, SizeFlagsHorizontal = SizeFlags.ExpandFill, Prefix = "X:" };
			s2 = new SpinBox { MinValue = min, MaxValue = max, Step = step, SizeFlagsHorizontal = SizeFlags.ExpandFill, Prefix = "Y:" };
			s1.ValueChanged += _ => EmitChange();
			s2.ValueChanged += _ => EmitChange();
			hbox.AddChild(s1);
			hbox.AddChild(s2);
			parent.AddChild(hbox);
		}

		private void SelectOptionByValue(OptionButton opt, string[] values, string target)
		{
			for (int i = 0; i < values.Length; i++)
			{
				if (values[i].Equals(target, StringComparison.OrdinalIgnoreCase))
				{
					opt.Select(i);
					return;
				}
			}
			opt.Select(0);
		}
	}
}
