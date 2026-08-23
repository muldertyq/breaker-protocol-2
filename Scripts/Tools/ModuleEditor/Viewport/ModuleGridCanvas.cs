using System;
using System.Collections.Generic;
using System.IO;
using Godot;
using BreakerProtocol.Data.Models;
using BreakerProtocol.Data.Models.Properties;
using BreakerProtocol.Tools.ModuleEditor.Viewport.Gizmos;

namespace BreakerProtocol.Tools.ModuleEditor.Viewport
{
	public enum EditGizmoMode
	{
		None,
		Pins,
		FirePoints,
		MunitionSlots,
		Exhausts,
		TurretArc,
		Shield,
		Emissive
	}

	public class DemoProjectile
	{
		public Vector2 Pos;
		public Vector2 Vel;
		public float Life;
		public float Radius;
		public float Length;
		public Color CoreColor;
		public Color GlowColor;
	}

	public class DemoMissile
	{
		public Vector2 Pos;
		public Vector2 Vel;
		public float AngleRad;
		public float Life;
		public float MaxLife;
		public float Width;
		public float Length;
		public Texture2D? Tex;
		public Color CoreColor;
		public Color GlowColor;
		public List<Vector2> Trail = new();
	}

	public class DemoBeam
	{
		public Vector2 Start;
		public Vector2 End;
		public float Life;
		public float MaxLife;
		public float Width;
		public Color CoreColor;
		public Color GlowColor;
	}

	public class SlotRuntimeState
	{
		public bool IsLoaded = true;
		public float ReloadTimer = 0.0f;
	}

	public partial class ModuleGridCanvas : Control
	{
		public const int GridUnitPixels = 80;

		public ModuleDataDefinition? CurrentModule { get; private set; }
		public Texture2D? BaseTexture { get; private set; }
		public Texture2D? OverlayTexture { get; private set; }
		public Texture2D? EmissiveTexture { get; private set; }
		public Texture2D? MissileTexture { get; private set; }

		private string _cachedMissilePath = string.Empty;

		public EditGizmoMode ActiveMode { get; set; } = EditGizmoMode.None;

		private Vector2 _canvasPan = new(240, 180);
		private float _canvasZoom = 1.0f;
		private bool _isPanning = false;
		private Vector2 _panStartMouse = Vector2.Zero;
		private Vector2 _panStartPos = Vector2.Zero;
		private bool _isDraggingGizmo = false;

		private bool _isTestFireHolding = false;

		private readonly PinGizmoHandler _pinHandler = new();
		private readonly ShieldGizmoHandler _shieldHandler = new();
		private readonly FirePointGizmoHandler _firePointHandler = new();
		private readonly MunitionSlotGizmoHandler _slotHandler = new();
		private readonly ExhaustGizmoHandler _exhaustHandler = new();
		private readonly TurretGizmoHandler _turretHandler = new();
		private readonly EmissiveGizmoHandler _emissiveHandler = new();

		private readonly List<DemoProjectile> _demoBullets = new();
		private readonly List<DemoMissile> _demoMissiles = new();
		private readonly List<DemoBeam> _demoBeams = new();
		private float _fireCooldown = 0.0f;

		private readonly List<SlotRuntimeState> _runtimeSlots = new();
		private int _nextFireSlotIndex = 0;
		private bool _isFullRackReloading = false;
		private float _fullRackTimer = 0.0f;

		public event Action? OnDataModified;
		public event Action<int>? OnPinSelectedOnCanvas;
		public event Action<int>? OnSlotSelectedOnCanvas;
		public event Action<int>? OnExhaustSelectedOnCanvas;
		public event Action<Vector2>? OnMouseMovedInCanvas;

		public override void _Ready()
		{
			ClipContents = true;
			CustomMinimumSize = new Vector2(400, 400);
			MouseFilter = MouseFilterEnum.Stop;
		}

		public void LoadModule(ModuleDataDefinition module, Texture2D? baseTex, Texture2D? overlayTex, Texture2D? emissiveTex = null)
		{
			CurrentModule = module;
			BaseTexture = baseTex;
			OverlayTexture = overlayTex;
			EmissiveTexture = emissiveTex;

			var wp = module.GetProperties<WeaponProperties>();
			_cachedMissilePath = wp?.DefaultMissileSprite ?? string.Empty;
			MissileTexture = LoadTextureAuto(_cachedMissilePath);

			_pinHandler.SelectedIndex = -1;
			_exhaustHandler.SelectedIndex = -1;
			_firePointHandler.SelectedIndex = -1;
			_slotHandler.SelectedIndex = -1;
			_isTestFireHolding = false;

			ClearDemoEntities();
			CenterView();
			QueueRedraw();
		}

		public void ClearDemoEntities()
		{
			_demoBullets.Clear();
			_demoMissiles.Clear();
			_demoBeams.Clear();
			ResetMunitionRack();
			QueueRedraw();
		}

		public void ResetMunitionRack()
		{
			_runtimeSlots.Clear();
			var wp = CurrentModule?.GetProperties<WeaponProperties>();
			int slotCount = wp?.MunitionSlots?.Length ?? 0;
			for (int i = 0; i < slotCount; i++)
			{
				_runtimeSlots.Add(new SlotRuntimeState { IsLoaded = true, ReloadTimer = 0.0f });
			}
			_nextFireSlotIndex = 0;
			_isFullRackReloading = false;
			_fullRackTimer = 0.0f;
		}

