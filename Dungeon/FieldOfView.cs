namespace SlimeDungeon.Dungeon;

/// <summary>Wall-blocked visibility: a tile is visible if within radius and there's an unobstructed
/// line of sight from the player's tile to it. "Seen" tiles stay remembered (dimmed) once explored.</summary>
public sealed class FieldOfView
{
    private const int Radius = 5;
    private readonly bool[,] _visible;
    private readonly bool[,] _everSeen;
    private readonly int _size;

    public FieldOfView(int size)
    {
        _size = size;
        _visible = new bool[size, size];
        _everSeen = new bool[size, size];
    }

    public bool IsVisible(int x, int y) => InBounds(x, y) && _visible[x, y];
    public bool WasSeen(int x, int y) => InBounds(x, y) && _everSeen[x, y];

    private bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < _size && y < _size;

    public void Recompute(DungeonMap map, int px, int py)
    {
        Array.Clear(_visible, 0, _visible.Length);

        for (var x = Math.Max(0, px - Radius); x <= Math.Min(_size - 1, px + Radius); x++)
        {
            for (var y = Math.Max(0, py - Radius); y <= Math.Min(_size - 1, py + Radius); y++)
            {
                var dx = x - px;
                var dy = y - py;
                if (dx * dx + dy * dy > Radius * Radius)
                    continue;

                if (HasLineOfSight(map, px, py, x, y))
                {
                    _visible[x, y] = true;
                    _everSeen[x, y] = true;
                }
            }
        }

        _visible[px, py] = true;
        _everSeen[px, py] = true;
    }

    public static bool HasLineOfSight(DungeonMap map, int x0, int y0, int x1, int y1)
    {
        var dx = Math.Abs(x1 - x0);
        var dy = -Math.Abs(y1 - y0);
        var sx = x0 < x1 ? 1 : -1;
        var sy = y0 < y1 ? 1 : -1;
        var err = dx + dy;
        var x = x0;
        var y = y0;

        while (true)
        {
            if ((x != x0 || y != y0) && (x != x1 || y != y1) && map.IsWall(x, y))
                return false;
            if (x == x1 && y == y1)
                return true;

            var e2 = 2 * err;
            if (e2 >= dy) { err += dy; x += sx; }
            if (e2 <= dx) { err += dx; y += sy; }
        }
    }
}
