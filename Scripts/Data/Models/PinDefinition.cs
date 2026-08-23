using System.Text.Json.Serialization;

namespace BreakerProtocol.Data.Models
{
	/// <summary>
	/// 引脚连接点定义 (支持四向边缘吸附与多通道)
	/// </summary>
	public class PinDefinition
	{
		[JsonPropertyName("pinId")] public string PinId { get; set; } = string.Empty;
		[JsonPropertyName("type")] public string Type { get; set; } = "IN"; // "IN" / "OUT"
		[JsonPropertyName("localGridX")] public int LocalGridX { get; set; } = 0;
		[JsonPropertyName("localGridY")] public int LocalGridY { get; set; } = 0;
		[JsonPropertyName("edge")] public string Edge { get; set; } = "Top"; // "Top", "Bottom", "Left", "Right"
		[JsonPropertyName("category")] public string Category { get; set; } = "PulsePower"; // "PulsePower", "Thermal", "Logic"
	}
}
