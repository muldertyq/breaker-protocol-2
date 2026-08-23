using System;
using System.Text.Json;
using Godot;
using BreakerProtocol.Data.Models;
using BreakerProtocol.Data.Models.Properties;

namespace BreakerProtocol.Tools.ModuleEditor.Viewport.Gizmos
{
	public enum ShieldHandleType
	{
		None,
		Emitter,
		Radius,
		ArcLeft,
		ArcRight
	}

	public class ShieldGizmoHandler
	{
		public ShieldHandleType ActiveHandle { get; private set; } = ShieldHandleType.None;
		public ShieldHandleType HoveredHandle { get; private set; } = ShieldHandleType.None;

		public bool OnLeftClickDown(ModuleDataDefinition? module, Vector2 localPx, float canvasZoom)
		{
			if (module == null) return false;
			var shield = module.GetProperties<ShieldProperties>();
			if (shield == null || shield.ShieldCapacity <= 0) return false;

			Vector2 emitter = new(module.PivotPixelX, module.PivotPixelY);
			float radius = shield.ShieldRadius;
			float halfArcRad = Mathf.DegToRad(shield.ShieldArc * 0.5f);
			float hitDist = 20.0f / canvasZoom;

			Vector2 radiusHandlePos = emitter + new Vector2(0, -radius);
			Vector2 leftHandlePos = emitter + new Vector2(-Mathf.Sin(halfArcRad), -Mathf.Cos(halfArcRad)) * radius;
			Vector2 rightHandlePos = emitter + new Vector2(Mathf.Sin(halfArcRad), -Mathf.Cos(halfArcRad)) * radius;

			if (localPx.DistanceTo(radiusHandlePos) <= hitDist) ActiveHandle = ShieldHandleType.Radius;
			else if (localPx.DistanceTo(leftHandlePos) <= hitDist) ActiveHandle = ShieldHandleType.ArcLeft;
			else if (localPx.DistanceTo(rightHandlePos) <= hitDist) ActiveHandle = ShieldHandleType.ArcRight;
			else if (localPx.DistanceTo(emitter) <= hitDist) ActiveHandle = ShieldHandleType.Emitter;
			else ActiveHandle = ShieldHandleType.None;

			return ActiveHandle != ShieldHandleType.None;
		}

		public void HandleDrag(ModuleDataDefinition? module, Vector2 localPixel)
		{
			if (module == null || ActiveHandle == ShieldHandleType.None) return;

			var shield = module.GetProperties<ShieldProperties>() ?? new ShieldProperties();
			Vector2 emitter = new(module.PivotPixelX, module.PivotPixelY);

			if (ActiveHandle == ShieldHandleType.Radius)
			{
				float newRadius = Mathf.Clamp(localPixel.DistanceTo(emitter), 40.0f, 400.0f);
				shield.ShieldRadius = Mathf.Round(newRadius);
			}
			else if (ActiveHandle is ShieldHandleType.ArcLeft or ShieldHandleType.ArcRight)
			{
				Vector2 dir = (localPixel - emitter).Normalized();
				float angleDeg = Mathf.RadToDeg(Mathf.Abs(Mathf.Atan2(dir.X, -dir.Y))) * 2.0f;
				shield.ShieldArc = Mathf.Clamp(Mathf.Round(angleDeg / 5.0f) * 5.0f, 30.0f, 360.0f);
			}
			else if (ActiveHandle == ShieldHandleType.Emitter)
			{
				module.PivotPixelX = Mathf.Round(localPixel.X);
				module.PivotPixelY = Mathf.Round(localPixel.Y);
			}

			module.Properties = JsonSerializer.SerializeToElement(shield);
		}

		public void ReleaseHandle()
		{
			ActiveHandle = ShieldHandleType.None;
		}

		public void UpdateHover(ModuleDataDefinition? module, Vector2 localPx, float canvasZoom)
		{
			if (module == null)
			{
				HoveredHandle = ShieldHandleType.None;
				return;
			}

			var shield = module.GetProperties<ShieldProperties>();
			if (shield == null || shield.ShieldCapacity <= 0)
			{
				HoveredHandle = ShieldHandleType.None;
				return;
			}

			Vector2 emitter = new(module.PivotPixelX, module.PivotPixelY);
			float radius = shield.ShieldRadius;
			float halfArcRad = Mathf.DegToRad(shield.ShieldArc * 0.5f);
			float hitDist = 20.0f / canvasZoom;

			Vector2 radiusHandlePos = emitter + new Vector2(0, -radius);
			Vector2 leftHandlePos = emitter + new Vector2(-Mathf.Sin(halfArcRad), -Mathf.Cos(halfArcRad)) * radius;
			Vector2 rightHandlePos = emitter + new Vector2(Mathf.Sin(halfArcRad), -Mathf.Cos(halfArcRad)) * radius;

			if (localPx.DistanceTo(radiusHandlePos) <= hitDist) HoveredHandle = ShieldHandleType.Radius;
			else if (localPx.DistanceTo(leftHandlePos) <= hitDist) HoveredHandle = ShieldHandleType.ArcLeft;
			else if (localPx.DistanceTo(rightHandlePos) <= hitDist) HoveredHandle = ShieldHandleType.ArcRight;
			else if (localPx.DistanceTo(emitter) <= hitDist) HoveredHandle = ShieldHandleType.Emitter;
			else HoveredHandle = ShieldHandleType.None;
		}

