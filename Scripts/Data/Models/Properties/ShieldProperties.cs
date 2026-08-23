using System.Text.Json.Serialization;

namespace BreakerProtocol.Data.Models.Properties
{
	public class ShieldProperties
	{
		[JsonPropertyName("shieldType")] public string ShieldType { get; set; } = "DirectionalArc"; // "DirectionalArc", "OmniBubble"
		[JsonPropertyName("shieldArc")] public float ShieldArc { get; set; } = 180.0f;
		[JsonPropertyName("shieldRadius")] public float ShieldRadius { get; set; } = 120.0f; // 像素投影半径
		[JsonPropertyName("shieldCapacity")] public float ShieldCapacity { get; set; } = 600.0f;
		[JsonPropertyName("rechargeRate")] public float RechargeRate { get; set; } = 25.0f;
		[JsonPropertyName("pulseCost")] public float PulseCost { get; set; } = 8.0f; // 维持每秒耗电
		[JsonPropertyName("pulseCostOnDamage")] public float PulseCostOnDamage { get; set; } = 0.5f;
		[JsonPropertyName("heatPerAbsorb")] public float HeatPerAbsorb { get; set; } = 0.1f;
		[JsonPropertyName("overloadCooldown")] public float OverloadCooldown { get; set; } = 6.0f;
		[JsonPropertyName("deflectChance")] public float DeflectChance { get; set; } = 0.0f; // 斜面装甲跳弹率
	}
}
