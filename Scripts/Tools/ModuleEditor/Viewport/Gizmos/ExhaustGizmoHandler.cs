using System;
using System.Collections.Generic;
using Godot;
using BreakerProtocol.Data.Models;

namespace BreakerProtocol.Tools.ModuleEditor.Viewport.Gizmos
{
	public enum ExhaustHandleType
	{
		None,
		Center,
		DirectionAndLengthTip, // 控制方向 + 尾焰长度
		WidthLeft,             // 控制喷口宽度 (左)
		WidthRight             // 控制喷口宽度 (右)
	}

	public class ExhaustGizmoHandler
	{
		public int SelectedIndex { get; set; } = -1;
		public int HoveredIndex { get; private set; } = -1;
		public ExhaustHandleType ActiveHandle { get; private set; } = ExhaustHandleType.None;
		public ExhaustHandleType HoveredHandle { get; private set; } = ExhaustHandleType.None;

		public bool OnLeftClickDown(ModuleDataDefinition? module, Vector2 localPx, float canvasZoom, bool isInsideBounds, out bool isNewCreated)
		{
			isNewCreated = false;
			if (module == null) return false;

			float hitDist = 16.0f / canvasZoom;

			// 1. 优先检测已有喷口的手柄抓取
			if (module.ExhaustPoints != null)
			{
				for (int i = 0; i < module.ExhaustPoints.Length; i++)
				{
					var ep = module.ExhaustPoints[i];
					Vector2 pos = new(ep.PixelOffsetX, ep.PixelOffsetY);
					Vector2 dir = new Vector2(ep.DirX, ep.DirY).Normalized();
					if (dir.LengthSquared() < 0.001f) dir = new Vector2(0, 1);
					Vector2 normal = new(-dir.Y, dir.X);

					Vector2 tipPos = pos + dir * Mathf.Max(ep.FlameLength, 15.0f);
					Vector2 leftHandle = pos + normal * (ep.FlameWidth * 0.5f);
					Vector2 rightHandle = pos - normal * (ep.FlameWidth * 0.5f);

					// 抓取尖端 (长度与方向)
					if (localPx.DistanceTo(tipPos) <= hitDist)
					{
						SelectedIndex = i;
						ActiveHandle = ExhaustHandleType.DirectionAndLengthTip;
						return true;
					}

					// 抓取宽度翼片手柄
					if (localPx.DistanceTo(leftHandle) <= hitDist)
					{
						SelectedIndex = i;
						ActiveHandle = ExhaustHandleType.WidthLeft;
						return true;
					}

					if (localPx.DistanceTo(rightHandle) <= hitDist)
					{
						SelectedIndex = i;
						ActiveHandle = ExhaustHandleType.WidthRight;
						return true;
					}

					// 抓取喷口基座中心 (位置移动)
					if (localPx.DistanceTo(pos) <= hitDist)
					{
						SelectedIndex = i;
						ActiveHandle = ExhaustHandleType.Center;
						return true;
					}
				}
			}

			// 2. 点击构件内部生成新喷口
			if (isInsideBounds)
			{
				var list = new List<ExhaustPointDefinition>(module.ExhaustPoints ?? Array.Empty<ExhaustPointDefinition>())
				{
					new()
					{
						Id = $"exhaust_{module.ExhaustPoints?.Length ?? 0}",
						PixelOffsetX = Mathf.Round(localPx.X),
						PixelOffsetY = Mathf.Round(localPx.Y),
						DirX = 0.0f,
						DirY = 1.0f,
						FlameLength = 40.0f,
						FlameWidth = 16.0f,
						FlameColorHex = "#38bdf8"
					}
				};

				module.ExhaustPoints = list.ToArray();
				SelectedIndex = list.Count - 1;
				ActiveHandle = ExhaustHandleType.Center;
				isNewCreated = true;
				return true;
			}

			SelectedIndex = -1;
			ActiveHandle = ExhaustHandleType.None;
			return false;
		}

