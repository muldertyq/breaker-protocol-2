using System;
using System.Collections.Generic;
using Godot;
using BreakerProtocol.Data.Models;
using BreakerProtocol.Data.Models.Properties;

namespace BreakerProtocol.Tools.ModuleEditor.Viewport.Gizmos
{
	public enum BayHandleType
	{
		None,
		Move,
		ResizeWidth,
		ResizeHeight
	}

	public class MissileBayGizmoHandler
	{
		public int SelectedIndex { get; set; } = -1;
		public int HoveredIndex { get; set; } = -1;
		public BayHandleType ActiveHandle { get; private set; } = BayHandleType.None;
		public bool IsDragging => ActiveHandle != BayHandleType.None;

		private Vector2 _dragStartMouse = Vector2.Zero;
		private Vector2 _dragStartOffset = Vector2.Zero;
		private Vector2 _dragStartSize = Vector2.Zero;

		public bool OnLeftClickDown(ModuleDataDefinition? module, Vector2 localPx, float zoom, float rotRad, bool insideExtended, out bool isCreated)
		{
			isCreated = false;
			if (module == null || module.Category != "Weapons") return false;

			var wp = module.GetProperties<WeaponProperties>() ?? new WeaponProperties();
			var bays = wp.Bays ?? Array.Empty<MissileBayDefinition>();
			float hitRadius = Mathf.Max(12.0f / zoom, 6.0f);
			Vector2 transMouse = ToLocalTurretSpace(module, localPx, rotRad);

			// 1. 检查选中仓盖的尺寸手柄
			if (SelectedIndex >= 0 && SelectedIndex < bays.Length)
			{
				var bay = bays[SelectedIndex];
				Vector2 bayCenter = new(bay.OffsetX, bay.OffsetY);
				Vector2 widHandle = bayCenter + new Vector2(bay.Width * 0.5f, 0);
				Vector2 hgtHandle = bayCenter + new Vector2(0, bay.Height * 0.5f);

				if (transMouse.DistanceTo(widHandle) <= hitRadius)
				{
					ActiveHandle = BayHandleType.ResizeWidth;
					_dragStartMouse = transMouse;
					_dragStartSize = new Vector2(bay.Width, bay.Height);
					return true;
				}

				if (transMouse.DistanceTo(hgtHandle) <= hitRadius)
				{
					ActiveHandle = BayHandleType.ResizeHeight;
					_dragStartMouse = transMouse;
					_dragStartSize = new Vector2(bay.Width, bay.Height);
					return true;
				}
			}

			// 2. 检查点击已有仓盖中心
			for (int i = 0; i < bays.Length; i++)
			{
				var bay = bays[i];
				Vector2 bayCenter = new(bay.OffsetX, bay.OffsetY);
				Rect2 bounds = new(bayCenter - new Vector2(bay.Width, bay.Height) * 0.5f, new Vector2(bay.Width, bay.Height));

				if (bounds.HasPoint(transMouse) || transMouse.DistanceTo(bayCenter) <= hitRadius)
				{
					SelectedIndex = i;
					ActiveHandle = BayHandleType.Move;
					_dragStartMouse = transMouse;
					_dragStartOffset = bayCenter;
					return true;
				}
			}

			// 3. 点击空白处创建新仓盖
			if (insideExtended)
			{
				var list = new List<MissileBayDefinition>(bays);
				var newBay = new MissileBayDefinition
				{
					BayId = $"bay_{list.Count}",
					OffsetX = Mathf.Round(transMouse.X),
					OffsetY = Mathf.Round(transMouse.Y),
					Width = 32.0f,
					Height = 48.0f,
					OpenDuration = 0.25f,
					AnimationType = "InstantHide"
				};
				list.Add(newBay);
				wp.Bays = list.ToArray();
				module.Properties = System.Text.Json.JsonSerializer.SerializeToElement(wp);

				SelectedIndex = list.Count - 1;
				ActiveHandle = BayHandleType.Move;
				_dragStartMouse = transMouse;
				_dragStartOffset = new Vector2(newBay.OffsetX, newBay.OffsetY);
				isCreated = true;
				return true;
			}

			SelectedIndex = -1;
			return false;
		}

		public void HandleDrag(ModuleDataDefinition? module, Vector2 localPx, float rotRad)
		{
			if (module == null || SelectedIndex < 0 || ActiveHandle == BayHandleType.None) return;

			var wp = module.GetProperties<WeaponProperties>();
			if (wp?.Bays == null || SelectedIndex >= wp.Bays.Length) return;

			var bay = wp.Bays[SelectedIndex];
			Vector2 transMouse = ToLocalTurretSpace(module, localPx, rotRad);
			Vector2 delta = transMouse - _dragStartMouse;

			switch (ActiveHandle)
			{
				case BayHandleType.Move:
					bay.OffsetX = Mathf.Round(_dragStartOffset.X + delta.X);
					bay.OffsetY = Mathf.Round(_dragStartOffset.Y + delta.Y);
					break;
				case BayHandleType.ResizeWidth:
					bay.Width = Mathf.Max(12.0f, Mathf.Round(_dragStartSize.X + delta.X * 2.0f));
					break;
				case BayHandleType.ResizeHeight:
					bay.Height = Mathf.Max(12.0f, Mathf.Round(_dragStartSize.Y + delta.Y * 2.0f));
					break;
			}

			module.Properties = System.Text.Json.JsonSerializer.SerializeToElement(wp);
		}

