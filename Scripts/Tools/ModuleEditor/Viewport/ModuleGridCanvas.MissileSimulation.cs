using System;
using System.Collections.Generic;
using Godot;
using BreakerProtocol.Data.Models.Properties;

namespace BreakerProtocol.Tools.ModuleEditor.Viewport
{
	public partial class ModuleGridCanvas
	{
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

		private void UpdateBayStates(float dt, WeaponProperties wp)
		{
			if (wp.Bays == null || wp.Bays.Length == 0) return;

			foreach (var bayDef in wp.Bays)
			{
				if (!_runtimeBays.TryGetValue(bayDef.BayId, out var bayState))
				{
					bayState = new BayRuntimeState();
					_runtimeBays[bayDef.BayId] = bayState;
				}

				float speed = 1.0f / Mathf.Max(bayDef.OpenDuration, 0.05f);

				switch (bayState.State)
				{
					case BayState.Opening:
						bayState.OpenProgress = Mathf.MoveToward(bayState.OpenProgress, 1.0f, speed * dt);
						if (bayState.OpenProgress >= 1.0f) bayState.State = BayState.Open;
						break;

					case BayState.Closing:
						bayState.OpenProgress = Mathf.MoveToward(bayState.OpenProgress, 0.0f, speed * dt);
						if (bayState.OpenProgress <= 0.0f) bayState.State = BayState.Closed;
						break;

					case BayState.Open:
						if (bayState.CloseDelayTimer > 0.0f)
						{
							bayState.CloseDelayTimer -= dt;
							if (bayState.CloseDelayTimer <= 0.0f) bayState.State = BayState.Closing;
						}
						break;
				}
			}
		}

		private bool EnsureBayOpen(string bayId, WeaponProperties wp)
		{
			if (string.IsNullOrEmpty(bayId) || wp.Bays == null || wp.Bays.Length == 0) return true;

			if (_runtimeBays.TryGetValue(bayId, out var bayState))
			{
				if (bayState.State is BayState.Closed or BayState.Closing)
				{
					bayState.State = BayState.Opening;
				}
				bayState.CloseDelayTimer = 0.8f; // 开火时重置关盖延时
				return bayState.OpenProgress >= 0.95f; // 仓盖开启超 95% 允许出舱
			}
			return true;
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
	}
}
