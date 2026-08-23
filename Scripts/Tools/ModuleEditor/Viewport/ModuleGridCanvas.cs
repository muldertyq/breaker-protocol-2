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