		public bool TryDeleteBayAt(ModuleDataDefinition? module, Vector2 localPx, float zoom, float rotRad)
		{
			if (module == null || module.Category != "Weapons") return false;
			var wp = module.GetProperties<WeaponProperties>();
			if (wp?.Bays == null || wp.Bays.Length == 0) return false;

			float hitRadius = Mathf.Max(14.0f / zoom, 8.0f);
			Vector2 transMouse = ToLocalTurretSpace(module, localPx, rotRad);

			for (int i = 0; i < wp.Bays.Length; i++)
			{
				var bay = wp.Bays[i];
				Vector2 center = new(bay.OffsetX, bay.OffsetY);
				if (transMouse.DistanceTo(center) <= hitRadius)
				{
					var list = new List<MissileBayDefinition>(wp.Bays);
					list.RemoveAt(i);
					wp.Bays = list.ToArray();
					module.Properties = System.Text.Json.JsonSerializer.SerializeToElement(wp);

					SelectedIndex = Mathf.Clamp(SelectedIndex, -1, list.Count - 1);
					return true;
				}
			}
			return false;
		}

		public void ReleaseHandle() => ActiveHandle = BayHandleType.None;

		public void UpdateHover(ModuleDataDefinition? module, Vector2 localPx, float zoom, float rotRad)
		{
			if (module == null || module.Category != "Weapons") { HoveredIndex = -1; return; }
			var wp = module.GetProperties<WeaponProperties>();
			if (wp?.Bays == null) { HoveredIndex = -1; return; }

			float hitRadius = Mathf.Max(14.0f / zoom, 8.0f);
			Vector2 transMouse = ToLocalTurretSpace(module, localPx, rotRad);

			for (int i = 0; i < wp.Bays.Length; i++)
			{
				var bay = wp.Bays[i];
				Vector2 center = new(bay.OffsetX, bay.OffsetY);
				Rect2 bounds = new(center - new Vector2(bay.Width, bay.Height) * 0.5f, new Vector2(bay.Width, bay.Height));
				if (bounds.HasPoint(transMouse) || transMouse.DistanceTo(center) <= hitRadius)
				{
					HoveredIndex = i;
					return;
				}
			}
			HoveredIndex = -1;
		}

		public void Draw(Control canvas, ModuleDataDefinition? module, Vector2 origin, float zoom, bool isActive, float rotRad)
		{
			if (!isActive || module == null || module.Category != "Weapons") return;

			var wp = module.GetProperties<WeaponProperties>();
			if (wp?.Bays == null || wp.Bays.Length == 0) return;

			bool isTurret = module.MountType == "Turret";
			Vector2 pivotScreen = isTurret ? origin + new Vector2(module.PivotPixelX, module.PivotPixelY) * zoom : origin;
			float drawRot = isTurret ? rotRad : 0.0f;

			canvas.DrawSetTransform(pivotScreen, drawRot, new Vector2(zoom, zoom));

			for (int i = 0; i < wp.Bays.Length; i++)
			{
				var bay = wp.Bays[i];
				Vector2 center = new(bay.OffsetX, bay.OffsetY);
				bool isSelected = (i == SelectedIndex);
				bool isHovered = (i == HoveredIndex);

				Rect2 bayRect = new(center - new Vector2(bay.Width, bay.Height) * 0.5f, new Vector2(bay.Width, bay.Height));

				Color strokeColor = isSelected ? new Color(1.0f, 0.45f, 0.2f) : (isHovered ? Colors.White : new Color(0.85f, 0.4f, 0.1f, 0.7f));
				canvas.DrawRect(bayRect, new Color(1.0f, 0.4f, 0.1f, 0.15f), true);
				canvas.DrawRect(bayRect, strokeColor, false, 1.4f / zoom);

				// 仓盖 ID 徽标
				canvas.DrawString(ThemeDB.FallbackFont, bayRect.Position + new Vector2(4, 14), $"🚪 {bay.BayId} [{bay.AnimationType}]", HorizontalAlignment.Left, -1, (int)(11 * Mathf.Clamp(zoom, 0.8f, 1.2f)), isSelected ? Colors.Yellow : Colors.White);

				// 尺寸调节手柄
				if (isSelected)
				{
					Vector2 widHandle = center + new Vector2(bay.Width * 0.5f, 0);
					Vector2 hgtHandle = center + new Vector2(0, bay.Height * 0.5f);

					canvas.DrawCircle(widHandle, 4.5f / zoom, new Color(1.0f, 0.85f, 0.2f));
					canvas.DrawCircle(hgtHandle, 4.5f / zoom, new Color(1.0f, 0.85f, 0.2f));
					canvas.DrawCircle(widHandle, 2.0f / zoom, Colors.White);
					canvas.DrawCircle(hgtHandle, 2.0f / zoom, Colors.White);
				}
			}

			canvas.DrawSetTransform(Vector2.Zero, 0, Vector2.One);
		}

		private Vector2 ToLocalTurretSpace(ModuleDataDefinition module, Vector2 localPx, float rotRad)
		{
			if (module.MountType == "Turret")
			{
				Vector2 pivot = new(module.PivotPixelX, module.PivotPixelY);
				return (localPx - pivot).Rotated(-rotRad);
			}
			return localPx;
		}
	}
}
