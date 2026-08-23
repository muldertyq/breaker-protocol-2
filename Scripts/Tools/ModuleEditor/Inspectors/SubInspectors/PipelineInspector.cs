using System;
using System.Text.Json;
using Godot;
using BreakerProtocol.Data.Models;
using BreakerProtocol.Data.Models.Properties;

namespace BreakerProtocol.Tools.ModuleEditor.Inspectors.SubInspectors
{
	public partial class PipelineInspector : VBoxContainer
	{
		private SpinBox _lossRate = null!;
		private CheckBox _isolatedCheck = null!;

		public event Action? OnValuesChanged;
		private ModuleDataDefinition? _boundData;
		private bool _isUpdating = false;

		public override void _Ready()
		{
			AddChild(new Label { Text = "── 管线与芯片参数 ──", HorizontalAlignment = HorizontalAlignment.Center });
			_lossRate = CreateNumberRow("传输线损率:", 0.0, 1.0, 0.01);
			_isolatedCheck = CreateCheckRow("十字绝缘隔离");
		}

		public void BindData(ModuleDataDefinition data)
		{
			_boundData = data;
			_isUpdating = true;
			var pipe = data.GetProperties<PipelineProperties>() ?? new PipelineProperties();
			_lossRate.Value = pipe.LossRate;
			_isolatedCheck.ButtonPressed = pipe.Isolated;
			_isUpdating = false;
		}

		public void ApplyToData(ModuleDataDefinition data)
		{
			if (_isUpdating) return;
			var pipe = data.GetProperties<PipelineProperties>() ?? new PipelineProperties();
			pipe.LossRate = (float)_lossRate.Value;
			pipe.Isolated = _isolatedCheck.ButtonPressed;
			data.Properties = JsonSerializer.SerializeToElement(pipe);
		}

		private void EmitChange()
		{
			if (_isUpdating || _boundData == null) return;
			ApplyToData(_boundData);
			OnValuesChanged?.Invoke();
		}

		private SpinBox CreateNumberRow(string labelText, double min, double max, double step)
		{
			var hbox = new HBoxContainer();
			hbox.AddChild(new Label { Text = labelText, CustomMinimumSize = new Vector2(110, 0) });
			var spin = new SpinBox { MinValue = min, MaxValue = max, Step = step, SizeFlagsHorizontal = SizeFlags.ExpandFill };
			spin.ValueChanged += _ => EmitChange();
			hbox.AddChild(spin);
			AddChild(hbox);
			return spin;
		}

		private CheckBox CreateCheckRow(string labelText)
		{
			var hbox = new HBoxContainer();
			hbox.AddChild(new Label { Text = labelText, CustomMinimumSize = new Vector2(110, 0) });
			var check = new CheckBox();
			check.Toggled += _ => EmitChange();
			hbox.AddChild(check);
			AddChild(hbox);
			return check;
		}
	}
}
