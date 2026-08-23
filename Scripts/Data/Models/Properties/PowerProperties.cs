using System.Text.Json.Serialization;

namespace BreakerProtocol.Data.Models.Properties
{
	public class PowerProperties
	{
		[JsonPropertyName("powerOutput")] public float PowerOutput { get; set; } = 0.0f; // 产电率 (+/s)
		[JsonPropertyName("maxHeat")] public float MaxHeat { get; set; } = 100.0f;
		[JsonPropertyName("coolingRate")] public float CoolingRate { get; set; } = 0.0f; // 散热率 (-/s)
		[JsonPropertyName("pulseCapacity")] public float PulseCapacity { get; set; } = 0.0f; // 电容容量
		[JsonPropertyName("requireExternal")] public bool RequireExternal { get; set; } = false; // 必须外置散热
		[JsonPropertyName("explosionRadius")] public float ExplosionRadius { get; set; } = 0.0f;
		[JsonPropertyName("explosionDamage")] public float ExplosionDamage { get; set; } = 0.0f;
	}
}