		public void HandleDrag(ModuleDataDefinition? module, Vector2 localPx)
		{
			if (module?.ExhaustPoints == null || SelectedIndex < 0 || SelectedIndex >= module.ExhaustPoints.Length || ActiveHandle == ExhaustHandleType.None) return;

			var ep = module.ExhaustPoints[SelectedIndex];
			Vector2 rootPos = new(ep.PixelOffsetX, ep.PixelOffsetY);

			if (ActiveHandle == ExhaustHandleType.Center)
			{
				ep.PixelOffsetX = Mathf.Round(localPx.X);
				ep.PixelOffsetY = Mathf.Round(localPx.Y);
			}
			else if (ActiveHandle == ExhaustHandleType.DirectionAndLengthTip)
			{
				Vector2 delta = localPx - rootPos;
				float len = delta.Length();
				if (len > 4.0f)
				{
					// 同步更新长度与方向
					ep.FlameLength = Mathf.Clamp(Mathf.Round(len), 10.0f, 400.0f);
					Vector2 norm = delta / len;
					ep.DirX = norm.X;
					ep.DirY = norm.Y;
				}
			}
			else if (ActiveHandle is ExhaustHandleType.WidthLeft or ExhaustHandleType.WidthRight)
			{
				Vector2 dir = new Vector2(ep.DirX, ep.DirY).Normalized();
				if (dir.LengthSquared() < 0.001f) dir = new Vector2(0, 1);
				Vector2 normal = new(-dir.Y, dir.X);

				// 投影到法线轴上计算宽度
				float halfWidth = Mathf.Abs((localPx - rootPos).Dot(normal));
				ep.FlameWidth = Mathf.Clamp(Mathf.Round(halfWidth * 2.0f), 4.0f, 120.0f);
			}
		}

		public void ReleaseHandle()
		{
			ActiveHandle = ExhaustHandleType.None;
		}

		public bool TryDeleteExhaustAt(ModuleDataDefinition? module, Vector2 localPx, float canvasZoom)
		{
			if (module?.ExhaustPoints == null || module.ExhaustPoints.Length == 0) return false;

			float hitDist = 18.0f / canvasZoom;
			for (int i = 0; i < module.ExhaustPoints.Length; i++)
			{
				var ep = module.ExhaustPoints[i];
				Vector2 pos = new(ep.PixelOffsetX, ep.PixelOffsetY);
				if (localPx.DistanceTo(pos) <= hitDist)
				{
					var list = new List<ExhaustPointDefinition>(module.ExhaustPoints);
					list.RemoveAt(i);
					module.ExhaustPoints = list.ToArray();
					SelectedIndex = list.Count > 0 ? Mathf.Clamp(i - 1, 0, list.Count - 1) : -1;
					return true;
				}
			}

			return false;
		}

		public void UpdateHover(ModuleDataDefinition? module, Vector2 localPx, float canvasZoom)
		{
			if (module?.ExhaustPoints == null || ActiveHandle != ExhaustHandleType.None)
			{
				HoveredIndex = -1;
				HoveredHandle = ExhaustHandleType.None;
				return;
			}

			float hitDist = 16.0f / canvasZoom;
			for (int i = 0; i < module.ExhaustPoints.Length; i++)
			{
				var ep = module.ExhaustPoints[i];
				Vector2 pos = new(ep.PixelOffsetX, ep.PixelOffsetY);
				Vector2 dir = new Vector2(ep.DirX, ep.DirY).Normalized();
				if (dir.LengthSquared() < 0.001f) dir = new Vector2(0, 1);
				Vector2 normal = new(-dir.Y, dir.X);

				Vector2 tipPos = pos + dir * Mathf.Max(ep.FlameLength, 15.0f);
				Vector2 leftHandle = pos + normal * (ep.FlameWidth * 0.5f);
				Vector2 rightHandle = pos - normal * (ep.FlameWidth * 0.5f);

				if (localPx.DistanceTo(tipPos) <= hitDist)
				{
					HoveredIndex = i;
					HoveredHandle = ExhaustHandleType.DirectionAndLengthTip;
					return;
				}

				if (localPx.DistanceTo(leftHandle) <= hitDist)
				{
					HoveredIndex = i;
					HoveredHandle = ExhaustHandleType.WidthLeft;
					return;
				}

				if (localPx.DistanceTo(rightHandle) <= hitDist)
				{
					HoveredIndex = i;
					HoveredHandle = ExhaustHandleType.WidthRight;
					return;
				}

				if (localPx.DistanceTo(pos) <= hitDist)
				{
					HoveredIndex = i;
					HoveredHandle = ExhaustHandleType.Center;
					return;
				}
			}

			HoveredIndex = -1;
			HoveredHandle = ExhaustHandleType.None;
		}

