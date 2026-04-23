using Godot;

/// <summary>
/// 房间数据资源 - 在编辑器中右键创建新的资源
/// </summary>
[GlobalClass]
public partial class RoomData : Resource
{
	/// <summary>房间唯一ID</summary>
	[Export]
	public string RoomId { get; set; } = "Room_";

	/// <summary>房间场景预制体</summary>
	[Export]
	public PackedScene RoomScene { get; set; }

	/// <summary>房间在世界中的全局位置</summary>
	[Export]
	public Vector2 RoomPosition { get; set; }
}
