using System;
using System.Collections.Generic;
using Godot;
using BreakerProtocol.Data.Models;

namespace BreakerProtocol.Tools.ModuleEditor.Inspectors.SubInspectors
{
	public partial class FirePointInspector : VBoxContainer
	{
		private ItemList _pointList = null!;
		private VBoxContainer _detailBox = null!;
		private LineEdit _idInput = null!;
		private SpinBox _positionX = null!;
		private SpinBox _positionY = null!;
		private SpinBox _angleOffset = null!;
		private SpinBox _sequenceIndex = null!;

		private ModuleDataDefinition? _boundData;
		private int _selectedIndex = -1;
		private bool _isUpdating;

		public event Action? OnValuesChanged;
		public event Action<int>? OnFirePointSelectedInInspector;

		public override void _Ready()
		{
			BuildUI();
		}

		private void BuildUI()
		{
			AddThemeConstantOverride("separation", 8);
			var (card, content) = CreateCard("开火点位与时序", new Color(1.0f, 0.55f, 0.25f));
			AddChild(card);

			_pointList = new ItemList
			{
				CustomMinimumSize = new Vector2(0, 82),
				SizeFlagsHorizontal = SizeFlags.ExpandFill,
				SelectMode = ItemList.SelectModeEnum.Single
			};
			_pointList.ItemSelected += index => SelectPoint((int)index, notifyCanvas: true);
			content.AddChild(_pointList);

			var commandRow = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.End };
			var addButton = new Button { Text = "+", TooltipText = "添加发射口", CustomMinimumSize = new Vector2(36, 30) };
			var removeButton = new Button { Text = "−", TooltipText = "删除当前发射口", CustomMinimumSize = new Vector2(36, 30) };
			addButton.Pressed += AddPoint;
			removeButton.Pressed += RemovePoint;
			commandRow.AddChild(addButton);
			commandRow.AddChild(removeButton);
			content.AddChild(commandRow);

			_detailBox = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
			_detailBox.AddThemeConstantOverride("separation", 6);
			content.AddChild(_detailBox);

			_idInput = CreateTextRow(_detailBox, "发射口 ID:");
			CreateDualNumberRow(_detailBox, "局部位置 (px):", -640, 640, 1, out _positionX, out _positionY);
			_angleOffset = CreateNumberRow(_detailBox, "方向偏移 (°):", -180, 180, 1);
			_sequenceIndex = CreateNumberRow(_detailBox, "时序组:", 0, 64, 1);
			_sequenceIndex.TooltipText = "相同时序组同时齐射；不同时序组按编号交替开火";

			_idInput.TextChanged += _ => ApplyFields();
			_positionX.ValueChanged += _ => ApplyFields();
			_positionY.ValueChanged += _ => ApplyFields();
			_angleOffset.ValueChanged += _ => ApplyFields();
			_sequenceIndex.ValueChanged += _ => ApplyFields();
			_detailBox.Visible = false;
		}

		public void BindData(ModuleDataDefinition data, int selectIndex = -1)
		{
			_boundData = data;
			RefreshList();
			int count = data.FirePoints?.Length ?? 0;
			if (count == 0)
			{
				_selectedIndex = -1;
				_detailBox.Visible = false;
				return;
			}

			int target = selectIndex >= 0 ? selectIndex : _selectedIndex;
			if (target < 0 || target >= count) target = 0;
			SelectPoint(target, notifyCanvas: false);
		}

		public void SelectFirePointExternal(int index)
		{
			if (_boundData?.FirePoints == null || index < 0 || index >= _boundData.FirePoints.Length)
			{
				_selectedIndex = -1;
				_pointList.DeselectAll();
				_detailBox.Visible = false;
				return;
			}
			SelectPoint(index, notifyCanvas: false);
		}

		private void SelectPoint(int index, bool notifyCanvas)
		{
			if (_boundData?.FirePoints == null || index < 0 || index >= _boundData.FirePoints.Length) return;
			_selectedIndex = index;
			_pointList.Select(index);
			_pointList.EnsureCurrentIsVisible();

			var point = _boundData.FirePoints[index];
			_isUpdating = true;
			_idInput.Text = point.Id;
			_positionX.Value = point.PixelOffsetX;
			_positionY.Value = point.PixelOffsetY;
			_angleOffset.Value = point.AngleOffset;
			_sequenceIndex.Value = point.SequenceIndex;
			_detailBox.Visible = true;
			_isUpdating = false;
			if (notifyCanvas) OnFirePointSelectedInInspector?.Invoke(index);
		}

