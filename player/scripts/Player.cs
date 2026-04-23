using Godot; 
using System;  
  
public partial class Player : CharacterBody2D // 自带 Velocity + MoveAndSlide
{ 
	// `Attack`：攻击输入触发时发出；具体攻击判定/生成 Hitbox 建议在别的节点或组件中做。 
	[Signal]   
	public delegate void AttackEventHandler();   
    
	// 状态切换信号说明。  
	[Signal]   
	public delegate void StateChangedEventHandler(int from, int to); // 声明状态切换信号类型（两个 int 参数）。  

	/// <summary>当前半心数量或最大半心数量变化时发出。参数为当前半心、最大半心。</summary>
	[Signal]
	public delegate void HealthChangedEventHandler(int currentHalfHearts, int maxHalfHearts);

	/// <summary>血量首次降到 0 时发出（每局一次，直到 <see cref="RefillHealth"/>）。</summary>
	[Signal]
	public delegate void DiedEventHandler();
    
	  
	public enum PlayerState // 定义玩家状态枚举。  
	{ // 枚举体开始。  
		Idle = 0, // 站立/静止。  
		Run = 1, // 跑动/移动。  
		Crouch = 2, // 下蹲。  
		Jump = 3, // 上升阶段。  
		Fall = 4, // 下落阶段。  
		Attack = 5, // 攻击覆盖态（短暂）。  
		Hook = 6, // 钩锁套索：由 HookAbility 驱动移动。  
	}   
    
