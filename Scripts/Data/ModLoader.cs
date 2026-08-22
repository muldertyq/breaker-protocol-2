using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Godot;
using BreakerProtocol.Data.Models;
using BreakerProtocol.Data.Registries;
using BreakerProtocol.Data.Validation;

namespace BreakerProtocol.Data
{
	public class ModLoader
	{
		public Registry<ModuleDataDefinition> ModuleRegistry { get; } = new("Modules");
		public Dictionary<string, ModManifest> LoadedMods { get; } = new();

		private readonly Dictionary<string, string> _filePathToModuleId = new();

		public static readonly JsonSerializerOptions JsonOptions = new()
		{
			PropertyNameCaseInsensitive = true,
			ReadCommentHandling = JsonCommentHandling.Skip,
			AllowTrailingCommas = true
		};

		public void LoadAllMods()
		{
			ModuleRegistry.Clear();
			LoadedMods.Clear();
			_filePathToModuleId.Clear();

			GD.PrintRich("[color=cyan]================ [BreakerProtocol ModLoader 全量加载] ================[/color]");

			string rootPath = OS.HasFeature("editor") 
				? ProjectSettings.GlobalizePath("res://") 
				: OS.GetExecutablePath().GetBaseDir();

			string coreDataPath = Path.Combine(rootPath, "core_data");
			string modsRootPath = Path.Combine(rootPath, "mods");

			List<Tuple<ModManifest, string>> modPacks = new();

			// 1. 扫描核心包
			if (Directory.Exists(coreDataPath))
			{
				var coreManifest = LoadManifestFromDirectory(coreDataPath) ?? new ModManifest
				{
					Id = "core_data",
					Name = "《断路协议》官方核心数据包",
					Version = "1.0.0",
					Priority = 0,
					Enabled = true
				};
				coreManifest.Priority = 0;
				modPacks.Add(new Tuple<ModManifest, string>(coreManifest, coreDataPath));
			}

			// 2. 扫描玩家 Mod
			if (Directory.Exists(modsRootPath))
			{
				foreach (string subDir in Directory.GetDirectories(modsRootPath))
				{
					var modManifest = LoadManifestFromDirectory(subDir);
					if (modManifest != null && modManifest.Enabled)
					{
						modPacks.Add(new Tuple<ModManifest, string>(modManifest, subDir));
					}
				}
			}
			else
			{
				Directory.CreateDirectory(modsRootPath);
			}

			// 3. 优先级排序并加载
			var sortedMods = modPacks.OrderBy(m => m.Item1.Priority).ToList();
			foreach (var (manifest, dirPath) in sortedMods)
			{
				GD.PrintRich($"[color=green]>>> 正在加载: [{manifest.Name}] (v{manifest.Version}) 来自: {dirPath}[/color]");
				LoadedMods[manifest.Id] = manifest;
				LoadModulesFromMod(dirPath);
			}

			GD.PrintRich($"[color=cyan]================ [加载完成: {LoadedMods.Count} 个 Mod, {ModuleRegistry.Count} 个合法构件] ================[/color]");
		}

		public bool ReloadSingleFile(string filePath)
		{
			string fileName = Path.GetFileName(filePath);

			if (fileName.Equals("mod_manifest.json", StringComparison.OrdinalIgnoreCase))
			{
				LoadAllMods();
				return true;
			}

			if (filePath.Replace("\\", "/").Contains("/modules/"))
			{
				try
				{
					string jsonContent = File.ReadAllText(filePath);
					var moduleDef = JsonSerializer.Deserialize<ModuleDataDefinition>(jsonContent, JsonOptions);

					if (moduleDef == null)
					{
						GD.PrintErr($"[ModLoader] 热更新失败：文件 [{filePath}] 反序列化为空！");
						return false;
					}

					if (!DataValidator.ValidateModule(moduleDef, filePath, out _))
					{
						GD.PrintErr($"[ModLoader] 热更新拦截：文件 [{filePath}] 未通过数据校验！");
						return false;
					}

					ModuleRegistry.Register(moduleDef.Id, moduleDef, allowOverwrite: true);
					_filePathToModuleId[filePath] = moduleDef.Id;

					GD.PrintRich($"[color=green][✔] 构件 [{moduleDef.Id}] 毫秒级单文件热更新成功！[/color]");
					return true;
				}
				catch (JsonException jex)
				{
					GD.PrintErr($"[ModLoader] 热更新 JSON 语法错误: {jex.Message}");
					return false;
				}
				catch (Exception ex)
				{
					GD.PrintErr($"[ModLoader] 热更新异常: {ex.Message}");
					return false;
				}
			}

			return false;
		}

		private ModManifest? LoadManifestFromDirectory(string dirPath)
		{
			string manifestPath = Path.Combine(dirPath, "mod_manifest.json");
			if (!File.Exists(manifestPath)) return null;

			try
			{
				string jsonContent = File.ReadAllText(manifestPath);
				return JsonSerializer.Deserialize<ModManifest>(jsonContent, JsonOptions);
			}
			catch (Exception ex)
			{
				GD.PrintErr($"[ModLoader] 解析清单 [{manifestPath}] 失败: {ex.Message}");
				return null;
			}
		}

		private void LoadModulesFromMod(string modDirPath)
		{
			string modulesPath = Path.Combine(modDirPath, "modules");
			if (!Directory.Exists(modulesPath)) return;

			string[] moduleFiles = Directory.GetFiles(modulesPath, "*.json", SearchOption.AllDirectories);
			foreach (string filePath in moduleFiles)
			{
				try
				{
					string jsonContent = File.ReadAllText(filePath);
					var moduleDef = JsonSerializer.Deserialize<ModuleDataDefinition>(jsonContent, JsonOptions);

					if (moduleDef != null)
					{
						if (DataValidator.ValidateModule(moduleDef, filePath, out _))
						{
							ModuleRegistry.Register(moduleDef.Id, moduleDef, allowOverwrite: true);
							_filePathToModuleId[filePath] = moduleDef.Id;
						}
					}
				}
				catch (Exception ex)
				{
					GD.PrintErr($"[ModLoader] 解析构件 [{filePath}] 失败: {ex.Message}");
				}
			}
		}
	}
}
