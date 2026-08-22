using System;
using System.Text.Json.Serialization;

namespace BreakerProtocol.Data.Models
{
	/// <summary>
	/// Mod 元信息清单数据模型 (对应 mod_manifest.json)
	/// </summary>
	public class ModManifest
	{
		[JsonPropertyName("id")]
		public string Id { get; set; } = string.Empty;

		[JsonPropertyName("name")]
		public string Name { get; set; } = string.Empty;

		[JsonPropertyName("version")]
		public string Version { get; set; } = "1.0.0";

		[JsonPropertyName("author")]
		public string Author { get; set; } = "Unknown";

		[JsonPropertyName("description")]
		public string Description { get; set; } = string.Empty;

		/// <summary>
		/// 加载优先级：数字越小越先加载。官方核心包为 0，普通 Mod 默认为 100。
		/// </summary>
		[JsonPropertyName("priority")]
		public int Priority { get; set; } = 100;

		[JsonPropertyName("dependencies")]
		public string[] Dependencies { get; set; } = Array.Empty<string>();

		[JsonPropertyName("enabled")]
		public bool Enabled { get; set; } = true;
	}
}
