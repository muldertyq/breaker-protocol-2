using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BreakerProtocol.Data.Models
{
	/// <summary>
	/// 构件完整元数据与拓扑定义 (对应 modules/*.json)
	/// </summary>
	public class ModuleDataDefinition
	{
		[JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
		[JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
		[JsonPropertyName("faction")] public string Faction { get; set; } = "Universal";
		[JsonPropertyName("category")] public string Category { get; set; } = "Weapons";

		// 几何与物理
		[JsonPropertyName("width")] public int Width { get; set; } = 1;
		[JsonPropertyName("height")] public int Height { get; set; } = 1;
		[JsonPropertyName("mass")] public float Mass { get; set; } = 1.0f;
		[JsonPropertyName("baseHp")] public float BaseHp { get; set; } = 100.0f;
		[JsonPropertyName("armorResistance")] public float ArmorResistance { get; set; } = 0.0f;

		// 挂载与射界
		[JsonPropertyName("mountType")] public string MountType { get; set; } = "Fixed"; // "Fixed", "Turret", "Hangar"
		[JsonPropertyName("rotationArc")] public float RotationArc { get; set; } = 0.0f;
		[JsonPropertyName("turnRate")] public float TurnRate { get; set; } = 0.0f;

		// 1. 底盘安装座物理位置 (Base Mount Point)
		[JsonPropertyName("pivotPixelX")] public float PivotPixelX { get; set; } = 40.0f;
		[JsonPropertyName("pivotPixelY")] public float PivotPixelY { get; set; } = 40.0f;

		// 2. 炮塔贴图自身转轴校准中心 (Sprite Local Axle Anchor)
		[JsonPropertyName("turretAnchorX")] public float TurretAnchorX { get; set; } = 40.0f;
		[JsonPropertyName("turretAnchorY")] public float TurretAnchorY { get; set; } = 40.0f;

		// 贴图通道 (相对于数据包根目录)
		[JsonPropertyName("spriteBase")] public string SpriteBase { get; set; } = string.Empty;
		[JsonPropertyName("spriteOverlay")] public string SpriteOverlay { get; set; } = string.Empty;
		[JsonPropertyName("spriteEmissive")] public string SpriteEmissive { get; set; } = string.Empty;

		// 发光层挂载模式与偏移控制
		[JsonPropertyName("emissiveAttachTo")] public string EmissiveAttachTo { get; set; } = "Overlay"; // "Overlay" (跟随炮塔/顶盖旋转) 或 "Base" (固定在底盘)
		[JsonPropertyName("emissiveOffsetX")] public float EmissiveOffsetX { get; set; } = 0.0f;
		[JsonPropertyName("emissiveOffsetY")] public float EmissiveOffsetY { get; set; } = 0.0f;
		[JsonPropertyName("emissiveAnchorX")] public float EmissiveAnchorX { get; set; } = 0.0f;
		[JsonPropertyName("emissiveAnchorY")] public float EmissiveAnchorY { get; set; } = 0.0f;

		// 标签与多锚点
		[JsonPropertyName("tags")] public string[] Tags { get; set; } = Array.Empty<string>();
		[JsonPropertyName("pins")] public PinDefinition[] Pins { get; set; } = Array.Empty<PinDefinition>();
		[JsonPropertyName("firePoints")] public FirePointDefinition[] FirePoints { get; set; } = Array.Empty<FirePointDefinition>();
		[JsonPropertyName("exhaustPoints")] public ExhaustPointDefinition[] ExhaustPoints { get; set; } = Array.Empty<ExhaustPointDefinition>();

		// 动态附加属性 (按分类强类型解析)
		[JsonPropertyName("properties")] public JsonElement Properties { get; set; }

		public T? GetProperties<T>() where T : class
		{
			if (Properties.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
			{
				return null;
			}

			try
			{
				return JsonSerializer.Deserialize<T>(Properties.GetRawText(), new JsonSerializerOptions
				{
					PropertyNameCaseInsensitive = true
				});
			}
			catch
			{
				return null;
			}
		}
	}
}
