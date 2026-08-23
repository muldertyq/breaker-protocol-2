using System;
using System.Collections.Generic;
using System.Text.Json;
using Godot;
using BreakerProtocol.Data.Models;
using BreakerProtocol.Data.Models.Properties;

namespace BreakerProtocol.Tools.ModuleEditor.Inspectors.SubInspectors
{
	public partial class WeaponInspector : VBoxContainer
	{
		private OptionButton _mountSelect = null!;
		private PanelContainer _turretCard = null!;
		private VBoxContainer _turretContent = null!;

		// 底座安装位与转轴
		private SpinBox _pivotX = null!;
		private SpinBox _pivotY = null!;
		private Button _centerPivotBtn = null!;
		private SpinBox _turretAnchorX = null!;
		private SpinBox _turretAnchorY = null!;
		private Button _centerAnchorBtn = null!;

		private SpinBox _turretArc = null!;
		private SpinBox _turretTurnRate = null!;
		private CheckButton _testFireToggle = null!;

		// 载荷与视觉着色
		private OptionButton _deliverySelect = null!;
		private HBoxContainer _colorHBox = null!;
		private Label _coreColorLabel = null!;
		private ColorPickerButton _bulletColorPicker = null!;
		private Label _glowColorLabel = null!;
		private ColorPickerButton _glowColorPicker = null!;

		// 激光与实弹尺寸
		private SpinBox _projRadiusInput = null!;
		private SpinBox _projLengthInput = null!;
		private SpinBox _beamWidthInput = null!;
		private SpinBox _beamDurationInput = null!;

		// 鱼雷挂架与多弹位管理
		private PanelContainer _missileCard = null!;
		private LineEdit _missileSpriteInput = null!;
		private Button _reloadTexBtn = null!;
		private CheckButton _showOnRackToggle = null!;
		private OptionButton _fireModeSelect = null!;
		private SpinBox _burstIntervalInput = null!;
		private OptionButton _reloadModeSelect = null!;
		private SpinBox _reloadDurationInput = null!;
		private SpinBox _trackingInput = null!;
		private ItemList _slotList = null!;
		private Button _addSlotBtn = null!;
		private Button _removeSlotBtn = null!;

		// 选中弹位属性微调面板
		private PanelContainer _slotDetailCard = null!;
		private LineEdit _slotIdInput = null!;
		private SpinBox _slotOrderInput = null!;
		private SpinBox _slotPosX = null!;
		private SpinBox _slotPosY = null!;
		private SpinBox _slotWidth = null!;
		private SpinBox _slotLength = null!;
		private SpinBox _slotAngle = null!;

		// 弹道与物理参数
		private SpinBox _damageInput = null!;
		private SpinBox _fireRateInput = null!;
		private SpinBox _rangeInput = null!;
		private SpinBox _speedInput = null!;
		private SpinBox _spreadInput = null!;
		private SpinBox _recoilInput = null!;
		private SpinBox _pulseCostInput = null!;
		private SpinBox _heatInput = null!;

		public event Action? OnValuesChanged;
		public event Action<bool>? OnTestFireModeToggled;
		public event Action<int>? OnSlotSelected;

		private ModuleDataDefinition? _boundData;
		private bool _isUpdating = false;
		private int _selectedSlotIndex = -1;

		private static readonly string[] DeliveryTypes = { "Ballistic", "PulseBeam", "ContinuousBeam", "Missile" };
		private static readonly string[] DeliveryNames = 
		{ 
			"⚡ 动能实弹 (Ballistic)", 
			"✨ 脉冲激光 (Pulse Beam)", 
			"🔴 持续光束 (Continuous Beam)", 
			"🚀 鱼雷/导弹 (Guided Missile)" 
		};

		public override void _Ready()
		{
			BuildUI();
		}

