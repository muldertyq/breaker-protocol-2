using System;
using System.Collections.Generic;
using Godot;
using BreakerProtocol.Data.Models;
using BreakerProtocol.Data.Models.Properties;

namespace BreakerProtocol.Tools.ModuleEditor.Viewport.Gizmos
{
	public enum SlotHandleType
	{
		None,
		Move,
		ResizeLength,
		ResizeWidth
	}

	public class MunitionSlotGizmoHandler
	{
		public int SelectedIndex { get; set; } = -1;
		public int HoveredIndex { get; set; } = -1;
		public SlotHandleType ActiveHandle { get; private set; } = SlotHandleType.None;
		public bool IsDragging => ActiveHandle != SlotHandleType.None;

		private Vector2 _dragStartMouse = Vector2.Zero;
		private Vector2 _dragStartOffset = Vector2.Zero;
		private Vector2 _dragStartSize = Vector2.Zero;

		public bool OnLeftClickDown(ModuleDataDefinition? module, Vector2 localPx, float zoom, float rotRad, bool insideExtended, out bool isCreated)
		{
			isCreated = false;
			if (module == null || module.Category != "Weapons") return false;

			var wp = module.GetProperties<WeaponProperties>() ?? new WeaponProperties();
			var slots = wp.MunitionSlots ?? Array.Empty<MunitionSlotDefinition>();
			float hitRadius = Mathf.Max(12.0f / zoom, 6.0f);

			// 1. 检查选中槽位的控制手柄 (尺寸拉伸与位移)
			if (SelectedIndex >= 0 && SelectedIndex < slots.Length)
			{
				var slot = slots[SelectedIndex];
				Vector2 slotCenter = GetSlotCenterLocal(module, slot);
				Vector2 forward = new Vector2(0, -1).Rotated(Mathf.DegToRad(slot.AngleOffsetDeg));
				Vector2 right = new Vector2(1, 0).Rotated(Mathf.DegToRad(slot.AngleOffsetDeg));

				Vector2 lenHandle = slotCenter + forward * (slot.Length * 0.5f);
				Vector2 widHandle = slotCenter + right * (slot.Width * 0.5f);

				Vector2 transMouse = ToLocalTurretSpace(module, localPx, rotRad);

				if (transMouse.DistanceTo(lenHandle) <= hitRadius)
				{
					ActiveHandle = SlotHandleType.ResizeLength;
					_dragStartMouse = transMouse;
					_dragStartSize = new Vector2(slot.Width, slot.Length);
					return true;
				}

				if (transMouse.DistanceTo(widHandle) <= hitRadius)
				{
					ActiveHandle = SlotHandleType.ResizeWidth;
					_dragStartMouse = transMouse;
					_dragStartSize = new Vector2(slot.Width, slot.Length);
					return true;
				}
			}

			// 2. 检查点击已有弹位
			for (int i = 0; i < slots.Length; i++)
			{
				var slot = slots[i];
				Vector2 slotCenter = GetSlotCenterLocal(module, slot);
				Vector2 transMouse = ToLocalTurretSpace(module, localPx, rotRad);

				Rect2 bounds = new(slotCenter - new Vector2(slot.Width, slot.Length) * 0.5f, new Vector2(slot.Width, slot.Length));
				if (bounds.HasPoint(transMouse) || transMouse.DistanceTo(slotCenter) <= hitRadius)
				{
					SelectedIndex = i;
					ActiveHandle = SlotHandleType.Move;
					_dragStartMouse = transMouse;
					_dragStartOffset = new Vector2(slot.OffsetX, slot.OffsetY);
					return true;
				}
			}

			// 3. 点击空白区域创建新弹位
			if (insideExtended)
			{
				Vector2 transMouse = ToLocalTurretSpace(module, localPx, rotRad);
				var list = new List<MunitionSlotDefinition>(slots);
				var newSlot = new MunitionSlotDefinition
				{
					SlotId = $"slot_{list.Count}",
					FireOrder = list.Count,
					OffsetX = Mathf.Round(transMouse.X),
					OffsetY = Mathf.Round(transMouse.Y),
					Width = wp.DefaultMissileWidth > 0 ? wp.DefaultMissileWidth : 14.0f,
					Length = wp.DefaultMissileLength > 0 ? wp.DefaultMissileLength : 42.0f
				};
				list.Add(newSlot);
				wp.MunitionSlots = list.ToArray();
				module.Properties = System.Text.Json.JsonSerializer.SerializeToElement(wp);

				SelectedIndex = list.Count - 1;
				ActiveHandle = SlotHandleType.Move;
				_dragStartMouse = transMouse;
				_dragStartOffset = new Vector2(newSlot.OffsetX, newSlot.OffsetY);
				isCreated = true;
				return true;
			}

			SelectedIndex = -1;
			return false;
		}

