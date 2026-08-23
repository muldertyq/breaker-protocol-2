using System;
using System.Text.Json;
using Godot;
using BreakerProtocol.Data.Models;
using BreakerProtocol.Data.Models.Properties;

namespace BreakerProtocol.Tools.ModuleEditor.Inspectors.SubInspectors
{
	public partial class PowerInspector : VBoxContainer
	{
		private SpinBox _powerOutput = null!;
		private SpinBox _coolingRate = null!;
		private SpinBox _pulseCapacity = null!;
		private SpinBox _maxHeat = null!;

		public event Action? OnValuesChanged;
		private ModuleDataDefinition? _boundData;
		private bool _isUpdating = false;

		public override void _Ready()
		{
			BuildUI();
		}

		private void BuildUI()
		{
			AddThemeConstantOverride("separation", 10);

			var (card, content) = CreateCard("🔋 能源产出与热力参数", new Color(0.45f, 0.95f, 0.65f));
			AddChild(card);

			_powerOutput = CreateNumberRowToParent(content, "发电功率 (P/s):", 0, 10000, 5);
			_coolingRate = CreateNumberRowToParent(content, "散热速率 (H/s):", 0, 10000, 5);
			_pulseCapacity = CreateNumberRowToParent(content, "电容容量 (P):", 0, 50000, 50);
			_maxHeat = CreateNumberRowToParent(content, "热容上限 (H):", 10, 50000, 50);
		}

		public void BindData(ModuleDataDefinition data)
		{
			_boundData = data;
			_isUpdating = true;

			var pp = data.GetProperties<PowerProperties>() ?? new PowerProperties();
			_powerOutput.Value = pp.PowerOutput;
			_coolingRate.Value = pp.CoolingRate;
			_pulseCapacity.Value = pp.PulseCapacity;
			_maxHeat.Value = pp.MaxHeat;

			_isUpdating = false;
		}

		public void ApplyToData(ModuleDataDefinition data)
		{
			if (_isUpdating) return;

			var pp = data.GetProperties<PowerProperties>() ?? new PowerProperties();
			pp.PowerOutput = (float)_powerOutput.Value;
			pp.CoolingRate = (float)_coolingRate.Value;
			pp.PulseCapacity = (float)_pulseCapacity.Value;
			pp.MaxHeat = (float)_maxHeat.Value;

			data.Properties = JsonSerializer.SerializeToElement(pp);
		}

		private void EmitChange()
		{
			if (_isUpdating || _boundData == null) return;
			ApplyToData(_boundData);
			OnValuesChanged?.Invoke();
		}

		private (PanelContainer card, VBoxContainer content) CreateCard(string title, Color accentColor)
		{
			var panel = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
			var style = new StyleBoxFlat
			{
				BgColor = new Color(0.11f, 0.13f, 0.17f, 0.95f),
				BorderColor = new Color(0.22f, 0.26f, 0.35f, 0.8f),
				BorderWidthBottom = 1,
				BorderWidthLeft = 1,
				BorderWidthRight = 1,
				BorderWidthTop = 1,
				CornerRadiusBottomLeft = 6,
				CornerRadiusBottomRight = 6,
				CornerRadiusTopLeft = 6,
				CornerRadiusTopRight = 6,
				ContentMarginBottom = 8,
				ContentMarginLeft = 10,
				ContentMarginRight = 10,
				ContentMarginTop = 8
			};
			panel.AddThemeStyleboxOverride("panel", style);

			var vbox = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
			vbox.AddThemeConstantOverride("separation", 6);

			var header = new Label { Text = title, HorizontalAlignment = HorizontalAlignment.Left };
			header.AddThemeColorOverride("font_color", accentColor);
			header.AddThemeFontSizeOverride("font_size", 13);
			vbox.AddChild(header);
			vbox.AddChild(new HSeparator());

			panel.AddChild(vbox);
			return (panel, vbox);
		}

		private SpinBox CreateNumberRowToParent(Control parent, string labelText, double min, double max, double step)
		{
			var hbox = new HBoxContainer();
			hbox.AddChild(new Label { Text = labelText, CustomMinimumSize = new Vector2(110, 0) });
			var spin = new SpinBox { MinValue = min, MaxValue = max, Step = step, SizeFlagsHorizontal = SizeFlags.ExpandFill };
			spin.ValueChanged += _ => EmitChange();
			hbox.AddChild(spin);
			parent.AddChild(hbox);
			return spin;
		}
	}
}
