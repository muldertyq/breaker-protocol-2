using System.Text.Json.Serialization;

namespace BreakerProtocol.Data.Models.Properties
{
	public class PropulsionProperties
	{
		[JsonPropertyName("thrustForce")] public float ThrustForce { get; set; } = 5000.0f;
		[JsonPropertyName("torquePower")] public float TorquePower { get; set; } = 1500.0f;
		[JsonPropertyName("boostMultiplier")] public float BoostMultiplier { get; set; } = 1.5f;
		[JsonPropertyName("pulseCost")] public float PulseCost { get; set; } = 1.0f;
		[JsonPropertyName("heatPerSec")] public float HeatPerSec { get; set; } = 5.0f;
	}
}
