using System.IO;
using System.Linq;
using Godot;
using BreakerProtocol.Data;
using BreakerProtocol.Data.Models;
using BreakerProtocol.Graphics;
using BreakerProtocol.Tools.ModuleEditor.Core;
using BreakerProtocol.Tools.ModuleEditor.Inspectors;
using BreakerProtocol.Tools.ModuleEditor.Viewport;

namespace BreakerProtocol.Tools.ModuleEditor
{
	public partial class ModuleEditorController : Control
	{
		private readonly ModuleDocument _document = new();
		private readonly ModuleDataService _dataService = new();

		private ModuleGridCanvas _canvas = null!;
		private BasePropInspector _inspector = null!;
		private ItemList _moduleList = null!;
		private LineEdit _searchBox = null!;
		private OptionButton _categoryFilter = null!;
		private OptionButton _gizmoModeSelect = null!;
		private Label _statusLabel = null!;
		private Label _coordLabel = null!;

		private string _activeCategoryFilter = "全部";

		public override void _Ready()
		{
			AddChild(_dataService);
			FactionPaletteManager.Instance.LoadAllFactions(ProjectSettings.GlobalizePath("res://core_data/factions"));

			BuildFullLayout();
			PopulateModuleList();

			_document.OnDocumentChanged += OnDocumentUpdated;
			_inspector.OnValuesChanged += OnInspectorValuesChanged;
			_canvas.OnDataModified += () =>
			{
				if (_canvas.CurrentModule != null)
				{
					_inspector.BindData(_canvas.CurrentModule);
				}
			};
			_canvas.OnMouseMovedInCanvas += (px) => _coordLabel.Text = $"局部像素: ({px.X:F0}, {px.Y:F0}) px";

			// 引脚联动
			_canvas.OnPinSelectedOnCanvas += (idx) => _inspector.SelectPinExternal(idx);
			_inspector.OnPinSelected += (idx) => _canvas.SelectPinExternal(idx);
			
			// 仓盖联动
			_canvas.OnBaySelectedOnCanvas += (idx) => _inspector.SelectBayExternal(idx);
			_inspector.OnBaySelected += (idx) => _canvas.SelectBayExternal(idx);

			// 弹位联动
			_canvas.OnSlotSelectedOnCanvas += (idx) => _inspector.SelectSlotExternal(idx);
			_inspector.OnSlotSelected += (idx) => _canvas.SelectSlotExternal(idx);

			// 跑道联动
			_canvas.OnRunwaySelectedOnCanvas += (idx) => _inspector.SelectRunwayExternal(idx);
			_inspector.OnRunwaySelected += (idx) => _canvas.SelectRunwayExternal(idx);

			_canvas.OnExhaustSelectedOnCanvas += (idx) => _inspector.SelectExhaustExternal(idx);
			_inspector.OnExhaustSelected += (idx) => _canvas.SelectExhaustExternal(idx);

			_inspector.OnTestFireModeToggled += (on) => _canvas.SetTestFiringMode(on);
		}

