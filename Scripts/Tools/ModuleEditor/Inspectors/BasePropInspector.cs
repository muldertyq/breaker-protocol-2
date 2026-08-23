using System;
using System.Linq;
using System.Text.Json;
using Godot;
using BreakerProtocol.Data.Models;
using BreakerProtocol.Tools.ModuleEditor.Inspectors.SubInspectors;

namespace BreakerProtocol.Tools.ModuleEditor.Inspectors
{
	public partial class BasePropInspector : VBoxContainer
	{
		private LineEdit _idInput = null!;
		private LineEdit _nameInput = null!;
		private OptionButton _categorySelect = null!;
		private SpinBox _widthInput = null!;
		private SpinBox _heightInput = null!;
		private SpinBox _massInput = null!;
		private SpinBox _hpInput = null!;
		private SpinBox _armorInput = null!;

		// 贴图与发光层属性
		private LineEdit _baseTexInput = null!;
		private LineEdit _overlayTexInput = null!;
		private LineEdit _emissiveTexInput = null!;
		private OptionButton _emissiveAttachSelect = null!;
		private SpinBox _emissiveOffsetX = null!;
		private SpinBox _emissiveOffsetY = null!;
		private SpinBox _emissiveAnchorX = null!;
		private SpinBox _emissiveAnchorY = null!;

		// 斜面装甲跳弹卡片
		private PanelContainer _armorCard = null!;
		private SpinBox _deflectInput = null!;

		// 子分类独立检视器
		private WeaponInspector _weaponInspector = null!;
		private ShieldInspector _shieldInspector = null!;
		private PowerInspector _powerInspector = null!;
		private PropulsionInspector _propulsionInspector = null!;
		private PipelineInspector _pipelineInspector = null!;
		private PinInspector _pinInspector = null!;

		public event Action? OnValuesChanged;
		public event Action<int>? OnPinSelected;
		public event Action<int>? OnSlotSelected;
		public event Action<int>? OnExhaustSelected;
		public event Action<bool>? OnTestFireModeToggled;

		private ModuleDataDefinition? _boundData;
		private bool _isUpdating = false;

		private static readonly string[] EmissiveAttachOptions = { "Overlay (跟随炮身/顶盖)", "Base (固定在底座)" };
		private static readonly string[] EmissiveAttachValues = { "Overlay", "Base" };

		public override void _Ready()
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill;
			BuildUI();
		}

