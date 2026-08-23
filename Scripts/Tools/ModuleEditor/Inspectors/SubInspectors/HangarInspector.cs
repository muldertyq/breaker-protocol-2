using System;
using System.Collections.Generic;
using System.Text.Json;
using Godot;
using BreakerProtocol.Data.Models;
using BreakerProtocol.Data.Models.Properties;

namespace BreakerProtocol.Tools.ModuleEditor.Inspectors.SubInspectors
{
	public partial class HangarInspector : VBoxContainer
	{
		private CheckButton _testFireToggle = null!;
		
		private SpinBox _operationRadiusInput = null!;

		private LineEdit _droneIdInput = null!;
		private LineEdit _droneSpriteInput = null!;
		private Button _refreshTexBtn = null!;
		private CheckButton _showOnRunwayToggle = null!;
		private OptionButton _launchModeSelect = null!;

		private SpinBox _droneWidthInput = null!;
		private SpinBox _droneLengthInput = null!;

		private SpinBox _maxDronesInput = null!;
		private SpinBox _rebuildTimeInput = null!;
		private SpinBox _pulseCostInput = null!;
		private SpinBox _launchIntervalInput = null!;

		// 跑道列表与管理
		private ItemList _runwayList = null!;
		private Button _addRunwayBtn = null!;
		private Button _removeRunwayBtn = null!;

		// 选中跑道微调面板
		private PanelContainer _runwayDetailCard = null!;
		private LineEdit _runwayIdInput = null!;
		private SpinBox _runwayOrderInput = null!;
		private SpinBox _startXInput = null!;
		private SpinBox _startYInput = null!;
		private SpinBox _exitXInput = null!;
		private SpinBox _exitYInput = null!;
		private SpinBox _catapultDurationInput = null!;
		private SpinBox _exitSpeedInput = null!;

		public event Action? OnValuesChanged;
		public event Action<bool>? OnTestFireModeToggled;
		public event Action<int>? OnRunwaySelected;

		private ModuleDataDefinition? _boundData;
		private bool _isUpdating = false;
		private int _selectedRunwayIndex = -1;

		public override void _Ready()
		{
			BuildUI();
		}