		private void BuildFullLayout()
		{
			SetAnchorsPreset(LayoutPreset.FullRect);
			AnchorRight = 1.0f;
			AnchorBottom = 1.0f;
			OffsetRight = 0;
			OffsetBottom = 0;
			SizeFlagsHorizontal = SizeFlags.ExpandFill;
			SizeFlagsVertical = SizeFlags.ExpandFill;

			var bgPanel = new ColorRect { Color = new Color(0.08f, 0.09f, 0.12f) };
			bgPanel.SetAnchorsPreset(LayoutPreset.FullRect);
			AddChild(bgPanel);

			var rootVBox = new VBoxContainer();
			rootVBox.SetAnchorsPreset(LayoutPreset.FullRect);
			rootVBox.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			rootVBox.SizeFlagsVertical = SizeFlags.ExpandFill;
			AddChild(rootVBox);

			// 1. 顶部操作栏
			var topBar = new PanelContainer { CustomMinimumSize = new Vector2(0, 44) };
			var topStyle = new StyleBoxFlat
			{
				BgColor = new Color(0.12f, 0.14f, 0.18f),
				BorderColor = new Color(0.22f, 0.26f, 0.35f),
				BorderWidthBottom = 1,
				ContentMarginLeft = 12,
				ContentMarginRight = 12,
				ContentMarginTop = 6,
				ContentMarginBottom = 6
			};
			topBar.AddThemeStyleboxOverride("panel", topStyle);

			var topHBox = new HBoxContainer();
			topHBox.AddThemeConstantOverride("separation", 10);

			var titleLabel = new Label { Text = "🛠️ 《断路协议》构件可视化编辑器" };
			titleLabel.AddThemeColorOverride("font_color", new Color(0.92f, 0.94f, 0.98f));
			titleLabel.AddThemeFontSizeOverride("font_size", 14);
			topHBox.AddChild(titleLabel);

			topHBox.AddChild(new VSeparator());

			var saveBtn = new Button { Text = " 💾 保存构件 (Ctrl+S) ", CustomMinimumSize = new Vector2(0, 30) };
			saveBtn.Pressed += SaveCurrentModule;
			topHBox.AddChild(saveBtn);

			var centerBtn = new Button { Text = " 🎯 视口居中 ", CustomMinimumSize = new Vector2(0, 30) };
			centerBtn.Pressed += () => _canvas.CenterView();
			topHBox.AddChild(centerBtn);

			topHBox.AddChild(new VSeparator());

			var gizmoLabel = new Label { Text = "Gizmo 模式:" };
			gizmoLabel.AddThemeColorOverride("font_color", new Color(0.7f, 0.75f, 0.85f));
			topHBox.AddChild(gizmoLabel);

			_gizmoModeSelect = new OptionButton { CustomMinimumSize = new Vector2(170, 30) };
			_gizmoModeSelect.AddItem("🚫 纯浏览模式 (None)", (int)EditGizmoMode.None);
			_gizmoModeSelect.AddItem("⚡ 引脚编辑 (Pins)", (int)EditGizmoMode.Pins);
			_gizmoModeSelect.AddItem("🚪 导弹仓盖 (Bays)", (int)EditGizmoMode.Bays);
			_gizmoModeSelect.AddItem("🚀 鱼雷弹位 (Munition Slots)", (int)EditGizmoMode.MunitionSlots);
			_gizmoModeSelect.AddItem("🛫 飞行跑道 (Runways)", (int)EditGizmoMode.Runways);
			_gizmoModeSelect.AddItem("🎯 开火点位 (Muzzles)", (int)EditGizmoMode.FirePoints);
			_gizmoModeSelect.AddItem("🔥 推进喷口 (Exhausts)", (int)EditGizmoMode.Exhausts);
			_gizmoModeSelect.AddItem("🔄 炮塔射界 (Turret Arc)", (int)EditGizmoMode.TurretArc);
			_gizmoModeSelect.AddItem("🛡️ 护盾力场 (Shield)", (int)EditGizmoMode.Shield);
			_gizmoModeSelect.AddItem("💡 发光通道 (Emissive)", (int)EditGizmoMode.Emissive);
			_gizmoModeSelect.Select(0);

			_gizmoModeSelect.ItemSelected += idx =>
			{
				_canvas.ActiveMode = (EditGizmoMode)_gizmoModeSelect.GetItemId((int)idx);
				_canvas.QueueRedraw();
			};
			topHBox.AddChild(_gizmoModeSelect);

			topBar.AddChild(topHBox);
			rootVBox.AddChild(topBar);

			// 2. 中间三栏工作区
			var mainArea = new HSplitContainer { SizeFlagsVertical = SizeFlags.ExpandFill, SizeFlagsHorizontal = SizeFlags.ExpandFill };
			rootVBox.AddChild(mainArea);

			var leftMarginWrapper = new MarginContainer
			{
				CustomMinimumSize = new Vector2(280, 0),
				SizeFlagsVertical = SizeFlags.ExpandFill
			};
			leftMarginWrapper.AddThemeConstantOverride("margin_left", 8);
			leftMarginWrapper.AddThemeConstantOverride("margin_right", 4);
			leftMarginWrapper.AddThemeConstantOverride("margin_top", 8);
			leftMarginWrapper.AddThemeConstantOverride("margin_bottom", 8);

			var leftPanelContainer = new PanelContainer { SizeFlagsVertical = SizeFlags.ExpandFill, SizeFlagsHorizontal = SizeFlags.ExpandFill };
			var leftCardStyle = new StyleBoxFlat
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
				ContentMarginLeft = 8,
				ContentMarginRight = 8,
				ContentMarginTop = 8
			};
			leftPanelContainer.AddThemeStyleboxOverride("panel", leftCardStyle);