		public void HandleDrag(ModuleDataDefinition? module, Vector2 localPx, float rotRad)
		{
			if (module == null || SelectedIndex < 0 || ActiveHandle == SlotHandleType.None) return;

			var wp = module.GetProperties<WeaponProperties>();
			if (wp?.MunitionSlots == null || SelectedIndex >= wp.MunitionSlots.Length) return;

			var slot = wp.MunitionSlots[SelectedIndex];
			Vector2 transMouse = ToLocalTurretSpace(module, localPx, rotRad);
			Vector2 delta = transMouse - _dragStartMouse;

			switch (ActiveHandle)
			{
				case SlotHandleType.Move:
					slot.OffsetX = Mathf.Round(_dragStartOffset.X + delta.X);
					slot.OffsetY = Mathf.Round(_dragStartOffset.Y + delta.Y);
					break;
				case SlotHandleType.ResizeLength:
					slot.Length = Mathf.Max(10.0f, Mathf.Round(_dragStartSize.Y - delta.Y * 2.0f));
					break;
				case SlotHandleType.ResizeWidth:
					slot.Width = Mathf.Max(4.0f, Mathf.Round(_dragStartSize.X + delta.X * 2.0f));
					break;
			}

			module.Properties = System.Text.Json.JsonSerializer.SerializeToElement(wp);
		}

		public bool TryDeleteSlotAt(ModuleDataDefinition? module, Vector2 localPx, float zoom, float rotRad)
		{
			if (module == null || module.Category != "Weapons") return false;
			var wp = module.GetProperties<WeaponProperties>();
			if (wp?.MunitionSlots == null || wp.MunitionSlots.Length == 0) return false;

			float hitRadius = Mathf.Max(14.0f / zoom, 8.0f);
			Vector2 transMouse = ToLocalTurretSpace(module, localPx, rotRad);

			for (int i = 0; i < wp.MunitionSlots.Length; i++)
			{
				var slot = wp.MunitionSlots[i];
				Vector2 slotCenter = GetSlotCenterLocal(module, slot);
				if (transMouse.DistanceTo(slotCenter) <= hitRadius)
				{
					var list = new List<MunitionSlotDefinition>(wp.MunitionSlots);
					list.RemoveAt(i);
					for (int k = 0; k < list.Count; k++) list[k].FireOrder = k; // 重排序号
					wp.MunitionSlots = list.ToArray();
					module.Properties = System.Text.Json.JsonSerializer.SerializeToElement(wp);

					SelectedIndex = Mathf.Clamp(SelectedIndex, -1, list.Count - 1);
					return true;
				}
			}
			return false;
		}

		public void ReleaseHandle() => ActiveHandle = SlotHandleType.None;

