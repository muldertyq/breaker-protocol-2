using System;
using System.Collections.Generic;
using Godot;
using BreakerProtocol.Data.Models;
using BreakerProtocol.Utils;

namespace BreakerProtocol.Tools.ModuleEditor.Viewport.Gizmos
{
	public class PinGizmoHandler
	{
		public int SelectedIndex { get; set; } = -1;
		public int HoveredIndex { get; private set; } = -1;
		public bool IsDragging { get; private set; } = false;

		private Vector2I _dragTargetCell;
		private string _dragTargetEdge = "";
		private bool _isTargetValid = false;
		private bool _isTargetOccupied = false;
		private Vector2 _dragMouseLocalPx = Vector2.Zero;

		public bool OnLeftClickDown(ModuleDataDefinition? module, Vector2 localPx, float canvasZoom, int gridUnitPx, out bool isNewPinCreated)
		{
			isNewPinCreated = false;
			if (module == null) return false;
			float hitDist = 16.0f / canvasZoom;

			// 1. 优先抓取已有引脚
			if (module.Pins != null)
			{
				for (int i = 0; i < module.Pins.Length; i++)
				{
					var p = module.Pins[i];
					Vector2 cellOrigin = new(p.LocalGridX * gridUnitPx, p.LocalGridY * gridUnitPx);
					Vector2 pinPos = GetPinLocalPos(cellOrigin, p.Edge, gridUnitPx);

					if (localPx.DistanceTo(pinPos) <= hitDist)
					{
						SelectedIndex = i;
						IsDragging = true;
						_dragMouseLocalPx = localPx;
						_isTargetValid = false;
						_isTargetOccupied = false;
						return true;
					}
				}
			}

			// 2. 点击空网格边缘生成新引脚
			Vector2I cell = AnchorPointMath.PixelToLocalGrid(localPx);
			if (cell.X >= 0 && cell.X < module.Width && cell.Y >= 0 && cell.Y < module.Height)
			{
				string edge = AnchorPointMath.GetClosestEdge(localPx);

				// 检查该位置是否已存在引脚，防止同边缘重复重叠
				if (HasPinAt(module, cell.X, cell.Y, edge, -1))
				{
					return false;
				}

				string defaultCat = module.Category switch
				{
					"Pipeline" => "Logic",
					"Power" when module.Tags != null && Array.IndexOf(module.Tags, "Cooling") >= 0 => "Thermal",
					_ => "PulsePower"
				};

				string defaultType = module.Category == "Power" && (module.Tags == null || Array.IndexOf(module.Tags, "Cooling") < 0) ? "OUT" : "IN";

				// 自动生成包含边缘信息的全局唯一 PinId
				string baseId = $"{defaultCat.ToLower()}_{defaultType.ToLower()}_{cell.X}_{cell.Y}_{edge.ToLower()}";
				string uniqueId = baseId;
				int counter = 1;

				var existingPins = module.Pins ?? Array.Empty<PinDefinition>();
				while (Array.Exists(existingPins, p => p.PinId.Equals(uniqueId, StringComparison.OrdinalIgnoreCase)))
				{
					uniqueId = $"{baseId}_{counter++}";
				}

				var pins = new List<PinDefinition>(existingPins)
				{
					new()
					{
						PinId = uniqueId,
						LocalGridX = cell.X,
						LocalGridY = cell.Y,
						Edge = edge,
						Type = defaultType,
						Category = defaultCat
					}
				};

				module.Pins = pins.ToArray();
				SelectedIndex = pins.Count - 1;
				IsDragging = false;
				isNewPinCreated = true;
				return true;
			}

			SelectedIndex = -1;
			IsDragging = false;
			return false;
		}

		public bool TryDeletePinAt(ModuleDataDefinition? module, Vector2 localPx, float canvasZoom, int gridUnitPx)
		{
			if (module?.Pins == null || module.Pins.Length == 0) return false;
			float hitDist = 16.0f / canvasZoom;

			for (int i = 0; i < module.Pins.Length; i++)
			{
				var p = module.Pins[i];
				Vector2 cellOrigin = new(p.LocalGridX * gridUnitPx, p.LocalGridY * gridUnitPx);
				Vector2 pinPos = GetPinLocalPos(cellOrigin, p.Edge, gridUnitPx);

				if (localPx.DistanceTo(pinPos) <= hitDist)
				{
					var list = new List<PinDefinition>(module.Pins);
					list.RemoveAt(i);
					module.Pins = list.ToArray();
					SelectedIndex = list.Count > 0 ? Mathf.Clamp(i - 1, 0, list.Count - 1) : -1;
					return true;
				}
			}
			return false;
		}

