using System.Text;
using System.Text.RegularExpressions;
using Godot;

namespace BreakerProtocol.Tools.ModuleEditor
{
	public partial class ModuleEditorHelpDialog : Window
	{
		private const string FullGuidePath = "res://MODULE_EDITOR_GUIDE.zh-CN.md";
		private RichTextLabel _guide = null!;
		private Button _quickGuideButton = null!;
		private Button _fullGuideButton = null!;

		private const string GuideBbCode = @"
[font_size=24][b]构件编辑器使用说明[/b][/font_size]
[color=#8da2c6]用于编辑现有构件 JSON。界面修改先保存在内存中，按“保存构件”或 Ctrl+S 才会写入文件。[/color]

[font_size=18][b][color=#61bdf2]一、基本流程[/color][/b][/font_size]
1. 在左侧按种族、分类或关键字筛选，并选择一个构件。
2. 在右侧修改基础属性和该分类的专属属性。
3. 需要编辑空间点位时，在顶部选择对应 Gizmo 模式，再到中央画布操作。
4. 检查预览和测试效果，最后保存；绿色 Toast 表示保存成功。

[font_size=18][b][color=#61bdf2]二、中央画布[/color][/b][/font_size]
• 鼠标滚轮：缩放。
• 中键拖动：平移画布。没有命中可删除对象时，右键拖动也可平移。
• 左键：选择、创建或拖动当前 Gizmo。
• 右键：删除当前 Gizmo 模式下命中的点位。
• “视口居中”：恢复适合当前构件的观察位置。

[font_size=18][b][color=#61bdf2]三、Gizmo 模式[/color][/b][/font_size]
• Pins：编辑引脚。引脚必须放在构件网格边缘；右侧可设置 IN/OUT 和通道类别。
• Bays：编辑导弹舱盖范围及开合方式。
• Munition Slots：编辑导弹或鱼雷在挂架上的弹位。
• Runways：编辑无人机起点、离舰点和弹射方向。
• Muzzles：编辑武器开火点、方向偏移和时序组。
• Exhausts：编辑推进器喷口位置、方向、尺寸和颜色。
• Turret Arc：编辑炮塔安装轴心和允许回转的射界。
• Shield：编辑护盾力场范围。
• Emissive：编辑发光层偏移和旋转中心。

[font_size=18][b][color=#61bdf2]四、引脚如何填写[/color][/b][/font_size]
• IN 表示构件接收该资源或信号，OUT 表示构件输出。
• PulsePower：常规脉冲电力；HeavyPulse：重型高压电力；Thermal：热量；Logic：控制信号；Universal：通用适配。
• 引脚 ID 根据类别、流向和位置自动生成。相同 ID、越界位置或非法边缘会导致保存校验失败。

[font_size=18][b][color=#61bdf2]五、武器与目标筛选[/color][/b][/font_size]
• 可命中类型：命中任意选中类型即可；空选表示不限制。
• 必须标签：目标必须同时拥有全部选中标签；空选表示不限制。
• 排除标签：目标拥有任意一个选中标签就会被排除。
• 同一标签不能同时出现在“必须标签”和“排除标签”中。
• 导弹、鱼雷使用弹体生命值；机库使用无人机生命值。它们必须大于 0。

[font_size=18][b][color=#61bdf2]六、多开火点与时序[/color][/b][/font_size]
• 每个开火点都有唯一 ID、局部 X/Y、方向偏移和时序组。
• 相同 sequenceIndex 的开火点在一次射击中同时齐射。
• 不同 sequenceIndex 按组号排序并在每次射击时轮流开火。
• 弹道、脉冲激光和持续光束都支持多个开火点。
• 开启“开火测试”后，移动鼠标瞄准，按住左键射击。切换构件会自动退出测试。

[font_size=18][b][color=#61bdf2]七、装饰模块[/color][/b][/font_size]
[b]适用条件[/b]决定装饰模块能作用于哪些武器：
• 武器标签：武器必须满足所选标签。
• 载荷类型：Ballistic、PulseBeam、ContinuousBeam、Missile 等，命中任意所选类型即可。
• 目标标签：只在命中目标符合标签时应用，通常配合 OnHit 效果。

[b]效果列表[/b]定义“何时触发、如何叠加”；[b]属性修改器[/b]定义“具体改什么”。一个效果可以包含多个属性修改器。

[b]示例：冷冻命中[/b]
效果：名称“冷冻命中”，触发 OnHit，叠加 Highest。
修改器 1：MoveSpeed / Decrease / Percent / 20。
修改器 2：StatusDuration / Set / Flat / 1。
这表示命中后降低目标 20% 移速，状态持续 1 秒。

触发方式：Passive 常驻；OnFire 开火时；OnHit 命中时；Interval 周期触发。
叠加规则：Additive 数值累加；Highest 只取最高值；Independent 各实例独立生效。
运算方式：Increase 增加；Decrease 减少；Set 直接设定。
数值类型：Percent 为百分比；Flat 为固定数值。数值填写正数，增减由运算方式决定。

[font_size=18][b][color=#61bdf2]八、保存与排错[/color][/b][/font_size]
• Ctrl+S 或顶部“保存构件”写入当前 JSON；保存后仍保持当前构件选中。
• 红色 Toast 表示校验或写入失败，请查看 Godot 控制台中的 DataValidator 消息。
• 常见失败原因：ID 为空或重复、引脚越界、开火点 ID 重复、生命值不大于 0、装饰效果没有修改器、必须标签与排除标签冲突。

[color=#8da2c6]仓库中的完整手册：[/color][url=full-guide][color=#61bdf2][u]MODULE_EDITOR_GUIDE.zh-CN.md[/u][/color][/url]
";

		public override void _Ready()
		{
			Visible = false;
			Title = "构件编辑器使用说明";
			MinSize = new Vector2I(720, 560);
			Size = new Vector2I(900, 720);
			Transient = true;
			Exclusive = true;
			WrapControls = true;
			CloseRequested += Hide;

			var background = new PanelContainer();
			background.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
			background.AddThemeStyleboxOverride("panel", new StyleBoxFlat
			{
				BgColor = new Color(0.08f, 0.09f, 0.12f),
				ContentMarginLeft = 20,
				ContentMarginRight = 20,
				ContentMarginTop = 18,
				ContentMarginBottom = 14
			});
			AddChild(background);

			var layout = new VBoxContainer();
			layout.AddThemeConstantOverride("separation", 12);
			background.AddChild(layout);

			var viewSwitch = new HBoxContainer();
			viewSwitch.AddThemeConstantOverride("separation", 6);
			_quickGuideButton = new Button { Text = "快速说明", CustomMinimumSize = new Vector2(110, 32) };
			_fullGuideButton = new Button { Text = "完整手册", CustomMinimumSize = new Vector2(110, 32) };
			_quickGuideButton.Pressed += ShowQuickGuide;
			_fullGuideButton.Pressed += ShowFullGuide;
			viewSwitch.AddChild(_quickGuideButton);
			viewSwitch.AddChild(_fullGuideButton);
			layout.AddChild(viewSwitch);

			_guide = new RichTextLabel
			{
				BbcodeEnabled = true,
				Text = GuideBbCode,
				FitContent = false,
				ScrollActive = true,
				SelectionEnabled = true,
				ContextMenuEnabled = true,
				SizeFlagsVertical = Control.SizeFlags.ExpandFill,
				SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
			};
			_guide.MetaClicked += meta =>
			{
				if (meta.AsString() == "full-guide") ShowFullGuide();
			};
			_guide.AddThemeFontSizeOverride("normal_font_size", 15);
			_guide.AddThemeConstantOverride("line_separation", 4);
			layout.AddChild(_guide);
			ShowQuickGuide();

			var closeButton = new Button
			{
				Text = "关闭",
				CustomMinimumSize = new Vector2(96, 32),
				SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd
			};
			closeButton.Pressed += Hide;
			layout.AddChild(closeButton);
		}

		public void Open()
		{
			PopupCentered(new Vector2I(900, 720));
		}

		private void ShowQuickGuide()
		{
			_guide.Text = GuideBbCode;
			_quickGuideButton.Disabled = true;
			_fullGuideButton.Disabled = false;
			_guide.ScrollToLine(0);
		}

		private void ShowFullGuide()
		{
			if (!FileAccess.FileExists(FullGuidePath))
			{
				_guide.Text = "[color=#ff7777]找不到完整手册文件：[/color] " + FullGuidePath;
			}
			else
			{
				_guide.Text = ConvertMarkdownToBbCode(FileAccess.GetFileAsString(FullGuidePath));
			}

			_quickGuideButton.Disabled = false;
			_fullGuideButton.Disabled = true;
			_guide.ScrollToLine(0);
		}

		private static string ConvertMarkdownToBbCode(string markdown)
		{
			var output = new StringBuilder();
			foreach (string rawLine in markdown.Replace("\r\n", "\n").Split('\n'))
			{
				string line = rawLine.TrimEnd();
				if (line.StartsWith("#### "))
					output.AppendLine($"[font_size=16][b][color=#a9bad8]{FormatInline(line[5..])}[/color][/b][/font_size]");
				else if (line.StartsWith("### "))
					output.AppendLine($"[font_size=17][b][color=#8fd6f7]{FormatInline(line[4..])}[/color][/b][/font_size]");
				else if (line.StartsWith("## "))
					output.AppendLine($"\n[font_size=19][b][color=#61bdf2]{FormatInline(line[3..])}[/color][/b][/font_size]");
				else if (line.StartsWith("# "))
					output.AppendLine($"[font_size=24][b]{FormatInline(line[2..])}[/b][/font_size]");
				else if (line.StartsWith("- "))
					output.AppendLine($"• {FormatInline(line[2..])}");
				else if (line.StartsWith('|'))
				{
					if (!Regex.IsMatch(line, @"^\|[\s|:\-]+\|$"))
						output.AppendLine(FormatInline(line.Trim('|').Replace("|", "    ")));
				}
				else
					output.AppendLine(FormatInline(line));
			}
			return output.ToString();
		}

		private static string FormatInline(string text)
		{
			string formatted = text.Replace("[", "[lb]").Replace("]", "[rb]");
			formatted = Regex.Replace(formatted, @"\*\*(.+?)\*\*", "[b]$1[/b]");
			return Regex.Replace(formatted, @"`([^`]+)`", "[color=#b8c9e8]$1[/color]");
		}
	}
}