			var leftVBox = new VBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill, SizeFlagsHorizontal = SizeFlags.ExpandFill };
			leftVBox.AddThemeConstantOverride("separation", 8);

			var listHeader = new Label { Text = "📦 构件资产库", HorizontalAlignment = HorizontalAlignment.Left };
			listHeader.AddThemeColorOverride("font_color", new Color(0.38f, 0.75f, 0.98f));
			listHeader.AddThemeFontSizeOverride("font_size", 13);
			leftVBox.AddChild(listHeader);
			leftVBox.AddChild(new HSeparator());

			_categoryFilter = new OptionButton { CustomMinimumSize = new Vector2(0, 30) };
			string[] categories = new[] { "全部", "Structural", "Power", "Propulsion", "Weapons", "Armor", "Pipeline" };
			for (int i = 0; i < categories.Length; i++) _categoryFilter.AddItem(categories[i], i);
			_categoryFilter.ItemSelected += idx => { _activeCategoryFilter = _categoryFilter.GetItemText((int)idx); PopulateModuleList(); };
			leftVBox.AddChild(_categoryFilter);

			_searchBox = new LineEdit { PlaceholderText = "🔍 搜索构件 ID / 名称...", CustomMinimumSize = new Vector2(0, 30) };
			_searchBox.TextChanged += _ => PopulateModuleList();
			leftVBox.AddChild(_searchBox);

