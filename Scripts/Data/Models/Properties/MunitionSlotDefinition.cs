using System.Text.Json.Serialization;

namespace BreakerProtocol.Data.Models.Properties
{
	public class MunitionSlotDefinition
	{
		[JsonPropertyName("slotId")] public string SlotId { get; set; } = "slot_0";
		[JsonPropertyName("bayId")] public string BayId { get; set; } = "bay_0";
		[JsonPropertyName("fireOrder")] public int FireOrder { get; set; } = 0;

		[JsonPropertyName("offsetX")] public float OffsetX { get; set; } = 0.0f;
		[JsonPropertyName("offsetY")] public float OffsetY { get; set; } = 0.0f;
		[JsonPropertyName("width")] public float Width { get; set; } = 14.0f;
		[JsonPropertyName("length")] public float Length { get; set; } = 42.0f;
		[JsonPropertyName("angleOffsetDeg")] public float AngleOffsetDeg { get; set; } = 0.0f;

		[JsonPropertyName("customSprite")] public string CustomSprite { get; set; } = string.Empty;
	}
}