		public void SetTestFiringMode(bool enabled)
		{
			_turretHandler.IsTestFiringMode = enabled;
			if (!enabled)
			{
				_isTestFireHolding = false;
				ClearDemoEntities();
			}
			QueueRedraw();
		}

		public void SelectPinExternal(int index) { _pinHandler.SelectedIndex = index; QueueRedraw(); }
		public void SelectSlotExternal(int index) { _slotHandler.SelectedIndex = index; QueueRedraw(); }
		public void SelectExhaustExternal(int index) { _exhaustHandler.SelectedIndex = index; QueueRedraw(); }

		public void CenterView()
		{
			if (CurrentModule == null) return;
			float totalW = CurrentModule.Width * GridUnitPixels * _canvasZoom;
			float totalH = CurrentModule.Height * GridUnitPixels * _canvasZoom;
			_canvasPan = (Size - new Vector2(totalW, totalH)) * 0.5f;
			QueueRedraw();
		}

		public override void _Process(double delta)
		{
			float dt = (float)delta;
			bool needRedraw = false;
			var wp = CurrentModule?.GetProperties<WeaponProperties>();

			// 1. 炮塔瞄准追踪
			if (CurrentModule != null)
			{
				if (CurrentModule.MountType == "Turret" && _turretHandler.IsTestFiringMode)
				{
					Vector2 mouseLocalPx = CanvasToWorldPixel(GetLocalMousePosition());
					Vector2 pivotPx = new(CurrentModule.PivotPixelX, CurrentModule.PivotPixelY);
					_turretHandler.AimAtMouse(mouseLocalPx, pivotPx, CurrentModule.RotationArc, CurrentModule.TurnRate, dt);
					needRedraw = true;
				}
				else
				{
					_turretHandler.CurrentAimAngleRad = -Mathf.Pi * 0.5f;
				}
			}

			// 2. 鱼雷挂架装填
			if (wp != null && wp.DeliveryType == "Missile")
			{
				if (wp.ReloadMode == RackReloadMode.FullRack)
				{
					if (_isFullRackReloading)
					{
						_fullRackTimer -= dt;
						needRedraw = true;
						if (_fullRackTimer <= 0.0f)
						{
							ResetMunitionRack();
						}
					}
				}
				else
				{
					for (int i = 0; i < _runtimeSlots.Count; i++)
					{
						var slot = _runtimeSlots[i];
						if (!slot.IsLoaded)
						{
							slot.ReloadTimer -= dt;
							needRedraw = true;
							if (slot.ReloadTimer <= 0.0f)
							{
								slot.IsLoaded = true;
							}
						}
					}
				}
			}

			// 3. 持续开火测试
			if (_turretHandler.IsTestFiringMode && _isTestFireHolding && CurrentModule != null)
			{
				if (wp?.DeliveryType != "ContinuousBeam" && _fireCooldown <= 0.0f) // 持续光束走底部的 _Draw 实时渲染，非连续光束才走 TrySpawnDemoPayload 实体生成
				{
					TrySpawnDemoPayload();
				}
			}

			// 4. 更新飞行的巡航鱼雷及尾部尾焰
			if (_demoMissiles.Count > 0)
			{
				for (int i = _demoMissiles.Count - 1; i >= 0; i--)
				{
					var m = _demoMissiles[i];
					m.Pos += m.Vel * dt;
					m.Life -= dt;

					// 正确定位尾部喷口点 (沿速度反方向偏移半个弹长)
					Vector2 moveDir = m.Vel.LengthSquared() > 0.01f ? m.Vel.Normalized() : new Vector2(0, -1).Rotated(m.AngleRad);
					Vector2 tailExhaustPos = m.Pos - moveDir * (m.Length * 0.5f);

					m.Trail.Insert(0, tailExhaustPos);
					if (m.Trail.Count > 16) m.Trail.RemoveAt(m.Trail.Count - 1);

					if (m.Life <= 0)
					{
						_demoMissiles.RemoveAt(i);
					}
				}
				needRedraw = true;
			}

			// 5. 更新实弹与激光
			if (_demoBullets.Count > 0)
			{
				for (int i = _demoBullets.Count - 1; i >= 0; i--)
				{
					var b = _demoBullets[i];
					b.Pos += b.Vel * dt;
					b.Life -= dt;
					if (b.Life <= 0) _demoBullets.RemoveAt(i);
				}
				needRedraw = true;
			}

			if (_demoBeams.Count > 0)
			{
				for (int i = _demoBeams.Count - 1; i >= 0; i--)
				{
					_demoBeams[i].Life -= dt;
					if (_demoBeams[i].Life <= 0) _demoBeams.RemoveAt(i);
				}
				needRedraw = true;
			}

			if (_fireCooldown > 0) _fireCooldown -= dt;

			if (needRedraw || _turretHandler.IsTestFiringMode) QueueRedraw();
		}