		private void BuildUI()
		{
			AddThemeConstantOverride("separation", 10);

			// 1. 🎮 视口交互与开火测试
			var (testCard, testBox) = CreateCard("🎮 视口交互与弹射测试", new Color(0.35f, 0.95f, 0.55f));
			AddChild(testCard);

			_testFireToggle = new CheckButton
			{
				Text = "开启实时弹射起飞测试",
				SizeFlagsHorizontal = SizeFlags.ExpandFill,
				CustomMinimumSize = new Vector2(0, 32)
			};
			_testFireToggle.Toggled += (on) => OnTestFireModeToggled?.Invoke(on);
			testBox.AddChild(_testFireToggle);

			// 2. 无人机规格与母舱配置
			var (hangarCard, hangarBox) = CreateCard("🛸 舰载无人机配置与规格", new Color(0.35f, 0.95f, 0.65f));
			AddChild(hangarCard);
			
			_operationRadiusInput = CreateNumberRowToParent(hangarBox, "作战半径 (m):", 20, 1000, 10);

			_droneIdInput = CreateTextRowToParent(hangarBox, "机型 ID:");
			_droneIdInput.TextChanged += _ => EmitChange();

			var texRow = new HBoxContainer();
			texRow.AddChild(new Label { Text = "无人机贴图:", CustomMinimumSize = new Vector2(110, 0) });
			_droneSpriteInput = new LineEdit { SizeFlagsHorizontal = SizeFlags.ExpandFill, PlaceholderText = "modules/heavy_foundry/weapons/..." };
			_droneSpriteInput.TextChanged += _ => EmitChange();
			texRow.AddChild(_droneSpriteInput);

			_refreshTexBtn = new Button { Text = "🔄 刷新" };
			_refreshTexBtn.Pressed += () => EmitChange();
			texRow.AddChild(_refreshTexBtn);
			hangarBox.AddChild(texRow);

			_showOnRunwayToggle = new CheckButton
			{
				Text = "跑道常驻停靠 (开启: 露天停放; 关闭: 升降机内置/起飞出现)",
				SizeFlagsHorizontal = SizeFlags.ExpandFill
			};
			_showOnRunwayToggle.Toggled += _ => EmitChange();
			hangarBox.AddChild(_showOnRunwayToggle);

			_launchModeSelect = CreateOptionRowToParent(hangarBox, "弹射模式:", new[] { "Sequential (单机轮流出动)", "Burst (跑道依次快速弹射)", "Salvo (多跑道全机齐发)" });

			CreateDualNumberRowToParent(hangarBox, "机体尺寸 (px):", 6, 200, 1, out _droneWidthInput, out _droneLengthInput);
			_maxDronesInput = CreateNumberRowToParent(hangarBox, "载机上限 (架):", 1, 32, 1);
			_rebuildTimeInput = CreateNumberRowToParent(hangarBox, "整备重构 (秒):", 1.0, 60.0, 0.5);
			_pulseCostInput = CreateNumberRowToParent(hangarBox, "起飞耗电 (P):", 0.0, 100.0, 0.5);
			_launchIntervalInput = CreateNumberRowToParent(hangarBox, "跑道起飞间隔 (秒):", 0.05, 3.0, 0.05);

			// 3. 🛫 多跑道弹射系统
			var (runwayCard, runwayBox) = CreateCard("🛫 弹射跑道与滑跑拓扑 (Runways)", new Color(0.2f, 0.85f, 1.0f));
			AddChild(runwayCard);

			runwayBox.AddChild(new Label { Text = "🛫 弹射跑道列表 (点击选择/视口拖拽):" });
			_runwayList = new ItemList { CustomMinimumSize = new Vector2(0, 80), SizeFlagsHorizontal = SizeFlags.ExpandFill };
			_runwayList.ItemSelected += idx => SelectRunway((int)idx);
			runwayBox.AddChild(_runwayList);

			var btnHBox = new HBoxContainer();
			_addRunwayBtn = new Button { Text = "➕ 添加跑道", SizeFlagsHorizontal = SizeFlags.ExpandFill };
			_addRunwayBtn.Pressed += AddNewRunway;
			_removeRunwayBtn = new Button { Text = "🗑️ 删除跑道", SizeFlagsHorizontal = SizeFlags.ExpandFill };
			_removeRunwayBtn.Pressed += RemoveSelectedRunway;
			btnHBox.AddChild(_addRunwayBtn);
			btnHBox.AddChild(_removeRunwayBtn);
			runwayBox.AddChild(btnHBox);

			// 3.1 独立跑道属性微调面板
			var (runwayDetailCard, runwayDetailBox) = CreateCard("✏️ 选中跑道参数配置", new Color(1.0f, 0.85f, 0.35f));
			_runwayDetailCard = runwayDetailCard;
			_runwayIdInput = CreateTextRowToParent(runwayDetailBox, "跑道标识 (ID):");
			_runwayIdInput.TextChanged += _ => UpdateCurrentRunwayFromFields();

			_runwayOrderInput = CreateNumberRowToParent(runwayDetailBox, "发射序号 (0基):", 0, 32, 1);
			_runwayOrderInput.ValueChanged += _ => UpdateCurrentRunwayFromFields();

			CreateDualNumberRowToParent(runwayDetailBox, "停靠起点 (px):", -640, 640, 1, out _startXInput, out _startYInput);
			_startXInput.ValueChanged += _ => UpdateCurrentRunwayFromFields();
			_startYInput.ValueChanged += _ => UpdateCurrentRunwayFromFields();

			CreateDualNumberRowToParent(runwayDetailBox, "出舱出口 (px):", -640, 640, 1, out _exitXInput, out _exitYInput);
			_exitXInput.ValueChanged += _ => UpdateCurrentRunwayFromFields();
			_exitYInput.ValueChanged += _ => UpdateCurrentRunwayFromFields();

			_catapultDurationInput = CreateNumberRowToParent(runwayDetailBox, "滑跑弹射 (秒):", 0.05, 3.0, 0.05);
			_catapultDurationInput.ValueChanged += _ => UpdateCurrentRunwayFromFields();

			_exitSpeedInput = CreateNumberRowToParent(runwayDetailBox, "离舰初速 (px/s):", 50, 1500, 25);
			_exitSpeedInput.ValueChanged += _ => UpdateCurrentRunwayFromFields();

			_runwayDetailCard.Visible = false;
			runwayBox.AddChild(_runwayDetailCard);
		}