		private void BuildUI()
		{
			AddThemeConstantOverride("separation", 10);

			// 1. 挂载与回转
			var (turretCard, turretBox) = CreateCard("🎯 武器挂载与回转", new Color(0.95f, 0.75f, 0.25f));
			_turretCard = turretCard;
			_turretContent = turretBox;
			AddChild(_turretCard);

			_mountSelect = CreateOptionRowToParent(_turretContent, "挂载方式:", new[] { "Fixed (固定槽)", "Turret (回转炮塔)", "Hangar (机库发射)" });
			_mountSelect.ItemSelected += _ =>
			{
				bool isTurret = _mountSelect.GetItemText(_mountSelect.Selected).StartsWith("Turret");
				UpdateTurretFieldsVisibility(isTurret);
				EmitChange();
			};

			CreateDualNumberRowToParent(_turretContent, "底座安装位 (px):", 0, 640, 1, out _pivotX, out _pivotY);
			_centerPivotBtn = new Button { Text = "🎯 安装位居中 (对齐底盘几何中心)", CustomMinimumSize = new Vector2(0, 28) };
			_centerPivotBtn.Pressed += CenterPivotToModule;
			_turretContent.AddChild(_centerPivotBtn);

			CreateDualNumberRowToParent(_turretContent, "贴图转轴中心 (px):", 0, 640, 1, out _turretAnchorX, out _turretAnchorY);
			_centerAnchorBtn = new Button { Text = "⚓ 贴图转轴对齐安装座", CustomMinimumSize = new Vector2(0, 28) };
			_centerAnchorBtn.Pressed += () =>
			{
				if (_boundData == null) return;
				_turretAnchorX.Value = _pivotX.Value;
				_turretAnchorY.Value = _pivotY.Value;
				EmitChange();
			};
			_turretContent.AddChild(_centerAnchorBtn);

			_turretArc = CreateNumberRowToParent(_turretContent, "射界扇区 (°):", 0, 360, 5);
			_turretTurnRate = CreateNumberRowToParent(_turretContent, "回转速度 (°/s):", 10, 720, 15);

			// 2. 开火测试
			var (testCard, testBox) = CreateCard("🎮 视口交互与开火测试", new Color(0.35f, 0.95f, 0.55f));
			AddChild(testCard);

			_testFireToggle = new CheckButton
			{
				Text = "开启实时瞄准与开火测试",
				SizeFlagsHorizontal = SizeFlags.ExpandFill,
				CustomMinimumSize = new Vector2(0, 32)
			};
			_testFireToggle.Toggled += (on) => OnTestFireModeToggled?.Invoke(on);
			testBox.AddChild(_testFireToggle);

			// 3. 载荷形态
			var (payloadCard, payloadBox) = CreateCard("🔴 载荷形态与着色", new Color(0.45f, 0.80f, 1.0f));
			AddChild(payloadCard);

			_deliverySelect = CreateOptionRowToParent(payloadBox, "载荷形态:", DeliveryNames);
			_deliverySelect.ItemSelected += _ =>
			{
				UpdateDeliveryVisibility();
				EmitChange();
			};

			_colorHBox = new HBoxContainer();
			_coreColorLabel = new Label { Text = "弹芯/光束色:", CustomMinimumSize = new Vector2(95, 0) };
			_colorHBox.AddChild(_coreColorLabel);
			_bulletColorPicker = new ColorPickerButton { SizeFlagsHorizontal = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0, 26) };
			_bulletColorPicker.ColorChanged += _ => EmitChange();
			_colorHBox.AddChild(_bulletColorPicker);

			_glowColorLabel = new Label { Text = "外层辉光:", CustomMinimumSize = new Vector2(75, 0) };
			_colorHBox.AddChild(_glowColorLabel);
			_glowColorPicker = new ColorPickerButton { SizeFlagsHorizontal = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0, 26) };
			_glowColorPicker.ColorChanged += _ => EmitChange();
			_colorHBox.AddChild(_glowColorPicker);
			payloadBox.AddChild(_colorHBox);

