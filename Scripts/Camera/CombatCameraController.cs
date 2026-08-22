using Godot;
using BreakerProtocol.Core;
using BreakerProtocol.Utils;

namespace BreakerProtocol.Camera
{
	public enum CameraZoomTier
	{
		WiringMode,   // 1.5x: Tab 键走线微观视图
		Standard,     // 1.0x: 常规交火视口
		Tactical,     // 0.7x: 高速战术拉远
		CapitalBoss   // 0.5x: 巨舰决战
	}

	/// <summary>
	/// 物理同步版：动态战斗摄像机控制器 (消除物理抖动与重影，完美平滑追踪)
	/// </summary>
	[GlobalClass]
	public partial class CombatCameraController : Camera2D
	{
		[ExportGroup("追踪目标与权重 (Lookahead)")]
		[Export] public Node2D? TargetShip { get; set; }

		[Export] public float VelocityLookaheadTime { get; set; } = 0.40f; // 航速前瞻时间 (秒)
		[Export] public float CursorLookaheadWeight { get; set; } = 0.20f; // 准星前瞻权重

		[ExportGroup("平滑阻尼时间 (Damping)")]
		[Export] public float PositionSmoothTime { get; set; } = 0.08f;    // 摄像机平滑延迟
		[Export] public float ZoomSmoothTime { get; set; } = 0.18f;        // 缩放平滑过渡

		[ExportGroup("自适应缩放阈值 (px/s)")]
		[Export] public float SpeedZoomOutThreshold { get; set; } = 300.0f; // 超 37.5m/s 开始拉远
		[Export] public float SpeedZoomInThreshold { get; set; } = 180.0f;  // 低于 22.5m/s 恢复

		private CameraZoomTier _currentZoomTier = CameraZoomTier.Standard;
		private Vector2 _currentVelocity = Vector2.Zero;
		private float _currentZoomVelocity = 0.0f;

		private Label? _debugLabel;

		public override void _Ready()
		{
			// 确保摄像机属性正确
			Enabled = true;
			PositionSmoothingEnabled = false; // 由 MathUtils 手动精确解算
			MakeCurrent();

			FindAndBindTarget();
			CreateDebugHUD();
		}

		// 关键改动：将逻辑全部放在 _PhysicsProcess 中执行，与 RigidBody2D 物理步长完全对齐！
		public override void _PhysicsProcess(double delta)
		{
			if (TargetShip == null || !IsInstanceValid(TargetShip))
			{
				FindAndBindTarget();
				if (TargetShip == null) return;
			}

			float dt = (float)delta;

			// 1. 读取飞船物理线速度
			Vector2 shipPos = TargetShip.GlobalPosition;
			Vector2 shipVelocity = Vector2.Zero;
			if (TargetShip is RigidBody2D rb)
			{
				shipVelocity = rb.LinearVelocity;
			}

			// 2. 状态机裁决 (Tab 优先，其次速度)
			bool isTabPressed = Input.IsKeyPressed(Key.Tab);
			float currentSpeed = shipVelocity.Length();

			if (isTabPressed)
			{
				_currentZoomTier = CameraZoomTier.WiringMode;
			}
			else
			{
				if (_currentZoomTier == CameraZoomTier.Standard && currentSpeed > SpeedZoomOutThreshold)
				{
					_currentZoomTier = CameraZoomTier.Tactical;
				}
				else if (_currentZoomTier == CameraZoomTier.Tactical && currentSpeed < SpeedZoomInThreshold)
				{
					_currentZoomTier = CameraZoomTier.Standard;
				}
				else if (_currentZoomTier == CameraZoomTier.WiringMode)
				{
					_currentZoomTier = CameraZoomTier.Standard;
				}
			}

			// 3. 计算前瞻中心目标点
			Vector2 mouseWorldPos = GetGlobalMousePosition();
			Vector2 velocityOffset = shipVelocity * VelocityLookaheadTime;
			Vector2 cursorOffset = (mouseWorldPos - shipPos) * CursorLookaheadWeight;

			Vector2 targetPos = shipPos + velocityOffset + cursorOffset;

			// 4. 临界阻尼平滑计算位置与缩放
			GlobalPosition = MathUtils.SmoothDampVec2(
				GlobalPosition,
				targetPos,
				ref _currentVelocity,
				PositionSmoothTime,
				dt
			);

			float targetZoomVal = GetTargetZoomScale(_currentZoomTier);
			float newZoom = MathUtils.SmoothDamp(
				Zoom.X,
				targetZoomVal,
				ref _currentZoomVelocity,
				ZoomSmoothTime,
				dt
			);
			Zoom = new Vector2(newZoom, newZoom);

			// 5. 刷新调试面板
			UpdateDebugHUD(currentSpeed, velocityOffset.Length(), cursorOffset.Length());
		}

		private void FindAndBindTarget()
		{
			if (TargetShip != null && IsInstanceValid(TargetShip)) return;

			var playerNode = GetTree().GetFirstNodeInGroup("Player") as Node2D;
			if (playerNode != null)
			{
				TargetShip = playerNode;
				GD.PrintRich("[color=green][CombatCamera] 成功绑定 Player 节点！[/color]");
			}
		}

		private float GetTargetZoomScale(CameraZoomTier tier)
		{
			return tier switch
			{
				CameraZoomTier.WiringMode => 1.5f,
				CameraZoomTier.Standard => 1.0f,
				CameraZoomTier.Tactical => 0.7f,
				CameraZoomTier.CapitalBoss => 0.5f,
				_ => 1.0f
			};
		}

		private void CreateDebugHUD()
		{
			var canvasLayer = new CanvasLayer();
			AddChild(canvasLayer);

			_debugLabel = new Label { Position = new Vector2(20, 20) };
			_debugLabel.AddThemeColorOverride("font_color", new Color(0.2f, 1.0f, 0.4f, 1.0f));
			_debugLabel.AddThemeFontSizeOverride("font_size", 16);
			canvasLayer.AddChild(_debugLabel);
		}

		private void UpdateDebugHUD(float speedPx, float velOffset, float curOffset)
		{
			if (_debugLabel == null) return;
			float speedMeters = GlobalMetrics.PixelsToMeters(speedPx);
			_debugLabel.Text = $"【《断路协议》TASK-01 摄像机战术遥测】\n" +
							   $"----------------------------------------\n" +
							   $"跟随目标: {(TargetShip != null ? TargetShip.Name : "正在搜寻...")}\n" +
							   $"当前航速: {speedMeters:F1} m/s ({speedPx:F0} px/s)\n" +
							   $"视口缩放: {Zoom.X:F2}x (当前模式: {_currentZoomTier})\n" +
							   $"前瞻偏移: 速度[{velOffset:F0}px] | 准星[{curOffset:F0}px]\n" +
							   $"----------------------------------------\n" +
							   $"[操作] WASD:移动 | Shift:加力冲刺 | Tab:1.5x走线 | Space:漂移";
		}
	}
}