		public override void _GuiInput(InputEvent @event)
		{
			float rotRad = GetCurrentTurretRotationRad();

			if (@event is InputEventMouseButton mb)
			{
				if (mb.ButtonIndex == MouseButton.Right)
				{
					if (mb.Pressed)
					{
						Vector2 localPx = CanvasToWorldPixel(mb.Position);

						if (ActiveMode == EditGizmoMode.Pins && _pinHandler.TryDeletePinAt(CurrentModule, localPx, _canvasZoom, GridUnitPixels))
						{
							OnPinSelectedOnCanvas?.Invoke(_pinHandler.SelectedIndex);
							OnDataModified?.Invoke();
							QueueRedraw();
							return;
						}

						if (ActiveMode == EditGizmoMode.MunitionSlots && _slotHandler.TryDeleteSlotAt(CurrentModule, localPx, _canvasZoom, rotRad))
						{
							ResetMunitionRack();
							OnSlotSelectedOnCanvas?.Invoke(_slotHandler.SelectedIndex);
							OnDataModified?.Invoke();
							QueueRedraw();
							return;
						}

						if (ActiveMode == EditGizmoMode.FirePoints && _firePointHandler.TryDeleteFirePointAt(CurrentModule, localPx, _canvasZoom))
						{
							OnDataModified?.Invoke();
							QueueRedraw();
							return;
						}

						if (ActiveMode == EditGizmoMode.Exhausts && _exhaustHandler.TryDeleteExhaustAt(CurrentModule, localPx, _canvasZoom))
						{
							OnExhaustSelectedOnCanvas?.Invoke(_exhaustHandler.SelectedIndex);
							OnDataModified?.Invoke();
							QueueRedraw();
							return;
						}

						_isPanning = true;
						_panStartMouse = mb.Position;
						_panStartPos = _canvasPan;
					}
					else _isPanning = false;
				}
				else if (mb.ButtonIndex == MouseButton.Middle)
				{
					_isPanning = mb.Pressed;
					if (mb.Pressed) { _panStartMouse = mb.Position; _panStartPos = _canvasPan; }
				}
				else if (mb.ButtonIndex == MouseButton.WheelUp && mb.Pressed) ZoomAtPoint(mb.Position, 1.15f);
				else if (mb.ButtonIndex == MouseButton.WheelDown && mb.Pressed) ZoomAtPoint(mb.Position, 0.85f);
				else if (mb.ButtonIndex == MouseButton.Left)
				{
					if (_turretHandler.IsTestFiringMode)
					{
						_isTestFireHolding = mb.Pressed;
						if (mb.Pressed) TrySpawnDemoPayload();
					}
					else
					{
						if (mb.Pressed) HandleLeftClickDown(mb.Position, rotRad);
						else
						{
							if (ActiveMode == EditGizmoMode.Pins && _pinHandler.IsDragging)
							{
								if (_pinHandler.OnLeftClickUp(CurrentModule))
								{
									OnPinSelectedOnCanvas?.Invoke(_pinHandler.SelectedIndex);
									OnDataModified?.Invoke();
								}
							}

							_isDraggingGizmo = false;
							_shieldHandler.ReleaseHandle();
							_exhaustHandler.ReleaseHandle();
							_turretHandler.ReleaseHandle();
							_emissiveHandler.ReleaseHandle();
							_slotHandler.ReleaseHandle();
							QueueRedraw();
						}
					}
				}
			}
			else if (@event is InputEventMouseMotion mm)
			{
				if (_isPanning)
				{
					_canvasPan = _panStartPos + (mm.Position - _panStartMouse);
					QueueRedraw();
				}
				else if ((_isDraggingGizmo || _pinHandler.IsDragging || _exhaustHandler.ActiveHandle != ExhaustHandleType.None || _turretHandler.ActiveHandle != TurretHandleType.None || _emissiveHandler.IsDragging || _slotHandler.IsDragging) && CurrentModule != null)
				{
					Vector2 localPx = CanvasToWorldPixel(mm.Position);
					if (ActiveMode == EditGizmoMode.Pins) _pinHandler.HandleDrag(CurrentModule, localPx, GridUnitPixels);
					else if (ActiveMode == EditGizmoMode.Shield) _shieldHandler.HandleDrag(CurrentModule, localPx);
					else if (ActiveMode == EditGizmoMode.FirePoints) _firePointHandler.HandleDrag(CurrentModule, localPx);
					else if (ActiveMode == EditGizmoMode.MunitionSlots) { _slotHandler.HandleDrag(CurrentModule, localPx, rotRad); ResetMunitionRack(); }
					else if (ActiveMode == EditGizmoMode.Exhausts) _exhaustHandler.HandleDrag(CurrentModule, localPx);
					else if (ActiveMode == EditGizmoMode.TurretArc) _turretHandler.HandleDrag(CurrentModule, localPx);
					else if (ActiveMode == EditGizmoMode.Emissive) _emissiveHandler.HandleDrag(CurrentModule, localPx, rotRad);

					OnDataModified?.Invoke();
					QueueRedraw();
				}
				else
				{
					Vector2 localPx = CanvasToWorldPixel(mm.Position);
					_pinHandler.UpdateHover(CurrentModule, localPx, _canvasZoom, GridUnitPixels);
					_shieldHandler.UpdateHover(CurrentModule, localPx, _canvasZoom);
					_firePointHandler.UpdateHover(CurrentModule, localPx, _canvasZoom);
					_slotHandler.UpdateHover(CurrentModule, localPx, _canvasZoom, rotRad);
					_exhaustHandler.UpdateHover(CurrentModule, localPx, _canvasZoom);
					_turretHandler.UpdateHover(CurrentModule, localPx, _canvasZoom);
					_emissiveHandler.UpdateHover(CurrentModule, EmissiveTexture, localPx, _canvasZoom, rotRad);
					QueueRedraw();
				}

				OnMouseMovedInCanvas?.Invoke(CanvasToWorldPixel(mm.Position));
			}
		}