	[ExportGroup("Movement")] // Inspector 分组：移动参数。  
	// BaseSpeed：刚开始移动时的基础水平速度（未“跑起来”）。 // 参数含义说明。  
	[Export] public float BaseSpeed { get; set; } = 140f; // 导出基础速度，默认 140。  
	// MaxSpeed：跑动到顶后的最大水平速度。 // 参数含义说明。  
	[Export] public float MaxSpeed { get; set; } = 260f; // 导出最大速度，默认 260。  
	// DistanceToMaxSpeed：需要“累计移动距离”达到多少，才算加速到 MaxSpeed（按距离加速，而不是按时间）。 // 参数含义说明。  
	[Export(PropertyHint.Range, "1,2000,1")] public float DistanceToMaxSpeed { get; set; } = 320f; // 导出跑满所需距离，默认 320。  
	// StopFriction：松开方向键后水平速度回到 0 的强度（越大停得越快）。 // 参数含义说明。  
	[Export(PropertyHint.Range, "0.1,50,0.1")] public float StopFriction { get; set; } = 18f; // 导出刹车摩擦，默认 18。  
	// MoveAcceleration：按方向键时，水平速度朝目标速度靠近的速度（越大越跟手）。 // 参数含义说明。  
	[Export(PropertyHint.Range, "0,300,1")] public float MoveAcceleration { get; set; } = 90f; // 导出移动加速度，默认 90。  
  // 空行：分隔移动与下蹲。  
	[ExportGroup("Crouch")] // Inspector 分组：下蹲参数。  
	// 下蹲移动速度倍率。 // 参数含义说明。  
	[Export(PropertyHint.Range, "0.1,1,0.01")] public float CrouchSpeedMultiplier { get; set; } = 0.45f; // 导出下蹲倍率，默认 0.45。  
  // 空行：分隔下蹲与跳跃。  
	[ExportGroup("Gravity & Jump")] // Inspector 分组：重力与跳跃。  
	// Gravity：重力加速度（像素/秒^2）。 // 参数含义说明。  
	[Export] public float Gravity { get; set; } = 1200f; // 导出重力，默认 1200。  
	// JumpVelocity：起跳初速度（像素/秒）。Godot 2D 里向上为负 Y，因此起跳时设为 -JumpVelocity。 // 参数含义说明。  
	[Export] public float JumpVelocity { get; set; } = 360f; // 导出起跳速度，默认 360。  
	// CoyoteTime（土狼时间）：离地后仍允许起跳的宽限时间（秒）。 // 参数含义说明。  
	[Export(PropertyHint.Range, "0,0.3,0.005")] public float CoyoteTime { get; set; } = 0.10f; // 导出土狼时间，默认 0.10s。  
	// JumpBufferTime（跳跃缓冲）：提前按跳的输入记忆时间（秒），用于“落地前按跳也能跳”。 // 参数含义说明。  
	[Export(PropertyHint.Range, "0,0.3,0.005")] public float JumpBufferTime { get; set; } = 0.10f; // 导出跳跃缓冲，默认 0.10s。  
	// JumpCutMultiplier：上升阶段松开跳跃键时，把向上速度乘以该倍率，实现“短按小跳/长按高跳”。 // 参数含义说明。  
	[Export(PropertyHint.Range, "0.1,1.0,0.01")] public float JumpCutMultiplier { get; set; } = 0.45f; // 导出切跳倍率，默认 0.45。  
  // 空行：分隔跳跃与攻击。  
	[ExportGroup("Attack")] // Inspector 分组：攻击参数。  
	[Export(PropertyHint.Range, "0.05,2.0,0.01")] public float AttackCooldown { get; set; } = 0.25f; // 攻击冷却，默认 0.25s。  
	[Export(PropertyHint.Range, "0,0.5,0.01")] public float AttackStateTime { get; set; } = 0.12f; // 攻击状态保持时间，默认 0.12s。  
	/// <summary>近战单次命中伤害（半心单位：1 = ½ 心，2 = 1 心，与敌人扣血逻辑对齐）。</summary>
	[Export(PropertyHint.Range, "1,40,1")] public int MeleeDamageHalfHearts { get; set; } = 2; // 默认 2（一整心）。  
	[Export(PropertyHint.Range, "0,0.2,0.005")] public float HitlagDurationSeconds { get; set; } = 0.05f;
	[Export(PropertyHint.Range, "0,1,0.01")] public float HitlagTimeScale { get; set; } = 0f;
	[Export(PropertyHint.Range, "0,0.6,0.01")] public float DamageShakeDurationSeconds { get; set; } = 0.18f;
	[Export(PropertyHint.Range, "0,20,0.1")] public float DamageShakeStrength { get; set; } = 7f;
  // 空行：分隔攻击与生命。  
	[ExportGroup("Health")] // Inspector：生命 / 心数。  
	/// <summary>最大心数（整颗心）。实际内部以半心为粒度存储。</summary>
	[Export(PropertyHint.Range, "1,20,1")] public int MaxHearts { get; set; } = 3; // 默认 3 颗心。  

	/// <summary>最大半心数量（只读，由 <see cref="MaxHearts"/> 推导）。</summary>
	public int MaxHalfHearts => Mathf.Max(1, MaxHearts * 2);

	/// <summary>当前剩余半心数量。</summary>
	public int CurrentHalfHearts { get; private set; }

	/// <summary>是否已经因血量归零触发过 <see cref="Died"/>（用 <see cref="RefillHealth"/> 可清除）。</summary>
	public bool IsDead { get; private set; }

	/// <summary>受伤一次后，在多少秒内免疫后续伤害（秒，在 Inspector 的 Health 里可调；0 表示关闭无敌帧）。</summary>
	[Export(PropertyHint.Range, "0,5,0.01")] public float HitInvulnerabilitySeconds { get; set; } = 1f;
	[ExportGroup("Abilities")]
	[Export] public CookieData CookieAbilityData { get; set; }
	[Export] public BoomrangData BoomerangAbilityData { get; set; }
	[Export] public HookData HookAbilityData { get; set; }
	[Export] public bool StartWithCookieAbility { get; set; } = false;
	[Export] public bool StartWithBoomerangAbility { get; set; } = false;
	[Export] public bool StartWithHookAbility { get; set; } = false;

