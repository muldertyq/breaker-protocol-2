using System.Text.Json.Serialization;

namespace BreakerProtocol.Data.Models
{
	/// <summary>
	/// 武器开火/发射口挂载点定义 (支持多管、交替/齐射序列)
	/// </summary>
	public class FirePointDefinition
	{
		[JsonPropertyName("id")] public string Id { get; set; } = "muzzle_0";
		[JsonPropertyName("pixelOffsetX")] public float PixelOffsetX { get; set; } = 40.0f;
		[JsonPropertyName("pixelOffsetY")] public float PixelOffsetY { get; set; } = 0.0f;
		[JsonPropertyName("angleOffset")] public float AngleOffset { get; set; } = 0.0f; // 相对前向的偏角 (度)
		[JsonPropertyName("sequenceIndex")] public int SequenceIndex { get; set; } = 0;   // 开火时序分组
	}

	/// <summary>
	/// 推进器尾喷口定义 (支持多并联喉衬与矢量偏转)
	/// </summary>
	public class ExhaustPointDefinition
	{
		[JsonPropertyName("id")] public string Id { get; set; } = "exhaust_0";
		[JsonPropertyName("pixelOffsetX")] public float PixelOffsetX { get; set; } = 40.0f;
		[JsonPropertyName("pixelOffsetY")] public float PixelOffsetY { get; set; } = 80.0f;
		[JsonPropertyName("dirX")] public float DirX { get; set; } = 0.0f;
		[JsonPropertyName("dirY")] public float DirY { get; set; } = 1.0f;
		[JsonPropertyName("flameLength")] public float FlameLength { get; set; } = 40.0f;
		[JsonPropertyName("flameWidth")] public float FlameWidth { get; set; } = 16.0f;
		[JsonPropertyName("flameColorHex")] public string FlameColorHex { get; set; } = "#38bdf8";
	}
}