		public void Draw(CanvasItem canvas, ModuleDataDefinition module, Vector2 origin, float canvasZoom, bool isEditMode)
		{
			bool isShield = module.Tags != null && Array.IndexOf(module.Tags, "Shield") >= 0;
			if (!isShield) return;

			var shield = module.GetProperties<ShieldProperties>();
			if (shield == null || shield.ShieldCapacity <= 0) return;

			Vector2 emitter = new(module.PivotPixelX, module.PivotPixelY);
			Vector2 emitterScreen = origin + emitter * canvasZoom;
			float outerR = shield.ShieldRadius * canvasZoom;
			float thickness = 24.0f * canvasZoom;
			float innerR = Mathf.Max(outerR - thickness, 8.0f);
			float halfArcRad = Mathf.DegToRad(shield.ShieldArc * 0.5f);

			// 科幻能量力场配色
			Color plasmaCore = new(0.10f, 0.70f, 1.0f, 0.35f);
			Color outerCrest = new(0.55f, 0.98f, 1.0f, 0.95f); // 迎弹面激波层
			Color innerEdge = new(0.18f, 0.55f, 0.95f, 0.35f);
			Color emitterBeam = new(0.35f, 0.80f, 1.0f, 0.25f); // 投射导光束

			// 1. 等离子厚度填充多边形
			if (shield.ShieldType == "OmniBubble" || shield.ShieldArc >= 360.0f)
			{
				canvas.DrawCircle(emitterScreen, outerR, plasmaCore);
				canvas.DrawArc(emitterScreen, outerR, 0, Mathf.Tau, 64, outerCrest, 3.0f);
				canvas.DrawArc(emitterScreen, innerR, 0, Mathf.Tau, 48, innerEdge, 1.5f);
			}
			else
			{
				int segments = 40;
				float startRad = -Mathf.Pi * 0.5f - halfArcRad;
				float stepRad = (halfArcRad * 2.0f) / segments;

				Vector2[] polyPoints = new Vector2[(segments + 1) * 2];
				for (int i = 0; i <= segments; i++)
				{
					float curRad = startRad + i * stepRad;
					Vector2 dir = new(Mathf.Cos(curRad), Mathf.Sin(curRad));
					polyPoints[i] = emitterScreen + dir * outerR;
					polyPoints[polyPoints.Length - 1 - i] = emitterScreen + dir * innerR;
				}

				canvas.DrawPolygon(polyPoints, new[] { plasmaCore });
				canvas.DrawArc(emitterScreen, outerR, startRad, startRad + halfArcRad * 2.0f, segments, outerCrest, 3.0f);
				canvas.DrawArc(emitterScreen, innerR, startRad, startRad + halfArcRad * 2.0f, segments, innerEdge, 1.5f);

				// 左右磁约束投射光束
				Vector2 leftTip = emitterScreen + new Vector2(-Mathf.Sin(halfArcRad), -Mathf.Cos(halfArcRad)) * outerR;
				Vector2 rightTip = emitterScreen + new Vector2(Mathf.Sin(halfArcRad), -Mathf.Cos(halfArcRad)) * outerR;
				canvas.DrawLine(emitterScreen, leftTip, emitterBeam, 1.5f);
				canvas.DrawLine(emitterScreen, rightTip, emitterBeam, 1.5f);
			}

			// 2. 交互手柄（仅在 Shield 模式显示）
			if (isEditMode)
			{
				// 发射源中心
				Color emitterColor = (HoveredHandle == ShieldHandleType.Emitter) ? Colors.White : Colors.Yellow;
				canvas.DrawCircle(emitterScreen, 6.0f * canvasZoom, emitterColor);
				canvas.DrawString(ThemeDB.FallbackFont, emitterScreen + new Vector2(10, 14), $"Emitter: ({emitter.X:F0}, {emitter.Y:F0}) px", HorizontalAlignment.Left, -1, 12, Colors.Yellow);

				// 半径调节手柄
				Vector2 apexScreen = emitterScreen + new Vector2(0, -outerR);
				Color radiusColor = (HoveredHandle == ShieldHandleType.Radius) ? Colors.White : new Color(1.0f, 0.85f, 0.2f);
				canvas.DrawCircle(apexScreen, 8.0f * canvasZoom, radiusColor);
				canvas.DrawCircle(apexScreen, 4.0f * canvasZoom, Colors.Black);
				canvas.DrawLine(emitterScreen, apexScreen, new Color(1, 1, 0, 0.35f), 1.0f);

				// 扇区左右手柄
				Vector2 leftHandle = emitterScreen + new Vector2(-Mathf.Sin(halfArcRad), -Mathf.Cos(halfArcRad)) * outerR;
				Vector2 rightHandle = emitterScreen + new Vector2(Mathf.Sin(halfArcRad), -Mathf.Cos(halfArcRad)) * outerR;
				Color leftColor = (HoveredHandle == ShieldHandleType.ArcLeft) ? Colors.White : Colors.Cyan;
				Color rightColor = (HoveredHandle == ShieldHandleType.ArcRight) ? Colors.White : Colors.Cyan;

				canvas.DrawCircle(leftHandle, 8.0f * canvasZoom, leftColor);
				canvas.DrawCircle(leftHandle, 4.0f * canvasZoom, Colors.Black);
				canvas.DrawCircle(rightHandle, 8.0f * canvasZoom, rightColor);
				canvas.DrawCircle(rightHandle, 4.0f * canvasZoom, Colors.Black);

				canvas.DrawString(ThemeDB.FallbackFont, apexScreen + new Vector2(14, -6), $"R:{shield.ShieldRadius:F0}px  弧度:{shield.ShieldArc:F0}°", HorizontalAlignment.Left, -1, 13, Colors.Yellow);
			}
		}
	}
}
