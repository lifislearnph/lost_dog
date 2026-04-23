using System;
using Godot;

/// <summary>
/// 房间管理器 - 处理房间加载和切换 - 单例模式
/// 使用方式: 在主场景中添加此脚本，或设为 AutoLoad
/// </summary>
public partial class RoomManager : Node2D
{
	[Export]
	public RoomData initialRoom;
	[Export]
    private Player player;
    [Export]
	private Camera2D _camera;
	[Export]
	private bool _autoSetLimit;

	/// <summary> 内部变量 </summary>
	public static RoomManager Instance { get; private set; }
	public RoomData CurrentRoom { get; private set; }
	private Node2D _currentRoomNode;
	public bool IsTransitioning { get; private set; }

	/// <summary>事件</summary>
	[Signal]//房间开始切换
	public delegate void RoomTransitionStartedEventHandler(Variant newRoom);
	[Signal]//房间切换完成
	public delegate void RoomTransitionCompletedEventHandler(Variant newRoom);

	public override void _Ready()
	{
		// 设置单例
		if (Instance == null){
			Instance = this;
		}
		else{
			QueueFree();
			return;
		}

		if (initialRoom != null){
			LoadRoom(initialRoom);
			SetCameraLimit();
		}
	}
	
    public async void ChangeRoom(RoomData newRoom, Vector2 playerPosition,Vector2 cameraPoint)
	{
		// 防抖检查
		if (IsTransitioning || newRoom == null || newRoom == CurrentRoom)
		{
			return;
		}

		IsTransitioning = true;
		GD.Print($"[LevelManager] 开始切换房间: {CurrentRoom?.RoomId} -> {newRoom.RoomId}");

		EmitSignal(SignalName.RoomTransitionStarted, newRoom);// 发送开始信号

		//锁定人物输入
		if (player != null)	player.SetLockPlayerInput(true);
		
		// 实例化新房间，并切换相机边界
		LoadRoom(newRoom);
		SetCameraLimit();

		// 3. 移动玩家到新位置
		if (player != null)
		{
			player.GlobalPosition = playerPosition;
			GD.Print($"[LevelManager] 玩家移动到: {playerPosition}");
		}

		CurrentRoom = newRoom;

		//解锁人物输入
		if (player != null)	player.SetLockPlayerInput(false);

		IsTransitioning = false;
		EmitSignal(SignalName.RoomTransitionCompleted, newRoom);

		GD.Print($"[LevelManager] 房间切换完成: {newRoom.RoomId}");
	}
	private void LoadRoom(RoomData roomData)
		{
			if (roomData == null) return;
			GD.Print($"[LevelManager] 正在加载房间: {roomData.RoomId}");
			CurrentRoom = roomData;

			if(_currentRoomNode!=null) _currentRoomNode.QueueFree();// 清理旧房间
			_currentRoomNode = roomData.RoomScene.Instantiate<Node2D>();
			_currentRoomNode.Name = $"{roomData.RoomId}";
			_currentRoomNode.GlobalPosition = roomData.RoomPosition; // 设置房间到指定位置
			GD.Print($"[LevelManager] 加载房间: {roomData.RoomId} 到位置: {roomData.RoomPosition}");

			AddChild(_currentRoomNode);
		}

	private void SetCameraLimit()
	{
		Level_bound bound=_currentRoomNode.GetNode<Level_bound>("Level_bound");
		if(_autoSetLimit||bound==null)AutoSetLimit();
		else BoundsSetLimit(bound);
	}

	//根据手动设置的level_bound更新摄像机边界
	private void BoundsSetLimit(Level_bound bounds)
	{
		if (_camera == null) return;
	
		Vector2 offset = CurrentRoom.RoomPosition;

		_camera.LimitLeft = (int)(bounds.Position.X + offset.X);
		_camera.LimitTop = (int)(bounds.Position.Y + offset.Y);
		_camera.LimitRight = (int)(bounds.Position.X + bounds.RectWidth + offset.X);
		_camera.LimitBottom = (int)(bounds.Position.Y + bounds.RectHeight + offset.Y);

		GD.Print($"[LevelManager] 摄像机{_camera}边界已更新为: Left={_camera.LimitLeft}, Top={_camera.LimitTop},LimitRight={_camera.LimitRight},LimitBottom={_camera.LimitBottom}");
	}

	//根据tilemap自动设置摄像机边界
	private void AutoSetLimit()
    {	
		_camera.LimitLeft = 10000000;
		_camera.LimitTop = 10000000;
		_camera.LimitRight = -10000000;
		_camera.LimitBottom = -10000000;

		Vector2 offset = CurrentRoom.RoomPosition;
		var tilemaps = GetTree().GetNodesInGroup("tilemap");
      	foreach (var tilemap in tilemaps)
		{
			if(tilemap is TileMapLayer tm)
			{
				var used = tm.GetUsedRect();
				var tileSize=tm.TileSet.TileSize.X;
				_camera.LimitLeft =(int)offset.X + Math.Min(used.Position.X *tileSize,_camera.LimitLeft);
				_camera.LimitTop =(int)offset.Y + Math.Min(used.Position.Y * tileSize, _camera.LimitTop);
				_camera.LimitRight =(int)offset.X + Math.Max((used.Position.X + used.Size.X) * tileSize, _camera.LimitRight);
				_camera.LimitBottom =(int)offset.Y + Math.Max((used.Position.Y + used.Size.Y) * tileSize, _camera.LimitBottom);
			}
		}

    }
}
