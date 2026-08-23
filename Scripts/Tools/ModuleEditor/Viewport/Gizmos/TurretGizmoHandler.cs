using System;
using Godot;
using BreakerProtocol.Data.Models;
using BreakerProtocol.Data.Models.Properties;

namespace BreakerProtocol.Tools.ModuleEditor.Viewport.Gizmos
{
	public enum TurretHandleType
	{
		None,
		MountPivot,
		TurretAnchor,
		ArcAngle,
		RangeRadius
	}

	public class TurretGizmoHandler
	{
		public TurretHandleType ActiveHandle { get; private set; } = TurretHandleType.None;
		public TurretHandleType HoveredHandle { get; private set; } = TurretHandleType.None;

		public bool IsTestFiringMode { get; set; } = false;
		public float CurrentAimAngleRad { get; set; } = -Mathf.Pi * 0.5f; // 默认垂直向上 (0° / -Y)

		private Vector2 _dragStartMouse = Vector2.Zero;
		private float _dragStartVal = 0.0f;

		public void UpdateTestAiming(float dt, float turnRateDeg)
		{
			// 固定槽武器锁定朝向正前方
			// 回转炮塔在测试模式下朝鼠标平滑旋转
			if (turnRateDeg <= 0)
			{
				CurrentAimAngleRad = -Mathf.Pi * 0.5f;
				return;
			}
		}

		public void AimAtMouse(Vector2 mouseLocalPx, Vector2 pivotPx, float arcDeg, float turnRateDeg, float dt)
		{
			Vector2 dir = mouseLocalPx - pivotPx;
			if (dir.LengthSquared() < 1.0f) return;

			float targetRad = Mathf.Atan2(dir.Y, dir.X);
			float baseForwardRad = -Mathf.Pi * 0.5f; // 正上方为基准方向

			if (arcDeg < 360.0f)
			{
				float halfArcRad = Mathf.DegToRad(arcDeg * 0.5f);
				float diff = Mathf.Wrap(targetRad - baseForwardRad, -Mathf.Pi, Mathf.Pi);
				diff = Mathf.Clamp(diff, -halfArcRad, halfArcRad);
				targetRad = baseForwardRad + diff;
			}

			float maxTurnRad = Mathf.DegToRad(turnRateDeg) * dt;
			float angleDiff = Mathf.Wrap(targetRad - CurrentAimAngleRad, -Mathf.Pi, Mathf.Pi);
			float step = Mathf.Clamp(angleDiff, -maxTurnRad, maxTurnRad);
			CurrentAimAngleRad += step;
		}

		public bool OnLeftClickDown(ModuleDataDefinition? module, Vector2 localPx, float zoom)
		{
			if (module == null || module.Category != "Weapons") return false;

			float hitDist = Mathf.Max(16.0f / zoom, 8.0f);
			var (pivotPos, rangePos, arcPos, anchorPos) = GetHandlePositions(module);

			// 1. 射程拖拽把手 (固定槽与炮塔通用)
			if (localPx.DistanceTo(rangePos) <= hitDist)
			{
				ActiveHandle = TurretHandleType.RangeRadius;
				_dragStartMouse = localPx;
				var wp = module.GetProperties<WeaponProperties>();
				_dragStartVal = wp?.Range ?? 150.0f;
				return true;
			}

			if (module.MountType == "Turret")
			{
				// 2. 射界角度把手
				if (localPx.DistanceTo(arcPos) <= hitDist)
				{
					ActiveHandle = TurretHandleType.ArcAngle;
					_dragStartMouse = localPx;
					_dragStartVal = module.RotationArc;
					return true;
				}

				// 3. 贴图转轴中心
				if (localPx.DistanceTo(anchorPos) <= hitDist)
				{
					ActiveHandle = TurretHandleType.TurretAnchor;
					_dragStartMouse = localPx;
					return true;
				}

				// 4. 底座安装位
				if (localPx.DistanceTo(pivotPos) <= hitDist)
				{
					ActiveHandle = TurretHandleType.MountPivot;
					_dragStartMouse = localPx;
					return true;
				}
			}

			return false;
		}