		private void BuildUI()
		{
			AddThemeConstantOverride("separation", 10);

			// 1. 基础几何与物理卡片
			var (baseCard, baseBox) = CreateCard("📦 基础几何与物理属性", new Color(0.38f, 0.65f, 0.98f));
			AddChild(baseCard);

			_idInput = CreateTextRowToParent(baseBox, "构件 ID:");
			_nameInput = CreateTextRowToParent(baseBox, "显示名称:");
			_categorySelect = CreateOptionRowToParent(baseBox, "构件分类:", new[] { "Structural", "Power", "Propulsion", "Weapons", "Armor", "Pipeline" });

			_widthInput = CreateNumberRowToParent(baseBox, "宽度 (GU):", 1, 8, 1);
			_heightInput = CreateNumberRowToParent(baseBox, "高度 (GU):", 1, 8, 1);
			_massInput = CreateNumberRowToParent(baseBox, "质量 (吨):", 0.1, 500.0, 0.5);
			_hpInput = CreateNumberRowToParent(baseBox, "结构耐久 (HP):", 10, 50000, 50);
			_armorInput = CreateNumberRowToParent(baseBox, "装甲抗性:", 0, 100, 5);

			// 2. 贴图与着色图层卡片
			var (texCard, texBox) = CreateCard("🎨 贴图与发光图层", new Color(0.85f, 0.45f, 0.95f));
			AddChild(texCard);

			_baseTexInput = CreateTextRowToParent(texBox, "底盘贴图 (Base):");
			_overlayTexInput = CreateTextRowToParent(texBox, "顶盖/炮塔 (Overlay):");
			_emissiveTexInput = CreateTextRowToParent(texBox, "发光通道 (Emissive):");

			_emissiveAttachSelect = CreateOptionRowToParent(texBox, "发光挂载层:", EmissiveAttachOptions);
			CreateDualNumberRowToParent(texBox, "发光局部偏移 (px):", -640, 640, 1, out _emissiveOffsetX, out _emissiveOffsetY);
			CreateDualNumberRowToParent(texBox, "发光自转轴心 (px):", 0, 640, 1, out _emissiveAnchorX, out _emissiveAnchorY);

			// 3. 装甲跳弹卡片
			var (armorCard, armorBox) = CreateCard("🛡️ 斜面装甲跳弹参数", new Color(0.95f, 0.65f, 0.25f));
			_armorCard = armorCard;
			_deflectInput = CreateNumberRowToParent(armorBox, "跳弹偏折几率:", 0.0, 1.0, 0.05);
			AddChild(_armorCard);

			// 4. 挂载子检视器
			_weaponInspector = new WeaponInspector();
			_weaponInspector.OnValuesChanged += () => OnValuesChanged?.Invoke();
			_weaponInspector.OnTestFireModeToggled += (on) => OnTestFireModeToggled?.Invoke(on);
			_weaponInspector.OnSlotSelected += (idx) => OnSlotSelected?.Invoke(idx);
			AddChild(_weaponInspector);

			_shieldInspector = new ShieldInspector();
			_shieldInspector.OnValuesChanged += () => OnValuesChanged?.Invoke();
			AddChild(_shieldInspector);

			_powerInspector = new PowerInspector();
			_powerInspector.OnValuesChanged += () => OnValuesChanged?.Invoke();
			AddChild(_powerInspector);

			_propulsionInspector = new PropulsionInspector();
			_propulsionInspector.OnValuesChanged += () => OnValuesChanged?.Invoke();
			_propulsionInspector.OnExhaustSelectedInInspector += (idx) => OnExhaustSelected?.Invoke(idx);
			AddChild(_propulsionInspector);

			_pipelineInspector = new PipelineInspector();
			_pipelineInspector.OnValuesChanged += () => OnValuesChanged?.Invoke();
			AddChild(_pipelineInspector);

			// 5. 引脚端口检视器
			_pinInspector = new PinInspector();
			_pinInspector.OnValuesChanged += () => OnValuesChanged?.Invoke();
			_pinInspector.OnPinSelectedInInspector += (idx) => OnPinSelected?.Invoke(idx);
			AddChild(_pinInspector);
		}

		public void BindData(ModuleDataDefinition data, int selectPinIndex = -1, int selectExhaustIndex = -1, int selectSlotIndex = -1)
		{
			_boundData = data;
			_isUpdating = true;

			_idInput.Text = data.Id;
			_nameInput.Text = data.Name;
			SelectOptionByText(_categorySelect, data.Category);

			_widthInput.Value = data.Width;
			_heightInput.Value = data.Height;
			_massInput.Value = data.Mass;
			_hpInput.Value = data.BaseHp;
			_armorInput.Value = data.ArmorResistance;

			_baseTexInput.Text = data.SpriteBase ?? string.Empty;
			_overlayTexInput.Text = data.SpriteOverlay ?? string.Empty;
			_emissiveTexInput.Text = data.SpriteEmissive ?? string.Empty;

			SelectOptionByValue(_emissiveAttachSelect, EmissiveAttachValues, data.EmissiveAttachTo);
			_emissiveOffsetX.Value = data.EmissiveOffsetX;
			_emissiveOffsetY.Value = data.EmissiveOffsetY;
			_emissiveAnchorX.Value = data.EmissiveAnchorX;
			_emissiveAnchorY.Value = data.EmissiveAnchorY;

			bool isShield = data.Tags != null && data.Tags.Contains("Shield");
			bool isArmor = data.Category == "Armor" && !isShield;

			_armorCard.Visible = isArmor;
			_weaponInspector.Visible = data.Category == "Weapons";
			_shieldInspector.Visible = isShield;
			_powerInspector.Visible = data.Category == "Power";
			_propulsionInspector.Visible = data.Category == "Propulsion";
			_pipelineInspector.Visible = data.Category == "Pipeline";
			_pinInspector.Visible = data.Category is not ("Structural" or "Armor") || isShield;

			if (isArmor)
			{
				try
				{
					if (data.Properties.ValueKind == JsonValueKind.Object && data.Properties.TryGetProperty("deflectChance", out var prop))
						_deflectInput.Value = prop.GetSingle();
				}
				catch { _deflectInput.Value = 0.0; }
			}
			else if (_weaponInspector.Visible) _weaponInspector.BindData(data, selectSlotIndex);
			else if (_shieldInspector.Visible) _shieldInspector.BindData(data);
			else if (_powerInspector.Visible) _powerInspector.BindData(data);
			else if (_propulsionInspector.Visible) _propulsionInspector.BindData(data, selectExhaustIndex);
			else if (_pipelineInspector.Visible) _pipelineInspector.BindData(data);

			if (_pinInspector.Visible)
			{
				_pinInspector.BindData(data, selectPinIndex);
			}

			_isUpdating = false;
		}

