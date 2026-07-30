using SlimeDungeon.Core;
using SlimeDungeon.Domain;

namespace SlimeDungeon.Dungeon;

public enum TileType { Floor, Wall }

public sealed class Chest
{
    public required int X { get; init; }
    public required int Y { get; init; }
    public bool Opened { get; set; }
    public bool IsMimic { get; init; }
    public List<Item> Items { get; init; } = new();
    public int Gold { get; set; }
}

public sealed class RoamingSlime
{
    public required int X { get; set; }
    public required int Y { get; set; }
    public required Slime Slime { get; init; }

    /// <summary>Seconds until this slime's next move attempt; randomized so slimes don't step in lockstep.</summary>
    public float MoveTimer { get; set; }

    /// <summary>True briefly after a move, so the map icon shows the "hop" sprite frame instead of idle.</summary>
    public bool HopFrame { get; set; }
    public float HopFrameTimer { get; set; }
}

public sealed class DungeonMap
{
    public const int Size = 12;

    public required TileType[,] Tiles { get; init; }
    public required (int X, int Y) StairsPos { get; init; }
    public required Rank DungeonRank { get; init; }
    public Element? DungeonElement { get; init; }
    public List<Chest> Chests { get; init; } = new();
    public List<RoamingSlime> Slimes { get; init; } = new();

    public bool IsWall(int x, int y) => x < 0 || y < 0 || x >= Size || y >= Size || Tiles[x, y] == TileType.Wall;

    public Chest? ChestAt(int x, int y) => Chests.FirstOrDefault(c => c.X == x && c.Y == y);

    public RoamingSlime? SlimeAt(int x, int y) => Slimes.FirstOrDefault(s => s.X == x && s.Y == y);

    public bool IsBlocked(int x, int y)
    {
        if (IsWall(x, y))
            return true;
        var chest = ChestAt(x, y);
        return chest is { Opened: false };
    }
}
