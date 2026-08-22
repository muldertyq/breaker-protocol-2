using System;
using System.Linq;
using Godot;
using BreakerProtocol.Data;
using BreakerProtocol.Data.Models;

namespace BreakerProtocol.Test
{
	/// <summary>
	/// TASK-03 独立运行热重载测试场景（零 Autoload 依赖，完全动态加载）
	/// </summary>
	public partial class Test_Hot_Reload : Control
	{
		private ModuleDataService _dataService = null!;
		private OptionButton _moduleSelector = null!;
		private Label _infoLabel = null!;
		private Label _tipsLabel = null!;
		private ColorRect _flashIndicator = null!;
		private string? _selectedModuleId = null;

		public override void _Ready()
		{
			// 1. 初始化并挂载独立数据服务
			_dataService = new ModuleDataService();
			AddChild(_dataService);
			_dataService.DataReloaded += OnDataHotReloaded;

			// 2. 构建 UI
			SetAnchorsPreset(LayoutPreset.FullRect);
			var bg = new ColorRect { Color = new Color(0.08f, 0.09f, 0.12f) };
			bg.SetAnchorsPreset(LayoutPreset.FullRect);
			AddChild(bg);

			var selectLabel = new Label
			{
				Text = "选择监控构件:",
				Position = new Vector2(40, 30)
			};
			selectLabel.AddThemeFontSizeOverride("font_size", 16);
			AddChild(selectLabel);

			_moduleSelector = new OptionButton
			{
				Position = new Vector2(160, 25),
				Size = new Vector2(380, 35)
			};
			_moduleSelector.ItemSelected += OnModuleSelected;
			AddChild(_moduleSelector);

			_infoLabel = new Label
			{
				Position = new Vector2(40, 80),
				Size = new Vector2(800, 420)
			};
			_infoLabel.AddThemeFontSizeOverride("font_size", 18);
			_infoLabel.AddThemeColorOverride("font_color", new Color(0.25f, 0.95f, 0.5f));
			AddChild(_infoLabel);

			_flashIndicator = new ColorRect
			{
				Position = new Vector2(40, 515),
				Size = new Vector2(16, 16),
				Color = Colors.Transparent
			};
			AddChild(_flashIndicator);

			_tipsLabel = new Label
			{
				Position = new Vector2(65, 510),
				Size = new Vector2(850, 160),
				Text = "【热重载实时测试指引】\n" +
					   "1. 保持当前窗口运行；\n" +
					   "2. 在上方下拉菜单中切换需要监控的任意构件；\n" +
					   "3. 使用外部编辑器修改对应 JSON 文件中的数值并保存 (Ctrl+S)；\n" +
					   "4. 面板数值将在 0.2s 内自动刷新并闪烁绿灯；\n" +
                       "5. 尝试故意输入非法值（如 width: -1 或引脚越界），观察控制台的拦截报警。"
			};
			_tipsLabel.AddThemeFontSizeOverride("font_size", 15);
			_tipsLabel.AddThemeColorOverride("font_color", new Color(0.9f, 0.85f, 0.4f));
			AddChild(_tipsLabel);

			// 3. 动态填充并渲染
			PopulateModuleSelector();
			RefreshDisplay();
		}

		private void PopulateModuleSelector()
		{
			_moduleSelector.Clear();

			var allModules = _dataService.Modules.GetAll().OrderBy(m => m.Id).ToList();
			if (allModules.Count == 0)
			{
				_infoLabel.Text = "[⚠] 注册表中未扫描到构件，请检查 core_data/modules 目录。";
				_selectedModuleId = null;
				return;
			}

			int selectIndex = 0;
			for (int i = 0; i < allModules.Count; i++)
			{
				var mod = allModules[i];
				_moduleSelector.AddItem($"{mod.Id} ({mod.Name})", i);

				if (mod.Id == _selectedModuleId)
				{
					selectIndex = i;
				}
			}

			_moduleSelector.Select(selectIndex);
			_selectedModuleId = allModules[selectIndex].Id;
		}

		private void OnModuleSelected(long index)
		{
			string itemText = _moduleSelector.GetItemText((int)index);
			_selectedModuleId = itemText.Split(' ')[0];
			RefreshDisplay();
		}

		private void OnDataHotReloaded()
		{
			string? prevSelected = _selectedModuleId;
			PopulateModuleSelector();

			if (!string.IsNullOrEmpty(prevSelected) && _dataService.Modules.Contains(prevSelected))
			{
				_selectedModuleId = prevSelected;
			}

			RefreshDisplay();
			TriggerFlash();
		}

		private void RefreshDisplay()
		{
			if (string.IsNullOrEmpty(_selectedModuleId)) return;

			if (_dataService.Modules.TryGet(_selectedModuleId, out var mod) && mod != null)
			{
				string mountInfo = mod.MountType == "Turret"
					? $"Turret (射界: {mod.RotationArc}°, 转向: {mod.TurnRate}°/s)"
					: mod.MountType;

				_infoLabel.Text = $"【《断路协议》构件运行时热更监控器】\n" +
								  $"----------------------------------------------------------------------\n" +
								  $"构件 ID (ID):          {mod.Id}\n" +
								  $"显示名称 (Name):       {mod.Name}\n" +
								  $"所属阵营 (Faction):    {mod.Faction}\n" +
								  $"分类 (Category):       {mod.Category}\n" +
								  $"网格尺寸 (Size):       {mod.Width} x {mod.Height} GU ({mod.Width * 80}x{mod.Height * 80} px)\n" +
								  $"机组质量 (Mass):       {mod.Mass:F1} 吨\n" +
								  $"基础耐久 (BaseHp):     {mod.BaseHp:F0} HP\n" +
								  $"装甲抗性 (Armor):      {mod.ArmorResistance:F1}\n" +
								  $"挂载方式 (Mount):      {mountInfo}\n" +
								  $"引脚数量 (Pins):       {mod.Pins.Length} 个引脚\n" +
								  $"贴图路径 (Base):       {mod.SpriteBase}\n" +
								  $"覆盖贴图 (Overlay):    {(string.IsNullOrEmpty(mod.SpriteOverlay) ? "(无)" : mod.SpriteOverlay)}\n" +
								  $"核心动态参数:         {mod.Properties.GetRawText()}\n" +
								  $"----------------------------------------------------------------------\n" +
								  $"最新同步时间戳:        {DateTime.Now:HH:mm:ss.fff}";
			}
			else
			{
				_infoLabel.Text = $"[✘] 构件 [{_selectedModuleId}] 已被移除或不存在。";
			}
		}

		private void TriggerFlash()
		{
			_flashIndicator.Color = Colors.Green;
			var tween = CreateTween();
			tween.TweenProperty(_flashIndicator, "color", Colors.Transparent, 0.4);
		}
	}
}