		private float GetCurrentTurretRotationRad()
		{
			if (CurrentModule == null || CurrentModule.MountType != "Turret") return 0.0f;
			return _turretHandler.IsTestFiringMode
				? _turretHandler.CurrentAimAngleRad + Mathf.Pi * 0.5f
				: 0.0f;
		}

		private List<(int slotIdx, Vector2 worldPos, MunitionSlotDefinition slotDef)> GetCurrentWorldMunitionSlots()
		{
			var list = new List<(int, Vector2, MunitionSlotDefinition)>();
			if (CurrentModule == null) return list;

			var wp = CurrentModule.GetProperties<WeaponProperties>();
			if (wp?.MunitionSlots == null) return list;

			Vector2 mountPos = new(CurrentModule.PivotPixelX, CurrentModule.PivotPixelY);
			float aimRad = _turretHandler.CurrentAimAngleRad;

			for (int i = 0; i < wp.MunitionSlots.Length; i++)
			{
				var slot = wp.MunitionSlots[i];
				if (CurrentModule.MountType == "Turret")
				{
					Vector2 localOffset = new(slot.OffsetX, slot.OffsetY);
					float rotatedX = localOffset.X * Mathf.Cos(aimRad + Mathf.Pi * 0.5f) - localOffset.Y * Mathf.Sin(aimRad + Mathf.Pi * 0.5f);
					float rotatedY = localOffset.X * Mathf.Sin(aimRad + Mathf.Pi * 0.5f) + localOffset.Y * Mathf.Cos(aimRad + Mathf.Pi * 0.5f);
					list.Add((i, mountPos + new Vector2(rotatedX, rotatedY), slot));
				}
				else
				{
					list.Add((i, new Vector2(slot.OffsetX, slot.OffsetY), slot));
				}
			}
			return list;
		}

		private List<Vector2> GetCurrentWorldFirePoints()
		{
			var list = new List<Vector2>();
			if (CurrentModule == null) return list;

			Vector2 mountPos = new(CurrentModule.PivotPixelX, CurrentModule.PivotPixelY);
			float aimRad = _turretHandler.CurrentAimAngleRad;

			if (CurrentModule.FirePoints != null && CurrentModule.FirePoints.Length > 0)
			{
				foreach (var fp in CurrentModule.FirePoints)
				{
					Vector2 localOffset = new(fp.PixelOffsetX - CurrentModule.PivotPixelX, fp.PixelOffsetY - CurrentModule.PivotPixelY);
					float rotatedX = localOffset.X * Mathf.Cos(aimRad + Mathf.Pi * 0.5f) - localOffset.Y * Mathf.Sin(aimRad + Mathf.Pi * 0.5f);
					float rotatedY = localOffset.X * Mathf.Sin(aimRad + Mathf.Pi * 0.5f) + localOffset.Y * Mathf.Cos(aimRad + Mathf.Pi * 0.5f);
					list.Add(mountPos + new Vector2(rotatedX, rotatedY));
				}
			}
			else
			{
				list.Add(mountPos);
			}

			return list;
		}

		private void TrySpawnDemoPayload()
		{
			if (CurrentModule == null || _isFullRackReloading) return;

			var wp = CurrentModule.GetProperties<WeaponProperties>() ?? new WeaponProperties();
			if (wp.DeliveryType == "ContinuousBeam") return;

			float aimRad = _turretHandler.CurrentAimAngleRad;
			Vector2 dir = new(Mathf.Cos(aimRad), Mathf.Sin(aimRad));
			Color coreColor = Color.FromHtml(string.IsNullOrEmpty(wp.BulletColorHex) ? "#ffe066" : wp.BulletColorHex);
			Color glowColor = Color.FromHtml(string.IsNullOrEmpty(wp.BulletGlowHex) ? "#ff9900" : wp.BulletGlowHex);
			float rangePx = (wp.Range > 0 ? wp.Range * 8.0f : 240.0f);
			float speedPx = (wp.Speed > 0 ? wp.Speed : 200.0f) * 3.0f;

			if (wp.DeliveryType == "Missile")
			{
				var slots = GetCurrentWorldMunitionSlots();
				if (_runtimeSlots.Count != slots.Count) ResetMunitionRack();

				if (wp.FireMode == MissileFireMode.Salvo)
				{
					bool launchedAny = false;
					for (int i = 0; i < _runtimeSlots.Count; i++)
					{
						if (_runtimeSlots[i].IsLoaded)
						{
							_runtimeSlots[i].IsLoaded = false;
							_runtimeSlots[i].ReloadTimer = wp.ReloadDuration;
							var sp = slots[i];
							SpawnMissileEntity(sp.worldPos, dir, aimRad, rangePx, speedPx, sp.slotDef, wp, coreColor, glowColor);
							launchedAny = true;
						}
					}
					if (launchedAny) CheckPostLaunchReload(wp);
					_fireCooldown = wp.BurstInterval > 0 ? wp.BurstInterval : 0.2f;
				}
				else
				{
					int targetSlot = -1;
					for (int i = 0; i < _runtimeSlots.Count; i++)
					{
						int idx = (_nextFireSlotIndex + i) % _runtimeSlots.Count;
						if (_runtimeSlots[idx].IsLoaded)
						{
							targetSlot = idx;
							_nextFireSlotIndex = (idx + 1) % _runtimeSlots.Count;
							break;
						}
					}

					if (targetSlot >= 0)
					{
						_runtimeSlots[targetSlot].IsLoaded = false;
						_runtimeSlots[targetSlot].ReloadTimer = wp.ReloadDuration;
						var sp = slots[targetSlot];
						SpawnMissileEntity(sp.worldPos, dir, aimRad, rangePx, speedPx, sp.slotDef, wp, coreColor, glowColor);

						_fireCooldown = wp.BurstInterval > 0 ? wp.BurstInterval : 0.2f;
						CheckPostLaunchReload(wp);
					}
				}
				return;
			}

			if (_fireCooldown > 0) return;
			_fireCooldown = 1.0f / Mathf.Max(wp.FireRate, 0.2f);

			Vector2 mountPos = new(CurrentModule.PivotPixelX, CurrentModule.PivotPixelY);

			if (wp.DeliveryType is "PulseBeam" or "Beam")
			{
				float duration = Mathf.Max(wp.BeamDuration, 0.05f);
				_demoBeams.Add(new DemoBeam
				{
					Start = mountPos,
					End = mountPos + dir * rangePx,
					Life = duration,
					MaxLife = duration,
					Width = Mathf.Max(wp.BeamWidth, 2.0f),
					CoreColor = coreColor,
					GlowColor = glowColor
				});
			}
			else
			{
				float radius = wp.ProjectileRadius > 0 ? wp.ProjectileRadius : 3.0f;
				float length = wp.ProjectileLength > 0 ? wp.ProjectileLength : 16.0f;
				float spreadRad = Mathf.DegToRad((float)GD.RandRange(-wp.Spread * 0.5f, wp.Spread * 0.5f));
				Vector2 bulletDir = dir.Rotated(spreadRad);

				_demoBullets.Add(new DemoProjectile
				{
					Pos = mountPos,
					Vel = bulletDir * speedPx,
					Life = 2.5f,
					Radius = radius,
					Length = length,
					CoreColor = coreColor,
					GlowColor = glowColor
				});
			}
		}