	/// <summary>当前是否处于受击无敌时间内（可用于闪烁材质等）。</summary>
	public bool IsHitInvulnerable => _hitInvulnRemaining > 0f;

  // 空行：分隔导出参数与运行时变量。  
	public PlayerState CurrentState => _stateMachine?.CurrentState ?? PlayerState.Idle;
	public PlayerStateMachine StateMachine => _stateMachine;
	public float FacingDirectionX => _facingDirectionX;

	/// <summary>非空时由钩锁能力接管本帧移动（内部使用）。</summary>
	internal IPlayerHookDriver HookDriver { get; set; }
  
	private PlayerStateMachine _stateMachine;
	private AbilityManager _abilityManager;
	private float _hitInvulnRemaining;
	private float _facingDirectionX = 1f;
	private bool _cookieAbilityKeyHeldLastFrame;
	private bool _boomerangAbilityKeyHeldLastFrame;
	private bool _hookAbilityKeyHeldLastFrame;
	private ulong _hitlagEndAtMs;
	private double _prevTimeScaleBeforeHitlag = 1.0;
	private Camera2D _shakeCamera;
	private Vector2 _shakeBaseOffset;
	private ulong _shakeEndAtMs;
	private float _shakeStrength;
	
	//锁定玩家对player操作有效输入
	private bool _lockPlayerInput;
	internal bool IsInputLocked => _lockPlayerInput;
	public void SetLockPlayerInput(bool islock)
	{
		_lockPlayerInput=islock;
	}

	/// <summary>子节点 _Ready 早于父节点，在此先把血量对齐到 Max，避免首帧读到 0。</summary>
	public override void _EnterTree()
	{
		CurrentHalfHearts = MaxHalfHearts;
		IsDead = false;
		_hitInvulnRemaining = 0f;
	}
  
	public override void _Ready() // 节点进入场景树时调用一次。  
	{ // _Ready 方法体开始。  
		UpDirection = Vector2.Up; // 指定上方向，让 IsOnFloor 的判定更稳定。  
		_stateMachine = new PlayerStateMachine(this);
		_stateMachine.Initialize();
		var cookieData = CookieAbilityData ?? new CookieData();
		var boomerangData = BoomerangAbilityData ?? new BoomrangData();
		var hookData = HookAbilityData ?? new HookData();
		_abilityManager = new AbilityManager(this, _stateMachine);
		_abilityManager.RegisterAbility(new CookieAbility
		{
			Data = cookieData,
			IsUnlocked = StartWithCookieAbility,
			CurrentUses = cookieData.MaxUses
		});
		_abilityManager.RegisterAbility(new BoomerangAbility
		{
			Data = boomerangData,
			IsUnlocked = StartWithBoomerangAbility,
			CurrentUses = boomerangData.MaxUses
		});
		_abilityManager.RegisterAbility(new HookAbility
		{
			Data = hookData,
			IsUnlocked = StartWithHookAbility,
			CurrentUses = hookData.MaxUses
		});
		RefillHealth();
	} // _Ready 方法体结束。  

	public override void _Process(double delta)
	{
		UpdateHitlag();
		UpdateCameraShake();
	}

	/// <summary>将当前血量回满并清除死亡标记（例如重生点、读档）。</summary>
	public void RefillHealth()
	{
		IsDead = false;
		_hitInvulnRemaining = 0f;
		CurrentHalfHearts = MaxHalfHearts;
		EmitSignal(SignalName.HealthChanged, CurrentHalfHearts, MaxHalfHearts);
	}

	/// <summary>休息/存档成功时调用：补满血量并按能力配置补充次数。</summary>
	public void OnRestOrSave()
	{
		RefillHealth();
		_abilityManager?.RefillAtRestPoint();
		GD.Print("[Player] Rest/Save refill applied.");
	}

