using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// 纯代码状态机：不要求在场景里额外挂子节点。
/// Player 作为“上下文”（参数 + Godot 运动 API），具体实现拆到不同 State 脚本里。
/// </summary>
public sealed class PlayerStateMachine
{
	internal readonly struct PlayerInput
	{
		public readonly bool AttackJustPressed;
		public readonly bool WantCrouch;
		public readonly bool CrouchJustPressed;
		public readonly bool JumpJustPressed;
		public readonly bool JumpJustReleased;
		public readonly float InputX;
		public readonly float MoveX;

		public PlayerInput(
			bool attackJustPressed,
			bool wantCrouch,
			bool crouchJustPressed,
			bool jumpJustPressed,
			bool jumpJustReleased,
			float inputX,
			float moveX)
		{
			AttackJustPressed = attackJustPressed;
			WantCrouch = wantCrouch;
			CrouchJustPressed = crouchJustPressed;
			JumpJustPressed = jumpJustPressed;
			JumpJustReleased = jumpJustReleased;
			InputX = inputX;
			MoveX = moveX;
		}
	}

	private readonly Player _player;
	private readonly Dictionary<Player.PlayerState, IPlayerState> _states = new();

	// runtime (原 Player.cs 里那些变量都搬到这里)
	internal Vector2 LastMoveDir = Vector2.Zero;
	internal float MoveDistanceAccum;

	internal float AttackCooldownLeft;
	internal float AttackStateLeft;

	internal float CoyoteLeft;
	internal float JumpBufferLeft;

	private bool _attackKeyHeldLastFrame;

	public Player.PlayerState CurrentState { get; private set; } = Player.PlayerState.Idle;
	public bool IsAttackLocking => AttackStateLeft > 0f;

	public PlayerStateMachine(Player player)
	{
		_player = player;
	}

	public void Initialize()
	{
		_states[Player.PlayerState.Idle] = new PlayerStateIdle();
		_states[Player.PlayerState.Run] = new PlayerStateRun();
		_states[Player.PlayerState.Crouch] = new PlayerStateCrouch();
		_states[Player.PlayerState.Jump] = new PlayerStateJump();
		_states[Player.PlayerState.Fall] = new PlayerStateFall();
		_states[Player.PlayerState.Hook] = new PlayerStateHook();

		ChangeState(Player.PlayerState.Idle);
	}

	public void PhysicsTick(float dt)
	{
		// ---- timers
		AttackCooldownLeft = Mathf.Max(0f, AttackCooldownLeft - dt);
		AttackStateLeft = Mathf.Max(0f, AttackStateLeft - dt);
		JumpBufferLeft = Mathf.Max(0f, JumpBufferLeft - dt);

		if (_player.IsInputLocked)
		{
			// 传入空输入，让 TickLocomotion 仍处理重力 + MoveAndSlide
			var emptyInput = new PlayerInput(
				attackJustPressed: false,
				wantCrouch: false,
				crouchJustPressed: false,
				jumpJustPressed: false,
				jumpJustReleased: false,
				inputX: 0f,
				moveX: 0f
			);
			_states[CurrentState].PhysicsUpdate(this, _player, emptyInput, dt);
			return;
		}
		
		// ---- input snapshot
		var onFloorStart = _player.IsOnFloor();
		CoyoteLeft = onFloorStart ? _player.CoyoteTime : Mathf.Max(0f, CoyoteLeft - dt);

		var inputX = Input.GetAxis("left", "right");
		var moveX = Mathf.Abs(inputX) > 0.001f ? Mathf.Sign(inputX) : 0f;

		var attackKeyHeld = Input.IsPhysicalKeyPressed(Key.J);
		var attackJustPressed = attackKeyHeld && !_attackKeyHeldLastFrame;
		_attackKeyHeldLastFrame = attackKeyHeld;

		var input = new PlayerInput(
			attackJustPressed: attackJustPressed,
			wantCrouch: Input.IsActionPressed("squat"),
			crouchJustPressed: Input.IsActionJustPressed("squat"),
			jumpJustPressed: Input.IsActionJustPressed("jump"),
			jumpJustReleased: Input.IsActionJustReleased("jump"),
			inputX: inputX,
			moveX: moveX
		);

		if (input.JumpJustPressed)
			JumpBufferLeft = _player.JumpBufferTime;

		// attack is global (与当前运动状态无关)；钩锁态禁止攻击。
		if (input.AttackJustPressed && CurrentState != Player.PlayerState.Hook)
			TryStartAttack();

		// ---- state update
		_states[CurrentState].PhysicsUpdate(this, _player, input, dt);
	}

	internal void ChangeState(Player.PlayerState next, float attackStateLeft = 0f)
	{
		if (CurrentState == next)
		{
			return;
		}

		var prev = CurrentState;
		_states[CurrentState].Exit(this, _player);
		CurrentState = next;

		_states[CurrentState].Enter(this, _player);
		_player.EmitStateChangedSignal(prev, next);
	}

	internal bool TryStartAttack()
	{
		if (AttackCooldownLeft > 0f)
			return false;

		AttackCooldownLeft = _player.AttackCooldown;
		AttackStateLeft = _player.AttackStateTime;
		_player.EmitAttackSignal();
		return true;
	}
}