		public void HandleDrag(ModuleDataDefinition? module, Vector2 localPx, int gridUnitPx)
		{
			if (module == null || !IsDragging || SelectedIndex < 0 || SelectedIndex >= module.Pins.Length) return;

			_dragMouseLocalPx = localPx;

			Vector2I cell = AnchorPointMath.PixelToLocalGrid(localPx);
			if (cell.X >= 0 && cell.X < module.Width && cell.Y >= 0 && cell.Y < module.Height)
			{
				string edge = AnchorPointMath.GetClosestEdge(localPx);
				_dragTargetCell = cell;
				_dragTargetEdge = edge;
				_isTargetValid = true;
				_isTargetOccupied = HasPinAt(module, cell.X, cell.Y, edge, ignoreIndex: SelectedIndex);
			}
			else
			{
				_isTargetValid = false;
				_isTargetOccupied = false;
			}
		}

		public bool OnLeftClickUp(ModuleDataDefinition? module)
		{
			if (!IsDragging) return false;
			IsDragging = false;

			if (module == null || SelectedIndex < 0 || SelectedIndex >= module.Pins.Length) return false;

			if (_isTargetValid && !_isTargetOccupied)
			{
				var currentPin = module.Pins[SelectedIndex];
				if (currentPin.LocalGridX != _dragTargetCell.X || currentPin.LocalGridY != _dragTargetCell.Y || currentPin.Edge != _dragTargetEdge)
				{
					currentPin.LocalGridX = _dragTargetCell.X;
					currentPin.LocalGridY = _dragTargetCell.Y;
					currentPin.Edge = _dragTargetEdge;
					return true;
				}
			}

			return false;
		}

		public void UpdateHover(ModuleDataDefinition? module, Vector2 localPx, float canvasZoom, int gridUnitPx)
		{
			if (module?.Pins == null || IsDragging)
			{
				HoveredIndex = -1;
				return;
			}

			float hitDist = 16.0f / canvasZoom;
			for (int i = 0; i < module.Pins.Length; i++)
			{
				var p = module.Pins[i];
				Vector2 cellOrigin = new(p.LocalGridX * gridUnitPx, p.LocalGridY * gridUnitPx);
				Vector2 pinPos = GetPinLocalPos(cellOrigin, p.Edge, gridUnitPx);

				if (localPx.DistanceTo(pinPos) <= hitDist)
				{
					HoveredIndex = i;
					return;
				}
			}
			HoveredIndex = -1;
		}

