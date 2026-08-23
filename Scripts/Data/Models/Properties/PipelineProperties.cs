using System.Text.Json.Serialization;

namespace BreakerProtocol.Data.Models.Properties
{
	public class PipelineProperties
	{
		[JsonPropertyName("lossRate")] public float LossRate { get; set; } = 0.0f;
		[JsonPropertyName("ports")] public int Ports { get; set; } = 2;
		[JsonPropertyName("splitRatio")] public float[] SplitRatio { get; set; } = System.Array.Empty<float>();
		[JsonPropertyName("isolated")] public bool Isolated { get; set; } = false; // 是否绝缘跨线
	}
}