		public void HandleDrag(ModuleDataDefinition? module, Vector2 localPx)
		{
			if (module == null || ActiveHandle == TurretHandleType.None) return;

			var (pivotPos, _, _, _) = GetHandlePositions(module);

			switch (ActiveHandle)
			{
				case TurretHandleType.RangeRadius:
				{
					var wp = module.GetProperties<WeaponProperties>() ?? new WeaponProperties();
					float distPx = localPx.DistanceTo(pivotPos);
					float newRange = Mathf.Max(10.0f, distPx / 8.0f); // 8px = 1m
					wp.Range = Mathf.Round(newRange / 5.0f) * 5.0f;
					module.Properties = System.Text.Json.JsonSerializer.SerializeToElement(wp);
					break;
				}
				case TurretHandleType.ArcAngle:
				{
					Vector2 dir = (localPx - pivotPos).Normalized();
					float angleDeg = Mathf.RadToDeg(Mathf.Atan2(dir.Y, dir.X)) + 90.0f;
					angleDeg = Mathf.Wrap(angleDeg, -180.0f, 180.0f);
					module.RotationArc = Mathf.Clamp(Mathf.Round(Mathf.Abs(angleDeg) * 2.0f / 5.0f) * 5.0f, 10.0f, 360.0f);
					break;
				}
				case TurretHandleType.TurretAnchor:
					module.TurretAnchorX = Mathf.Round(localPx.X);
					module.TurretAnchorY = Mathf.Round(localPx.Y);
					break;

				case TurretHandleType.MountPivot:
					module.PivotPixelX = Mathf.Round(localPx.X);
					module.PivotPixelY = Mathf.Round(localPx.Y);
					break;
			}
		}

		public void ReleaseHandle() => ActiveHandle = TurretHandleType.None;

		public void UpdateHover(ModuleDataDefinition? module, Vector2 localPx, float zoom)
		{
			if (module == null || module.Category != "Weapons") { HoveredHandle = TurretHandleType.None; return; }

			float hitDist = Mathf.Max(16.0f / zoom, 8.0f);
			var (pivotPos, rangePos, arcPos, anchorPos) = GetHandlePositions(module);

			if (localPx.DistanceTo(rangePos) <= hitDist) HoveredHandle = TurretHandleType.RangeRadius;
			else if (module.MountType == "Turret" && localPx.DistanceTo(arcPos) <= hitDist) HoveredHandle = TurretHandleType.ArcAngle;
			else if (module.MountType == "Turret" && localPx.DistanceTo(anchorPos) <= hitDist) HoveredHandle = TurretHandleType.TurretAnchor;
			else if (module.MountType == "Turret" && localPx.DistanceTo(pivotPos) <= hitDist) HoveredHandle = TurretHandleType.MountPivot;
			else HoveredHandle = TurretHandleType.None;
		}

