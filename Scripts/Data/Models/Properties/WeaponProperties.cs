using System;
using System.Text.Json.Serialization;

namespace BreakerProtocol.Data.Models.Properties
{
	[JsonConverter(typeof(JsonStringEnumConverter))]
	public enum MissileFireMode
	{
		Sequential, // 轮射：每次开火触发队列中的下一枚
		Burst,      // 连发：按固定间隔依次清空所有已就绪槽位
		Salvo       // 齐射：所有已就绪槽位瞬间同时发射
	}

	[JsonConverter(typeof(JsonStringEnumConverter))]
	public enum RackReloadMode
	{
		FullRack,   // 整架装填：全部弹位打空后，统一倒计时完成全弹填充
		Incremental // 逐发装填：发射后各槽位按冷却时间依次独立填充
	}

	public class WeaponProperties
	{
		// 1. 发射与载荷类型
		[JsonPropertyName("deliveryType")] public string DeliveryType { get; set; } = "Ballistic"; // "Ballistic" (实弹), "PulseBeam" (脉冲激光), "ContinuousBeam" (持续光束), "Missile" (鱼雷/导弹)
		[JsonPropertyName("projectileDefId")] public string ProjectileDefId { get; set; } = "proj_torpedo_heavy";

		// 2. 视觉与弹道尺寸/着色
		[JsonPropertyName("bulletColorHex")] public string BulletColorHex { get; set; } = "#ffe066"; // 弹芯/弹头色
		[JsonPropertyName("bulletGlowHex")] public string BulletGlowHex { get; set; } = "#ff9900";   // 尾焰/辉光色
		[JsonPropertyName("projectileRadius")] public float ProjectileRadius { get; set; } = 3.0f;   // 实弹半径 (px)
		[JsonPropertyName("projectileLength")] public float ProjectileLength { get; set; } = 16.0f;  // 实弹长度 (px)
		[JsonPropertyName("beamWidth")] public float BeamWidth { get; set; } = 4.0f;                 // 激光光束宽度 (px)
		[JsonPropertyName("beamDuration")] public float BeamDuration { get; set; } = 0.12f;          // 脉冲激光时长 (秒)

		// 3. 🚀 鱼雷/导弹挂架与多弹位系统
		[JsonPropertyName("defaultMissileSprite")] public string DefaultMissileSprite { get; set; } = string.Empty; // 默认鱼雷贴图
		[JsonPropertyName("defaultMissileWidth")] public float DefaultMissileWidth { get; set; } = 14.0f;           // 默认鱼雷宽度 (px)
		[JsonPropertyName("defaultMissileLength")] public float DefaultMissileLength { get; set; } = 42.0f;         // 默认鱼雷长度 (px)
		[JsonPropertyName("showMissileOnRack")] public bool ShowMissileOnRack { get; set; } = true;                // 是否在架常驻可见 (true: 裸装鱼雷架; false: 蜂巢/内置垂发管)
		[JsonPropertyName("trackingStrength")] public float TrackingStrength { get; set; } = 45.0f;                // 寻标转向速率 (°/s)

		// 发射模式与时序
		[JsonPropertyName("fireMode")] public MissileFireMode FireMode { get; set; } = MissileFireMode.Burst;       // 发射模式 (轮射/连发/齐射)
		[JsonPropertyName("burstInterval")] public float BurstInterval { get; set; } = 0.2f;                        // 连发/齐射间隔 (秒)

		// 装填策略
		[JsonPropertyName("reloadMode")] public RackReloadMode ReloadMode { get; set; } = RackReloadMode.FullRack; // 装填模式 (整架/逐发)
		[JsonPropertyName("reloadDuration")] public float ReloadDuration { get; set; } = 6.0f;                      // 装填周期 (秒)

		// 多弹位拓扑列表
		[JsonPropertyName("munitionSlots")] public MunitionSlotDefinition[] MunitionSlots { get; set; } = Array.Empty<MunitionSlotDefinition>();

		// 4. 基础作战参数
		[JsonPropertyName("damage")] public float Damage { get; set; } = 280.0f;
		[JsonPropertyName("fireRate")] public float FireRate { get; set; } = 1.0f; // 发/秒
		[JsonPropertyName("range")] public float Range { get; set; } = 350.0f;     // 有效射程 (m)
		[JsonPropertyName("speed")] public float Speed { get; set; } = 180.0f;     // 初速/巡航速度 (m/s)
		[JsonPropertyName("pierce")] public int Pierce { get; set; } = 0;          // 穿透数
		[JsonPropertyName("spread")] public float Spread { get; set; } = 0.0f;     // 初始散布角 (度)
		[JsonPropertyName("recoil")] public float Recoil { get; set; } = 150.0f;   // 发射后坐力 (N)

		// 5. 资源消耗
		[JsonPropertyName("pulseCost")] public float PulseCost { get; set; } = 15.0f;
		[JsonPropertyName("heatPerShot")] public float HeatPerShot { get; set; } = 20.0f;
		[JsonPropertyName("ammoMax")] public int AmmoMax { get; set; } = 4;
	}
}