			_projRadiusInput = CreateNumberRowToParent(payloadBox, "弹丸半径 (px):", 1.0, 32.0, 0.5);
			_projLengthInput = CreateNumberRowToParent(payloadBox, "弹身长度 (px):", 2.0, 160.0, 1.0);
			_beamWidthInput = CreateNumberRowToParent(payloadBox, "光束宽度 (px):", 1.0, 48.0, 0.5);
			_beamDurationInput = CreateNumberRowToParent(payloadBox, "脉冲时长 (秒):", 0.05, 1.0, 0.02);

			// 4. 🚀 鱼雷/导弹多弹位管理
			var (missileCard, missileBox) = CreateCard("🚀 鱼雷挂架与多弹位系统", new Color(0.3f, 0.85f, 1.0f));
			_missileCard = missileCard;
			AddChild(_missileCard);

			var texRow = new HBoxContainer();
			texRow.AddChild(new Label { Text = "鱼雷贴图:", CustomMinimumSize = new Vector2(110, 0) });
			_missileSpriteInput = new LineEdit { SizeFlagsHorizontal = SizeFlags.ExpandFill, PlaceholderText = "modules/heavy_foundry/weapons/..." };
			_missileSpriteInput.TextChanged += _ => EmitChange();
			texRow.AddChild(_missileSpriteInput);

			_reloadTexBtn = new Button { Text = "🔄 刷新" };
			_reloadTexBtn.Pressed += () => EmitChange();
			texRow.AddChild(_reloadTexBtn);
			missileBox.AddChild(texRow);

			_showOnRackToggle = new CheckButton
			{
				Text = "在架弹体可见 (开启: 裸露鱼雷挂架; 关闭: 蜂巢/内置垂发管)",
				SizeFlagsHorizontal = SizeFlags.ExpandFill
			};
			_showOnRackToggle.Toggled += _ => EmitChange();
			missileBox.AddChild(_showOnRackToggle);

			_fireModeSelect = CreateOptionRowToParent(missileBox, "发射模式:", new[] { "Burst (依次连发)", "Sequential (单发轮射)", "Salvo (全架齐射)" });
			_burstIntervalInput = CreateNumberRowToParent(missileBox, "连发间隔 (秒):", 0.05, 2.0, 0.05);

			_reloadModeSelect = CreateOptionRowToParent(missileBox, "装填策略:", new[] { "FullRack (打空整架装填)", "Incremental (单发独立装填)" });
			_reloadDurationInput = CreateNumberRowToParent(missileBox, "装填周期 (秒):", 0.5, 30.0, 0.5);
			_trackingInput = CreateNumberRowToParent(missileBox, "制导回转 (°/s):", 0.0, 360.0, 5.0);

			missileBox.AddChild(new Label { Text = "📦 挂架弹位列表 (点击选择/视口拖拽):" });
			_slotList = new ItemList { CustomMinimumSize = new Vector2(0, 90), SizeFlagsHorizontal = SizeFlags.ExpandFill };
			_slotList.ItemSelected += idx => SelectSlot((int)idx);
			missileBox.AddChild(_slotList);

			var btnHBox = new HBoxContainer();
			_addSlotBtn = new Button { Text = "➕ 添加弹位", SizeFlagsHorizontal = SizeFlags.ExpandFill };
			_addSlotBtn.Pressed += AddNewSlot;
			_removeSlotBtn = new Button { Text = "🗑️ 删除弹位", SizeFlagsHorizontal = SizeFlags.ExpandFill };
			_removeSlotBtn.Pressed += RemoveSelectedSlot;
			btnHBox.AddChild(_addSlotBtn);
			btnHBox.AddChild(_removeSlotBtn);
			missileBox.AddChild(btnHBox);

			// 4.1 独立弹位属性编辑器
			var (slotDetailCard, slotDetailBox) = CreateCard("✏️ 选中弹位属性配置", new Color(0.95f, 0.85f, 0.35f));
			_slotDetailCard = slotDetailCard;
			_slotIdInput = CreateTextRowToParent(slotDetailBox, "槽位标识 (ID):");
			_slotIdInput.TextChanged += _ => UpdateCurrentSlotFromFields();