		private void SpawnMissileEntity(Vector2 pos, Vector2 dir, float aimRad, float rangePx, float speedPx, MunitionSlotDefinition slot, WeaponProperties wp, Color core, Color glow)
		{
			Texture2D? tex = !string.IsNullOrEmpty(slot.CustomSprite) ? LoadTextureAuto(slot.CustomSprite) : MissileTexture;
			_demoMissiles.Add(new DemoMissile
			{
				Pos = pos,
				Vel = dir * speedPx,
				AngleRad = aimRad + Mathf.Pi * 0.5f + Mathf.DegToRad(slot.AngleOffsetDeg),
				Life = rangePx / speedPx + 0.5f,
				MaxLife = rangePx / speedPx + 0.5f,
				Width = slot.Width > 0 ? slot.Width : 24.0f,
				Length = slot.Length > 0 ? slot.Length : 80.0f,
				Tex = tex,
				CoreColor = core,
				GlowColor = glow
			});
		}

		private void CheckPostLaunchReload(WeaponProperties wp)
		{
			if (wp.ReloadMode == RackReloadMode.FullRack)
			{
				bool allEmpty = true;
				foreach (var slot in _runtimeSlots)
				{
					if (slot.IsLoaded) { allEmpty = false; break; }
				}
				if (allEmpty)
				{
					_isFullRackReloading = true;
					_fullRackTimer = wp.ReloadDuration > 0 ? wp.ReloadDuration : 6.0f;
				}
			}
		}

		private void ZoomAtPoint(Vector2 pivot, float factor)
		{
			float newZoom = Mathf.Clamp(_canvasZoom * factor, 0.25f, 4.0f);
			Vector2 mouseWorld = (pivot - _canvasPan) / _canvasZoom;
			_canvasPan = pivot - mouseWorld * newZoom;
			_canvasZoom = newZoom;
			QueueRedraw();
		}

		private void HandleLeftClickDown(Vector2 screenPos, float rotRad)
		{
			if (CurrentModule == null) return;
			Vector2 localPx = CanvasToWorldPixel(screenPos);
			bool insideExact = IsInsideExactBounds(localPx);
			bool insideExtended = IsInsideExtendedBounds(localPx, 160.0f);

			switch (ActiveMode)
			{
				case EditGizmoMode.Pins:
					if (insideExact && _pinHandler.OnLeftClickDown(CurrentModule, localPx, _canvasZoom, GridUnitPixels, out bool isPinCreated))
					{
						OnPinSelectedOnCanvas?.Invoke(_pinHandler.SelectedIndex);
						if (isPinCreated) OnDataModified?.Invoke();
					}
					break;
				case EditGizmoMode.MunitionSlots:
					_isDraggingGizmo = _slotHandler.OnLeftClickDown(CurrentModule, localPx, _canvasZoom, rotRad, insideExtended, out bool isSlotCreated);
					OnSlotSelectedOnCanvas?.Invoke(_slotHandler.SelectedIndex);
					if (isSlotCreated || _isDraggingGizmo) { ResetMunitionRack(); OnDataModified?.Invoke(); }
					break;
				case EditGizmoMode.Shield:
					_isDraggingGizmo = _shieldHandler.OnLeftClickDown(CurrentModule, localPx, _canvasZoom);
					break;
				case EditGizmoMode.FirePoints:
					_isDraggingGizmo = _firePointHandler.OnLeftClickDown(CurrentModule, localPx, _canvasZoom, insideExtended, out bool isFpCreated);
					if (isFpCreated || _isDraggingGizmo) OnDataModified?.Invoke();
					break;
				case EditGizmoMode.Exhausts:
					_isDraggingGizmo = _exhaustHandler.OnLeftClickDown(CurrentModule, localPx, _canvasZoom, insideExtended, out bool isExhaustCreated);
					OnExhaustSelectedOnCanvas?.Invoke(_exhaustHandler.SelectedIndex);
					if (isExhaustCreated) OnDataModified?.Invoke();
					break;
				case EditGizmoMode.TurretArc:
					_isDraggingGizmo = _turretHandler.OnLeftClickDown(CurrentModule, localPx, _canvasZoom);
					break;
				case EditGizmoMode.Emissive:
					_isDraggingGizmo = _emissiveHandler.OnLeftClickDown(CurrentModule, EmissiveTexture, localPx, _canvasZoom, rotRad);
					if (_isDraggingGizmo) OnDataModified?.Invoke();
					break;
			}
			QueueRedraw();
		}

