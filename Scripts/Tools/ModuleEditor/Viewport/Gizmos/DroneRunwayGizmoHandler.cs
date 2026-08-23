using System;
using System.Collections.Generic;
using Godot;
using BreakerProtocol.Data.Models;
using BreakerProtocol.Data.Models.Properties;

namespace BreakerProtocol.Tools.ModuleEditor.Viewport.Gizmos
{
	public enum RunwayHandleType
	{
		None,
		StartPoint,       // 跑道起点
		ExitPoint,        // 跑道出口
		MoveLine,         // 跑道整体平移
		OperationRadius   // 作战半径边缘把手
	}

	public class DroneRunwayGizmoHandler
	{
		public int SelectedIndex { get; set; } = -1;
		public int HoveredIndex { get; set; } = -1;
		public RunwayHandleType ActiveHandle { get; private set; } = RunwayHandleType.None;
		public bool IsDragging => ActiveHandle != RunwayHandleType.None;

		private Vector2 _dragStartMouse = Vector2.Zero;
		private Vector2 _dragStartPos = Vector2.Zero;
		private Vector2 _dragStartExit = Vector2.Zero;

		public bool OnLeftClickDown(ModuleDataDefinition? module, Vector2 localPx, float zoom, bool insideExtended, out bool isCreated)
		{
			isCreated = false;
			if (module == null) return false;

			bool isHangar = module.MountType == "Hangar" || (module.Tags != null && Array.IndexOf(module.Tags, "Hangar") >= 0);
			if (!isHangar) return false;

			var hp = module.GetProperties<HangarProperties>() ?? new HangarProperties();
			var runways = hp.Runways ?? Array.Empty<DroneRunwayDefinition>();
			float hitRadius = Mathf.Max(14.0f / zoom, 8.0f);

			Vector2 moduleCenter = new(module.Width * 80 * 0.5f, module.Height * 80 * 0.5f);
			float radiusPx = (hp.OperationRadius > 0 ? hp.OperationRadius * 8.0f : 1200.0f);
			Vector2 radiusHandlePos = moduleCenter + new Vector2(0, -radiusPx);

			// 1. 优先检测作战半径边缘手柄
			if (localPx.DistanceTo(radiusHandlePos) <= hitRadius)
			{
				ActiveHandle = RunwayHandleType.OperationRadius;
				_dragStartMouse = localPx;
				return true;
			}

			// 2. 检查抓取选中跑道的手柄
			if (SelectedIndex >= 0 && SelectedIndex < runways.Length)
			{
				var rw = runways[SelectedIndex];
				Vector2 startPos = new(rw.StartOffsetX, rw.StartOffsetY);
				Vector2 exitPos = new(rw.ExitOffsetX, rw.ExitOffsetY);

				if (localPx.DistanceTo(exitPos) <= hitRadius)
				{
					ActiveHandle = RunwayHandleType.ExitPoint;
					_dragStartMouse = localPx;
					_dragStartExit = exitPos;
					return true;
				}

				if (localPx.DistanceTo(startPos) <= hitRadius)
				{
					ActiveHandle = RunwayHandleType.StartPoint;
					_dragStartMouse = localPx;
					_dragStartPos = startPos;
					return true;
				}
			}

			// 3. 检查抓取任意跑道
			for (int i = 0; i < runways.Length; i++)
			{
				var rw = runways[i];
				Vector2 startPos = new(rw.StartOffsetX, rw.StartOffsetY);
				Vector2 exitPos = new(rw.ExitOffsetX, rw.ExitOffsetY);

				if (localPx.DistanceTo(exitPos) <= hitRadius)
				{
					SelectedIndex = i;
					ActiveHandle = RunwayHandleType.ExitPoint;
					_dragStartMouse = localPx;
					_dragStartExit = exitPos;
					return true;
				}

				if (localPx.DistanceTo(startPos) <= hitRadius)
				{
					SelectedIndex = i;
					ActiveHandle = RunwayHandleType.StartPoint;
					_dragStartMouse = localPx;
					_dragStartPos = startPos;
					return true;
				}

				float distToLine = HandleMathUtils.DistanceToSegment(localPx, startPos, exitPos);
				if (distToLine <= hitRadius)
				{
					SelectedIndex = i;
					ActiveHandle = RunwayHandleType.MoveLine;
					_dragStartMouse = localPx;
					_dragStartPos = startPos;
					_dragStartExit = exitPos;
					return true;
				}
			}

			// 4. 点击空白区域创建新跑道
			if (insideExtended)
			{
				var list = new List<DroneRunwayDefinition>(runways);
				var newRw = new DroneRunwayDefinition
				{
					RunwayId = $"runway_{list.Count}",
					LaunchOrder = list.Count,
					StartOffsetX = Mathf.Round(localPx.X),
					StartOffsetY = Mathf.Round(localPx.Y + 20),
					ExitOffsetX = Mathf.Round(localPx.X),
					ExitOffsetY = Mathf.Round(localPx.Y - 40),
					CatapultDuration = 0.5f,
					ExitSpeed = 320.0f
				};
				list.Add(newRw);
				hp.Runways = list.ToArray();
				module.Properties = System.Text.Json.JsonSerializer.SerializeToElement(hp);

				SelectedIndex = list.Count - 1;
				ActiveHandle = RunwayHandleType.ExitPoint;
				_dragStartMouse = localPx;
				_dragStartExit = new Vector2(newRw.ExitOffsetX, newRw.ExitOffsetY);
				isCreated = true;
				return true;
			}

			SelectedIndex = -1;
			return false;
		}

