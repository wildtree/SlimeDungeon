using SlimeDungeon.Core;
using SlimeDungeon.Domain;

namespace SlimeDungeon.Dungeon;

/// <summary>
/// Generates a 12x12 one-screen dungeon: four open corners, no 2x2 wall blocks, walls under 40% of tiles,
/// fully connected, exactly one staircase (the player's start), 1-10 chests, and a handful of roaming slimes.
/// </summary>
public static class DungeonGenerator
{
    private const int Size = DungeonMap.Size;
    private const double MaxWallFraction = 0.40;

    /// <summary>Share of dungeons that have an element at all; the rest are featureless.</summary>
    public const double ElementDungeonChance = 0.4;

    /// <summary>Mean number of roaming slimes on a floor, i.e. how many you can meet in one visit.</summary>
    public const double AverageSlimesPerDungeon = 6.0;

    /// <summary>60% featureless, 40% split evenly across the four elements.</summary>
    public static Element? RollDungeonElement()
    {
        var rnd = RandomUtil.Shared;
        if (rnd.NextDouble() >= ElementDungeonChance)
            return null;
        var elements = new[] { Element.Fire, Element.Water, Element.Wind, Element.Earth };
        return elements[rnd.Next(elements.Length)];
    }

    public static DungeonMap Generate(Rank dungeonRank, Element? element = null)
    {
        var rnd = RandomUtil.Shared;
        var tiles = new TileType[Size, Size];

        var wallTarget = (int)(Size * Size * (0.15 + rnd.NextDouble() * (MaxWallFraction - 0.15)));
        var placed = 0;
        var attempts = 0;
        const int maxAttempts = 4000;

        while (placed < wallTarget && attempts < maxAttempts)
        {
            attempts++;
            var x = rnd.Next(Size);
            var y = rnd.Next(Size);

            if (tiles[x, y] == TileType.Wall)
                continue;
            if (IsCorner(x, y))
                continue;

            tiles[x, y] = TileType.Wall;

            if (CreatesWallBlock(tiles, x, y) || !IsFullyConnected(tiles))
            {
                tiles[x, y] = TileType.Floor;
                continue;
            }

            placed++;
        }

        var floorTiles = AllFloorTiles(tiles);
        var stairs = floorTiles[rnd.Next(floorTiles.Count)];

        var map = new DungeonMap
        {
            Tiles = tiles,
            StairsPos = stairs,
            DungeonRank = dungeonRank,
            DungeonElement = element,
        };

        PlaceChests(map, floorTiles, rnd);
        PlaceSlimes(map, floorTiles, rnd);

        return map;
    }

    private static bool IsCorner(int x, int y) =>
        (x == 0 || x == Size - 1) && (y == 0 || y == Size - 1);

    private static bool CreatesWallBlock(TileType[,] tiles, int x, int y)
    {
        for (var dx = -1; dx <= 0; dx++)
        {
            for (var dy = -1; dy <= 0; dy++)
            {
                if (AllWallsIn2x2(tiles, x + dx, y + dy))
                    return true;
            }
        }
        return false;
    }

    private static bool AllWallsIn2x2(TileType[,] tiles, int ox, int oy)
    {
        for (var x = ox; x <= ox + 1; x++)
        {
            for (var y = oy; y <= oy + 1; y++)
            {
                if (x < 0 || y < 0 || x >= Size || y >= Size || tiles[x, y] == TileType.Floor)
                    return false;
            }
        }
        return true;
    }

    private static bool IsFullyConnected(TileType[,] tiles)
    {
        var floor = AllFloorTiles(tiles);
        if (floor.Count == 0)
            return false;

        var visited = new bool[Size, Size];
        var stack = new Stack<(int X, int Y)>();
        stack.Push(floor[0]);
        visited[floor[0].X, floor[0].Y] = true;
        var count = 1;

        Span<(int, int)> dirs = stackalloc (int, int)[] { (1, 0), (-1, 0), (0, 1), (0, -1) };
        while (stack.Count > 0)
        {
            var (cx, cy) = stack.Pop();
            foreach (var (dx, dy) in dirs)
            {
                var nx = cx + dx;
                var ny = cy + dy;
                if (nx < 0 || ny < 0 || nx >= Size || ny >= Size)
                    continue;
                if (tiles[nx, ny] == TileType.Wall || visited[nx, ny])
                    continue;
                visited[nx, ny] = true;
                count++;
                stack.Push((nx, ny));
            }
        }

        return count == floor.Count;
    }

    private static List<(int X, int Y)> AllFloorTiles(TileType[,] tiles)
    {
        var list = new List<(int, int)>();
        for (var x = 0; x < Size; x++)
            for (var y = 0; y < Size; y++)
                if (tiles[x, y] == TileType.Floor)
                    list.Add((x, y));
        return list;
    }

