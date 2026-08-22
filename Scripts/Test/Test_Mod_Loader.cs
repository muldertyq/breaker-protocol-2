using System;
using System.IO;
using System.Linq;
using Godot;
using BreakerProtocol.Data;
using BreakerProtocol.Data.Models;

namespace BreakerProtocol.Test
{
	/// <summary>
	/// 独立构件加载与资产完整性测试器（零 Autoload 依赖，完全动态读取）
	/// </summary>
	public partial class Test_Mod_Loader : Node
	{
		public override void _Ready()
		{
			RunDynamicVerification();
		}

		public void RunDynamicVerification()
		{
			GD.PrintRich("\n[color=cyan]================ [开始全自动构件完整性验证] ================[/color]");

			// 1. 现场实例化独立的 Mod 加载引擎
			var loader = new ModLoader();
			loader.LoadAllMods();
			var registry = loader.ModuleRegistry;

			string rootPath = OS.HasFeature("editor") 
				? ProjectSettings.GlobalizePath("res://") 
				: OS.GetExecutablePath().GetBaseDir();

			var allLoaded = registry.GetAll().ToList();

			// 2. 动态检索磁盘所有模块 JSON（排除清单文件）
			var diskJsonFiles = Directory.GetFiles(rootPath, "*.json", SearchOption.AllDirectories)
				.Where(f => !f.EndsWith("mod_manifest.json", StringComparison.OrdinalIgnoreCase))
				.Where(f => f.Replace("\\", "/").Contains("/modules/"))
				.ToList();

			GD.PrintRich($"[color=white]磁盘物理 JSON 文件: [b]{diskJsonFiles.Count}[/b] 个 | 注册表成功载入: [b]{allLoaded.Count}[/b] 个[/color]");

			// 3. 分阵营与分类动态格式化输出
			var factionGroups = allLoaded.GroupBy(m => m.Faction);
			foreach (var fGroup in factionGroups)
			{
				GD.PrintRich($"\n[color=yellow]▼ 阵营: [{fGroup.Key}] (共 {fGroup.Count()} 个构件)[/color]");

				var categoryGroups = fGroup.GroupBy(m => m.Category);
				foreach (var cGroup in categoryGroups)
				{
					GD.PrintRich($"  [color=gray]├─ 分类: {cGroup.Key} ({cGroup.Count()} 项)[/color]");
					foreach (var mod in cGroup)
					{
						string mountInfo = mod.MountType == "Turret"
							? $"Turret (Arc:{mod.RotationArc}°, Turn:{mod.TurnRate}°/s)"
							: mod.MountType;

						string pinSummary = mod.Pins != null && mod.Pins.Length > 0 
							? $"{mod.Pins.Length} 个引脚" 
							: "无引脚";

						GD.PrintRich($"  │   [color=green]✔[/color] [b]{mod.Id}[/b] | {mod.Name} | {mod.Width}x{mod.Height} GU | 质量:{mod.Mass}t | 耐久:{mod.BaseHp} | 挂载:{mountInfo} | [{pinSummary}]");

						// 校验物理贴图文件是否存在
						ValidateTexturePath(rootPath, mod.Id, "spriteBase", mod.SpriteBase);
						if (!string.IsNullOrWhiteSpace(mod.SpriteOverlay))
						{
							ValidateTexturePath(rootPath, mod.Id, "spriteOverlay", mod.SpriteOverlay);
						}
					}
				}
			}

			// 4. 磁盘物理文件与内存注册表一致性校验
			GD.PrintRich("\n[color=cyan]---------------- [磁盘与内存一致性校验] ----------------[/color]");
			if (diskJsonFiles.Count == allLoaded.Count)
			{
				GD.PrintRich($"[color=green][✔] 完美一致：磁盘上的 {diskJsonFiles.Count} 个模块已 100% 全部正确加载！[/color]");
			}
			else
			{
				GD.PrintRich($"[color=red][✘] 数量不一致：磁盘文件 {diskJsonFiles.Count} 个，实际加载 {allLoaded.Count} 个！请检查是否存在 JSON 语法错误或数据校验拦截。[/color]");
			}

			GD.PrintRich("[color=cyan]========================================================[/color]\n");
		}

		private static void ValidateTexturePath(string rootPath, string moduleId, string fieldName, string relativePath)
		{
			if (string.IsNullOrWhiteSpace(relativePath)) return;

			string fullPath = Path.Combine(rootPath, "core_data", relativePath);
			if (!File.Exists(fullPath))
			{
				fullPath = Path.Combine(rootPath, relativePath);
			}

			if (!File.Exists(fullPath))
			{
				GD.PrintRich($"  │       [color=red]✘ [{moduleId}] 贴图丢失: {relativePath}[/color]");
			}
		}
	}
}
