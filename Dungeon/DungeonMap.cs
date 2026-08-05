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

    /// <summary>
    /// Empties as much of this chest into the player as will fit, and reports what was taken.
    ///
    /// Gold always goes in — it takes no room. Items go in while the bag has slots, and whatever is left stays
    /// in the chest, which then stays *shut*: a chest with something still in it should look like somewhere
    /// worth coming back to, not like an empty box. That is also what makes declining the swap prompt free —
    /// nothing is touched until this is called.
    /// </summary>
    public (int Gold, List<Item> Taken) TakeInto(Player player)
    {
        var gold = Gold;
        if (gold > 0)
            player.EarnGold(gold);
        Gold = 0;

        var taken = new List<Item>();
        while (Items.Count > 0 && player.BagHasRoom)
        {
            var item = Items[0];
            Items.RemoveAt(0);
            player.Bag.Add(item);
            taken.Add(item);
        }

        Opened = Items.Count == 0;
        return (gold, taken);
    }
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

    // How long a slime rests between steps. Held here rather than at the two call sites because the dungeon
    // generator sets the first wait and the screen sets every one after it, and the two drifting apart would
    // mean slimes moved at one pace on arrival and another for the rest of the trip.
    private const float RoamMin = 1.4f;
    private const float RoamRange = 1.12f;

    /// <summary>The first wait, once the floor is generated. Wider than the rest so a room full of slimes does
    /// not all take its first step on the same frame the player walks in.</summary>
    public static float FirstDelay(Random rnd) => (float)(rnd.NextDouble() * 1.68 + 0.84);

    public static float NextDelay(Random rnd) => (float)(rnd.NextDouble() * RoamRange + RoamMin);

    /// <summary>
    /// How long before a step the slime spends shivering. Short: it is a wind-up, not a pause.
    /// </summary>
    public const float ShiverSeconds = 0.4f;

    /// <summary>
    /// 0 while the slime is resting, then climbing to 1 at the instant it steps. Drawing multiplies the
    /// shiver by this, so the tremble builds instead of switching on — a fixed-amplitude wobble that appears
    /// from nothing reads as a glitch rather than as an animal gathering itself.
    /// </summary>
    public float ShiverProgress =>
        MoveTimer <= 0 || MoveTimer > ShiverSeconds ? 0f : (ShiverSeconds - MoveTimer) / ShiverSeconds;
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