    private static void PlaceChests(DungeonMap map, List<(int X, int Y)> floorTiles, Random rnd)
    {
        var count = rnd.Next(1, 11);
        var occupied = new HashSet<(int, int)> { map.StairsPos };
        var candidates = floorTiles.Where(t => t != map.StairsPos).OrderBy(_ => rnd.Next()).Take(count).ToList();

        foreach (var (x, y) in candidates)
        {
            occupied.Add((x, y));
            var isMimic = rnd.NextDouble() < 0.05;
            var chest = new Chest { X = x, Y = y, IsMimic = isMimic };
            if (!isMimic)
                LootTables.FillChest(chest, map.DungeonRank);
            map.Chests.Add(chest);
        }

        GuaranteeChestsWith(map, floorTiles, occupied, rnd, ItemCategory.Herb, 2, ItemFactory.CreateHerb);
        GuaranteeChestsWith(map, floorTiles, occupied, rnd, ItemCategory.Antidote, 1, ItemFactory.CreateAntidoteHerb);
    }

    /// <summary>Guarantees at least <paramref name="minChests"/> distinct non-mimic chests hold an item of
    /// <paramref name="category"/>. Every chest still caps at one item (gold aside), so this claims already-empty
    /// chests first and only spawns a brand-new chest when none are left to claim.</summary>
    private static void GuaranteeChestsWith(DungeonMap map, List<(int X, int Y)> floorTiles, HashSet<(int, int)> occupied, Random rnd, ItemCategory category, int minChests, Func<Rank, Item> create)
    {
        var have = map.Chests.Count(c => !c.IsMimic && c.Items.Any(i => i.Category == category));
        while (have < minChests)
        {
            var target = map.Chests.FirstOrDefault(c => !c.IsMimic && c.Items.Count == 0);
            if (target is null)
            {
                var freeTiles = floorTiles.Where(t => !occupied.Contains(t)).ToList();
                var pos = freeTiles[rnd.Next(freeTiles.Count)];
                occupied.Add(pos);
                target = new Chest { X = pos.X, Y = pos.Y };
                map.Chests.Add(target);
            }

            target.Items.Add(create(RandomUtil.SampleRank(map.DungeonRank, 0.7, 2)));
            have++;
        }
    }

    /// <summary>
    /// How often an SS dungeon has a dragon slime in it. Low enough that meeting one is an event, high enough
    /// that someone who has finished the slime set and gone looking will find one within an evening's delving.
    /// </summary>
    public const double DragonChance = 0.08;

    private static void PlaceSlimes(DungeonMap map, List<(int X, int Y)> floorTiles, Random rnd)
    {
        var count = rnd.Next(4, 9);
        var occupied = new HashSet<(int, int)>(map.Chests.Select(c => (c.X, c.Y))) { map.StairsPos };
        var candidates = floorTiles.Where(t => !occupied.Contains(t)).OrderBy(_ => rnd.Next()).Take(count).ToList();

        foreach (var (x, y) in candidates)
        {
            var (color, gem) = Slime.Roll(map.DungeonElement, map.DungeonRank);
            var slime = Slime.Create(color, map.DungeonRank, map.DungeonElement, gem);
            var moveTimer = (float)(rnd.NextDouble() * 2.4 + 1.2);
            map.Slimes.Add(new RoamingSlime { X = x, Y = y, Slime = slime, MoveTimer = moveTimer });
        }

        PlaceDragon(map, floorTiles, occupied, rnd);
    }

    /// <summary>
    /// The mutant, if this dungeon has one. It is placed well away from the stairs so that walking in never
    /// puts you next to it before you have seen it — the whole design of the encounter is that the player
    /// looks at it across the floor and decides.
    /// </summary>
    private static void PlaceDragon(DungeonMap map, List<(int X, int Y)> floorTiles,
        HashSet<(int, int)> occupied, Random rnd)
    {
        if (map.DungeonRank != Rank.SS || rnd.NextDouble() >= DragonChance)
            return;

        var taken = new HashSet<(int, int)>(occupied);
        foreach (var s in map.Slimes)
            taken.Add((s.X, s.Y));

        var (sx, sy) = map.StairsPos;
        var farthest = floorTiles
            .Where(t => !taken.Contains(t))
            .OrderByDescending(t => Math.Abs(t.X - sx) + Math.Abs(t.Y - sy))
            .Take(6)
            .OrderBy(_ => rnd.Next())
            .ToList();

        if (farthest.Count == 0)
            return;

        var spot = farthest[0];
        map.Slimes.Add(new RoamingSlime
        {
            X = spot.X,
            Y = spot.Y,
            Slime = Slime.CreateDragon(),
            // It does not roam. It waits.
            MoveTimer = float.MaxValue,
        });
    }
}