			_moduleList = new ItemList
			{
				SizeFlagsVertical = SizeFlags.ExpandFill,
				SizeFlagsHorizontal = SizeFlags.ExpandFill,
				SelectMode = ItemList.SelectModeEnum.Single
			};
			var listStyle = new StyleBoxFlat
			{
				BgColor = new Color(0.08f, 0.09f, 0.12f, 0.9f),
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
			_moduleList.AddThemeStyleboxOverride("panel", listStyle);
			_moduleList.ItemSelected += OnModuleSelected;
			leftVBox.AddChild(_moduleList);

			leftPanelContainer.AddChild(leftVBox);
			leftMarginWrapper.AddChild(leftPanelContainer);
			mainArea.AddChild(leftMarginWrapper);

			var rightSplit = new HSplitContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
			mainArea.AddChild(rightSplit);

			_canvas = new ModuleGridCanvas { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
			_canvas.ActiveMode = EditGizmoMode.None;
			rightSplit.AddChild(_canvas);

			_inspector = new BasePropInspector();
			var scrollInspector = new ScrollContainer
			{
				CustomMinimumSize = new Vector2(420, 0),
				HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
				SizeFlagsVertical = SizeFlags.ExpandFill
			};

			var marginWrapper = new MarginContainer();
			marginWrapper.AddThemeConstantOverride("margin_left", 6);
			marginWrapper.AddThemeConstantOverride("margin_right", 12);
			marginWrapper.AddThemeConstantOverride("margin_top", 8);
			marginWrapper.AddThemeConstantOverride("margin_bottom", 12);
			marginWrapper.SizeFlagsHorizontal = SizeFlags.ExpandFill;

			marginWrapper.AddChild(_inspector);
			scrollInspector.AddChild(marginWrapper);
			rightSplit.AddChild(scrollInspector);

			// 3. 底部状态栏
			var statusBar = new PanelContainer { CustomMinimumSize = new Vector2(0, 28) };
			var statusStyle = new StyleBoxFlat
			{
				BgColor = new Color(0.09f, 0.10f, 0.13f),
				BorderColor = new Color(0.20f, 0.23f, 0.30f),
				BorderWidthTop = 1,
				ContentMarginLeft = 12,
				ContentMarginRight = 12
			};
			statusBar.AddThemeStyleboxOverride("panel", statusStyle);

			var statusHBox = new HBoxContainer();
			_statusLabel = new Label { Text = "就绪", SizeFlagsHorizontal = SizeFlags.ExpandFill };
			_statusLabel.AddThemeColorOverride("font_color", new Color(0.75f, 0.8f, 0.9f));
			_coordLabel = new Label { Text = "局部像素: (0, 0) px", CustomMinimumSize = new Vector2(180, 0) };
			_coordLabel.AddThemeColorOverride("font_color", new Color(0.5f, 0.7f, 0.9f));
			statusHBox.AddChild(_statusLabel);
			statusHBox.AddChild(_coordLabel);
			statusBar.AddChild(statusHBox);
			rootVBox.AddChild(statusBar);
		}

		private void PopulateModuleList()
		{
			_moduleList.Clear();
			string filter = _searchBox.Text.Trim().ToLower();

			var modules = _dataService.Modules.GetAll()
				.Where(m => _activeCategoryFilter == "全部" || m.Category.Equals(_activeCategoryFilter, System.StringComparison.OrdinalIgnoreCase))
				.Where(m => string.IsNullOrEmpty(filter) || m.Id.ToLower().Contains(filter) || m.Name.ToLower().Contains(filter))
				.OrderBy(m => m.Category)
				.ThenBy(m => m.Id)
				.ToList();

			for (int i = 0; i < modules.Count; i++)
			{
				var mod = modules[i];
				_moduleList.AddItem($"[{mod.Category}] {mod.Name} ({mod.Id})");
				_moduleList.SetItemMetadata(i, mod.Id);
			}

			if (modules.Count > 0 && _moduleList.GetSelectedItems().Length == 0)
			{
				_moduleList.Select(0);
				OnModuleSelected(0);
			}
		}

		private void OnModuleSelected(long index)
		{
			string moduleId = (string)_moduleList.GetItemMetadata((int)index);
			if (!_dataService.Modules.TryGet(moduleId, out var mod) || mod == null) return;

			string rootPath = OS.HasFeature("editor") ? ProjectSettings.GlobalizePath("res://") : OS.GetExecutablePath().GetBaseDir();
			string jsonPath = FindModuleJsonPath(rootPath, mod.Id);

			if (!string.IsNullOrEmpty(jsonPath))
			{
				_document.LoadFromFile(jsonPath);
			}
			else
			{
				LoadModuleToEditor(mod);
			}
		}

		private void OnDocumentUpdated()
		{
			LoadModuleToEditor(_document.CurrentData);
		}

		private void LoadModuleToEditor(ModuleDataDefinition mod)
		{
			Texture2D? baseTex = LoadTextureAuto(mod.SpriteBase);
			Texture2D? overlayTex = LoadTextureAuto(mod.SpriteOverlay);
			Texture2D? emissiveTex = LoadTextureAuto(mod.SpriteEmissive);

			_inspector.BindData(mod);
			_canvas.LoadModule(mod, baseTex, overlayTex, emissiveTex);
			_statusLabel.Text = $"当前加载: [{mod.Id}] - {mod.Name}";
		}

		private Texture2D? LoadTextureAuto(string relativePath)
		{
			if (string.IsNullOrWhiteSpace(relativePath)) return null;

			string resPath = "res://core_data/" + relativePath;
			if (ResourceLoader.Exists(resPath))
			{
				return GD.Load<Texture2D>(resPath);
			}

			string rootPath = OS.HasFeature("editor") ? ProjectSettings.GlobalizePath("res://") : OS.GetExecutablePath().GetBaseDir();
			string fullPath = Path.Combine(rootPath, "core_data", relativePath);
			if (File.Exists(fullPath))
			{
				var img = Image.LoadFromFile(fullPath);
				return ImageTexture.CreateFromImage(img);
			}
			return null;
		}

		private string FindModuleJsonPath(string rootPath, string moduleId)
		{
			string modulesDir = Path.Combine(rootPath, "core_data", "modules");
			if (!Directory.Exists(modulesDir)) return string.Empty;

			var files = Directory.GetFiles(modulesDir, $"{moduleId}.json", SearchOption.AllDirectories);
			return files.Length > 0 ? files[0] : string.Empty;
		}

		private void OnInspectorValuesChanged()
		{
			_canvas.ClearDemoEntities();
			_canvas.QueueRedraw();
		}

		private void SaveCurrentModule()
		{
			bool ok = _document.Save();
			_statusLabel.Text = ok ? "✅ 保存成功，已触发热更新！" : "❌ 保存失败，请检查控制台校验错误！";
		}
	}
}
