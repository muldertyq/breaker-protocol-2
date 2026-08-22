using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BreakerProtocol.Data.Models
{
	public class PinDefinition
	{
		[JsonPropertyName("pinId")] public string PinId { get; set; } = string.Empty;
		[JsonPropertyName("type")] public string Type { get; set; } = "IN"; // "IN" 或 "OUT"
		[JsonPropertyName("localGridX")] public int LocalGridX { get; set; } = 0;
		[JsonPropertyName("localGridY")] public int LocalGridY { get; set; } = 0;
		[JsonPropertyName("category")] public string Category { get; set; } = "Standard";
	}

	public class ModuleDataDefinition
	{
		[JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
		[JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
		[JsonPropertyName("faction")] public string Faction { get; set; } = "Universal"; 
		[JsonPropertyName("category")] public string Category { get; set; } = "Weapons"; 

		[JsonPropertyName("width")] public int Width { get; set; } = 1;
		[JsonPropertyName("height")] public int Height { get; set; } = 1;
		[JsonPropertyName("mass")] public float Mass { get; set; } = 1.0f;
		[JsonPropertyName("baseHp")] public float BaseHp { get; set; } = 100.0f;
		[JsonPropertyName("armorResistance")] public float ArmorResistance { get; set; } = 0.0f;

		// 挂载与射界控制
		[JsonPropertyName("mountType")] public string MountType { get; set; } = "Fixed"; // "Fixed" 或 "Turret"
		[JsonPropertyName("rotationArc")] public float RotationArc { get; set; } = 0.0f; // 0~360 度
		[JsonPropertyName("turnRate")] public float TurnRate { get; set; } = 0.0f;       // 旋转角速度 (度/秒)

		// 素材贴图路径 (相对对应数据包根目录)
		[JsonPropertyName("spriteBase")] public string SpriteBase { get; set; } = string.Empty;
		[JsonPropertyName("spriteOverlay")] public string SpriteOverlay { get; set; } = string.Empty;

		[JsonPropertyName("tags")] public string[] Tags { get; set; } = Array.Empty<string>();
		[JsonPropertyName("pins")] public PinDefinition[] Pins { get; set; } = Array.Empty<PinDefinition>();
		[JsonPropertyName("properties")] public JsonElement Properties { get; set; }
	}
}
