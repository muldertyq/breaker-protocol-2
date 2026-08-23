using System;
using System.Collections.Generic;
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
		Bays,
		MunitionSlots,
		Runways,       // 🛫 无人机跑道
		FirePoints,
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

	public enum DroneFlightStage
	{
		CatapultTaxi, // 跑道弹射滑跑
		Airborne      // 点火脱离自由盘旋
	}

	public class DemoDrone
	{
		public Vector2 Pos;
		public Vector2 Vel;
		public float AngleRad;
		public DroneFlightStage Stage = DroneFlightStage.CatapultTaxi;
		public float TaxiTimer = 0.0f;
		public float CatapultDuration = 0.5f;
		public Vector2 StartPos;
		public Vector2 ExitPos;
		public float ExitSpeed = 320.0f;
		public float Life = 8.0f;
		public float Width = 28.0f;
		public float Length = 36.0f;
		public Texture2D? Tex;
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

	public enum BayState
	{
		Closed,
		Opening,
		Open,
		Closing
	}

	public class BayRuntimeState
	{
		public BayState State = BayState.Closed;
		public float OpenProgress = 0.0f;
		public float CloseDelayTimer = 0.0f;
	}

	public class RunwayRuntimeState
	{
		public bool IsReady = true;
		public float CooldownTimer = 0.0f;
	}

	public partial class ModuleGridCanvas : Control
	{
		public const int GridUnitPixels = 80;

		public ModuleDataDefinition? CurrentModule { get; private set; }
		public Texture2D? BaseTexture { get; private set; }
		public Texture2D? OverlayTexture { get; private set; }
		public Texture2D? EmissiveTexture { get; private set; }
		public Texture2D? MissileTexture { get; private set; }
		public Texture2D? DroneTexture { get; private set; }

		private string _cachedMissilePath = string.Empty;
		private string _cachedDronePath = string.Empty;

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
		private readonly MissileBayGizmoHandler _bayHandler = new();
		private readonly MunitionSlotGizmoHandler _slotHandler = new();
		private readonly DroneRunwayGizmoHandler _runwayHandler = new();
		private readonly ExhaustGizmoHandler _exhaustHandler = new();
		private readonly TurretGizmoHandler _turretHandler = new();
		private readonly EmissiveGizmoHandler _emissiveHandler = new();

		private readonly List<DemoProjectile> _demoBullets = new();
		private readonly List<DemoMissile> _demoMissiles = new();
		private readonly List<DemoDrone> _demoDrones = new();
		private readonly List<DemoBeam> _demoBeams = new();
		private float _fireCooldown = 0.0f;

		private readonly List<SlotRuntimeState> _runtimeSlots = new();
		private readonly Dictionary<string, BayRuntimeState> _runtimeBays = new();
		private readonly List<RunwayRuntimeState> _runtimeRunways = new();

		private int _nextFireSlotIndex = 0;
		private int _nextRunwayIndex = 0;
		private bool _isFullRackReloading = false;
		private float _fullRackTimer = 0.0f;

		public event Action? OnDataModified;
		public event Action<int>? OnPinSelectedOnCanvas;
		public event Action<int>? OnBaySelectedOnCanvas;
		public event Action<int>? OnSlotSelectedOnCanvas;
		public event Action<int>? OnRunwaySelectedOnCanvas;
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

			var hp = module.GetProperties<HangarProperties>();
			_cachedDronePath = hp?.DroneSprite ?? string.Empty;
			DroneTexture = LoadTextureAuto(_cachedDronePath);

			_pinHandler.SelectedIndex = -1;
			_exhaustHandler.SelectedIndex = -1;
			_firePointHandler.SelectedIndex = -1;
			_bayHandler.SelectedIndex = -1;
			_slotHandler.SelectedIndex = -1;
			_runwayHandler.SelectedIndex = -1;
			_isTestFireHolding = false;

			ClearDemoEntities();
			CenterView();
			QueueRedraw();
		}

		public void ClearDemoEntities()
		{
			_demoBullets.Clear();
			_demoMissiles.Clear();
			_demoDrones.Clear();
			_demoBeams.Clear();
			ResetMunitionRack();
			QueueRedraw();
		}

		public void ResetMunitionRack()
		{
			_runtimeSlots.Clear();
			_runtimeBays.Clear();
			_runtimeRunways.Clear();

			var wp = CurrentModule?.GetProperties<WeaponProperties>();
			int slotCount = wp?.MunitionSlots?.Length ?? 0;
			for (int i = 0; i < slotCount; i++)
			{
				_runtimeSlots.Add(new SlotRuntimeState { IsLoaded = true, ReloadTimer = 0.0f });
			}

			if (wp?.Bays != null)
			{
				foreach (var bay in wp.Bays)
				{
					_runtimeBays[bay.BayId] = new BayRuntimeState { State = BayState.Closed, OpenProgress = 0.0f };
				}
			}

			var hp = CurrentModule?.GetProperties<HangarProperties>();
			int runwayCount = hp?.Runways?.Length ?? 0;
			for (int i = 0; i < runwayCount; i++)
			{
				_runtimeRunways.Add(new RunwayRuntimeState { IsReady = true, CooldownTimer = 0.0f });
			}

			_nextFireSlotIndex = 0;
			_nextRunwayIndex = 0;
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
		public void SelectBayExternal(int index) { _bayHandler.SelectedIndex = index; QueueRedraw(); }
		public void SelectSlotExternal(int index) { _slotHandler.SelectedIndex = index; QueueRedraw(); }
		public void SelectRunwayExternal(int index) { _runwayHandler.SelectedIndex = index; QueueRedraw(); }
		public void SelectExhaustExternal(int index) { _exhaustHandler.SelectedIndex = index; QueueRedraw(); }

		public void CenterView()
		{
			if (CurrentModule == null) return;
			float totalW = CurrentModule.Width * GridUnitPixels * _canvasZoom;
			float totalH = CurrentModule.Height * GridUnitPixels * _canvasZoom;
			_canvasPan = (Size - new Vector2(totalW, totalH)) * 0.5f;
			QueueRedraw();
		}

		private void ZoomAtPoint(Vector2 pivot, float factor)
		{
			float newZoom = Mathf.Clamp(_canvasZoom * factor, 0.25f, 4.0f);
			Vector2 mouseWorld = (pivot - _canvasPan) / _canvasZoom;
			_canvasPan = pivot - mouseWorld * newZoom;
			_canvasZoom = newZoom;
			QueueRedraw();
		}
	}
}
