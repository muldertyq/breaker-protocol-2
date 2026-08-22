using System;
using System.Collections.Generic;
using Godot;
using BreakerProtocol.Data.Models;

namespace BreakerProtocol.Data.Validation
{
	public enum ValidationSeverity
	{
		Info,
		Warning,
		Error
	}

	public class ValidationEntry
	{
		public ValidationSeverity Severity { get; set; }
		public string Message { get; set; } = string.Empty;

		public ValidationEntry(ValidationSeverity severity, string message)
		{
			Severity = severity;
			Message = message;
		}
	}

	/// <summary>
	/// 强类型数据合规校验器：拦截非法、越界及格式错误的数据
	/// </summary>
	public static class DataValidator
	{
		public static bool ValidateModule(ModuleDataDefinition module, string sourceFilePath, out List<ValidationEntry> entries)
		{
			entries = new List<ValidationEntry>();
			bool hasFatalError = false;

			// 1. 基础字段非空检查
			if (string.IsNullOrWhiteSpace(module.Id))
			{
				entries.Add(new ValidationEntry(ValidationSeverity.Error, "构件 ID 不能为空！"));
				hasFatalError = true;
			}

			if (string.IsNullOrWhiteSpace(module.Name))
			{
				entries.Add(new ValidationEntry(ValidationSeverity.Warning, $"构件 [{module.Id}] 未填写显示名称 (Name)。"));
			}

			// 2. 几何网格尺寸检查 (1x1 到 8x8)
			if (module.Width <= 0 || module.Height <= 0)
			{
				entries.Add(new ValidationEntry(ValidationSeverity.Error, $"构件 [{module.Id}] 尺寸非法 ({module.Width}x{module.Height})，宽高必须 >= 1！"));
				hasFatalError = true;
			}
			else if (module.Width > 8 || module.Height > 8)
			{
				entries.Add(new ValidationEntry(ValidationSeverity.Warning, $"构件 [{module.Id}] 尺寸过大 ({module.Width}x{module.Height})，超过推荐上限 8x8。"));
			}

			// 3. 物理属性合理性检查
			if (module.Mass <= 0.0f)
			{
				entries.Add(new ValidationEntry(ValidationSeverity.Warning, $"构件 [{module.Id}] 质量 <= 0，已自动修正为默认质量 1.0t。"));
				module.Mass = 1.0f;
			}

			if (module.BaseHp <= 0.0f)
			{
				entries.Add(new ValidationEntry(ValidationSeverity.Error, $"构件 [{module.Id}] 基础耐久 BaseHp 必须大于 0！"));
				hasFatalError = true;
			}

			// 4. 挂载与转向参数检查
			if (module.MountType is not ("Fixed" or "Turret" or "Hangar"))
			{
				entries.Add(new ValidationEntry(ValidationSeverity.Warning, $"构件 [{module.Id}] 挂载类型 [{module.MountType}] 未知，建议为 Fixed / Turret / Hangar。"));
			}

			if (module.RotationArc < 0.0f || module.RotationArc > 360.0f)
			{
				entries.Add(new ValidationEntry(ValidationSeverity.Error, $"构件 [{module.Id}] 旋转射界 RotationArc ({module.RotationArc}°) 超出 [0, 360] 范围！"));
				hasFatalError = true;
			}

			// 5. 引脚坐标与越界检查
			if (module.Pins != null && module.Pins.Length > 0)
			{
				HashSet<string> pinIdSet = new();
				HashSet<Vector2I> pinCoordSet = new();

				foreach (var pin in module.Pins)
				{
					if (string.IsNullOrWhiteSpace(pin.PinId))
					{
						entries.Add(new ValidationEntry(ValidationSeverity.Error, $"构件 [{module.Id}] 存在未命名的 Pin！"));
						hasFatalError = true;
					}
					else if (!pinIdSet.Add(pin.PinId))
					{
						entries.Add(new ValidationEntry(ValidationSeverity.Error, $"构件 [{module.Id}] 存在重复 PinId: [{pin.PinId}]！"));
						hasFatalError = true;
					}

					if (pin.LocalGridX < 0 || pin.LocalGridX >= module.Width ||
						pin.LocalGridY < 0 || pin.LocalGridY >= module.Height)
					{
						entries.Add(new ValidationEntry(ValidationSeverity.Error, 
							$"构件 [{module.Id}] 引脚 [{pin.PinId}] 坐标越界！坐标: ({pin.LocalGridX}, {pin.LocalGridY})，模块尺寸: {module.Width}x{module.Height}。"));
						hasFatalError = true;
					}

					Vector2I coord = new(pin.LocalGridX, pin.LocalGridY);
					if (!pinCoordSet.Add(coord))
					{
						entries.Add(new ValidationEntry(ValidationSeverity.Warning, 
							$"构件 [{module.Id}] 在网格 ({pin.LocalGridX}, {pin.LocalGridY}) 存在重叠引脚。"));
					}

					if (pin.Type is not ("IN" or "OUT"))
					{
						entries.Add(new ValidationEntry(ValidationSeverity.Error, 
							$"构件 [{module.Id}] 引脚 [{pin.PinId}] 类型 [{pin.Type}] 非法，必须是 'IN' 或 'OUT'。"));
						hasFatalError = true;
					}
				}
			}

			// 6. 控制台输出诊断报告
			if (entries.Count > 0)
			{
				foreach (var entry in entries)
				{
					string color = entry.Severity switch
					{
						ValidationSeverity.Error => "red",
						ValidationSeverity.Warning => "yellow",
						_ => "white"
					};
					GD.PrintRich($"[color={color}][DataValidator:{entry.Severity}] [{sourceFilePath}] -> {entry.Message}[/color]");
				}
			}

			return !hasFatalError;
		}
	}
}
