#nullable enable

using System;
using System.Text.Json.Serialization;

namespace BreakerProtocol.Data.Models.Properties
{
	public static class DecoratorTriggers
	{
		public const string Passive = "Passive";
		public const string OnFire = "OnFire";
		public const string OnHit = "OnHit";
		public const string Interval = "Interval";

		public static readonly string[] All = { Passive, OnFire, OnHit, Interval };
	}

	public static class DecoratorStackModes
	{
		public const string Additive = "Additive";
		public const string Highest = "Highest";
		public const string Independent = "Independent";

		public static readonly string[] All = { Additive, Highest, Independent };
	}

	public static class DecoratorModifierOperations
	{
		public const string Increase = "Increase";
		public const string Decrease = "Decrease";
		public const string Set = "Set";

		public static readonly string[] All = { Increase, Decrease, Set };
	}

	public static class DecoratorValueTypes
	{
		public const string Flat = "Flat";
		public const string Percent = "Percent";

		public static readonly string[] All = { Flat, Percent };
	}

	public static class DecoratorAttributes
	{
		public const string Damage = "Damage";
		public const string FireRate = "FireRate";
		public const string HeatPerShot = "HeatPerShot";
		public const string EnergyCost = "EnergyCost";
		public const string ProjectileCount = "ProjectileCount";
		public const string ScatterAngle = "ScatterAngle";
		public const string StunDuration = "StunDuration";
		public const string MoveSpeed = "MoveSpeed";
		public const string StatusDuration = "StatusDuration";
		public const string Pierce = "Pierce";
		public const string AmmoCapacity = "AmmoCapacity";
		public const string TriggerInterval = "TriggerInterval";
		public const string ExplosionChance = "ExplosionChance";
		public const string ExplosionRadius = "ExplosionRadius";
		public const string GuidanceTurnRate = "GuidanceTurnRate";

		public static readonly string[] All =
		{
			Damage,
			FireRate,
			HeatPerShot,
			EnergyCost,
			ProjectileCount,
			ScatterAngle,
			StunDuration,
			MoveSpeed,
			StatusDuration,
			Pierce,
			AmmoCapacity,
			TriggerInterval,
			ExplosionChance,
			ExplosionRadius,
			GuidanceTurnRate
		};
	}

	public class DecoratorModifierDefinition
	{
		[JsonPropertyName("attribute")] public string Attribute { get; set; } = DecoratorAttributes.Damage;
		[JsonPropertyName("operation")] public string Operation { get; set; } = DecoratorModifierOperations.Increase;
		[JsonPropertyName("valueType")] public string ValueType { get; set; } = DecoratorValueTypes.Percent;
		[JsonPropertyName("value")] public float Value { get; set; }
	}

	public class DecoratorEffectDefinition
	{
		[JsonPropertyName("effectId")] public string EffectId { get; set; } = "effect_0";
		[JsonPropertyName("name")] public string Name { get; set; } = "新效果";
		[JsonPropertyName("trigger")] public string Trigger { get; set; } = DecoratorTriggers.Passive;
		[JsonPropertyName("stackMode")] public string StackMode { get; set; } = DecoratorStackModes.Additive;
		[JsonPropertyName("modifiers")] public DecoratorModifierDefinition[] Modifiers { get; set; } = Array.Empty<DecoratorModifierDefinition>();
	}

	/// <summary>
	/// Data-only decorator contract. Empty condition arrays mean unrestricted.
	/// Weapon tags and target tags require every selected tag; delivery types match any selected value.
	/// </summary>
	public class DecoratorProperties
	{
		[JsonPropertyName("applicableWeaponTags")]
		public string[] ApplicableWeaponTags { get; set; } = Array.Empty<string>();

		[JsonPropertyName("applicableDeliveryTypes")]
		public string[] ApplicableDeliveryTypes { get; set; } = Array.Empty<string>();

		[JsonPropertyName("requiredTargetTags")]
		public string[] RequiredTargetTags { get; set; } = Array.Empty<string>();

		[JsonPropertyName("effects")]
		public DecoratorEffectDefinition[] Effects { get; set; } = Array.Empty<DecoratorEffectDefinition>();
	}
}
