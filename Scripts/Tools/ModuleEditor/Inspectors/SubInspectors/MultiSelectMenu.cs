using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace BreakerProtocol.Tools.ModuleEditor.Inspectors.SubInspectors
{
	internal sealed class MultiSelectMenu
	{
		private readonly MenuButton _button;
		private readonly List<string> _values = new();
		private readonly HashSet<string> _selected = new(StringComparer.OrdinalIgnoreCase);
		private readonly Action _changed;

		public MultiSelectMenu(Control parent, string labelText, string tooltip, Action changed)
		{
			_changed = changed;
			var row = new HBoxContainer();
			row.AddChild(new Label { Text = labelText, CustomMinimumSize = new Vector2(110, 0) });
			_button = new MenuButton
			{
				Text = "不限（全部）",
				TooltipText = tooltip,
				SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
				CustomMinimumSize = new Vector2(0, 30)
			};
			_button.GetPopup().HideOnCheckableItemSelection = false;
			_button.GetPopup().IdPressed += OnItemPressed;
			row.AddChild(_button);

			var clearButton = new Button
			{
				Text = "×",
				TooltipText = "清除选择",
				CustomMinimumSize = new Vector2(30, 30)
			};
			clearButton.Pressed += ClearSelection;
			row.AddChild(clearButton);
			parent.AddChild(row);
		}

		public void SetOptions(IEnumerable<string> options, IEnumerable<string> selected)
		{
			string[] selectedValues = selected
				.Where(value => !string.IsNullOrWhiteSpace(value))
				.Select(value => value.Trim())
				.ToArray();

			_values.Clear();
			_values.AddRange(options.Concat(selectedValues)
				.Where(value => !string.IsNullOrWhiteSpace(value))
				.Select(value => value.Trim())
				.Distinct(StringComparer.OrdinalIgnoreCase)
				.OrderBy(value => value));

			_selected.Clear();
			foreach (string value in selectedValues) _selected.Add(value);
			RebuildPopup();
		}

		public void SetSelected(IEnumerable<string> selected)
		{
			SetOptions(_values.ToArray(), selected);
		}

		public string[] GetSelected()
		{
			return _values.Where(value => _selected.Contains(value)).ToArray();
		}

		private void RebuildPopup()
		{
			var popup = _button.GetPopup();
			popup.Clear();
			for (int i = 0; i < _values.Count; i++)
			{
				popup.AddCheckItem(_values[i], i);
				popup.SetItemChecked(i, _selected.Contains(_values[i]));
			}
			UpdateButtonText();
		}

		private void OnItemPressed(long id)
		{
			int index = (int)id;
			if (index < 0 || index >= _values.Count) return;

			string value = _values[index];
			if (!_selected.Add(value)) _selected.Remove(value);
			_button.GetPopup().SetItemChecked(index, _selected.Contains(value));
			UpdateButtonText();
			_changed();
		}

		private void ClearSelection()
		{
			if (_selected.Count == 0) return;
			_selected.Clear();
			RebuildPopup();
			_changed();
		}

		private void UpdateButtonText()
		{
			_button.Text = _selected.Count switch
			{
				0 => "不限（全部）",
				1 => _selected.First(),
				_ => $"已选择 {_selected.Count} 项"
			};
		}
	}
}
