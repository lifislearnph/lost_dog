using Godot;

/// <summary>
/// 房间切换触发器 - 放置在房间边缘的 Area2D
/// </summary>
public partial class RoomTrigger : Area2D
{
	//锁定文件为资源类型
	[Export(PropertyHint.File, "*.tres")]
	public string TargetRoomPath;

	[Export] public Vector2 cameraPoint;
	[Export] public Vector2 PlayerSpawnPosition;//出生点
	

	public override void _Ready()
	{
		BodyEntered += OnBodyEntered;

		// 设置碰撞层 (玩家在第1层)
		CollisionLayer = 0;
		CollisionMask = 1; // 只检测玩家
	}

	private void OnBodyEntered(Node2D body)
	{
		if (body.Name.ToString().Contains("Player", System.StringComparison.OrdinalIgnoreCase))
		{
			// 使用 CallDeferred 延迟执行，避免在物理查询期间修改场景树
			CallDeferred(nameof(TriggerRoomChange));
		}
	}

	private void TriggerRoomChange()
	{
		if (!string.IsNullOrEmpty(TargetRoomPath) && RoomManager.Instance != null)
		{
			// 运行时动态加载房间数据，避免场景循环依赖RoomData数据
			var targetRoom = GD.Load<RoomData>(TargetRoomPath);
			if (targetRoom != null)
			{
				RoomManager.Instance.ChangeRoom(targetRoom, PlayerSpawnPosition,cameraPoint);
			}
		}
	}
}