		public void SelectPinExternal(int index) => _pinInspector.SelectPinExternal(index);
		public void SelectSlotExternal(int index) => _weaponInspector.SelectSlotExternal(index);
		public void SelectExhaustExternal(int index) => _propulsionInspector.SelectExhaustExternal(index);

		private void EmitDataChanged()
		{
			if (_isUpdating || _boundData == null) return;

			_boundData.Id = _idInput.Text.Trim();
			_boundData.Name = _nameInput.Text.Trim();
			_boundData.Category = _categorySelect.GetItemText(_categorySelect.Selected);
			_boundData.Width = (int)_widthInput.Value;
			_boundData.Height = (int)_heightInput.Value;
			_boundData.Mass = (float)_massInput.Value;
			_boundData.BaseHp = (float)_hpInput.Value;
			_boundData.ArmorResistance = (float)_armorInput.Value;

			_boundData.SpriteBase = _baseTexInput.Text.Trim();
			_boundData.SpriteOverlay = _overlayTexInput.Text.Trim();
			_boundData.SpriteEmissive = _emissiveTexInput.Text.Trim();

			_boundData.EmissiveAttachTo = EmissiveAttachValues[Mathf.Clamp(_emissiveAttachSelect.Selected, 0, EmissiveAttachValues.Length - 1)];
			_boundData.EmissiveOffsetX = (float)_emissiveOffsetX.Value;
			_boundData.EmissiveOffsetY = (float)_emissiveOffsetY.Value;
			_boundData.EmissiveAnchorX = (float)_emissiveAnchorX.Value;
			_boundData.EmissiveAnchorY = (float)_emissiveAnchorY.Value;

			if (_armorCard.Visible)
			{
				var ap = new { deflectChance = (float)_deflectInput.Value };
				_boundData.Properties = JsonSerializer.SerializeToElement(ap);
			}

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

		private LineEdit CreateTextRowToParent(Control parent, string labelText)
		{
			var hbox = new HBoxContainer();
			hbox.AddChild(new Label { Text = labelText, CustomMinimumSize = new Vector2(110, 0) });
			var edit = new LineEdit { SizeFlagsHorizontal = SizeFlags.ExpandFill };
			edit.TextChanged += _ => EmitDataChanged();
			hbox.AddChild(edit);
			parent.AddChild(hbox);
			return edit;
		}

		private OptionButton CreateOptionRowToParent(Control parent, string labelText, string[] items)
		{
			var hbox = new HBoxContainer();
			hbox.AddChild(new Label { Text = labelText, CustomMinimumSize = new Vector2(110, 0) });
			var opt = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
			for (int i = 0; i < items.Length; i++) opt.AddItem(items[i], i);
			opt.ItemSelected += _ =>
			{
				if (_boundData != null)
				{
					_boundData.Category = opt.GetItemText(opt.Selected);
					BindData(_boundData);
				}
				EmitDataChanged();
			};
			hbox.AddChild(opt);
			parent.AddChild(hbox);
			return opt;
		}

		private SpinBox CreateNumberRowToParent(Control parent, string labelText, double min, double max, double step)
		{
			var hbox = new HBoxContainer();
			hbox.AddChild(new Label { Text = labelText, CustomMinimumSize = new Vector2(110, 0) });
			var spin = new SpinBox { MinValue = min, MaxValue = max, Step = step, SizeFlagsHorizontal = SizeFlags.ExpandFill };
			spin.ValueChanged += _ => EmitDataChanged();
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
			s1.ValueChanged += _ => EmitDataChanged();
			s2.ValueChanged += _ => EmitDataChanged();
			hbox.AddChild(s1);
			hbox.AddChild(s2);
			parent.AddChild(hbox);
		}

		private void SelectOptionByText(OptionButton opt, string text)
		{
			for (int i = 0; i < opt.ItemCount; i++)
			{
				if (opt.GetItemText(i).Equals(text, StringComparison.OrdinalIgnoreCase))
				{
					opt.Select(i);
					return;
				}
			}
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
