using Godot;
using BreakerProtocol.Tools.ModuleEditor.Viewport.Gizmos;

namespace BreakerProtocol.Tools.ModuleEditor.Viewport
{
	public partial class ModuleGridCanvas
	{
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

		public Vector2 CanvasToWorldPixel(Vector2 canvasPos) => (canvasPos - _canvasPan) / _canvasZoom;
	}
}
