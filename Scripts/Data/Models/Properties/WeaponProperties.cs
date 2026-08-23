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

	public class MissileBayDefinition
	{
		[JsonPropertyName("bayId")] public string BayId { get; set; } = "bay_0";
		[JsonPropertyName("offsetX")] public float OffsetX { get; set; } = 40.0f;
		[JsonPropertyName("offsetY")] public float OffsetY { get; set; } = 40.0f;
		[JsonPropertyName("width")] public float Width { get; set; } = 32.0f;
		[JsonPropertyName("height")] public float Height { get; set; } = 48.0f;
		[JsonPropertyName("openDuration")] public float OpenDuration { get; set; } = 0.25f; // 开盖耗时 (秒)
		
		// "InstantHide"(直接消失), "Split"(左右对开), "SlideOut"(单侧滑开), "Fade"(透明渐隐)
		[JsonPropertyName("animationType")] public string AnimationType { get; set; } = "InstantHide"; 
		[JsonPropertyName("customHatchSprite")] public string CustomHatchSprite { get; set; } = string.Empty;
	}

	public class WeaponProperties
	{
		// 1. 发射与载荷类型
		[JsonPropertyName("deliveryType")] public string DeliveryType { get; set; } = "Ballistic";
		[JsonPropertyName("projectileDefId")] public string ProjectileDefId { get; set; } = "proj_torpedo_heavy";

		// 目标类型任意匹配；必须标签全部匹配；排除标签任意匹配即拒绝目标。
		[JsonPropertyName("targetTypes")] public string[] TargetTypes { get; set; } = Array.Empty<string>();
		[JsonPropertyName("requiredTargetTags")] public string[] RequiredTargetTags { get; set; } = Array.Empty<string>();
		[JsonPropertyName("excludedTargetTags")] public string[] ExcludedTargetTags { get; set; } = Array.Empty<string>();

		// 2. 视觉与弹道尺寸/着色
		[JsonPropertyName("bulletColorHex")] public string BulletColorHex { get; set; } = "#ffe066";
		[JsonPropertyName("bulletGlowHex")] public string BulletGlowHex { get; set; } = "#ff9900";
		[JsonPropertyName("projectileRadius")] public float ProjectileRadius { get; set; } = 3.0f;
		[JsonPropertyName("projectileLength")] public float ProjectileLength { get; set; } = 16.0f;
		[JsonPropertyName("beamWidth")] public float BeamWidth { get; set; } = 4.0f;
		[JsonPropertyName("beamDuration")] public float BeamDuration { get; set; } = 0.12f;

		// 3. 🚀 鱼雷/导弹挂架与多弹位系统
		[JsonPropertyName("defaultMissileSprite")] public string DefaultMissileSprite { get; set; } = string.Empty;
		[JsonPropertyName("defaultMissileWidth")] public float DefaultMissileWidth { get; set; } = 14.0f;
		[JsonPropertyName("defaultMissileLength")] public float DefaultMissileLength { get; set; } = 42.0f;
		[JsonPropertyName("showMissileOnRack")] public bool ShowMissileOnRack { get; set; } = true;
		[JsonPropertyName("trackingStrength")] public float TrackingStrength { get; set; } = 45.0f;
		[JsonPropertyName("munitionHp")] public float MunitionHp { get; set; } = 50.0f;

		// 导弹仓盖列表
		[JsonPropertyName("bays")] public MissileBayDefinition[] Bays { get; set; } = Array.Empty<MissileBayDefinition>();

		// 发射模式与时序
		[JsonPropertyName("fireMode")] public MissileFireMode FireMode { get; set; } = MissileFireMode.Burst;
		[JsonPropertyName("burstInterval")] public float BurstInterval { get; set; } = 0.2f;

		// 装填策略
		[JsonPropertyName("reloadMode")] public RackReloadMode ReloadMode { get; set; } = RackReloadMode.FullRack;
		[JsonPropertyName("reloadDuration")] public float ReloadDuration { get; set; } = 6.0f;

		// 多弹位拓扑列表
		[JsonPropertyName("munitionSlots")] public MunitionSlotDefinition[] MunitionSlots { get; set; } = Array.Empty<MunitionSlotDefinition>();

		// 4. 基础作战参数
		[JsonPropertyName("damage")] public float Damage { get; set; } = 280.0f;
		[JsonPropertyName("fireRate")] public float FireRate { get; set; } = 1.0f;
		[JsonPropertyName("range")] public float Range { get; set; } = 350.0f;
		[JsonPropertyName("speed")] public float Speed { get; set; } = 180.0f;
		[JsonPropertyName("pierce")] public int Pierce { get; set; } = 0;
		[JsonPropertyName("spread")] public float Spread { get; set; } = 0.0f;
		[JsonPropertyName("recoil")] public float Recoil { get; set; } = 150.0f;

		// 5. 资源消耗
		[JsonPropertyName("pulseCost")] public float PulseCost { get; set; } = 15.0f;
		[JsonPropertyName("heatPerShot")] public float HeatPerShot { get; set; } = 20.0f;
		[JsonPropertyName("ammoMax")] public int AmmoMax { get; set; } = 4;
	}
}
