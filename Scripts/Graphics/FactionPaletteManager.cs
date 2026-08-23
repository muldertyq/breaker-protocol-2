using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Godot;
using BreakerProtocol.Data.Models;

namespace BreakerProtocol.Graphics
{
	public class FactionPaletteManager
	{
		public static FactionPaletteManager Instance { get; } = new();
		private readonly Dictionary<string, FactionDefinition> _factions = new();

		public void LoadAllFactions(string factionsDirPath)
		{
			_factions.Clear();
			if (!Directory.Exists(factionsDirPath)) return;

			foreach (var file in Directory.GetFiles(factionsDirPath, "*.json"))
			{
				try
				{
					string json = File.ReadAllText(file);
					var faction = JsonSerializer.Deserialize<FactionDefinition>(json);
					if (faction != null && !string.IsNullOrEmpty(faction.Id))
					{
						_factions[faction.Id] = faction;
					}
				}
				catch (System.Exception ex)
				{
					GD.PrintErr($"[FactionPaletteManager] 解析阵营配置失败 [{file}]: {ex.Message}");
				}
			}
		}

		public void ApplyPaletteToMaterial(ShaderMaterial material, string factionId)
		{
			if (!_factions.TryGetValue(factionId, out var faction)) return;

			var p = faction.Palette;
			material.SetShaderParameter("color_armor_dark", new Color(p.ArmorBaseDark));
			material.SetShaderParameter("color_armor_mid", new Color(p.ArmorBaseMid));
			material.SetShaderParameter("color_armor_highlight", new Color(p.ArmorHighlight));
			material.SetShaderParameter("color_stripe_primary", new Color(p.StripePrimary));
			material.SetShaderParameter("color_stripe_secondary", new Color(p.StripeSecondary));
			material.SetShaderParameter("color_emissive_pulse", new Color(p.EmissivePulse));
			material.SetShaderParameter("color_shield", new Color(p.ShieldColor));
		}

		public IReadOnlyCollection<FactionDefinition> GetAllFactions() => _factions.Values;
	}
}
