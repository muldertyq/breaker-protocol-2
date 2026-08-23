using System.Text.Json.Serialization;

namespace BreakerProtocol.Data.Models.Properties
{
	public class DroneRunwayDefinition
	{
		[JsonPropertyName("runwayId")] public string RunwayId { get; set; } = "runway_0";
		[JsonPropertyName("launchOrder")] public int LaunchOrder { get; set; } = 0;

		// 跑道起点（停靠/就绪锚点）
		[JsonPropertyName("startOffsetX")] public float StartOffsetX { get; set; } = 40.0f;
		[JsonPropertyName("startOffsetY")] public float StartOffsetY { get; set; } = 60.0f;

		// 跑道终点（出舱/离舰点火点）
		[JsonPropertyName("exitOffsetX")] public float ExitOffsetX { get; set; } = 40.0f;
		[JsonPropertyName("exitOffsetY")] public float ExitOffsetY { get; set; } = -10.0f;

		// 弹射滑跑耗时 (秒)
		[JsonPropertyName("catapultDuration")] public float CatapultDuration { get; set; } = 0.5f;

		// 出舱点火初速 (px/s)
		[JsonPropertyName("exitSpeed")] public float ExitSpeed { get; set; } = 320.0f;
	}
}