		public void HandleDrag(ModuleDataDefinition? module, Vector2 localPx)
		{
			if (module == null || ActiveHandle == RunwayHandleType.None) return;

			var hp = module.GetProperties<HangarProperties>() ?? new HangarProperties();
			Vector2 delta = localPx - _dragStartMouse;

			if (ActiveHandle == RunwayHandleType.OperationRadius)
			{
				Vector2 moduleCenter = new(module.Width * 80 * 0.5f, module.Height * 80 * 0.5f);
				float distPx = localPx.DistanceTo(moduleCenter);
				float newRadiusMeters = Mathf.Clamp(Mathf.Round(distPx / 8.0f / 5.0f) * 5.0f, 20.0f, 1500.0f);
				hp.OperationRadius = newRadiusMeters;
				module.Properties = System.Text.Json.JsonSerializer.SerializeToElement(hp);
				return;
			}

			if (hp.Runways == null || SelectedIndex < 0 || SelectedIndex >= hp.Runways.Length) return;
			var rw = hp.Runways[SelectedIndex];

			switch (ActiveHandle)
			{
				case RunwayHandleType.StartPoint:
					rw.StartOffsetX = Mathf.Round(_dragStartPos.X + delta.X);
					rw.StartOffsetY = Mathf.Round(_dragStartPos.Y + delta.Y);
					break;
				case RunwayHandleType.ExitPoint:
					rw.ExitOffsetX = Mathf.Round(_dragStartExit.X + delta.X);
					rw.ExitOffsetY = Mathf.Round(_dragStartExit.Y + delta.Y);
					break;
				case RunwayHandleType.MoveLine:
					rw.StartOffsetX = Mathf.Round(_dragStartPos.X + delta.X);
					rw.StartOffsetY = Mathf.Round(_dragStartPos.Y + delta.Y);
					rw.ExitOffsetX = Mathf.Round(_dragStartExit.X + delta.X);
					rw.ExitOffsetY = Mathf.Round(_dragStartExit.Y + delta.Y);
					break;
			}

			module.Properties = System.Text.Json.JsonSerializer.SerializeToElement(hp);
		}

