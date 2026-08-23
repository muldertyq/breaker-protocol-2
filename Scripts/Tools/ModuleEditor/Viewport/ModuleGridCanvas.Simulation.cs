using System;
using System.Collections.Generic;
using System.Linq;
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
			var hp = CurrentModule?.GetProperties<HangarProperties>();

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

			// 2. 仓盖与鱼雷装填
			if (wp != null && wp.DeliveryType == "Missile")
			{
				UpdateBayStates(dt, wp);
				needRedraw = true;

				if (wp.ReloadMode == RackReloadMode.FullRack)
				{
					if (_isFullRackReloading)
					{
						_fullRackTimer -= dt;
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
							if (slot.ReloadTimer <= 0.0f)
							{
								slot.IsLoaded = true;
							}
						}
					}
				}
			}

			// 3. 机库跑道冷却
			if (hp != null && _runtimeRunways.Count > 0)
			{
				for (int i = 0; i < _runtimeRunways.Count; i++)
				{
					var rState = _runtimeRunways[i];
					if (!rState.IsReady)
					{
						rState.CooldownTimer -= dt;
						if (rState.CooldownTimer <= 0.0f)
						{
							rState.IsReady = true;
						}
					}
				}
			}

			// 4. 持续开火测试
			if (_turretHandler.IsTestFiringMode && _isTestFireHolding && CanTestFireCurrentModule())
			{
				if (wp?.DeliveryType == "ContinuousBeam")
				{
					TrySpawnDemoPayload();
				}
				else if (_fireCooldown <= 0.0f)
				{
					TrySpawnDemoPayload();
				}
			}

			// 5. 🚀 鱼雷实体
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

			// 6. 🛸 无人机弹射滑跑与离舰点火飞行模拟
			if (_demoDrones.Count > 0)
			{
				for (int i = _demoDrones.Count - 1; i >= 0; i--)
				{
					var d = _demoDrones[i];

					if (d.Stage == DroneFlightStage.CatapultTaxi)
					{
						d.TaxiTimer += dt;
						float progress = Mathf.Clamp(d.TaxiTimer / Mathf.Max(d.CatapultDuration, 0.05f), 0.0f, 1.0f);
						d.Pos = d.StartPos.Lerp(d.ExitPos, progress);

						if (progress >= 1.0f)
						{
							// 滑跑结束，正式点火出舱！
							d.Stage = DroneFlightStage.Airborne;
							Vector2 launchDir = (d.ExitPos - d.StartPos).Normalized();
							if (launchDir.LengthSquared() < 0.001f) launchDir = new Vector2(0, -1);
							d.Vel = launchDir * d.ExitSpeed;
							d.AngleRad = Mathf.Atan2(launchDir.Y, launchDir.X) + Mathf.Pi * 0.5f;
						}
					}
					else
					{
						// 自由飞行阶段：在作战空域内自主朝鼠标位置转向追击
						Vector2 targetPos = CanvasToWorldPixel(GetLocalMousePosition());
						Vector2 desiredDir = (targetPos - d.Pos).Normalized();
						
						float currentAngle = Mathf.Atan2(d.Vel.Y, d.Vel.X);
						float targetAngle = Mathf.Atan2(desiredDir.Y, desiredDir.X);
						float smoothAngle = Mathf.LerpAngle(currentAngle, targetAngle, 4.0f * dt); // 平滑转向

						d.Vel = new Vector2(Mathf.Cos(smoothAngle), Mathf.Sin(smoothAngle)) * d.ExitSpeed;
						d.AngleRad = smoothAngle + Mathf.Pi * 0.5f;

						d.Pos += d.Vel * dt;
						d.Life -= dt;

						Vector2 moveDir = d.Vel.Normalized();
						Vector2 tailPos = d.Pos - moveDir * (d.Length * 0.45f);
						d.Trail.Insert(0, tailPos);
						if (d.Trail.Count > 12) d.Trail.RemoveAt(d.Trail.Count - 1);

						if (d.Life <= 0)
						{
							_demoDrones.RemoveAt(i);
						}
					}
				}
				needRedraw = true;
			}

			// 7. 实弹与激光
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

		private List<(Vector2 Position, float AngleOffsetDeg, int SequenceIndex)> GetCurrentWorldFirePoints()
		{
			var list = new List<(Vector2, float, int)>();
			if (CurrentModule == null) return list;

			Vector2 mountPos = new(CurrentModule.PivotPixelX, CurrentModule.PivotPixelY);
			float aimRad = _turretHandler.CurrentAimAngleRad;

			if (CurrentModule.FirePoints != null && CurrentModule.FirePoints.Length > 0)
			{
				foreach (var fp in CurrentModule.FirePoints.OrderBy(point => point.SequenceIndex))
				{
					Vector2 localOffset = new(fp.PixelOffsetX - CurrentModule.PivotPixelX, fp.PixelOffsetY - CurrentModule.PivotPixelY);
					float rotatedX = localOffset.X * Mathf.Cos(aimRad + Mathf.Pi * 0.5f) - localOffset.Y * Mathf.Sin(aimRad + Mathf.Pi * 0.5f);
					float rotatedY = localOffset.X * Mathf.Sin(aimRad + Mathf.Pi * 0.5f) + localOffset.Y * Mathf.Cos(aimRad + Mathf.Pi * 0.5f);
					list.Add((mountPos + new Vector2(rotatedX, rotatedY), fp.AngleOffset, fp.SequenceIndex));
				}
			}
			else
			{
				list.Add((mountPos, 0.0f, 0));
			}

			return list;
		}

		private List<(Vector2 Position, float AngleOffsetDeg, int SequenceIndex)> GetNextWorldFirePointGroup()
		{
			var firePoints = GetCurrentWorldFirePoints();
			if (firePoints.Count <= 1) return firePoints;

			var groups = firePoints
				.GroupBy(point => point.SequenceIndex)
				.OrderBy(group => group.Key)
				.ToList();
			int groupIndex = _nextFirePointGroupIndex % groups.Count;
			_nextFirePointGroupIndex = (groupIndex + 1) % groups.Count;
			return groups[groupIndex].ToList();
		}

		private void TrySpawnDemoPayload()
		{
			if (!CanTestFireCurrentModule() || CurrentModule == null || _isFullRackReloading) return;

			// 🛸 1. 机库无人机多跑道弹射起飞
			if (CurrentModule.MountType == "Hangar" || (CurrentModule.Tags != null && Array.IndexOf(CurrentModule.Tags, "Hangar") >= 0))
			{
				var hp = CurrentModule.GetProperties<HangarProperties>() ?? new HangarProperties();
				if (hp.Runways == null || hp.Runways.Length == 0) return;

				if (_runtimeRunways.Count != hp.Runways.Length) ResetMunitionRack();

				if (hp.LaunchMode == MissileFireMode.Salvo)
				{
					// 全跑道同时弹射起飞
					bool launchedAny = false;
					for (int i = 0; i < _runtimeRunways.Count; i++)
					{
						if (_runtimeRunways[i].IsReady)
						{
							_runtimeRunways[i].IsReady = false;
							_runtimeRunways[i].CooldownTimer = hp.LaunchInterval > 0 ? hp.LaunchInterval : 0.4f;

							var rw = hp.Runways[i];
							Vector2 startPos = new(rw.StartOffsetX, rw.StartOffsetY);
							Vector2 exitPos = new(rw.ExitOffsetX, rw.ExitOffsetY);
							Vector2 dir = (exitPos - startPos).Normalized();
							if (dir.LengthSquared() < 0.001f) dir = new Vector2(0, -1);
							float angleRad = Mathf.Atan2(dir.Y, dir.X) + Mathf.Pi * 0.5f;

							_demoDrones.Add(new DemoDrone
							{
								Pos = startPos,
								StartPos = startPos,
								ExitPos = exitPos,
								AngleRad = angleRad,
								Stage = DroneFlightStage.CatapultTaxi,
								TaxiTimer = 0.0f,
								CatapultDuration = rw.CatapultDuration > 0 ? rw.CatapultDuration : 0.5f,
								ExitSpeed = rw.ExitSpeed > 0 ? rw.ExitSpeed : 320.0f,
								Life = 8.0f,
								Width = hp.DroneWidth > 0 ? hp.DroneWidth : 28.0f,
								Length = hp.DroneLength > 0 ? hp.DroneLength : 36.0f,
								Tex = DroneTexture
							});
							launchedAny = true;
						}
					}
					if (launchedAny) _fireCooldown = hp.LaunchInterval > 0 ? hp.LaunchInterval : 0.4f;
				}
				else
				{
					// 轮射 / 依次快速弹射
					int targetRunway = -1;
					for (int i = 0; i < _runtimeRunways.Count; i++)
					{
						int idx = (_nextRunwayIndex + i) % _runtimeRunways.Count;
						if (_runtimeRunways[idx].IsReady)
						{
							targetRunway = idx;
							_nextRunwayIndex = (idx + 1) % _runtimeRunways.Count;
							break;
						}
					}

					if (targetRunway >= 0)
					{
						var rw = hp.Runways[targetRunway];
						_runtimeRunways[targetRunway].IsReady = false;
						_runtimeRunways[targetRunway].CooldownTimer = hp.LaunchInterval > 0 ? hp.LaunchInterval : 0.4f;

						Vector2 startPos = new(rw.StartOffsetX, rw.StartOffsetY);
						Vector2 exitPos = new(rw.ExitOffsetX, rw.ExitOffsetY);
						Vector2 dir = (exitPos - startPos).Normalized();
						if (dir.LengthSquared() < 0.001f) dir = new Vector2(0, -1);
						float angleRad = Mathf.Atan2(dir.Y, dir.X) + Mathf.Pi * 0.5f;

						_demoDrones.Add(new DemoDrone
						{
							Pos = startPos,
							StartPos = startPos,
							ExitPos = exitPos,
							AngleRad = angleRad,
							Stage = DroneFlightStage.CatapultTaxi,
							TaxiTimer = 0.0f,
							CatapultDuration = rw.CatapultDuration > 0 ? rw.CatapultDuration : 0.5f,
							ExitSpeed = rw.ExitSpeed > 0 ? rw.ExitSpeed : 320.0f,
							Life = 8.0f,
							Width = hp.DroneWidth > 0 ? hp.DroneWidth : 28.0f,
							Length = hp.DroneLength > 0 ? hp.DroneLength : 36.0f,
							Tex = DroneTexture
						});

						_fireCooldown = hp.LaunchInterval > 0 ? hp.LaunchInterval : 0.4f;
					}
				}
				return;
			}

			// 2. 常规武器开火
			var wp = CurrentModule.GetProperties<WeaponProperties>() ?? new WeaponProperties();
			float aimRad = _turretHandler.CurrentAimAngleRad;
			Vector2 aimDir = new(Mathf.Cos(aimRad), Mathf.Sin(aimRad));
			Color coreColor = Color.FromHtml(string.IsNullOrEmpty(wp.BulletColorHex) ? "#ffe066" : wp.BulletColorHex);
			Color glowColor = Color.FromHtml(string.IsNullOrEmpty(wp.BulletGlowHex) ? "#ff9900" : wp.BulletGlowHex);
			float rangePx = (wp.Range > 0 ? wp.Range * 8.0f : 240.0f);
			float speedPx = (wp.Speed > 0 ? wp.Speed : 200.0f) * 3.0f;

			if (wp.DeliveryType == "ContinuousBeam") return;

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
							var sp = slots[i];
							if (!EnsureBayOpen(sp.slotDef.BayId, wp)) continue;

							_runtimeSlots[i].IsLoaded = false;
							_runtimeSlots[i].ReloadTimer = wp.ReloadDuration;
							SpawnMissileEntity(sp.worldPos, aimDir, aimRad, rangePx, speedPx, sp.slotDef, wp, coreColor, glowColor);
							launchedAny = true;
						}
					}
					if (launchedAny) CheckPostLaunchReload(wp);
					_fireCooldown = wp.BurstInterval > 0 ? wp.BurstInterval : 0.2f;
				}
				else // Sequential / Burst
				{
					int targetSlot = -1;
					for (int i = 0; i < _runtimeSlots.Count; i++)
					{
						int idx = (_nextFireSlotIndex + i) % _runtimeSlots.Count;
						if (_runtimeSlots[idx].IsLoaded)
						{
							targetSlot = idx;
							break;
						}
					}

					if (targetSlot >= 0)
					{
						var sp = slots[targetSlot];
						if (!EnsureBayOpen(sp.slotDef.BayId, wp)) return;

						_nextFireSlotIndex = (targetSlot + 1) % _runtimeSlots.Count;
						_runtimeSlots[targetSlot].IsLoaded = false;
						_runtimeSlots[targetSlot].ReloadTimer = wp.ReloadDuration;
						SpawnMissileEntity(sp.worldPos, aimDir, aimRad, rangePx, speedPx, sp.slotDef, wp, coreColor, glowColor);

						_fireCooldown = wp.BurstInterval > 0 ? wp.BurstInterval : 0.2f;
						CheckPostLaunchReload(wp);
					}
				}
				return;
			}

			if (_fireCooldown > 0) return;
			_fireCooldown = 1.0f / Mathf.Max(wp.FireRate, 0.2f);

			var activeFirePoints = GetNextWorldFirePointGroup();
			if (wp.DeliveryType is "PulseBeam" or "Beam")
			{
				float duration = Mathf.Max(wp.BeamDuration, 0.05f);
				foreach (var firePoint in activeFirePoints)
				{
					Vector2 fireDir = aimDir.Rotated(Mathf.DegToRad(firePoint.AngleOffsetDeg));
					_demoBeams.Add(new DemoBeam
					{
						Start = firePoint.Position,
						End = firePoint.Position + fireDir * rangePx,
						Life = duration,
						MaxLife = duration,
						Width = Mathf.Max(wp.BeamWidth, 2.0f),
						CoreColor = coreColor,
						GlowColor = glowColor
					});
				}
			}
			else
			{
				float radius = wp.ProjectileRadius > 0 ? wp.ProjectileRadius : 3.0f;
				float length = wp.ProjectileLength > 0 ? wp.ProjectileLength : 16.0f;
				foreach (var firePoint in activeFirePoints)
				{
					float spreadRad = Mathf.DegToRad((float)GD.RandRange(-wp.Spread * 0.5f, wp.Spread * 0.5f));
					float muzzleOffsetRad = Mathf.DegToRad(firePoint.AngleOffsetDeg);
					Vector2 bulletDir = aimDir.Rotated(muzzleOffsetRad + spreadRad);

					_demoBullets.Add(new DemoProjectile
					{
						Pos = firePoint.Position,
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
}
