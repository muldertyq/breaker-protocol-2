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
				float newRadius = Mathf.Clamp(localPixel.DistanceTo(emitter), 40.0f, 600.0f);
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

			Vector2 emitterScreen = origin + new Vector2(module.PivotPixelX, module.PivotPixelY) * canvasZoom;
			float outerR = shield.ShieldRadius * canvasZoom;
			float halfArcRad = Mathf.DegToRad(shield.ShieldArc * 0.5f);
			bool isOmni = shield.ShieldType == "OmniBubble" || shield.ShieldArc >= 360.0f;

			// ==========================================
			// 1. 星舰力场配色体系 (深邃能量紫蓝 + 高亮冲击波外层)
			// ==========================================
			Color deepFieldOuter = new(0.20f, 0.40f, 0.95f, 0.38f); // 迎弹面饱满半透
			Color deepFieldMid   = new(0.18f, 0.28f, 0.85f, 0.18f); // 中层柔和过渡
			Color deepFieldInner = new(0.12f, 0.15f, 0.65f, 0.02f); // 根部渐隐收束
			Color crestGlowOuter = new(0.35f, 0.75f, 1.0f, 0.45f);  // 外层柔光晕
			Color crestCore      = new(0.70f, 0.92f, 1.0f, 0.95f);  // 能量激波主棱线
			Color crestHighlight = new(0.95f, 0.98f, 1.0f, 0.90f);  // 白炽锋刃亮线

			int arcSegments = isOmni ? 64 : 48;
			float startRad = -Mathf.Pi * 0.5f - halfArcRad;
			float sweepRad = halfArcRad * 2.0f;

			// ==========================================
			// 2. 扇区多层径向渐变填充 (Gradient Shield Field)
			// ==========================================
			if (isOmni)
			{
				canvas.DrawCircle(emitterScreen, outerR, deepFieldOuter);
				canvas.DrawCircle(emitterScreen, outerR * 0.65f, deepFieldMid);
				canvas.DrawCircle(emitterScreen, outerR * 0.30f, deepFieldInner);

				canvas.DrawArc(emitterScreen, outerR + 2.0f, 0, Mathf.Tau, arcSegments, crestGlowOuter, 5.0f * canvasZoom);
				canvas.DrawArc(emitterScreen, outerR, 0, Mathf.Tau, arcSegments, crestCore, 2.5f * canvasZoom);
				canvas.DrawArc(emitterScreen, outerR, 0, Mathf.Tau, arcSegments, crestHighlight, 1.0f * canvasZoom);
			}
			else
			{
				// 分 3 个同心渐变带进行平滑扇区填充
				float[] radii = { outerR, outerR * 0.70f, outerR * 0.35f, outerR * 0.08f };
				Color[] layerColors = { deepFieldOuter, deepFieldMid, deepFieldInner };

				for (int layer = 0; layer < 3; layer++)
				{
					float rOuter = radii[layer];
					float rInner = radii[layer + 1];
					Vector2[] ringMesh = new Vector2[(arcSegments + 1) * 2];

					for (int i = 0; i <= arcSegments; i++)
					{
						float curRad = startRad + (sweepRad * i / arcSegments);
						Vector2 dir = new(Mathf.Cos(curRad), Mathf.Sin(curRad));
						ringMesh[i] = emitterScreen + dir * rOuter;
						ringMesh[ringMesh.Length - 1 - i] = emitterScreen + dir * rInner;
					}
					canvas.DrawPolygon(ringMesh, new[] { layerColors[layer] });
				}

				// ==========================================
				// 3. 迎弹面多层泛光激波外弧 (Layered Crest Glow)
				// ==========================================
				canvas.DrawArc(emitterScreen, outerR + 2.0f, startRad, startRad + sweepRad, arcSegments, crestGlowOuter, 6.0f * canvasZoom);
				canvas.DrawArc(emitterScreen, outerR, startRad, startRad + sweepRad, arcSegments, crestCore, 2.8f * canvasZoom);
				canvas.DrawArc(emitterScreen, outerR, startRad, startRad + sweepRad, arcSegments, crestHighlight, 1.2f * canvasZoom);

				// 侧翼磁约束微弱边缘线
				Vector2 leftTip = emitterScreen + new Vector2(-Mathf.Sin(halfArcRad), -Mathf.Cos(halfArcRad)) * outerR;
				Vector2 rightTip = emitterScreen + new Vector2(Mathf.Sin(halfArcRad), -Mathf.Cos(halfArcRad)) * outerR;
				Color edgeFade = new(0.35f, 0.65f, 1.0f, 0.30f);
				canvas.DrawLine(emitterScreen, leftTip, edgeFade, 1.5f * canvasZoom);
				canvas.DrawLine(emitterScreen, rightTip, edgeFade, 1.5f * canvasZoom);
			}

			// ==========================================
			// 4. 发生源中心能量光核
			// ==========================================
			canvas.DrawCircle(emitterScreen, 7.0f * canvasZoom, new Color(0.3f, 0.7f, 1.0f, 0.6f));
			canvas.DrawCircle(emitterScreen, 3.5f * canvasZoom, Colors.White);

			// ==========================================
			// 5. 编辑模式交互手柄 (Gizmos)
			// ==========================================
			if (isEditMode)
			{
				Color emitterColor = (HoveredHandle == ShieldHandleType.Emitter || ActiveHandle == ShieldHandleType.Emitter) ? Colors.Yellow : Colors.White;
				canvas.DrawCircle(emitterScreen, 6.0f * canvasZoom, emitterColor);
				canvas.DrawCircle(emitterScreen, 2.5f * canvasZoom, Colors.Black);

				Vector2 apexScreen = emitterScreen + new Vector2(0, -outerR);
				Color radiusColor = (HoveredHandle == ShieldHandleType.Radius || ActiveHandle == ShieldHandleType.Radius) ? Colors.Yellow : new Color(0.35f, 1.0f, 0.65f);
				canvas.DrawCircle(apexScreen, 6.0f * canvasZoom, radiusColor);
				canvas.DrawCircle(apexScreen, 2.5f * canvasZoom, Colors.Black);

				if (!isOmni)
				{
					Vector2 leftHandle = emitterScreen + new Vector2(-Mathf.Sin(halfArcRad), -Mathf.Cos(halfArcRad)) * outerR;
					Vector2 rightHandle = emitterScreen + new Vector2(Mathf.Sin(halfArcRad), -Mathf.Cos(halfArcRad)) * outerR;
					Color leftColor = (HoveredHandle == ShieldHandleType.ArcLeft || ActiveHandle == ShieldHandleType.ArcLeft) ? Colors.Yellow : Colors.Cyan;
					Color rightColor = (HoveredHandle == ShieldHandleType.ArcRight || ActiveHandle == ShieldHandleType.ArcRight) ? Colors.Yellow : Colors.Cyan;

					canvas.DrawCircle(leftHandle, 6.0f * canvasZoom, leftColor);
					canvas.DrawCircle(leftHandle, 2.5f * canvasZoom, Colors.Black);
					canvas.DrawCircle(rightHandle, 6.0f * canvasZoom, rightColor);
					canvas.DrawCircle(rightHandle, 2.5f * canvasZoom, Colors.Black);
				}

				canvas.DrawString(ThemeDB.FallbackFont, apexScreen + new Vector2(12, -6), $"🛡️ 护盾半径: {shield.ShieldRadius:F0}px  偏导弧: {shield.ShieldArc:F0}°", HorizontalAlignment.Left, -1, (int)(11 * Mathf.Clamp(canvasZoom, 0.8f, 1.2f)), Colors.Yellow);
			}
		}
	}
}
