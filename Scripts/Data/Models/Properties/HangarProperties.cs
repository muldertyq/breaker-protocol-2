using System.Text.Json.Serialization;

namespace BreakerProtocol.Data.Models.Properties
{
	public class HangarProperties
	{
		[JsonPropertyName("maxDrones")] public int MaxDrones { get; set; } = 3;
		[JsonPropertyName("rebuildTime")] public float RebuildTime { get; set; } = 15.0f;
		[JsonPropertyName("droneId")] public string DroneId { get; set; } = "hf_drone_assault";
		[JsonPropertyName("pulseCostPerLaunch")] public float PulseCostPerLaunch { get; set; } = 10.0f;
	}
}
