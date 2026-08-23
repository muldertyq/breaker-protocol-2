using Godot;
using BreakerProtocol.Data.Models;

namespace BreakerProtocol.Tools.ModuleEditor.Viewport.Gizmos
{
	public class EmissiveGizmoHandler
	{
		public bool IsDragging { get; private set; } = false;
		public bool IsHovered { get; private set; } = false;

		private Vector2 _dragStartMouse = Vector2.Zero;
		private Vector2 _dragStartOffset = Vector2.Zero;

		public bool OnLeftClickDown(ModuleDataDefinition? module, Texture2D? emissiveTex, Vector2 localPx, float zoom, float rotRad)
		{
			if (module == null || emissiveTex == null) return false;

			Vector2 localMouse = ToLocalEmissiveSpace(module, localPx, rotRad);
			Rect2 localBounds = GetLocalRect(module, emissiveTex);
			Vector2 centerPos = new(module.EmissiveOffsetX, module.EmissiveOffsetY);
			float hitRadius = Mathf.Max(14.0f / zoom, 8.0f);

			if (localBounds.HasPoint(localMouse) || localMouse.DistanceTo(centerPos) <= hitRadius)
			{
				IsDragging = true;
				_dragStartMouse = localPx;
				_dragStartOffset = new Vector2(module.EmissiveOffsetX, module.EmissiveOffsetY);
				return true;
			}

			return false;
		}

		public void HandleDrag(ModuleDataDefinition? module, Vector2 localPx, float rotRad)
		{
			if (!IsDragging || module == null) return;

			Vector2 deltaWorld = localPx - _dragStartMouse;
			Vector2 deltaLocal = deltaWorld.Rotated(-rotRad); // 逆旋转变换，保证任意角度下鼠标拖拽直观平滑

			module.EmissiveOffsetX = Mathf.Round(_dragStartOffset.X + deltaLocal.X);
			module.EmissiveOffsetY = Mathf.Round(_dragStartOffset.Y + deltaLocal.Y);
		}

		public void ReleaseHandle() => IsDragging = false;

		public void UpdateHover(ModuleDataDefinition? module, Texture2D? emissiveTex, Vector2 localPx, float zoom, float rotRad)
		{
			if (module == null || emissiveTex == null)
			{
				IsHovered = false;
				return;
			}

			Vector2 localMouse = ToLocalEmissiveSpace(module, localPx, rotRad);
			Rect2 localBounds = GetLocalRect(module, emissiveTex);
			Vector2 centerPos = new(module.EmissiveOffsetX, module.EmissiveOffsetY);
			float hitRadius = Mathf.Max(14.0f / zoom, 8.0f);

			IsHovered = localBounds.HasPoint(localMouse) || localMouse.DistanceTo(centerPos) <= hitRadius;
		}

		public void Draw(Control canvas, ModuleDataDefinition? module, Texture2D? emissiveTex, Vector2 origin, float zoom, bool isActive, float rotRad)
		{
			if (!isActive || module == null || emissiveTex == null) return;

			bool isTurret = module.EmissiveAttachTo == "Overlay" && module.MountType == "Turret";
			Vector2 pivotScreen = isTurret
				? origin + new Vector2(module.PivotPixelX, module.PivotPixelY) * zoom
				: origin;

			float drawRot = isTurret ? rotRad : 0.0f;

			canvas.DrawSetTransform(pivotScreen, drawRot, new Vector2(zoom, zoom));

			// 1. 发光贴图虚线框与半透填充（在局部旋转坐标系中绘制）
			Rect2 localRect = GetLocalRect(module, emissiveTex);
			Color boxColor = IsHovered || IsDragging ? new Color(0.95f, 0.45f, 1.0f, 0.95f) : new Color(0.75f, 0.35f, 0.95f, 0.65f);
			canvas.DrawRect(localRect, new Color(0.9f, 0.35f, 1.0f, 0.15f), filled: true);
			canvas.DrawRect(localRect, boxColor, filled: false, width: 1.5f / zoom);

			// 2. 发光中心十字把手
			Vector2 centerPos = new(module.EmissiveOffsetX, module.EmissiveOffsetY);
			canvas.DrawCircle(centerPos, 5.0f, new Color(1.0f, 0.4f, 1.0f));
			canvas.DrawCircle(centerPos, 3.0f, Colors.White);
			canvas.DrawLine(centerPos - new Vector2(7, 0), centerPos + new Vector2(7, 0), Colors.White, 1.2f / zoom);
			canvas.DrawLine(centerPos - new Vector2(0, 7), centerPos + new Vector2(0, 7), Colors.White, 1.2f / zoom);

			// 3. 提示文本
			canvas.DrawString(
				ThemeDB.FallbackFont,
				centerPos + new Vector2(10, -8),
				$"💡 发光层 ({module.EmissiveOffsetX}, {module.EmissiveOffsetY})",
				HorizontalAlignment.Left,
				-1,
				(int)(11 * Mathf.Clamp(zoom, 0.8f, 1.2f)),
				new Color(0.95f, 0.65f, 1.0f)
			);

			canvas.DrawSetTransform(Vector2.Zero, 0, Vector2.One);
		}

		private Vector2 ToLocalEmissiveSpace(ModuleDataDefinition module, Vector2 localPx, float rotRad)
		{
			if (module.EmissiveAttachTo == "Overlay" && module.MountType == "Turret")
			{
				Vector2 pivot = new(module.PivotPixelX, module.PivotPixelY);
				return (localPx - pivot).Rotated(-rotRad);
			}
			return localPx;
		}

		private Rect2 GetLocalRect(ModuleDataDefinition module, Texture2D tex)
		{
			Vector2 topLeft = new Vector2(module.EmissiveOffsetX, module.EmissiveOffsetY) - new Vector2(module.EmissiveAnchorX, module.EmissiveAnchorY);
			return new Rect2(topLeft, tex.GetSize());
		}
	}
}
