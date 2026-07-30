using System.Diagnostics.CodeAnalysis;
using SlimeDungeon.Core;

namespace SlimeDungeon.Dungeon;

/// <summary>Runtime state for one dungeon visit: player half-tile position, facing, FOV, and transient UI state.</summary>
public sealed class DungeonSession
{
    public const float SecondsPerHalfStep = 0.11f;
    public const float FullMapRevealSeconds = 10f;

    public required DungeonMap Map { get; init; }
    public FieldOfView Fov { get; }

    public int HalfX { get; set; }
    public int HalfY { get; set; }
    public Direction Facing { get; set; } = Direction.Down;
    public WalkFrame Frame { get; set; } = WalkFrame.A;
    public float MoveCooldown { get; set; }
    public float FullMapRevealTimer { get; set; }
    public string? Message { get; set; }
    public float MessageTimer { get; set; }

    [SetsRequiredMembers]
    public DungeonSession(DungeonMap map)
    {
        Map = map;
        Fov = new FieldOfView(DungeonMap.Size);
        HalfX = map.StairsPos.X * 2;
        HalfY = map.StairsPos.Y * 2;
        Fov.Recompute(map, map.StairsPos.X, map.StairsPos.Y);
    }

    public int TileX => HalfX / 2;
    public int TileY => HalfY / 2;

    public bool IsOnStairs => (TileX, TileY) == Map.StairsPos;

    public void ShowMessage(string text, float seconds = 2.5f)
    {
        Message = text;
        MessageTimer = seconds;
    }
}