		public bool TryDeleteRunwayAt(ModuleDataDefinition? module, Vector2 localPx, float zoom)
		{
			if (module == null) return false;
			var hp = module.GetProperties<HangarProperties>();
			if (hp?.Runways == null || hp.Runways.Length == 0) return false;

			float hitRadius = Mathf.Max(14.0f / zoom, 8.0f);

			for (int i = 0; i < hp.Runways.Length; i++)
			{
				var rw = hp.Runways[i];
				Vector2 startPos = new(rw.StartOffsetX, rw.StartOffsetY);
				Vector2 exitPos = new(rw.ExitOffsetX, rw.ExitOffsetY);

				if (localPx.DistanceTo(startPos) <= hitRadius || localPx.DistanceTo(exitPos) <= hitRadius || HandleMathUtils.DistanceToSegment(localPx, startPos, exitPos) <= hitRadius)
				{
					var list = new List<DroneRunwayDefinition>(hp.Runways);
					list.RemoveAt(i);
					for (int k = 0; k < list.Count; k++) list[k].LaunchOrder = k;
					hp.Runways = list.ToArray();
					module.Properties = System.Text.Json.JsonSerializer.SerializeToElement(hp);

					SelectedIndex = Mathf.Clamp(SelectedIndex, -1, list.Count - 1);
					return true;
				}
			}
			return false;
		}

		public void ReleaseHandle() => ActiveHandle = RunwayHandleType.None;

		public void UpdateHover(ModuleDataDefinition? module, Vector2 localPx, float zoom)
		{
			if (module == null) { HoveredIndex = -1; return; }

			bool isHangar = module.MountType == "Hangar" || (module.Tags != null && Array.IndexOf(module.Tags, "Hangar") >= 0);
			if (!isHangar) { HoveredIndex = -1; return; }

			var hp = module.GetProperties<HangarProperties>();
			if (hp == null) { HoveredIndex = -1; return; }

			float hitRadius = Mathf.Max(14.0f / zoom, 8.0f);

			// 检查半径把手悬浮
			Vector2 moduleCenter = new(module.Width * 80 * 0.5f, module.Height * 80 * 0.5f);
			float radiusPx = (hp.OperationRadius > 0 ? hp.OperationRadius * 8.0f : 1200.0f);
			Vector2 radiusHandlePos = moduleCenter + new Vector2(0, -radiusPx);

			if (localPx.DistanceTo(radiusHandlePos) <= hitRadius)
			{
				HoveredIndex = 9999;
				return;
			}

			if (hp.Runways == null) { HoveredIndex = -1; return; }

			for (int i = 0; i < hp.Runways.Length; i++)
			{
				var rw = hp.Runways[i];
				Vector2 startPos = new(rw.StartOffsetX, rw.StartOffsetY);
				Vector2 exitPos = new(rw.ExitOffsetX, rw.ExitOffsetY);

				if (localPx.DistanceTo(startPos) <= hitRadius || localPx.DistanceTo(exitPos) <= hitRadius || HandleMathUtils.DistanceToSegment(localPx, startPos, exitPos) <= hitRadius)
				{
					HoveredIndex = i;
					return;
				}
			}
			HoveredIndex = -1;
		}