	/// <summary>解锁指定能力；已解锁时返回 true。</summary>
	public bool UnlockAbility(string abilityId)
	{
		if (string.IsNullOrEmpty(abilityId) || _abilityManager == null)
			return false;

		if (_abilityManager.IsUnlocked(abilityId))
			return true;

		_abilityManager.UnlockAbility(abilityId);
		return _abilityManager.IsUnlocked(abilityId);
	}

	/// <summary>受到半颗心（1/2 心）伤害。</summary>
	public void TakeHalfHeartDamage() => ApplyHalfHeartDamage(1);

	/// <summary>受到一整颗心伤害。</summary>
	public void TakeFullHeartDamage() => ApplyHalfHeartDamage(2);

	/// <summary>按半心为单位扣血（1 = 半心，2 = 一心）。</summary>
	public void ApplyHalfHeartDamage(int halfHearts)
	{
		if (halfHearts <= 0 || IsDead)
			return;

		if (_hitInvulnRemaining > 0f)
			return;

		CurrentHalfHearts = Mathf.Max(0, CurrentHalfHearts - halfHearts);
		EmitSignal(SignalName.HealthChanged, CurrentHalfHearts, MaxHalfHearts);
		StartDamageShake(DamageShakeDurationSeconds, DamageShakeStrength);

		if (CurrentHalfHearts <= 0)
		{
			IsDead = true;
			EmitSignal(SignalName.Died);
			return;
		}

		if (HitInvulnerabilitySeconds > 0f)
			_hitInvulnRemaining = HitInvulnerabilitySeconds;
	}
  // 空行：分隔 _Ready 与物理更新。  
	public override void _PhysicsProcess(double delta) // 每个物理帧调用（固定步长），处理移动/碰撞最合适。  
	{ // _PhysicsProcess 方法体开始。  
		var dt = (float)delta; // 把 delta 转为 float 方便 Mathf 和字段类型一致。
		if(!_lockPlayerInput){  
			var lookX = Mathf.Sign(Input.GetAxis("left", "right"));
			if (lookX != 0f)
				_facingDirectionX = lookX;
		}
		if (_hitInvulnRemaining > 0f)
			_hitInvulnRemaining = Mathf.Max(0f, _hitInvulnRemaining - dt);
		_stateMachine.PhysicsTick(dt);
		_abilityManager?.Update(dt);

		if(_lockPlayerInput)
			return;

		var hookBusy = _stateMachine?.CurrentState == PlayerState.Hook;
		var attackBusy = _stateMachine?.IsAttackLocking == true;
		var highPriorityBusy = hookBusy || attackBusy; // 套索/咬(攻击)同优先级，启动后锁定到结束。
		var movementBusy = _stateMachine != null
			&& _stateMachine.CurrentState != PlayerState.Idle
			&& _stateMachine.CurrentState != PlayerState.Attack
			&& _stateMachine.CurrentState != PlayerState.Hook;

		// 套索/咬 > 移动 > 其他：Cookie/飞盘属于“其他”，仅在高优先级空闲且非移动态时触发。
		if (!highPriorityBusy && !movementBusy)
		{
			var cookieKeyHeld = Input.IsPhysicalKeyPressed(Key.R);
			var cookieJustPressed = cookieKeyHeld && !_cookieAbilityKeyHeldLastFrame;
			_cookieAbilityKeyHeldLastFrame = cookieKeyHeld;
			if (cookieJustPressed)
			{
				var used = _abilityManager?.TryUseAbility("cookie") == true;
				GD.Print($"[Player] Cookie (R): used={used}");
			}

			var boomerangKeyHeld = Input.IsPhysicalKeyPressed(Key.K);
			var boomerangJustPressed = boomerangKeyHeld && !_boomerangAbilityKeyHeldLastFrame;
			_boomerangAbilityKeyHeldLastFrame = boomerangKeyHeld;
			if (boomerangJustPressed)
			{
				var used = _abilityManager?.TryUseAbility("boomerang") == true;
				GD.Print($"[Player] Boomerang (K): used={used}");
			}
		}

		var hookKeyHeld = Input.IsPhysicalKeyPressed(Key.F);
		var hookJustPressed = hookKeyHeld && !_hookAbilityKeyHeldLastFrame;
		_hookAbilityKeyHeldLastFrame = hookKeyHeld;
		if (hookJustPressed && !highPriorityBusy)
		{
			var used = _abilityManager?.TryUseAbility("hook") == true;
			GD.Print($"[Player] Hook (F): used={used}");
		}
	} // _PhysicsProcess 方法体结束。  

