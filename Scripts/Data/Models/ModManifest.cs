using System.Text.Json.Serialization;

namespace BreakerProtocol.Data.Models
{
	/// <summary>
	/// Mod 元数据清单定义 (对应 mod_manifest.json)
	/// </summary>
	public class ModManifest
	{
		[JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
		[JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
		[JsonPropertyName("version")] public string Version { get; set; } = "1.0.0";
		[JsonPropertyName("author")] public string Author { get; set; } = string.Empty;
		[JsonPropertyName("description")] public string Description { get; set; } = string.Empty;
		[JsonPropertyName("priority")] public int Priority { get; set; } = 100;
		[JsonPropertyName("enabled")] public bool Enabled { get; set; } = true;
	}
}