		private bool IsInsideExactBounds(Vector2 px) =>
			CurrentModule != null &&
			px.X >= 0 && px.X <= CurrentModule.Width * GridUnitPixels &&
			px.Y >= 0 && px.Y <= CurrentModule.Height * GridUnitPixels;

		private bool IsInsideExtendedBounds(Vector2 px, float margin) =>
			CurrentModule != null &&
			px.X >= -margin && px.X <= CurrentModule.Width * GridUnitPixels + margin &&
			px.Y >= -margin && px.Y <= CurrentModule.Height * GridUnitPixels + margin;

		public override void _Draw()
		{
			DrawRect(new Rect2(Vector2.Zero, Size), new Color(0.07f, 0.08f, 0.11f), filled: true);
			if (CurrentModule == null) return;

			int w = CurrentModule.Width;
			int h = CurrentModule.Height;
			Vector2 origin = _canvasPan;
			Vector2 totalPx = new Vector2(w * GridUnitPixels, h * GridUnitPixels) * _canvasZoom;
			float rotRad = GetCurrentTurretRotationRad();
			var wp = CurrentModule.GetProperties<WeaponProperties>();

			// 自动同步鱼雷贴图
			if (wp != null && wp.DefaultMissileSprite != _cachedMissilePath)
			{
				_cachedMissilePath = wp.DefaultMissileSprite ?? string.Empty;
				MissileTexture = LoadTextureAuto(_cachedMissilePath);
			}

			// 1. 底盘网格
			for (int x = 0; x < w; x++)
			{
				for (int y = 0; y < h; y++)
				{
					Vector2 cellScreen = origin + new Vector2(x * GridUnitPixels, y * GridUnitPixels) * _canvasZoom;
					Rect2 cellRect = new(cellScreen, new Vector2(GridUnitPixels, GridUnitPixels) * _canvasZoom);
					DrawRect(cellRect, new Color(0.12f, 0.14f, 0.19f, 0.95f), filled: true);
					DrawRect(cellRect, new Color(0.22f, 0.26f, 0.35f, 0.6f), filled: false, width: 1.0f);
				}
			}

			// 2. 底盘贴图
			if (BaseTexture != null) DrawTextureRect(BaseTexture, new Rect2(origin, totalPx), tile: false);
			DrawRect(new Rect2(origin, totalPx), new Color(1.0f, 0.55f, 0.0f), filled: false, width: 2.0f);

			// 3. 顶盖/炮塔贴图
			if (OverlayTexture != null)
			{
				if (CurrentModule.MountType == "Turret")
				{
					Vector2 mountScreen = origin + new Vector2(CurrentModule.PivotPixelX, CurrentModule.PivotPixelY) * _canvasZoom;
					DrawSetTransform(mountScreen, rotRad, new Vector2(_canvasZoom, _canvasZoom));
					Vector2 texDrawOffset = -new Vector2(CurrentModule.TurretAnchorX, CurrentModule.TurretAnchorY);
					DrawTexture(OverlayTexture, texDrawOffset);
					DrawSetTransform(Vector2.Zero, 0, Vector2.One);
				}
				else
				{
					DrawTextureRect(OverlayTexture, new Rect2(origin, totalPx), tile: false);
				}
			}

			// 4. 发光通道
			if (EmissiveTexture != null)
			{
				Color glowTint = Color.FromHtml(string.IsNullOrEmpty(wp?.BulletGlowHex) ? "#38bdf8" : wp.BulletGlowHex) with { A = 0.95f };
				bool attachToOverlay = CurrentModule.EmissiveAttachTo != "Base";

				if (attachToOverlay && CurrentModule.MountType == "Turret")
				{
					Vector2 mountScreen = origin + new Vector2(CurrentModule.PivotPixelX, CurrentModule.PivotPixelY) * _canvasZoom;
					DrawSetTransform(mountScreen, rotRad, new Vector2(_canvasZoom, _canvasZoom));
					Vector2 emissiveLocalPos = new(CurrentModule.EmissiveOffsetX - CurrentModule.EmissiveAnchorX, CurrentModule.EmissiveOffsetY - CurrentModule.EmissiveAnchorY);
					DrawTexture(EmissiveTexture, emissiveLocalPos, glowTint);
					DrawSetTransform(Vector2.Zero, 0, Vector2.One);
				}
				else
				{
					Vector2 drawPos = origin + new Vector2(CurrentModule.EmissiveOffsetX, CurrentModule.EmissiveOffsetY) * _canvasZoom;
					DrawSetTransform(drawPos, 0, new Vector2(_canvasZoom, _canvasZoom));
					Vector2 texDrawOffset = -new Vector2(CurrentModule.EmissiveAnchorX, CurrentModule.EmissiveAnchorY);
					DrawTexture(EmissiveTexture, texDrawOffset, glowTint);
					DrawSetTransform(Vector2.Zero, 0, Vector2.One);
				}
			}

			// 5. 🚀 鱼雷挂架渲染
			if (wp != null && wp.DeliveryType == "Missile")
			{
				// 只要开启了在架弹体可见，平时和测试时就都显示（测试时打空的位置不显示）
				if (wp.ShowMissileOnRack)
				{
					var rackSlots = GetCurrentWorldMunitionSlots();
					float aimAngle = _turretHandler.CurrentAimAngleRad + Mathf.Pi * 0.5f;

					for (int i = 0; i < rackSlots.Count; i++)
					{
						// 非测试模式下全部显示；测试模式下根据运行时状态（打空与否）决定是否显示
						bool isLoaded = !_turretHandler.IsTestFiringMode || (i < _runtimeSlots.Count && _runtimeSlots[i].IsLoaded);
						if (isLoaded)
						{
							var sp = rackSlots[i];
							Vector2 slotScreen = origin + sp.worldPos * _canvasZoom;
							Texture2D? slotTex = !string.IsNullOrEmpty(sp.slotDef.CustomSprite) ? LoadTextureAuto(sp.slotDef.CustomSprite) : MissileTexture;
							DrawMissileSprite(slotScreen, aimAngle + Mathf.DegToRad(sp.slotDef.AngleOffsetDeg), sp.slotDef.Width * _canvasZoom, sp.slotDef.Length * _canvasZoom, slotTex, wp);
						}
					}
				}

				// 全架装填进度条
				if (_isFullRackReloading)
				{
					Vector2 barPos = origin + new Vector2(CurrentModule.Width * GridUnitPixels * 0.5f, -18) * _canvasZoom;
					float maxTime = wp.ReloadDuration > 0 ? wp.ReloadDuration : 6.0f;
					float pct = 1.0f - (_fullRackTimer / maxTime);
					DrawRect(new Rect2(barPos - new Vector2(30, 4) * _canvasZoom, new Vector2(60, 8) * _canvasZoom), new Color(0.1f, 0.1f, 0.15f, 0.9f), true);
					DrawRect(new Rect2(barPos - new Vector2(30, 4) * _canvasZoom, new Vector2(60 * pct, 8) * _canvasZoom), new Color(0.3f, 0.85f, 1.0f), true);
					DrawRect(new Rect2(barPos - new Vector2(30, 4) * _canvasZoom, new Vector2(60, 8) * _canvasZoom), Colors.White, false, 1.0f);
				}
			}

			// 6. 🚀 巡航鱼雷实体与精准尾焰渲染
			foreach (var m in _demoMissiles)
			{
				// 6.1 历史尾迹烟雾粒子
				for (int i = 0; i < m.Trail.Count; i++)
				{
					float trailAlpha = (1.0f - (float)i / m.Trail.Count) * 0.35f;
					Vector2 tScreen = origin + m.Trail[i] * _canvasZoom;
					DrawCircle(tScreen, (m.Width * 0.35f + i * 0.6f) * _canvasZoom, m.GlowColor with { A = trailAlpha });
				}

				Vector2 mScreen = origin + m.Pos * _canvasZoom;
				Vector2 moveDir = m.Vel.LengthSquared() > 0.01f ? m.Vel.Normalized() : new Vector2(0, -1).Rotated(m.AngleRad);
				Vector2 tailScreen = mScreen - moveDir * (m.Length * 0.5f * _canvasZoom);

				// 6.2 尾部喷口推进火焰 (小火核 + 大羽流)
				DrawCircle(tailScreen, m.Width * 0.45f * _canvasZoom, m.GlowColor with { A = 0.85f });
				DrawCircle(tailScreen, m.Width * 0.25f * _canvasZoom, Colors.White);

				DrawMissileSprite(mScreen, m.AngleRad, m.Width * _canvasZoom, m.Length * _canvasZoom, m.Tex, wp);
			}

			// 7. 实弹与脉冲激光
			foreach (var beam in _demoBeams)
			{
				Vector2 p1 = origin + beam.Start * _canvasZoom;
				Vector2 p2 = origin + beam.End * _canvasZoom;
				float alpha = beam.Life / beam.MaxLife;
				DrawLine(p1, p2, beam.GlowColor with { A = 0.45f * alpha }, beam.Width * 2.2f * _canvasZoom);
				DrawLine(p1, p2, beam.CoreColor with { A = 0.95f * alpha }, beam.Width * 0.8f * _canvasZoom);
			}

			// 7.1 持续光束实时渲染（测试模式按住左键时，根部实时跟随当前炮口）
			if (_turretHandler.IsTestFiringMode && _isTestFireHolding && wp?.DeliveryType == "ContinuousBeam")
			{
				float aimRad = _turretHandler.CurrentAimAngleRad;
				Vector2 dir = new(Mathf.Cos(aimRad), Mathf.Sin(aimRad));
				float rangePx = (wp.Range > 0 ? wp.Range * 8.0f : 240.0f);
				float width = Mathf.Max(wp.BeamWidth, 2.0f);
				Color coreColor = Color.FromHtml(string.IsNullOrEmpty(wp.BulletColorHex) ? "#ffe066" : wp.BulletColorHex);
				Color glowColor = Color.FromHtml(string.IsNullOrEmpty(wp.BulletGlowHex) ? "#ff9900" : wp.BulletGlowHex);

				var currentMuzzles = GetCurrentWorldFirePoints();
				foreach (var sp in currentMuzzles)
				{
					Vector2 p1 = origin + sp * _canvasZoom;
					Vector2 p2 = origin + (sp + dir * rangePx) * _canvasZoom;
					DrawLine(p1, p2, glowColor with { A = 0.5f }, width * 2.4f * _canvasZoom);
					DrawLine(p1, p2, coreColor with { A = 0.95f }, width * 0.9f * _canvasZoom);
					DrawCircle(p1, width * 1.5f * _canvasZoom, glowColor);
					DrawCircle(p1, width * 0.8f * _canvasZoom, Colors.White);
				}
			}

			foreach (var b in _demoBullets)
			{
				Vector2 bScreen = origin + b.Pos * _canvasZoom;
				Vector2 dir = b.Vel.Normalized();
				float halfLen = b.Length * 0.5f * _canvasZoom;
				Vector2 head = bScreen + dir * halfLen;
				Vector2 tail = bScreen - dir * halfLen;
				DrawLine(tail, head, b.GlowColor with { A = 0.5f }, (b.Radius * 2.0f + 2.5f) * _canvasZoom);
				DrawLine(tail, head, b.CoreColor, b.Radius * 2.0f * _canvasZoom);
				DrawCircle(head, b.Radius * _canvasZoom, b.CoreColor);
				DrawCircle(tail, b.Radius * 0.8f * _canvasZoom, b.CoreColor);
			}

			// 8. Gizmos
			_turretHandler.Draw(this, CurrentModule, origin, _canvasZoom, ActiveMode == EditGizmoMode.TurretArc);
			_pinHandler.Draw(this, CurrentModule, origin, _canvasZoom, ActiveMode == EditGizmoMode.Pins, GridUnitPixels);
			_slotHandler.Draw(this, CurrentModule, origin, _canvasZoom, ActiveMode == EditGizmoMode.MunitionSlots, rotRad, MissileTexture);
			_firePointHandler.Draw(this, CurrentModule, origin, _canvasZoom, ActiveMode == EditGizmoMode.FirePoints);
			_exhaustHandler.Draw(this, CurrentModule, origin, _canvasZoom, ActiveMode == EditGizmoMode.Exhausts);
			_shieldHandler.Draw(this, CurrentModule, origin, _canvasZoom, ActiveMode == EditGizmoMode.Shield);
			_emissiveHandler.Draw(this, CurrentModule, EmissiveTexture, origin, _canvasZoom, ActiveMode == EditGizmoMode.Emissive, rotRad);
		}