	internal void EmitAttackSignal() => EmitSignal(SignalName.Attack);
	internal void OnAttackHitConfirmed()
	{
		StartHitlag(HitlagDurationSeconds, HitlagTimeScale);
	}
	internal void EmitStateChangedSignal(PlayerState from, PlayerState to)
	{
		_abilityManager?.NotifyStateChanged(from, to);
		EmitSignal(SignalName.StateChanged, (int)from, (int)to);
	}

	public override void _ExitTree()
	{
		ClearHitlagIfActive();
		ClearShakeOffset();
		_abilityManager?.Dispose();
	}

	private void StartHitlag(float durationSeconds, float timeScale)
	{
		var durationMs = (ulong)Mathf.RoundToInt(Mathf.Max(0f, durationSeconds) * 1000f);
		if (durationMs == 0)
			return;

		if (_hitlagEndAtMs == 0)
			_prevTimeScaleBeforeHitlag = Engine.TimeScale;

		Engine.TimeScale = Mathf.Clamp(timeScale, 0f, 1f);
		var now = Time.GetTicksMsec();
		_hitlagEndAtMs = Math.Max(_hitlagEndAtMs, now + durationMs);
	}

	private void UpdateHitlag()
	{
		if (_hitlagEndAtMs == 0)
			return;

		var now = Time.GetTicksMsec();
		if (now < _hitlagEndAtMs)
			return;

		Engine.TimeScale = _prevTimeScaleBeforeHitlag <= 0f ? 1f : _prevTimeScaleBeforeHitlag;
		_hitlagEndAtMs = 0;
	}

	private void ClearHitlagIfActive()
	{
		if (_hitlagEndAtMs == 0)
			return;

		Engine.TimeScale = _prevTimeScaleBeforeHitlag <= 0f ? 1f : _prevTimeScaleBeforeHitlag;
		_hitlagEndAtMs = 0;
	}

	private void StartDamageShake(float durationSeconds, float strength)
	{
		var camera = GetViewport()?.GetCamera2D();
		if (camera == null)
			return;

		if (_shakeCamera == null || _shakeCamera != camera)
		{
			_shakeCamera = camera;
			_shakeBaseOffset = camera.Offset;
		}

		_shakeStrength = Mathf.Max(_shakeStrength, Mathf.Max(0f, strength));
		var endAt = Time.GetTicksMsec() + (ulong)Mathf.RoundToInt(Mathf.Max(0f, durationSeconds) * 1000f);
		_shakeEndAtMs = Math.Max(_shakeEndAtMs, endAt);
	}

	private void UpdateCameraShake()
	{
		if (_shakeCamera == null || _shakeEndAtMs == 0)
			return;

		var now = Time.GetTicksMsec();
		if (now >= _shakeEndAtMs)
		{
			ClearShakeOffset();
			return;
		}

		var t = (_shakeEndAtMs - now) / 1000f;
		var amp = Mathf.Clamp(_shakeStrength * t * 8f, 0f, _shakeStrength);
		var rand = new Vector2(
			(float)GD.RandRange(-amp, amp),
			(float)GD.RandRange(-amp, amp)
		);
		_shakeCamera.Offset = _shakeBaseOffset + rand;
	}

	private void ClearShakeOffset()
	{
		if (_shakeCamera != null)
			_shakeCamera.Offset = _shakeBaseOffset;
		_shakeEndAtMs = 0;
		_shakeStrength = 0f;
	}
} // 类体结束。  
