using System;
using System.Collections.Generic;
using Godot;
using BreakerProtocol.Data.Models;

namespace BreakerProtocol.Tools.ModuleEditor.Viewport.Gizmos
{
	public class FirePointGizmoHandler
	{
		public int SelectedIndex { get; set; } = -1;
		public int HoveredIndex { get; private set; } = -1;

		public bool OnLeftClickDown(ModuleDataDefinition? module, Vector2 localPx, float canvasZoom, bool isInsideValidArea, out bool isNewCreated)
		{
			isNewCreated = false;
			if (module == null) return false;
			float hitDist = 20.0f / canvasZoom;

			// 1. 优先抓取已有开火点（无论在网格内还是外）
			if (module.FirePoints != null)
			{
				for (int i = 0; i < module.FirePoints.Length; i++)
				{
					var fp = module.FirePoints[i];
					Vector2 pos = new(fp.PixelOffsetX, fp.PixelOffsetY);
					if (localPx.DistanceTo(pos) <= hitDist)
					{
						SelectedIndex = i;
						return true;
					}
				}
			}

			// 2. 点击炮管/构件有效区域直接生成新开火点
			if (isInsideValidArea)
			{
				var list = new List<FirePointDefinition>(module.FirePoints ?? Array.Empty<FirePointDefinition>())
				{
					new()
					{
						Id = $"muzzle_{module.FirePoints?.Length ?? 0}",
						PixelOffsetX = Mathf.Round(localPx.X),
						PixelOffsetY = Mathf.Round(localPx.Y),
						AngleOffset = 0.0f,
						SequenceIndex = module.FirePoints?.Length ?? 0
					}
				};
				module.FirePoints = list.ToArray();
				SelectedIndex = list.Count - 1;
				isNewCreated = true;
				return true;
			}

			SelectedIndex = -1;
			return false;
		}

		public void HandleDrag(ModuleDataDefinition? module, Vector2 localPx)
		{
			if (module?.FirePoints == null || SelectedIndex < 0 || SelectedIndex >= module.FirePoints.Length) return;

			module.FirePoints[SelectedIndex].PixelOffsetX = Mathf.Round(localPx.X);
			module.FirePoints[SelectedIndex].PixelOffsetY = Mathf.Round(localPx.Y);
		}

		public bool TryDeleteFirePointAt(ModuleDataDefinition? module, Vector2 localPx, float canvasZoom)
		{
			if (module?.FirePoints == null || module.FirePoints.Length == 0) return false;
			float hitDist = 20.0f / canvasZoom;

			for (int i = 0; i < module.FirePoints.Length; i++)
			{
				var fp = module.FirePoints[i];
				Vector2 pos = new(fp.PixelOffsetX, fp.PixelOffsetY);
				if (localPx.DistanceTo(pos) <= hitDist)
				{
					var list = new List<FirePointDefinition>(module.FirePoints);
					list.RemoveAt(i);
					module.FirePoints = list.ToArray();
					SelectedIndex = list.Count > 0 ? Mathf.Clamp(i - 1, 0, list.Count - 1) : -1;
					return true;
				}
			}
			return false;
		}

		public void UpdateHover(ModuleDataDefinition? module, Vector2 localPx, float canvasZoom)
		{
			if (module?.FirePoints == null)
			{
				HoveredIndex = -1;
				return;
			}

			float hitDist = 20.0f / canvasZoom;
			for (int i = 0; i < module.FirePoints.Length; i++)
			{
				var fp = module.FirePoints[i];
				Vector2 pos = new(fp.PixelOffsetX, fp.PixelOffsetY);
				if (localPx.DistanceTo(pos) <= hitDist)
				{
					HoveredIndex = i;
					return;
				}
			}
			HoveredIndex = -1;
		}

		public void Draw(CanvasItem canvas, ModuleDataDefinition module, Vector2 origin, float canvasZoom, bool isEditMode)
		{
			if (module.FirePoints == null || module.FirePoints.Length == 0) return;

			float alpha = isEditMode ? 1.0f : 0.35f;

			for (int i = 0; i < module.FirePoints.Length; i++)
			{
				var fp = module.FirePoints[i];
				Vector2 screenPos = origin + new Vector2(fp.PixelOffsetX, fp.PixelOffsetY) * canvasZoom;

				Color color = isEditMode && (i == SelectedIndex)
					? Colors.Yellow
					: (isEditMode && i == HoveredIndex ? Colors.Orange : new Color(1.0f, 0.25f, 0.25f, alpha));

				float cross = 8.0f * canvasZoom;
				canvas.DrawLine(screenPos - new Vector2(cross, 0), screenPos + new Vector2(cross, 0), color, 2.0f);
				canvas.DrawLine(screenPos - new Vector2(0, cross), screenPos + new Vector2(0, cross), color, 2.0f);
				canvas.DrawCircle(screenPos, 5.0f * canvasZoom, color);
				canvas.DrawCircle(screenPos, 2.0f * canvasZoom, Colors.Black);

				float dirRad = Mathf.DegToRad(fp.AngleOffset - 90.0f);
				Vector2 muzzleDir = new(Mathf.Cos(dirRad), Mathf.Sin(dirRad));
				canvas.DrawLine(screenPos, screenPos + muzzleDir * 16.0f * canvasZoom, new Color(1.0f, 0.5f, 0.2f, 0.8f), 1.5f);

				if (isEditMode)
				{
					canvas.DrawString(ThemeDB.FallbackFont, screenPos + new Vector2(10, -6), $"#{fp.SequenceIndex} [{fp.Id}] ({fp.PixelOffsetX:F0},{fp.PixelOffsetY:F0})", HorizontalAlignment.Left, -1, 11, Colors.Yellow);
				}
			}
		}
	}
}