		public void BindData(ModuleDataDefinition data, int selectRunwayIdx = -1)
		{
			_boundData = data;
			_isUpdating = true;

			var hp = data.GetProperties<HangarProperties>() ?? new HangarProperties();
			_operationRadiusInput.Value = hp.OperationRadius > 0 ? hp.OperationRadius : 150.0f;
			_droneIdInput.Text = hp.DroneId ?? string.Empty;
			_droneSpriteInput.Text = hp.DroneSprite ?? string.Empty;
			_showOnRunwayToggle.SetPressedNoSignal(hp.ShowDroneOnRunway);
			_launchModeSelect.Select((int)hp.LaunchMode);

			_droneWidthInput.Value = hp.DroneWidth > 0 ? hp.DroneWidth : 28.0f;
			_droneLengthInput.Value = hp.DroneLength > 0 ? hp.DroneLength : 36.0f;

			_maxDronesInput.Value = hp.MaxDrones;
			_rebuildTimeInput.Value = hp.RebuildTime;
			_pulseCostInput.Value = hp.PulseCostPerLaunch;
			_launchIntervalInput.Value = hp.LaunchInterval > 0 ? hp.LaunchInterval : 0.4f;

			PopulateRunwayList(hp, selectRunwayIdx);
			_isUpdating = false;
		}

		private void PopulateRunwayList(HangarProperties hp, int selectIdx = -1)
		{
			_runwayList.Clear();
			if (hp.Runways == null || hp.Runways.Length == 0)
			{
				_runwayDetailCard.Visible = false;
				_selectedRunwayIndex = -1;
				return;
			}

			for (int i = 0; i < hp.Runways.Length; i++)
			{
				var rw = hp.Runways[i];
				_runwayList.AddItem($"[#{rw.LaunchOrder + 1}] {rw.RunwayId} ({rw.StartOffsetX},{rw.StartOffsetY}) ➔ ({rw.ExitOffsetX},{rw.ExitOffsetY})");
			}

			int target = Mathf.Clamp(selectIdx >= 0 ? selectIdx : _selectedRunwayIndex, 0, hp.Runways.Length - 1);
			_runwayList.Select(target);
			SelectRunway(target);
		}

		public void SelectRunway(int index)
		{
			_selectedRunwayIndex = index;
			if (_boundData == null) return;
			var hp = _boundData.GetProperties<HangarProperties>();
			if (hp?.Runways == null || index < 0 || index >= hp.Runways.Length)
			{
				_runwayDetailCard.Visible = false;
				return;
			}

			var rw = hp.Runways[index];
			_runwayDetailCard.Visible = true;

			_isUpdating = true;
			_runwayIdInput.Text = rw.RunwayId;
			_runwayOrderInput.Value = rw.LaunchOrder;
			_startXInput.Value = rw.StartOffsetX;
			_startYInput.Value = rw.StartOffsetY;
			_exitXInput.Value = rw.ExitOffsetX;
			_exitYInput.Value = rw.ExitOffsetY;
			_catapultDurationInput.Value = rw.CatapultDuration;
			_exitSpeedInput.Value = rw.ExitSpeed;
			_isUpdating = false;

			OnRunwaySelected?.Invoke(index);
		}

		private void UpdateCurrentRunwayFromFields()
		{
			if (_isUpdating || _boundData == null || _selectedRunwayIndex < 0) return;
			var hp = _boundData.GetProperties<HangarProperties>() ?? new HangarProperties();
			if (hp.Runways == null || _selectedRunwayIndex >= hp.Runways.Length) return;

			var rw = hp.Runways[_selectedRunwayIndex];
			rw.RunwayId = _runwayIdInput.Text.Trim();
			rw.LaunchOrder = (int)_runwayOrderInput.Value;
			rw.StartOffsetX = (float)_startXInput.Value;
			rw.StartOffsetY = (float)_startYInput.Value;
			rw.ExitOffsetX = (float)_exitXInput.Value;
			rw.ExitOffsetY = (float)_exitYInput.Value;
			rw.CatapultDuration = (float)_catapultDurationInput.Value;
			rw.ExitSpeed = (float)_exitSpeedInput.Value;

			_boundData.Properties = JsonSerializer.SerializeToElement(hp);
			_runwayList.SetItemText(_selectedRunwayIndex, $"[#{rw.LaunchOrder + 1}] {rw.RunwayId} ({rw.StartOffsetX},{rw.StartOffsetY}) ➔ ({rw.ExitOffsetX},{rw.ExitOffsetY})");
			EmitChange();
		}

