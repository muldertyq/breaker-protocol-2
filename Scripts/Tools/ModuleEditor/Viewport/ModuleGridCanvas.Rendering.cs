using System.IO;
using Godot;
using BreakerProtocol.Data.Models.Properties;

namespace BreakerProtocol.Tools.ModuleEditor.Viewport
{
    public partial class ModuleGridCanvas
    {
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

            if (wp != null && wp.DefaultMissileSprite != _cachedMissilePath)
            {
                _cachedMissilePath = wp.DefaultMissileSprite ?? string.Empty;
                MissileTexture = LoadTextureAuto(_cachedMissilePath);
            }

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

            if (BaseTexture != null) DrawTextureRect(BaseTexture, new Rect2(origin, totalPx), tile: false);
            DrawRect(new Rect2(origin, totalPx), new Color(1.0f, 0.55f, 0.0f), filled: false, width: 2.0f);

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

            if (wp != null && wp.DeliveryType == "Missile")
            {
                if (wp.ShowMissileOnRack)
                {
                    var rackSlots = GetCurrentWorldMunitionSlots();
                    float aimAngle = _turretHandler.CurrentAimAngleRad + Mathf.Pi * 0.5f;

                    for (int i = 0; i < rackSlots.Count; i++)
                    {
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

            foreach (var m in _demoMissiles)
            {
                for (int i = 0; i < m.Trail.Count; i++)
                {
                    float trailAlpha = (1.0f - (float)i / m.Trail.Count) * 0.35f;
                    Vector2 tScreen = origin + m.Trail[i] * _canvasZoom;
                    DrawCircle(tScreen, (m.Width * 0.35f + i * 0.6f) * _canvasZoom, m.GlowColor with { A = trailAlpha });
                }

                Vector2 mScreen = origin + m.Pos * _canvasZoom;
                Vector2 moveDir = m.Vel.LengthSquared() > 0.01f ? m.Vel.Normalized() : new Vector2(0, -1).Rotated(m.AngleRad);
                Vector2 tailScreen = mScreen - moveDir * (m.Length * 0.5f * _canvasZoom);

                DrawCircle(tailScreen, m.Width * 0.45f * _canvasZoom, m.GlowColor with { A = 0.85f });
                DrawCircle(tailScreen, m.Width * 0.25f * _canvasZoom, Colors.White);

                DrawMissileSprite(mScreen, m.AngleRad, m.Width * _canvasZoom, m.Length * _canvasZoom, m.Tex, wp);
            }

            foreach (var beam in _demoBeams)
            {
                Vector2 p1 = origin + beam.Start * _canvasZoom;
                Vector2 p2 = origin + beam.End * _canvasZoom;
                float alpha = beam.Life / beam.MaxLife;
                DrawLine(p1, p2, beam.GlowColor with { A = 0.45f * alpha }, beam.Width * 2.2f * _canvasZoom);
                DrawLine(p1, p2, beam.CoreColor with { A = 0.95f * alpha }, beam.Width * 0.8f * _canvasZoom);
            }

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
    }
}