		public void Draw(CanvasItem canvas, ModuleDataDefinition module, Vector2 origin, float canvasZoom, bool isEditMode)
		{
			if (module.ExhaustPoints == null || module.ExhaustPoints.Length == 0) return;

			float alpha = isEditMode ? 1.0f : 0.4f;

			for (int i = 0; i < module.ExhaustPoints.Length; i++)
			{
				var ep = module.ExhaustPoints[i];
				Vector2 screenRoot = origin + new Vector2(ep.PixelOffsetX, ep.PixelOffsetY) * canvasZoom;
				Vector2 dir = new Vector2(ep.DirX, ep.DirY).Normalized();
				if (dir.LengthSquared() < 0.001f) dir = new Vector2(0, 1);
				Vector2 normal = new(-dir.Y, dir.X);

				Color flameColor = Color.FromHtml(string.IsNullOrEmpty(ep.FlameColorHex) ? "#38bdf8" : ep.FlameColorHex);
				flameColor.A = alpha;

				float length = Mathf.Max(ep.FlameLength, 15.0f) * canvasZoom;
				float halfWidth = Mathf.Max(ep.FlameWidth * 0.5f, 4.0f) * canvasZoom;

				// 1. 等离子尾焰羽流
				Vector2 pRootLeft = screenRoot + normal * halfWidth;
				Vector2 pRootRight = screenRoot - normal * halfWidth;
				Vector2 pTip = screenRoot + dir * length;
				Vector2 pCoreTip = screenRoot + dir * (length * 0.55f);

				Color outerFlame = flameColor with { A = 0.25f * alpha };
				canvas.DrawPolygon(new[] { pRootLeft, pRootRight, pTip }, new[] { outerFlame });

				Color coreFlame = new Color(1.0f, 1.0f, 1.0f, 0.7f * alpha);
				canvas.DrawPolygon(new[] { screenRoot + normal * (halfWidth * 0.4f), screenRoot - normal * (halfWidth * 0.4f), pCoreTip }, new[] { coreFlame });

				// 2. 喷口基座与交互手柄
				bool isSelected = isEditMode && (i == SelectedIndex);
				bool isHovered = isEditMode && (i == HoveredIndex);

				Color baseColor = isSelected ? Colors.Yellow : (isHovered ? Colors.White : flameColor);
				canvas.DrawCircle(screenRoot, 6.0f * canvasZoom, baseColor);
				canvas.DrawCircle(screenRoot, 2.5f * canvasZoom, Colors.Black);

				if (isEditMode)
				{
					// 尖端方向与长度手柄
					Color tipColor = (isSelected && ActiveHandle == ExhaustHandleType.DirectionAndLengthTip) || (isHovered && HoveredHandle == ExhaustHandleType.DirectionAndLengthTip)
						? Colors.Yellow
						: new Color(1.0f, 0.6f, 0.2f);

					canvas.DrawLine(screenRoot, pTip, new Color(1.0f, 1.0f, 1.0f, 0.45f), 1.5f);
					canvas.DrawCircle(pTip, 6.0f * canvasZoom, tipColor);
					canvas.DrawCircle(pTip, 2.5f * canvasZoom, Colors.Black);

					// 左右宽度手柄
					Color leftColor = (isSelected && ActiveHandle == ExhaustHandleType.WidthLeft) || (isHovered && HoveredHandle == ExhaustHandleType.WidthLeft)
						? Colors.Yellow
						: Colors.Cyan;

					Color rightColor = (isSelected && ActiveHandle == ExhaustHandleType.WidthRight) || (isHovered && HoveredHandle == ExhaustHandleType.WidthRight)
						? Colors.Yellow
						: Colors.Cyan;

					canvas.DrawLine(pRootLeft, pRootRight, new Color(0.2f, 0.9f, 1.0f, 0.6f), 1.5f);
					canvas.DrawCircle(pRootLeft, 5.0f * canvasZoom, leftColor);
					canvas.DrawCircle(pRootLeft, 2.0f * canvasZoom, Colors.Black);
					canvas.DrawCircle(pRootRight, 5.0f * canvasZoom, rightColor);
					canvas.DrawCircle(pRootRight, 2.0f * canvasZoom, Colors.Black);

					float angleDeg = Mathf.PosMod(Mathf.RadToDeg(Mathf.Atan2(dir.Y, dir.X)) + 90.0f, 360.0f);
					canvas.DrawString(ThemeDB.FallbackFont, screenRoot + new Vector2(10, -8), $"[{ep.Id}] {angleDeg:F0}° 焰长:{ep.FlameLength:F0}px 宽:{ep.FlameWidth:F0}px", HorizontalAlignment.Left, -1, 11, Colors.Yellow);
				}
			}
		}
	}
}
