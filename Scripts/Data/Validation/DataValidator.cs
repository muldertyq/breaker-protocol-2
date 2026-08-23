using System;
using System.Collections.Generic;
using Godot;
using BreakerProtocol.Data.Models;
using BreakerProtocol.Data.Models.Properties;

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

			if (string.IsNullOrWhiteSpace(module.Faction))
			{
				entries.Add(new ValidationEntry(ValidationSeverity.Error, $"构件 [{module.Id}] 必须指定 faction。"));
				hasFatalError = true;
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
				HashSet<(Vector2I Coord, string Edge)> pinLocationSet = new();

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
					if (!pinLocationSet.Add((coord, pin.Edge)))
					{
						entries.Add(new ValidationEntry(ValidationSeverity.Warning, 
							$"构件 [{module.Id}] 在网格 ({pin.LocalGridX}, {pin.LocalGridY}) 的 {pin.Edge} 边存在重叠引脚。"));
					}

					if (pin.Edge is not ("Top" or "Bottom" or "Left" or "Right"))
					{
						entries.Add(new ValidationEntry(ValidationSeverity.Error,
							$"构件 [{module.Id}] 引脚 [{pin.PinId}] 边缘 [{pin.Edge}] 非法。"));
						hasFatalError = true;
					}

					if (pin.Type is not ("IN" or "OUT"))
					{
						entries.Add(new ValidationEntry(ValidationSeverity.Error, 
							$"构件 [{module.Id}] 引脚 [{pin.PinId}] 类型 [{pin.Type}] 非法，必须是 'IN' 或 'OUT'。"));
						hasFatalError = true;
					}
				}
			}

			if (module.Category.Equals("Weapons", StringComparison.OrdinalIgnoreCase) &&
				!module.MountType.Equals("Hangar", StringComparison.OrdinalIgnoreCase) &&
				module.FirePoints != null)
			{
				var firePointIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
				foreach (var firePoint in module.FirePoints)
				{
					if (string.IsNullOrWhiteSpace(firePoint.Id) || !firePointIds.Add(firePoint.Id))
					{
						entries.Add(new ValidationEntry(ValidationSeverity.Error,
							$"武器模块 [{module.Id}] 的发射口 ID 为空或重复: [{firePoint.Id}]。"));
						hasFatalError = true;
					}

					if (firePoint.SequenceIndex < 0)
					{
						entries.Add(new ValidationEntry(ValidationSeverity.Error,
							$"武器模块 [{module.Id}] 的发射口 [{firePoint.Id}] 时序组不能小于 0。"));
						hasFatalError = true;
					}
				}
			}

			// 6. 控制台输出诊断报告
			if (module.Category.Equals("Decorators", StringComparison.OrdinalIgnoreCase))
			{
				hasFatalError |= ValidateDecorator(module, entries);
			}
			else if (module.Category.Equals("Weapons", StringComparison.OrdinalIgnoreCase))
			{
				hasFatalError |= module.MountType.Equals("Hangar", StringComparison.OrdinalIgnoreCase)
					? ValidateHangar(module, entries)
					: ValidateWeaponTargeting(module, entries);
			}

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

		private static bool ValidateWeaponTargeting(ModuleDataDefinition module, List<ValidationEntry> entries)
		{
			var properties = module.GetProperties<WeaponProperties>();
			if (properties == null)
			{
				entries.Add(new ValidationEntry(ValidationSeverity.Error,
					$"武器模块 [{module.Id}] 的 properties 无法解析。"));
				return true;
			}

			bool hasError = ValidateTargetingTagConflict(
				module.Id,
				"武器模块",
				properties.RequiredTargetTags,
				properties.ExcludedTargetTags,
				entries);

			if (string.Equals(properties.DeliveryType, "Missile", StringComparison.OrdinalIgnoreCase) && properties.MunitionHp <= 0)
			{
				entries.Add(new ValidationEntry(ValidationSeverity.Error,
					$"武器模块 [{module.Id}] 的导弹/鱼雷弹体生命值必须大于 0。"));
				hasError = true;
			}

			return hasError;
		}

		private static bool ValidateHangar(ModuleDataDefinition module, List<ValidationEntry> entries)
		{
			var properties = module.GetProperties<HangarProperties>();
			if (properties == null)
			{
				entries.Add(new ValidationEntry(ValidationSeverity.Error,
					$"机库模块 [{module.Id}] 的 properties 无法解析。"));
				return true;
			}

			bool hasError = ValidateTargetingTagConflict(
				module.Id,
				"机库模块",
				properties.RequiredTargetTags,
				properties.ExcludedTargetTags,
				entries);

			if (properties.DroneHp <= 0)
			{
				entries.Add(new ValidationEntry(ValidationSeverity.Error,
					$"机库模块 [{module.Id}] 的无人机生命值必须大于 0。"));
				hasError = true;
			}

			return hasError;
		}

		private static bool ValidateTargetingTagConflict(
			string moduleId,
			string moduleKind,
			string[] requiredTargetTags,
			string[] excludedTargetTags,
			List<ValidationEntry> entries)
		{
			var conflicts = new HashSet<string>(requiredTargetTags ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
			conflicts.IntersectWith(excludedTargetTags ?? Array.Empty<string>());
			if (conflicts.Count == 0) return false;

			entries.Add(new ValidationEntry(ValidationSeverity.Error,
				$"{moduleKind} [{moduleId}] 的必须标签与排除标签冲突: {string.Join(", ", conflicts)}。"));
			return true;
		}

		private static bool ValidateDecorator(ModuleDataDefinition module, List<ValidationEntry> entries)
		{
			var properties = module.GetProperties<DecoratorProperties>();
			if (properties == null)
			{
				entries.Add(new ValidationEntry(ValidationSeverity.Error,
					$"装饰模块 [{module.Id}] 的 properties 无法解析。"));
				return true;
			}

			bool hasError = false;
			if (properties.Effects == null || properties.Effects.Length == 0)
			{
				entries.Add(new ValidationEntry(ValidationSeverity.Error,
					$"装饰模块 [{module.Id}] 至少需要定义一个效果。"));
				return true;
			}

			var effectIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			foreach (var effect in properties.Effects)
			{
				if (string.IsNullOrWhiteSpace(effect.EffectId) || !effectIds.Add(effect.EffectId))
				{
					entries.Add(new ValidationEntry(ValidationSeverity.Error,
						$"装饰模块 [{module.Id}] 的效果 ID 为空或重复: [{effect.EffectId}]。"));
					hasError = true;
				}

				if (string.IsNullOrWhiteSpace(effect.Name))
				{
					entries.Add(new ValidationEntry(ValidationSeverity.Error,
						$"装饰模块 [{module.Id}] 的效果 [{effect.EffectId}] 缺少名称。"));
					hasError = true;
				}

				if (Array.IndexOf(DecoratorTriggers.All, effect.Trigger) < 0)
				{
					entries.Add(new ValidationEntry(ValidationSeverity.Error,
						$"装饰模块 [{module.Id}] 的效果 [{effect.EffectId}] 使用了未知触发时机 [{effect.Trigger}]。"));
					hasError = true;
				}

				if (Array.IndexOf(DecoratorStackModes.All, effect.StackMode) < 0)
				{
					entries.Add(new ValidationEntry(ValidationSeverity.Error,
						$"装饰模块 [{module.Id}] 的效果 [{effect.EffectId}] 使用了未知叠加规则 [{effect.StackMode}]。"));
					hasError = true;
				}

				if (effect.Modifiers == null || effect.Modifiers.Length == 0)
				{
					entries.Add(new ValidationEntry(ValidationSeverity.Error,
						$"装饰模块 [{module.Id}] 的效果 [{effect.EffectId}] 至少需要一个属性修改项。"));
					hasError = true;
					continue;
				}

				foreach (var modifier in effect.Modifiers)
				{
					if (Array.IndexOf(DecoratorAttributes.All, modifier.Attribute) < 0)
					{
						entries.Add(new ValidationEntry(ValidationSeverity.Error,
							$"装饰模块 [{module.Id}] 的效果 [{effect.EffectId}] 使用了未知属性 [{modifier.Attribute}]。"));
						hasError = true;
					}

					if (Array.IndexOf(DecoratorModifierOperations.All, modifier.Operation) < 0)
					{
						entries.Add(new ValidationEntry(ValidationSeverity.Error,
							$"装饰模块 [{module.Id}] 的效果 [{effect.EffectId}] 使用了未知运算 [{modifier.Operation}]。"));
						hasError = true;
					}

					if (Array.IndexOf(DecoratorValueTypes.All, modifier.ValueType) < 0)
					{
						entries.Add(new ValidationEntry(ValidationSeverity.Error,
							$"装饰模块 [{module.Id}] 的效果 [{effect.EffectId}] 使用了未知数值类型 [{modifier.ValueType}]。"));
						hasError = true;
					}

					if (modifier.Value <= 0)
					{
						entries.Add(new ValidationEntry(ValidationSeverity.Error,
							$"装饰模块 [{module.Id}] 的效果 [{effect.EffectId}] 属性 [{modifier.Attribute}] 数值必须大于 0。"));
						hasError = true;
					}

					if (modifier.Attribute == DecoratorAttributes.ExplosionChance &&
						modifier.ValueType == DecoratorValueTypes.Percent && modifier.Value > 100)
					{
						entries.Add(new ValidationEntry(ValidationSeverity.Error,
							$"装饰模块 [{module.Id}] 的爆炸概率不能超过 100%。"));
						hasError = true;
					}
				}
			}

			if (properties.RequiredTargetTags != null && properties.RequiredTargetTags.Length > 0 &&
				!Array.Exists(properties.Effects, effect => effect.Trigger == DecoratorTriggers.OnHit))
			{
				entries.Add(new ValidationEntry(ValidationSeverity.Warning,
					$"装饰模块 [{module.Id}] 设置了命中目标标签，但没有 OnHit 效果。"));
			}

			return hasError;
		}
	}
}
