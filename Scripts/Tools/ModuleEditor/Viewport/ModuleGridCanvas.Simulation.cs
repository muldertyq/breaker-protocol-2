using System.Collections.Generic;
using Godot;
using BreakerProtocol.Data.Models.Properties;

namespace BreakerProtocol.Tools.ModuleEditor.Viewport
{
    public partial class ModuleGridCanvas
    {
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
                if (wp?.DeliveryType != "ContinuousBeam" && _fireCooldown <= 0.0f)
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

        private float GetCurrentTurretRotationRad()
        {
            if (CurrentModule == null || CurrentModule.MountType != "Turret") return 0.0f;
            return _turretHandler.IsTestFiringMode
                ? _turretHandler.CurrentAimAngleRad + Mathf.Pi * 0.5f
                : 0.0f;
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

    }
}