		private void AddNewRunway()
		{
			if (_boundData == null) return;
			var hp = _boundData.GetProperties<HangarProperties>() ?? new HangarProperties();
			var list = new List<DroneRunwayDefinition>(hp.Runways ?? Array.Empty<DroneRunwayDefinition>());

			list.Add(new DroneRunwayDefinition
			{
				RunwayId = $"runway_{list.Count}",
				LaunchOrder = list.Count,
				StartOffsetX = 40 + list.Count * 20,
				StartOffsetY = 80,
				ExitOffsetX = 40 + list.Count * 20,
				ExitOffsetY = -20,
				CatapultDuration = 0.5f,
				ExitSpeed = 320.0f
			});

			hp.Runways = list.ToArray();
			_boundData.Properties = JsonSerializer.SerializeToElement(hp);
			BindData(_boundData, list.Count - 1);
			EmitChange();
		}

		private void RemoveSelectedRunway()
		{
			if (_boundData == null) return;
			var selected = _runwayList.GetSelectedItems();
			if (selected.Length == 0) return;

			var hp = _boundData.GetProperties<HangarProperties>() ?? new HangarProperties();
			var list = new List<DroneRunwayDefinition>(hp.Runways ?? Array.Empty<DroneRunwayDefinition>());
			int removeIdx = selected[0];

			if (removeIdx >= 0 && removeIdx < list.Count)
			{
				list.RemoveAt(removeIdx);
				for (int i = 0; i < list.Count; i++) list[i].LaunchOrder = i;
				hp.Runways = list.ToArray();
				_boundData.Properties = JsonSerializer.SerializeToElement(hp);
				BindData(_boundData, Mathf.Clamp(removeIdx - 1, 0, list.Count - 1));
				EmitChange();
			}
		}

		public void SelectRunwayExternal(int index)
		{
			if (index >= 0 && index < _runwayList.ItemCount)
			{
				_runwayList.Select(index);
				SelectRunway(index);
			}
			else
			{
				_runwayList.DeselectAll();
				_runwayDetailCard.Visible = false;
				_selectedRunwayIndex = -1;
			}
		}

		public void ApplyToData(ModuleDataDefinition data)
		{
			if (_isUpdating) return;
			var hp = data.GetProperties<HangarProperties>() ?? new HangarProperties();
			hp.OperationRadius = (float)_operationRadiusInput.Value;
			hp.DroneId = _droneIdInput.Text.Trim();
			hp.DroneSprite = _droneSpriteInput.Text.Trim();
			hp.ShowDroneOnRunway = _showOnRunwayToggle.ButtonPressed;
			hp.LaunchMode = (MissileFireMode)_launchModeSelect.Selected;
			hp.DroneWidth = (float)_droneWidthInput.Value;
			hp.DroneLength = (float)_droneLengthInput.Value;
			hp.MaxDrones = (int)_maxDronesInput.Value;
			hp.RebuildTime = (float)_rebuildTimeInput.Value;
			hp.PulseCostPerLaunch = (float)_pulseCostInput.Value;
			hp.LaunchInterval = (float)_launchIntervalInput.Value;
			data.Properties = JsonSerializer.SerializeToElement(hp);
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
			s1 = new SpinBox { MinValue = min, MaxValue = max, Step = step, SizeFlagsHorizontal = SizeFlags.ExpandFill, Prefix = "W/X:" };
			s2 = new SpinBox { MinValue = min, MaxValue = max, Step = step, SizeFlagsHorizontal = SizeFlags.ExpandFill, Prefix = "L/Y:" };
			s1.ValueChanged += _ => EmitChange();
			s2.ValueChanged += _ => EmitChange();
			hbox.AddChild(s1);
			hbox.AddChild(s2);
			parent.AddChild(hbox);
		}
	}
}