		private void DrawMissileSprite(Vector2 centerScreen, float angleRad, float w, float h, Texture2D? tex, WeaponProperties? wp)
		{
			DrawSetTransform(centerScreen, angleRad, Vector2.One);

			if (tex != null)
			{
				DrawTextureRect(tex, new Rect2(-new Vector2(w, h) * 0.5f, new Vector2(w, h)), false);
			}
			else
			{
				Color bodyColor = new(0.85f, 0.88f, 0.95f);
				Color tipColor = Color.FromHtml(string.IsNullOrEmpty(wp?.BulletColorHex) ? "#ffe066" : wp!.BulletColorHex);
				Color finColor = new(0.45f, 0.50f, 0.60f);

				DrawRect(new Rect2(-w * 0.4f, -h * 0.35f, w * 0.8f, h * 0.7f), bodyColor, true);
				Vector2[] tipPoints = { new(0, -h * 0.5f), new(-w * 0.4f, -h * 0.35f), new(w * 0.4f, -h * 0.35f) };
				DrawColoredPolygon(tipPoints, tipColor);
				Vector2[] leftFin = { new(-w * 0.4f, h * 0.15f), new(-w * 0.7f, h * 0.42f), new(-w * 0.4f, h * 0.35f) };
				Vector2[] rightFin = { new(w * 0.4f, h * 0.15f), new(w * 0.7f, h * 0.42f), new(w * 0.4f, h * 0.35f) };
				DrawColoredPolygon(leftFin, finColor);
				DrawColoredPolygon(rightFin, finColor);
			}

			DrawSetTransform(Vector2.Zero, 0, Vector2.One);
		}

		public Texture2D? LoadTextureAuto(string? relativePath)
		{
			if (string.IsNullOrWhiteSpace(relativePath)) return null;

			string cleanPath = relativePath.Trim();

			string rootPath = OS.HasFeature("editor") ? ProjectSettings.GlobalizePath("res://") : OS.GetExecutablePath().GetBaseDir();
			string fullPath = Path.Combine(rootPath, "core_data", cleanPath);

			if (File.Exists(fullPath))
			{
				var img = Image.LoadFromFile(fullPath);
				return ImageTexture.CreateFromImage(img);
			}

			string resPath = "res://core_data/" + cleanPath;
			if (ResourceLoader.Exists(resPath)) return GD.Load<Texture2D>(resPath);

			return null;
		}

		public Vector2 CanvasToWorldPixel(Vector2 canvasPos) => (canvasPos - _canvasPan) / _canvasZoom;
	}
}