		public void Draw(Control canvas, ModuleDataDefinition? module, Vector2 origin, float zoom, bool isActive)
		{
			// 如果是机库，直接退出，不绘制枪炮的垂直射程直线
			if (module == null || module.Category != "Weapons" || module.MountType == "Hangar") return;

			var wp = module.GetProperties<WeaponProperties>() ?? new WeaponProperties();
			float rangePx = (wp.Range > 0 ? wp.Range * 8.0f : 240.0f);
			Vector2 pivotScreen = origin + new Vector2(module.PivotPixelX, module.PivotPixelY) * zoom;

			// ==========================================
			// 1. 固定槽武器 (Fixed Mount) 专属正向射程标尺
			// ==========================================
			if (module.MountType != "Turret")
			{
				Vector2 forwardDir = new(0, -1);
				Vector2 rangeEndScreen = pivotScreen + forwardDir * rangePx * zoom;

				// 1.1 射程弹道中心标线
				canvas.DrawLine(pivotScreen, rangeEndScreen, new Color(0.35f, 0.85f, 1.0f, 0.45f), 1.5f);

				// 1.2 弹道散布角锥形范围
				if (wp.Spread > 0.1f)
				{
					float halfSpreadRad = Mathf.DegToRad(wp.Spread * 0.5f);
					Vector2 leftDir = forwardDir.Rotated(-halfSpreadRad);
					Vector2 rightDir = forwardDir.Rotated(halfSpreadRad);
					canvas.DrawLine(pivotScreen, pivotScreen + leftDir * rangePx * zoom, new Color(1.0f, 0.85f, 0.3f, 0.35f), 1.0f);
					canvas.DrawLine(pivotScreen, pivotScreen + rightDir * rangePx * zoom, new Color(1.0f, 0.85f, 0.3f, 0.35f), 1.0f);
				}

				// 1.3 射程落点端标与十字把手
				Color handleColor = HoveredHandle == TurretHandleType.RangeRadius || ActiveHandle == TurretHandleType.RangeRadius
					? new Color(1.0f, 0.9f, 0.2f)
					: new Color(0.35f, 0.85f, 1.0f);

				// 端点横向标尺线
				canvas.DrawLine(rangeEndScreen - new Vector2(16, 0) * zoom, rangeEndScreen + new Vector2(16, 0) * zoom, handleColor, 2.0f);
				canvas.DrawCircle(rangeEndScreen, 5.0f * zoom, handleColor);
				canvas.DrawCircle(rangeEndScreen, 2.5f * zoom, Colors.White);

				// 1.4 射程提示文字
				canvas.DrawString(
					ThemeDB.FallbackFont,
					rangeEndScreen + new Vector2(14, 4) * zoom,
					$"🎯 固定有效射程: {wp.Range:F0}m ({(wp.Range * 8.0f):F0}px)",
					HorizontalAlignment.Left,
					-1,
					(int)(11 * Mathf.Clamp(zoom, 0.8f, 1.3f)),
					handleColor
				);
				return;
			}

			// ==========================================
			// 2. 回转炮塔 (Turret Mount) 射界与射程圆环
			// ==========================================
			float radiusScreen = rangePx * zoom;

			// 2.1 射界扇区与射程圆环
			if (module.RotationArc >= 360.0f)
			{
				canvas.DrawArc(pivotScreen, radiusScreen, 0, Mathf.Tau, 64, new Color(1.0f, 0.85f, 0.25f, 0.5f), 1.5f);
			}
			else
			{
				float halfArc = Mathf.DegToRad(module.RotationArc * 0.5f);
				float startAngle = -Mathf.Pi * 0.5f - halfArc;
				float endAngle = -Mathf.Pi * 0.5f + halfArc;

				canvas.DrawArc(pivotScreen, radiusScreen, startAngle, endAngle, 32, new Color(1.0f, 0.85f, 0.25f, 0.7f), 1.5f);
				canvas.DrawLine(pivotScreen, pivotScreen + new Vector2(Mathf.Cos(startAngle), Mathf.Sin(startAngle)) * radiusScreen, new Color(1.0f, 0.85f, 0.25f, 0.4f), 1.0f);
				canvas.DrawLine(pivotScreen, pivotScreen + new Vector2(Mathf.Cos(endAngle), Mathf.Sin(endAngle)) * radiusScreen, new Color(1.0f, 0.85f, 0.25f, 0.4f), 1.0f);
			}

			// 2.2 射程调节把手 (正上方边缘点)
			Vector2 rangeHandlePos = pivotScreen + new Vector2(0, -radiusScreen);
			Color rangeHandleColor = HoveredHandle == TurretHandleType.RangeRadius || ActiveHandle == TurretHandleType.RangeRadius
				? new Color(1.0f, 0.9f, 0.2f)
				: new Color(0.35f, 0.85f, 1.0f);
			canvas.DrawCircle(rangeHandlePos, 5.0f * zoom, rangeHandleColor);
			canvas.DrawCircle(rangeHandlePos, 2.5f * zoom, Colors.White);

			// 2.3 射程文本标记
			canvas.DrawString(
				ThemeDB.FallbackFont,
				rangeHandlePos + new Vector2(10, -6) * zoom,
				$"🎯 炮塔射程: {wp.Range:F0}m",
				HorizontalAlignment.Left,
				-1,
				(int)(11 * Mathf.Clamp(zoom, 0.8f, 1.3f)),
				rangeHandleColor
			);

			if (!isActive) return;

			// 2.4 底座安装位与转轴联动指示
			Vector2 anchorScreen = origin + new Vector2(module.TurretAnchorX, module.TurretAnchorY) * zoom;
			canvas.DrawLine(pivotScreen, anchorScreen, new Color(0.2f, 0.85f, 1.0f, 0.6f), 1.5f);

			// 底座安装位把手 (黄框)
			canvas.DrawRect(new Rect2(pivotScreen - new Vector2(6, 6) * zoom, new Vector2(12, 12) * zoom), new Color(1.0f, 0.85f, 0.0f), filled: true);
			canvas.DrawRect(new Rect2(pivotScreen - new Vector2(6, 6) * zoom, new Vector2(12, 12) * zoom), Colors.Black, filled: false, width: 1.0f);

			// 贴图转轴中心把手 (青圆)
			canvas.DrawCircle(anchorScreen, 5.0f * zoom, new Color(0.2f, 0.85f, 1.0f));
			canvas.DrawCircle(anchorScreen, 2.5f * zoom, Colors.White);
		}

		private (Vector2 pivot, Vector2 range, Vector2 arc, Vector2 anchor) GetHandlePositions(ModuleDataDefinition module)
		{
			Vector2 pivot = new(module.PivotPixelX, module.PivotPixelY);
			var wp = module.GetProperties<WeaponProperties>();
			float rangePx = (wp != null && wp.Range > 0 ? wp.Range * 8.0f : 240.0f);

			Vector2 range = pivot + new Vector2(0, -rangePx);

			float halfArcRad = Mathf.DegToRad(module.RotationArc * 0.5f);
			float endAngle = -Mathf.Pi * 0.5f + halfArcRad;
			Vector2 arc = pivot + new Vector2(Mathf.Cos(endAngle), Mathf.Sin(endAngle)) * rangePx;

			Vector2 anchor = new(module.TurretAnchorX, module.TurretAnchorY);
			return (pivot, range, arc, anchor);
		}
	}
}
