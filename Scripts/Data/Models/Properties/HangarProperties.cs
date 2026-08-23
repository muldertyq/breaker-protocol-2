using System;
using System.Text.Json.Serialization;

namespace BreakerProtocol.Data.Models.Properties
{
	public class HangarProperties
	{
		[JsonPropertyName("droneId")] public string DroneId { get; set; } = "hf_drone_assault";
		[JsonPropertyName("droneSprite")] public string DroneSprite { get; set; } = "modules/heavy_foundry/weapons/hf_hangar_bay_drone_drone.png";
		[JsonPropertyName("droneWidth")] public float DroneWidth { get; set; } = 28.0f;
		[JsonPropertyName("droneLength")] public float DroneLength { get; set; } = 36.0f;

		// 是否在跑道起点常驻显示待命机体 (开启: 露天停机坪; 关闭: 升降机内置/起飞瞬间出现)
		[JsonPropertyName("showDroneOnRunway")] public bool ShowDroneOnRunway { get; set; } = false;

		// 起飞弹射模式 (轮射 / 连发 / 全跑道齐发)
		[JsonPropertyName("launchMode")] public MissileFireMode LaunchMode { get; set; } = MissileFireMode.Sequential;

		// 作战/巡逻指挥半径 (米，1m = 8px)
		[JsonPropertyName("operationRadius")] public float OperationRadius { get; set; } = 150.0f;

		[JsonPropertyName("maxDrones")] public int MaxDrones { get; set; } = 4;
		[JsonPropertyName("rebuildTime")] public float RebuildTime { get; set; } = 12.0f;
		[JsonPropertyName("pulseCostPerLaunch")] public float PulseCostPerLaunch { get; set; } = 10.0f;
		[JsonPropertyName("launchInterval")] public float LaunchInterval { get; set; } = 0.4f;

		[JsonPropertyName("runways")] public DroneRunwayDefinition[] Runways { get; set; } = Array.Empty<DroneRunwayDefinition>();
	}
}
