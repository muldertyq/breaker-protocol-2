using System;
using System.IO;
using System.Text.Json;
using Godot;
using BreakerProtocol.Data;
using BreakerProtocol.Data.Models;
using BreakerProtocol.Data.Validation;

namespace BreakerProtocol.Tools.ModuleEditor.Core
{
	public class ModuleDocument
	{
		public ModuleDataDefinition CurrentData { get; private set; } = new();
		public string? ActiveFilePath { get; private set; }
		public bool IsDirty { get; set; } = false;

		public event Action? OnDocumentChanged;

		public void LoadFromFile(string fullPath)
		{
			if (!File.Exists(fullPath)) return;

			string json = File.ReadAllText(fullPath);
			var data = JsonSerializer.Deserialize<ModuleDataDefinition>(json, ModLoader.JsonOptions);
			if (data != null)
			{
				CurrentData = data;
				ActiveFilePath = fullPath;
				IsDirty = false;
				OnDocumentChanged?.Invoke();
			}
		}

		public bool Save()
		{
			if (string.IsNullOrEmpty(ActiveFilePath)) return false;

			// 1. 强类型合规校验
			if (!DataValidator.ValidateModule(CurrentData, ActiveFilePath, out var entries))
			{
				GD.PrintErr("[ModuleDocument] 保存被拦截：存在合规错误！");
				return false;
			}

			// 2. 格式化序列化并回写磁盘
			var options = new JsonSerializerOptions { WriteIndented = true };
			string outputJson = JsonSerializer.Serialize(CurrentData, options);
			File.WriteAllText(ActiveFilePath, outputJson);

			IsDirty = false;
			GD.PrintRich($"[color=green][✔] 构件 [{CurrentData.Id}] 已成功保存回磁盘！[/color]");
			return true;
		}
	}
}
