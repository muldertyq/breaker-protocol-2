using System.Text.Json.Serialization;

namespace BreakerProtocol.Data.Models
{
	/// <summary>
	/// 阵营颜色配置字典
	/// </summary>
	public class FactionPalette
	{
		[JsonPropertyName("armorBaseDark")] public string ArmorBaseDark { get; set; } = "#1e212b";
		[JsonPropertyName("armorBaseMid")] public string ArmorBaseMid { get; set; } = "#2d3242";
		[JsonPropertyName("armorHighlight")] public string ArmorHighlight { get; set; } = "#8892b0";
		[JsonPropertyName("stripePrimary")] public string StripePrimary { get; set; } = "#ff7700";
		[JsonPropertyName("stripeSecondary")] public string StripeSecondary { get; set; } = "#ffd000";
		[JsonPropertyName("emissivePulse")] public string EmissivePulse { get; set; } = "#ff9900";
		[JsonPropertyName("emissiveHot")] public string EmissiveHot { get; set; } = "#ff3300";
		[JsonPropertyName("shieldColor")] public string ShieldColor { get; set; } = "#33bbff";
	}

	/// <summary>
	/// 阵营元数据定义
	/// </summary>
	public class FactionDefinition
	{
		[JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
		[JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
		[JsonPropertyName("description")] public string Description { get; set; } = string.Empty;
		[JsonPropertyName("palette")] public FactionPalette Palette { get; set; } = new();
	}
}