			_slotOrderInput = CreateNumberRowToParent(slotDetailBox, "发射序号 (0基):", 0, 32, 1);
			_slotOrderInput.ValueChanged += _ => UpdateCurrentSlotFromFields();

			CreateDualNumberRowToParent(slotDetailBox, "局部坐标 (px):", -640, 640, 1, out _slotPosX, out _slotPosY);
			_slotPosX.ValueChanged += _ => UpdateCurrentSlotFromFields();
			_slotPosY.ValueChanged += _ => UpdateCurrentSlotFromFields();

			CreateDualNumberRowToParent(slotDetailBox, "弹体尺寸 (px):", 4, 300, 1, out _slotWidth, out _slotLength);
			_slotWidth.ValueChanged += _ => UpdateCurrentSlotFromFields();
			_slotLength.ValueChanged += _ => UpdateCurrentSlotFromFields();

			_slotAngle = CreateNumberRowToParent(slotDetailBox, "偏移角度 (°):", -90, 90, 1);
			_slotAngle.ValueChanged += _ => UpdateCurrentSlotFromFields();

			_slotDetailCard.Visible = false;
			missileBox.AddChild(_slotDetailCard);

			// 5. 伤害能耗
			var (combatCard, combatBox) = CreateCard("💥 弹道作战与能耗参数", new Color(1.0f, 0.45f, 0.45f));
			AddChild(combatCard);