		private void ApplyFields()
		{
			if (_isUpdating || _boundData?.FirePoints == null || _selectedIndex < 0 || _selectedIndex >= _boundData.FirePoints.Length) return;
			var point = _boundData.FirePoints[_selectedIndex];
			point.Id = _idInput.Text.Trim();
			point.PixelOffsetX = (float)_positionX.Value;
			point.PixelOffsetY = (float)_positionY.Value;
			point.AngleOffset = (float)_angleOffset.Value;
			point.SequenceIndex = (int)_sequenceIndex.Value;
			RefreshList();
			_pointList.Select(_selectedIndex);
			OnValuesChanged?.Invoke();
		}

		private void AddPoint()
		{
			if (_boundData == null) return;
			var points = new List<FirePointDefinition>(_boundData.FirePoints ?? Array.Empty<FirePointDefinition>());
			points.Add(new FirePointDefinition
			{
				Id = $"muzzle_{points.Count}",
				PixelOffsetX = _boundData.PivotPixelX,
				PixelOffsetY = Mathf.Max(0, _boundData.PivotPixelY - 40),
				SequenceIndex = 0
			});
			_boundData.FirePoints = points.ToArray();
			RefreshList();
			SelectPoint(points.Count - 1, notifyCanvas: true);
			OnValuesChanged?.Invoke();
		}

		private void RemovePoint()
		{
			if (_boundData?.FirePoints == null || _selectedIndex < 0 || _selectedIndex >= _boundData.FirePoints.Length) return;
			var points = new List<FirePointDefinition>(_boundData.FirePoints);
			points.RemoveAt(_selectedIndex);
			_boundData.FirePoints = points.ToArray();
			RefreshList();
			if (points.Count > 0)
			{
				SelectPoint(Mathf.Clamp(_selectedIndex, 0, points.Count - 1), notifyCanvas: true);
			}
			else
			{
				_selectedIndex = -1;
				_detailBox.Visible = false;
				OnFirePointSelectedInInspector?.Invoke(-1);
			}
			OnValuesChanged?.Invoke();
		}

		private void RefreshList()
		{
			_pointList.Clear();
			if (_boundData?.FirePoints == null) return;
			foreach (var point in _boundData.FirePoints)
			{
				_pointList.AddItem($"[组 {point.SequenceIndex}] {point.Id} ({point.PixelOffsetX:F0}, {point.PixelOffsetY:F0})  {point.AngleOffset:+0;-0;0}°");
			}
		}

		private static LineEdit CreateTextRow(Control parent, string labelText)
		{
			var row = new HBoxContainer();
			row.AddChild(new Label { Text = labelText, CustomMinimumSize = new Vector2(110, 0) });
			var input = new LineEdit { SizeFlagsHorizontal = SizeFlags.ExpandFill };
			row.AddChild(input);
			parent.AddChild(row);
			return input;
		}

		private static SpinBox CreateNumberRow(Control parent, string labelText, double min, double max, double step)
		{
			var row = new HBoxContainer();
			row.AddChild(new Label { Text = labelText, CustomMinimumSize = new Vector2(110, 0) });
			var input = new SpinBox { MinValue = min, MaxValue = max, Step = step, SizeFlagsHorizontal = SizeFlags.ExpandFill };
			row.AddChild(input);
			parent.AddChild(row);
			return input;
		}

		private static void CreateDualNumberRow(Control parent, string labelText, double min, double max, double step, out SpinBox first, out SpinBox second)
		{
			var row = new HBoxContainer();
			row.AddChild(new Label { Text = labelText, CustomMinimumSize = new Vector2(110, 0) });
			first = new SpinBox { MinValue = min, MaxValue = max, Step = step, Prefix = "X:", SizeFlagsHorizontal = SizeFlags.ExpandFill };
			second = new SpinBox { MinValue = min, MaxValue = max, Step = step, Prefix = "Y:", SizeFlagsHorizontal = SizeFlags.ExpandFill };
			row.AddChild(first);
			row.AddChild(second);
			parent.AddChild(row);
		}

		private static (PanelContainer Card, VBoxContainer Content) CreateCard(string title, Color accentColor)
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
			var content = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
			content.AddThemeConstantOverride("separation", 6);
			var header = new Label { Text = title };
			header.AddThemeColorOverride("font_color", accentColor);
			header.AddThemeFontSizeOverride("font_size", 13);
			content.AddChild(header);
			content.AddChild(new HSeparator());
			panel.AddChild(content);
			return (panel, content);
		}
	}
}