		public void Draw(CanvasItem canvas, ModuleDataDefinition module, Vector2 origin, float canvasZoom, bool isEditMode, int gridUnitPx)
		{
			if (module.Pins == null || module.Pins.Length == 0) return;

			float alpha = isEditMode ? 1.0f : 0.25f;

			for (int i = 0; i < module.Pins.Length; i++)
			{
				var pin = module.Pins[i];
				Vector2 cellOrigin = origin + new Vector2(pin.LocalGridX * gridUnitPx, pin.LocalGridY * gridUnitPx) * canvasZoom;
				Vector2 pinPos = GetPinScreenPos(cellOrigin, pin.Edge, gridUnitPx * canvasZoom);

				Color baseColor = GetCategoryColor(pin.Category, alpha);
				Color strokeColor = pin.Type == "IN" ? new Color(0.2f, 1.0f, 0.4f, alpha) : baseColor;

				if (IsDragging && i == SelectedIndex)
				{
					canvas.DrawCircle(pinPos, 7.0f * canvasZoom, baseColor * 0.35f);
					continue;
				}

				if (isEditMode && i == SelectedIndex)
				{
					canvas.DrawCircle(pinPos, 11.0f * canvasZoom, Colors.Yellow);
					canvas.DrawCircle(pinPos, 9.0f * canvasZoom, Colors.Black);
				}
				else if (isEditMode && i == HoveredIndex)
				{
					canvas.DrawCircle(pinPos, 9.0f * canvasZoom, Colors.White);
				}

				canvas.DrawCircle(pinPos, 7.0f * canvasZoom, baseColor);
				canvas.DrawCircle(pinPos, 4.0f * canvasZoom, strokeColor);
				canvas.DrawCircle(pinPos, 2.0f * canvasZoom, new Color(1, 1, 1, alpha));
			}

			if (IsDragging && isEditMode && SelectedIndex >= 0 && SelectedIndex < module.Pins.Length)
			{
				var pin = module.Pins[SelectedIndex];
				Vector2 floatingPos = origin + _dragMouseLocalPx * canvasZoom;
				Color baseColor = GetCategoryColor(pin.Category, 1.0f);

				if (_isTargetValid)
				{
					Vector2 targetCellOrigin = origin + new Vector2(_dragTargetCell.X * gridUnitPx, _dragTargetCell.Y * gridUnitPx) * canvasZoom;
					Vector2 snapPos = GetPinScreenPos(targetCellOrigin, _dragTargetEdge, gridUnitPx * canvasZoom);

					if (_isTargetOccupied)
					{
						canvas.DrawCircle(snapPos, 10.0f * canvasZoom, new Color(1.0f, 0.15f, 0.15f, 0.7f));
						canvas.DrawLine(floatingPos, snapPos, new Color(1.0f, 0.2f, 0.2f, 0.8f), 2.0f);
						canvas.DrawString(ThemeDB.FallbackFont, floatingPos + new Vector2(14, -6), "❌ 该边缘已存在引脚", HorizontalAlignment.Left, -1, 12, Colors.Red);
					}
					else
					{
						canvas.DrawCircle(snapPos, 9.0f * canvasZoom, new Color(0.2f, 1.0f, 0.4f, 0.4f));
						canvas.DrawCircle(snapPos, 5.0f * canvasZoom, baseColor);
						canvas.DrawLine(floatingPos, snapPos, new Color(0.3f, 0.9f, 1.0f, 0.6f), 1.5f);
					}
				}

				canvas.DrawCircle(floatingPos, 8.0f * canvasZoom, baseColor);
				canvas.DrawCircle(floatingPos, 3.0f * canvasZoom, Colors.White);
			}
		}

		private Color GetCategoryColor(string category, float alpha)
		{
			return category switch
			{
				"Universal" => new Color(0.9f, 0.95f, 1.0f, alpha), // 🌐 通用：银白流光
				"HeavyPulse" => new Color(0.85f, 0.2f, 1.0f, alpha), // 🔮 重脉冲：紫色
				"Thermal" => new Color(1.0f, 0.45f, 0.0f, alpha),    // 🔥 热力：炽橙
				"Logic" => new Color(1.0f, 0.9f, 0.0f, alpha),       // 💡 逻辑：琥珀黄
				_ => new Color(0.0f, 0.75f, 1.0f, alpha)             // ⚡ 脉冲电：高能蓝
			};
		}

		private bool HasPinAt(ModuleDataDefinition module, int x, int y, string edge, int ignoreIndex)
		{
			if (module.Pins == null) return false;
			for (int i = 0; i < module.Pins.Length; i++)
			{
				if (i == ignoreIndex) continue;
				var p = module.Pins[i];
				if (p.LocalGridX == x && p.LocalGridY == y && p.Edge.Equals(edge, StringComparison.OrdinalIgnoreCase))
				{
					return true;
				}
			}
			return false;
		}

		private Vector2 GetPinLocalPos(Vector2 cellOrigin, string edge, float size)
		{
			float half = size * 0.5f;
			return edge switch
			{
				"Top" => cellOrigin + new Vector2(half, 0),
				"Bottom" => cellOrigin + new Vector2(half, size),
				"Left" => cellOrigin + new Vector2(0, half),
				_ => cellOrigin + new Vector2(size, half)
			};
		}

		private Vector2 GetPinScreenPos(Vector2 cellOrigin, string edge, float size) => GetPinLocalPos(cellOrigin, edge, size);
	}
}