			_damageInput = CreateNumberRowToParent(combatBox, "单发/秒伤 (HP):", 1, 50000, 10);
			_fireRateInput = CreateNumberRowToParent(combatBox, "发射速率 (发/秒):", 0.1, 60, 0.5);
			_rangeInput = CreateNumberRowToParent(combatBox, "有效射程 (m):", 10, 3000, 10);
			_speedInput = CreateNumberRowToParent(combatBox, "初速/航速 (m/s):", 30, 2500, 25);
			_spreadInput = CreateNumberRowToParent(combatBox, "弹道散布 (°):", 0.0, 45.0, 0.5);
			_recoilInput = CreateNumberRowToParent(combatBox, "后坐力 (N):", 0, 10000, 100);
			_pulseCostInput = CreateNumberRowToParent(combatBox, "耗电量 (P):", 0.0, 100.0, 0.5);
			_heatInput = CreateNumberRowToParent(combatBox, "射热量 (H):", 0.0, 100.0, 0.5);
		}

		public void BindData(ModuleDataDefinition data, int selectSlotIdx = -1)
		{
			_boundData = data;
			_isUpdating = true;

			SelectOptionByTextPrefix(_mountSelect, data.MountType);
			bool isTurret = data.MountType == "Turret";
			UpdateTurretFieldsVisibility(isTurret);

			_pivotX.Value = data.PivotPixelX;
			_pivotY.Value = data.PivotPixelY;
			_turretAnchorX.Value = data.TurretAnchorX;
			_turretAnchorY.Value = data.TurretAnchorY;

			_turretArc.Value = data.RotationArc;
			_turretTurnRate.Value = data.TurnRate;

			var wp = data.GetProperties<WeaponProperties>() ?? new WeaponProperties();
			SelectDeliveryType(wp.DeliveryType);

			_bulletColorPicker.Color = Color.FromHtml(string.IsNullOrEmpty(wp.BulletColorHex) ? "#ffe066" : wp.BulletColorHex);
			_glowColorPicker.Color = Color.FromHtml(string.IsNullOrEmpty(wp.BulletGlowHex) ? "#ff9900" : wp.BulletGlowHex);

			_projRadiusInput.Value = wp.ProjectileRadius > 0 ? wp.ProjectileRadius : 3.0f;
			_projLengthInput.Value = wp.ProjectileLength > 0 ? wp.ProjectileLength : 16.0f;

			_beamWidthInput.Value = wp.BeamWidth > 0 ? wp.BeamWidth : 4.0f;
			_beamDurationInput.Value = wp.BeamDuration > 0 ? wp.BeamDuration : 0.12f;

			_missileSpriteInput.Text = wp.DefaultMissileSprite ?? string.Empty;
			_showOnRackToggle.SetPressedNoSignal(wp.ShowMissileOnRack);

			_fireModeSelect.Select((int)wp.FireMode);
			_burstIntervalInput.Value = wp.BurstInterval > 0 ? wp.BurstInterval : 0.2f;
			_reloadModeSelect.Select((int)wp.ReloadMode);
			_reloadDurationInput.Value = wp.ReloadDuration > 0 ? wp.ReloadDuration : 6.0f;
			_trackingInput.Value = wp.TrackingStrength;

			PopulateSlotList(wp, selectSlotIdx);

			_damageInput.Value = wp.Damage;
			_fireRateInput.Value = wp.FireRate;
			_rangeInput.Value = wp.Range;
			_speedInput.Value = wp.Speed;
			_spreadInput.Value = wp.Spread;
			_recoilInput.Value = wp.Recoil;
			_pulseCostInput.Value = wp.PulseCost;
			_heatInput.Value = wp.HeatPerShot;

			UpdateDeliveryVisibility();
			_isUpdating = false;
		}

		private void PopulateSlotList(WeaponProperties wp, int selectIdx)
		{
			_slotList.Clear();
			if (wp.MunitionSlots == null || wp.MunitionSlots.Length == 0)
			{
				_slotDetailCard.Visible = false;
				_selectedSlotIndex = -1;
				return;
			}

			for (int i = 0; i < wp.MunitionSlots.Length; i++)
			{
				var slot = wp.MunitionSlots[i];
				_slotList.AddItem($"[#{slot.FireOrder + 1}] {slot.SlotId} 偏移:({slot.OffsetX}, {slot.OffsetY}) 尺寸:{slot.Width}x{slot.Length}");
			}

			int target = Mathf.Clamp(selectIdx >= 0 ? selectIdx : _selectedSlotIndex, 0, wp.MunitionSlots.Length - 1);
			_slotList.Select(target);
			SelectSlot(target);
		}

		public void SelectSlot(int index)
		{
			_selectedSlotIndex = index;
			if (_boundData == null) return;
			var wp = _boundData.GetProperties<WeaponProperties>();
			if (wp?.MunitionSlots == null || index < 0 || index >= wp.MunitionSlots.Length)
			{
				_slotDetailCard.Visible = false;
				return;
			}

			var slot = wp.MunitionSlots[index];
			_slotDetailCard.Visible = true;

			_isUpdating = true;
			_slotIdInput.Text = slot.SlotId;
			_slotOrderInput.Value = slot.FireOrder;
			_slotPosX.Value = slot.OffsetX;
			_slotPosY.Value = slot.OffsetY;
			_slotWidth.Value = slot.Width;
			_slotLength.Value = slot.Length;
			_slotAngle.Value = slot.AngleOffsetDeg;
			_isUpdating = false;

			OnSlotSelected?.Invoke(index);
		}

		private void UpdateCurrentSlotFromFields()
		{
			if (_isUpdating || _boundData == null || _selectedSlotIndex < 0) return;
			var wp = _boundData.GetProperties<WeaponProperties>() ?? new WeaponProperties();
			if (wp.MunitionSlots == null || _selectedSlotIndex >= wp.MunitionSlots.Length) return;

			var slot = wp.MunitionSlots[_selectedSlotIndex];
			slot.SlotId = _slotIdInput.Text.Trim();
			slot.FireOrder = (int)_slotOrderInput.Value;
			slot.OffsetX = (float)_slotPosX.Value;
			slot.OffsetY = (float)_slotPosY.Value;
			slot.Width = (float)_slotWidth.Value;
			slot.Length = (float)_slotLength.Value;
			slot.AngleOffsetDeg = (float)_slotAngle.Value;

			_boundData.Properties = JsonSerializer.SerializeToElement(wp);
			_slotList.SetItemText(_selectedSlotIndex, $"[#{slot.FireOrder + 1}] {slot.SlotId} 偏移:({slot.OffsetX}, {slot.OffsetY}) 尺寸:{slot.Width}x{slot.Length}");
			EmitChange();
		}

		private void AddNewSlot()
		{
			if (_boundData == null) return;
			var wp = _boundData.GetProperties<WeaponProperties>() ?? new WeaponProperties();
			var list = new List<MunitionSlotDefinition>(wp.MunitionSlots ?? Array.Empty<MunitionSlotDefinition>());

			list.Add(new MunitionSlotDefinition
			{
				SlotId = $"slot_{list.Count}",
				FireOrder = list.Count,
				OffsetX = 0,
				OffsetY = -20,
				Width = 24,
				Length = 80
			});

			wp.MunitionSlots = list.ToArray();
			_boundData.Properties = JsonSerializer.SerializeToElement(wp);
			BindData(_boundData, list.Count - 1);
			EmitChange();
		}

		private void RemoveSelectedSlot()
		{
			if (_boundData == null) return;
			var selected = _slotList.GetSelectedItems();
			if (selected.Length == 0) return;

			var wp = _boundData.GetProperties<WeaponProperties>() ?? new WeaponProperties();
			var list = new List<MunitionSlotDefinition>(wp.MunitionSlots ?? Array.Empty<MunitionSlotDefinition>());
			int removeIdx = selected[0];

			if (removeIdx >= 0 && removeIdx < list.Count)
			{
				list.RemoveAt(removeIdx);
				for (int i = 0; i < list.Count; i++) list[i].FireOrder = i;
				wp.MunitionSlots = list.ToArray();
				_boundData.Properties = JsonSerializer.SerializeToElement(wp);
				BindData(_boundData, Mathf.Clamp(removeIdx - 1, 0, list.Count - 1));
				EmitChange();
			}
		}

		public void SelectSlotExternal(int index)
		{
			if (index >= 0 && index < _slotList.ItemCount)
			{
				_slotList.Select(index);
				SelectSlot(index);
			}
			else
			{
				_slotList.DeselectAll();
				_slotDetailCard.Visible = false;
				_selectedSlotIndex = -1;
			}
		}

		private void UpdateTurretFieldsVisibility(bool isTurret)
		{
			_turretArc.GetParent<Control>().Visible = isTurret;
			_turretTurnRate.GetParent<Control>().Visible = isTurret;
			_centerPivotBtn.Visible = isTurret;
			_centerAnchorBtn.Visible = isTurret;
			_turretAnchorX.GetParent<Control>().Visible = isTurret;
		}

		private void UpdateDeliveryVisibility()
		{
			string delivery = DeliveryTypes[Mathf.Clamp(_deliverySelect.Selected, 0, DeliveryTypes.Length - 1)];
			bool isPulseBeam = delivery == "PulseBeam";
			bool isContinuousBeam = delivery == "ContinuousBeam";
			bool isBeam = isPulseBeam || isContinuousBeam;
			bool isMissile = delivery == "Missile";
			bool isBallistic = delivery == "Ballistic";

			_coreColorLabel.Visible = !isMissile;
			_bulletColorPicker.Visible = !isMissile;
			_glowColorLabel.Text = isMissile ? "尾焰辉光:" : "外层辉光:";

			_projRadiusInput.GetParent<Control>().Visible = isBallistic;
			_projLengthInput.GetParent<Control>().Visible = isBallistic;
			_beamWidthInput.GetParent<Control>().Visible = isBeam;
			_beamDurationInput.GetParent<Control>().Visible = isPulseBeam;
			_missileCard.Visible = isMissile;

			_speedInput.GetParent<Control>().Visible = !isBeam;
			_spreadInput.GetParent<Control>().Visible = !isBeam;
			_fireRateInput.GetParent<Control>().Visible = isBallistic || isPulseBeam;
		}

		public void ApplyToData(ModuleDataDefinition data)
		{
			if (_isUpdating) return;

			string selectedText = _mountSelect.GetItemText(_mountSelect.Selected);
			data.MountType = selectedText.StartsWith("Turret") ? "Turret" : (selectedText.StartsWith("Hangar") ? "Hangar" : "Fixed");

			if (data.MountType == "Turret")
			{
				data.PivotPixelX = (float)_pivotX.Value;
				data.PivotPixelY = (float)_pivotY.Value;
				data.TurretAnchorX = (float)_turretAnchorX.Value;
				data.TurretAnchorY = (float)_turretAnchorY.Value;
				data.RotationArc = (float)_turretArc.Value;
				data.TurnRate = (float)_turretTurnRate.Value;
			}
			else
			{
				data.RotationArc = 0.0f;
				data.TurnRate = 0.0f;
			}

			var wp = data.GetProperties<WeaponProperties>() ?? new WeaponProperties();
			wp.DeliveryType = DeliveryTypes[Mathf.Clamp(_deliverySelect.Selected, 0, DeliveryTypes.Length - 1)];
			wp.BulletColorHex = $"#{_bulletColorPicker.Color.ToHtml(false)}";
			wp.BulletGlowHex = $"#{_glowColorPicker.Color.ToHtml(false)}";

			wp.ProjectileRadius = (float)_projRadiusInput.Value;
			wp.ProjectileLength = (float)_projLengthInput.Value;
			wp.BeamWidth = (float)_beamWidthInput.Value;
			wp.BeamDuration = (float)_beamDurationInput.Value;

			wp.DefaultMissileSprite = _missileSpriteInput.Text.Trim();
			wp.ShowMissileOnRack = _showOnRackToggle.ButtonPressed;
			wp.FireMode = (MissileFireMode)_fireModeSelect.Selected;
			wp.BurstInterval = (float)_burstIntervalInput.Value;
			wp.ReloadMode = (RackReloadMode)_reloadModeSelect.Selected;
			wp.ReloadDuration = (float)_reloadDurationInput.Value;
			wp.TrackingStrength = (float)_trackingInput.Value;

			wp.Damage = (float)_damageInput.Value;
			wp.FireRate = (float)_fireRateInput.Value;
			wp.Range = (float)_rangeInput.Value;
			wp.Speed = (float)_speedInput.Value;
			wp.Spread = (float)_spreadInput.Value;
			wp.Recoil = (float)_recoilInput.Value;
			wp.PulseCost = (float)_pulseCostInput.Value;
			wp.HeatPerShot = (float)_heatInput.Value;
			data.Properties = JsonSerializer.SerializeToElement(wp);
		}

		private void CenterPivotToModule()
		{
			if (_boundData == null) return;
			_pivotX.Value = _boundData.Width * 80 * 0.5f;
			_pivotY.Value = _boundData.Height * 80 * 0.5f;
			EmitChange();
		}

		private void SelectDeliveryType(string delivery)
		{
			if (delivery.Equals("Beam", StringComparison.OrdinalIgnoreCase)) delivery = "PulseBeam";
			for (int i = 0; i < DeliveryTypes.Length; i++)
			{
				if (DeliveryTypes[i].Equals(delivery, StringComparison.OrdinalIgnoreCase))
				{
					_deliverySelect.Select(i);
					return;
				}
			}
			_deliverySelect.Select(0);
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

		private LineEdit CreateTextRowToParent(Control parent, string labelText)
		{
			var hbox = new HBoxContainer();
			hbox.AddChild(new Label { Text = labelText, CustomMinimumSize = new Vector2(110, 0) });
			var edit = new LineEdit { SizeFlagsHorizontal = SizeFlags.ExpandFill };
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
			hbox.AddChild(s1);
			hbox.AddChild(s2);
			parent.AddChild(hbox);
		}

		private void SelectOptionByTextPrefix(OptionButton opt, string prefix)
		{
			for (int i = 0; i < opt.ItemCount; i++)
			{
				if (opt.GetItemText(i).StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
				{
					opt.Select(i);
					return;
				}
			}
		}
	}
}