		public void Draw(Control canvas, ModuleDataDefinition? module, Vector2 origin, float zoom, bool isActive)
		{
			if (module == null) return;

			bool isHangar = module.MountType == "Hangar" || (module.Tags != null && Array.IndexOf(module.Tags, "Hangar") >= 0);
			if (!isHangar) return;

			var hp = module.GetProperties<HangarProperties>();
			if (hp == null) return;

			// 1. 全向作战空域半径圆环与拖拽把手
			float radiusPx = (hp.OperationRadius > 0 ? hp.OperationRadius * 8.0f : 1200.0f) * zoom;
			Vector2 centerScreen = origin + new Vector2(module.Width * 80 * 0.5f, module.Height * 80 * 0.5f) * zoom;
			Vector2 radiusHandlePos = centerScreen + new Vector2(0, -radiusPx);

			Color ringColor = new(0.2f, 0.85f, 0.45f, 0.45f);
			canvas.DrawArc(centerScreen, radiusPx, 0, Mathf.Tau, 64, ringColor, 1.5f);

			// 把手顶点与文字标牌
			Color handleColor = (ActiveHandle == RunwayHandleType.OperationRadius || HoveredIndex == 9999)
				? Colors.Yellow
				: new Color(0.35f, 1.0f, 0.65f);

			canvas.DrawCircle(radiusHandlePos, 5.0f * zoom, handleColor);
			canvas.DrawCircle(radiusHandlePos, 2.5f * zoom, Colors.White);

			canvas.DrawString(
				ThemeDB.FallbackFont,
				radiusHandlePos + new Vector2(10, -6),
				$"🛸 无人机作战半径: {hp.OperationRadius:F0}m ({(hp.OperationRadius * 8.0f):F0}px)",
				HorizontalAlignment.Left,
				-1,
				(int)(11 * Mathf.Clamp(zoom, 0.8f, 1.2f)),
				handleColor
			);

			// 2. 仅在 Runways 编辑模式且存在跑道时绘制跑道把手
			if (!isActive || hp.Runways == null || hp.Runways.Length == 0) return;

			for (int i = 0; i < hp.Runways.Length; i++)
			{
				var rw = hp.Runways[i];
				Vector2 startScreen = origin + new Vector2(rw.StartOffsetX, rw.StartOffsetY) * zoom;
				Vector2 exitScreen = origin + new Vector2(rw.ExitOffsetX, rw.ExitOffsetY) * zoom;
				bool isSelected = (i == SelectedIndex);
				bool isHovered = (i == HoveredIndex);

				Color lineColor = isSelected ? new Color(0.35f, 1.0f, 0.65f) : (isHovered ? Colors.White : new Color(0.2f, 0.8f, 0.5f, 0.7f));

				canvas.DrawLine(startScreen, exitScreen, lineColor, (isSelected ? 2.5f : 1.5f));

				Vector2 dir = (exitScreen - startScreen).Normalized();
				if (dir.LengthSquared() > 0.001f)
				{
					Vector2 normal = new(-dir.Y, dir.X);
					Vector2 arrowTip = exitScreen;
					Vector2 arrowL = arrowTip - dir * 10.0f * zoom + normal * 6.0f * zoom;
					Vector2 arrowR = arrowTip - dir * 10.0f * zoom - normal * 6.0f * zoom;
					canvas.DrawPolygon(new[] { arrowTip, arrowL, arrowR }, new[] { lineColor });
				}

				canvas.DrawCircle(startScreen, 5.0f * zoom, new Color(0.2f, 0.85f, 0.45f));
				canvas.DrawCircle(startScreen, 2.0f * zoom, Colors.Black);

				Rect2 exitRect = new(exitScreen - new Vector2(4, 4) * zoom, new Vector2(8, 8) * zoom);
				canvas.DrawRect(exitRect, isSelected ? Colors.Yellow : new Color(1.0f, 0.6f, 0.2f), true);
				canvas.DrawRect(exitRect, Colors.Black, false, 1.0f);

				canvas.DrawString(ThemeDB.FallbackFont, startScreen + new Vector2(8, 12), $"🛫 #{rw.LaunchOrder + 1} {rw.RunwayId} ({rw.CatapultDuration:F2}s)", HorizontalAlignment.Left, -1, (int)(11 * Mathf.Clamp(zoom, 0.8f, 1.2f)), isSelected ? Colors.Yellow : Colors.White);
			}
		}
	}

	internal static class HandleMathUtils
	{
		public static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
		{
			Vector2 ab = b - a;
			float len2 = ab.LengthSquared();
			if (len2 < 0.0001f) return p.DistanceTo(a);
			float t = Mathf.Clamp((p - a).Dot(ab) / len2, 0.0f, 1.0f);
			return p.DistanceTo(a + t * ab);
		}
	}
}