		public void UpdateHover(ModuleDataDefinition? module, Vector2 localPx, float zoom, float rotRad)
		{
			if (module == null || module.Category != "Weapons") { HoveredIndex = -1; return; }
			var wp = module.GetProperties<WeaponProperties>();
			if (wp?.MunitionSlots == null) { HoveredIndex = -1; return; }

			float hitRadius = Mathf.Max(14.0f / zoom, 8.0f);
			Vector2 transMouse = ToLocalTurretSpace(module, localPx, rotRad);

			for (int i = 0; i < wp.MunitionSlots.Length; i++)
			{
				var slot = wp.MunitionSlots[i];
				Vector2 slotCenter = GetSlotCenterLocal(module, slot);
				Rect2 bounds = new(slotCenter - new Vector2(slot.Width, slot.Length) * 0.5f, new Vector2(slot.Width, slot.Length));
				if (bounds.HasPoint(transMouse) || transMouse.DistanceTo(slotCenter) <= hitRadius)
				{
					HoveredIndex = i;
					return;
				}
			}
			HoveredIndex = -1;
		}

		public void Draw(Control canvas, ModuleDataDefinition? module, Vector2 origin, float zoom, bool isActive, float rotRad, Texture2D? defaultMissileTex)
		{
			if (!isActive || module == null || module.Category != "Weapons") return;

			var wp = module.GetProperties<WeaponProperties>();
			if (wp?.MunitionSlots == null || wp.MunitionSlots.Length == 0) return;

			bool isTurret = module.MountType == "Turret";
			Vector2 pivotScreen = isTurret ? origin + new Vector2(module.PivotPixelX, module.PivotPixelY) * zoom : origin;
			float drawRot = isTurret ? rotRad : 0.0f;

			canvas.DrawSetTransform(pivotScreen, drawRot, new Vector2(zoom, zoom));

			for (int i = 0; i < wp.MunitionSlots.Length; i++)
			{
				var slot = wp.MunitionSlots[i];
				Vector2 center = GetSlotCenterLocal(module, slot);
				bool isSelected = (i == SelectedIndex);
				bool isHovered = (i == HoveredIndex);

				Rect2 slotRect = new(center - new Vector2(slot.Width, slot.Length) * 0.5f, new Vector2(slot.Width, slot.Length));

				// 1. 槽位半透明预览框
				Color boxColor = isSelected ? new Color(0.2f, 0.9f, 1.0f) : (isHovered ? Colors.White : new Color(0.4f, 0.7f, 0.9f, 0.6f));
				canvas.DrawRect(slotRect, new Color(0.15f, 0.45f, 0.75f, 0.2f), true);
				canvas.DrawRect(slotRect, boxColor, false, 1.2f / zoom);

				// 2. 发射序号徽标
				Vector2 badgePos = slotRect.Position + new Vector2(4, 12);
				canvas.DrawCircle(badgePos + new Vector2(4, -4), 7.0f / zoom, isSelected ? new Color(0.2f, 0.9f, 1.0f) : new Color(0.1f, 0.15f, 0.25f, 0.85f));
				canvas.DrawString(ThemeDB.FallbackFont, badgePos, $"#{slot.FireOrder + 1}", HorizontalAlignment.Left, -1, (int)(10 * Mathf.Clamp(zoom, 0.8f, 1.2f)), isSelected ? Colors.Black : Colors.White);

				// 3. 选中态的缩放手柄
				if (isSelected)
				{
					Vector2 lenHandle = center + new Vector2(0, -slot.Length * 0.5f);
					Vector2 widHandle = center + new Vector2(slot.Width * 0.5f, 0);

					canvas.DrawCircle(lenHandle, 4.5f / zoom, new Color(1.0f, 0.85f, 0.2f));
					canvas.DrawCircle(widHandle, 4.5f / zoom, new Color(1.0f, 0.85f, 0.2f));
					canvas.DrawCircle(lenHandle, 2.0f / zoom, Colors.White);
					canvas.DrawCircle(widHandle, 2.0f / zoom, Colors.White);
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

		private Vector2 GetSlotCenterLocal(ModuleDataDefinition module, MunitionSlotDefinition slot) =>
			new(slot.OffsetX, slot.OffsetY);
	}
}
