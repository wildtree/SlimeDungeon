using SDL3;
using SlimeDungeon.Core;
using SlimeDungeon.Domain;

namespace SlimeDungeon.Graphics;

/// <summary>
/// The battle arena backdrop: a stone chamber with a brick back wall, a floor receding to a vanishing point,
/// and a warm pool of lantern light from the player's side. Built once per element so a battle in a Fire
/// dungeon looks like it is happening in that dungeon, matching how the wall/floor tiles are already tinted.
/// </summary>
public static class CombatArt
{
    public const int Width = 400;
    public const int Height = 400;

    /// <summary>Where the back wall meets the floor. The combat screen stands the slimes on this line.</summary>
    public const int GroundY = 100;

    /// <summary>
    /// Wall-torch anchor points (the top of each sconce cup), shared with the combat screen so its animated
    /// flames land on the baked brackets. Pushed out to the arena edges so they never sit behind a slime:
    /// with a full pack of four, the leftmost sprite still starts well right of x=39.
    /// </summary>
    public static readonly (int X, int Y)[] TorchPositions = [(30, 42), (370, 42)];

    /// <summary>
    /// How far to carry the element tint. <see cref="SpriteFactory.Tint"/> is calibrated for 32px tiles where
    /// a strong shift reads as an accent; applied at full strength across a 400x400 background it turns the
    /// whole screen into a flat wash of one hue, so the tint is only partly applied here.
    /// </summary>
    private const double TintStrength = 0.34;

    public static PixelCanvas BuildBackdrop(Element element)
    {
        var c = new PixelCanvas(Width, Height);

        DrawBackWall(c, element);
        DrawFloor(c, element);
        DrawWallBase(c, element);
        DrawRubble(c, element);
        DrawLanternGlow(c);
        DrawVignette(c);

        // Torches go on last so their warm light sits over the vignette instead of being dimmed by it —
        // they are the arena's own light source, and they're what keeps the tinted stone from reading as a
        // single flat hue.
        foreach (var (tx, ty) in TorchPositions)
        {
            DrawTorchGlow(c, tx, ty - 14);
            DrawSconce(c, tx, ty);
        }

        return c;
    }

    private static SDL.Color Stone(int r, int g, int b, Element element)
    {
        var neutral = Colors.Rgb((byte)Math.Clamp(r, 0, 255), (byte)Math.Clamp(g, 0, 255), (byte)Math.Clamp(b, 0, 255));
        return Lerp(neutral, SpriteFactory.Tint(neutral, element), TintStrength);
    }

    private static void DrawBackWall(PixelCanvas c, Element element)
    {
        var mortar = Stone(22, 20, 26, element);
        c.FillRect(0, 0, Width, GroundY, Stone(56, 52, 62, element));

        const int bw = 42, bh = 19;
        var rnd = new Random(4711);
        for (var row = 0; row * bh < GroundY + bh; row++)
        {
            var y = row * bh;
            var offset = row % 2 == 0 ? 0 : -bw / 2;
            for (var x = offset; x < Width; x += bw)
            {
                var jitter = rnd.Next(-7, 8);
                c.FillRect(x + 1, y + 1, bw - 2, bh - 2, Stone(56 + jitter, 52 + jitter, 62 + jitter, element));
                c.FillRect(x, y, bw, 1, mortar);
                c.FillRect(x, y, 1, bh, mortar);
            }
        }

        // The chamber has no light overhead, so the wall falls off into darkness toward the ceiling.
        for (var y = 0; y < GroundY; y++)
        {
            var t = 1 - y / (double)GroundY;
            c.BlendRect(0, y, Width, 1, Colors.Rgb(4, 4, 7, (byte)Math.Round(Math.Pow(t, 1.4) * 240)));
        }
    }

    /// <summary>
    /// A stone floor in one-point perspective: courses bunch up toward the horizon and the seams between
    /// slabs all converge on a vanishing point just above it. Evenly spaced rows and columns (the obvious
    /// approach) read as a second brick wall lying behind the first rather than as ground.
    /// </summary>
    private static void DrawFloor(PixelCanvas c, Element element)
    {
        var slab = Stone(50, 47, 56, element);
        var slabAlt = Stone(42, 39, 48, element);
        var seam = Stone(17, 16, 21, element);

        c.FillRect(0, GroundY, Width, Height - GroundY, slab);

        const int courses = 9;
        var courseY = new int[courses + 1];
        for (var i = 0; i <= courses; i++)
            courseY[i] = GroundY + (int)Math.Round((Height - GroundY) * Math.Pow(i / (double)courses, 1.95));

        // Seams fade out toward the horizon. At full strength they all pile into the vanishing point as a
        // dense starburst; fading by depth dissolves that and doubles as aerial perspective.
        for (var i = 0; i < courses; i++)
        {
            if (i % 2 == 1)
                c.FillRect(0, courseY[i], Width, courseY[i + 1] - courseY[i], slabAlt);
            c.BlendRect(0, courseY[i], Width, i >= courses - 3 ? 2 : 1, WithAlpha(seam, SeamAlpha(courseY[i])));
        }

        const double vpX = Width / 2.0;
        const double vpY = GroundY - 16.0;
        for (var k = -7; k <= 7; k++)
            DrawConvergingSeam(c, vpX, vpY, vpX + k * 58.0, seam);
    }

    private static byte SeamAlpha(int y)
    {
        var t = Math.Clamp((y - GroundY) / (double)(Height - GroundY), 0, 1);
        return (byte)Math.Round(35 + t * 185);
    }

    private static SDL.Color WithAlpha(SDL.Color c, byte alpha) => Colors.Rgb(c.R, c.G, c.B, alpha);

    /// <summary>Draws one floor seam from the vanishing point out to <paramref name="bottomX"/> at the bottom
    /// edge, clipped to the floor area. Stepped per scanline since these lines are always steep.</summary>
    private static void DrawConvergingSeam(PixelCanvas c, double vpX, double vpY, double bottomX, SDL.Color color)
    {
        for (var y = GroundY; y < Height; y++)
        {
            var t = (y - vpY) / (Height - vpY);
            var x = (int)Math.Round(vpX + (bottomX - vpX) * t);
            var faded = WithAlpha(color, SeamAlpha(y));
            var thickness = y >= Height - 110 ? 2 : 1;
            for (var i = 0; i < thickness; i++)
                c.Blend(x + i, y, faded);
        }
    }

    /// <summary>A lit lip along the top of the floor plus a contact shadow, so the wall reads as standing on
    /// the ground rather than being pasted behind it.</summary>
    private static void DrawWallBase(PixelCanvas c, Element element)
    {
        c.FillRect(0, GroundY, Width, 1, Stone(66, 62, 72, element));
        for (var i = 0; i < 12; i++)
            c.BlendRect(0, GroundY + 2 + i, Width, 1, Colors.Rgb(3, 3, 6, (byte)(125 - i * 10)));
    }

    private static void DrawRubble(PixelCanvas c, Element element)
    {
        var rnd = new Random(2029);
        var stone = Stone(74, 70, 80, element);
        var stoneDark = Stone(28, 26, 33, element);

        for (var i = 0; i < 80; i++)
        {
            var x = rnd.Next(Width);
            // Clustered near the wall base, thinning out toward the viewer.
            var y = GroundY + 6 + (int)(Math.Pow(rnd.NextDouble(), 1.7) * 150);
            var w = rnd.Next(2, 6);
            c.FillRect(x, y, w, Math.Max(1, w - 2), rnd.NextDouble() < 0.45 ? stoneDark : stone);
        }
    }

    /// <summary>Warm light welling up from the player's side of the arena — it separates the slimes from the
    /// wall behind them and gives the flat floor a focal point.</summary>
    private static void DrawLanternGlow(PixelCanvas c)
    {
        const int cx = Width / 2;
        const int cy = 290;
        const double radius = 250;

        for (var y = GroundY; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                var dx = (x - cx) / radius;
                var dy = (y - cy) / (radius * 0.7);
                var dist = Math.Sqrt(dx * dx + dy * dy);
                if (dist >= 1)
                    continue;
                c.Blend(x, y, Colors.Rgb(255, 198, 132, (byte)Math.Round(Math.Pow(1 - dist, 2.0) * 44)));
            }
        }
    }

    private static void DrawSconce(PixelCanvas c, int x, int y)
    {
        var iron = Colors.Rgb(44, 40, 42);
        var ironLight = Colors.Rgb(68, 62, 64);
        var wood = Colors.Rgb(72, 50, 34);

        c.FillRect(x - 2, y, 5, 22, wood);          // handle
        c.FillRect(x - 7, y - 5, 15, 7, iron);      // cup
        c.FillRect(x - 7, y - 5, 15, 2, ironLight);
        c.FillRect(x - 1, y + 22, 3, 7, iron);      // bracket into the wall
        c.FillRect(x - 5, y + 27, 11, 3, ironLight);
    }

    /// <summary>The pool of warm light a torch throws onto the surrounding brickwork. Baked rather than
    /// animated because the renderer's rect fills are opaque; the per-frame flicker is the flame itself.</summary>
    private static void DrawTorchGlow(PixelCanvas c, int cx, int cy)
    {
        const double radius = 96;
        for (var y = (int)(cy - radius); y <= cy + radius; y++)
        {
            for (var x = (int)(cx - radius); x <= cx + radius; x++)
            {
                var dx = x - cx;
                var dy = (y - cy) / 0.85;
                var dist = Math.Sqrt(dx * dx + dy * dy);
                if (dist > radius)
                    continue;
                var falloff = 1 - dist / radius;
                c.Blend(x, y, Colors.Rgb(255, 172, 84, (byte)Math.Round(Math.Pow(falloff, 2.1) * 96)));
            }
        }
    }

    private static void DrawVignette(PixelCanvas c)
    {
        var cx = Width / 2.0;
        var cy = Height / 2.0;
        var maxDist = Math.Sqrt(cx * cx + cy * cy);

        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                var dx = x - cx;
                var dy = y - cy;
                var t = Math.Sqrt(dx * dx + dy * dy) / maxDist;
                if (t < 0.40)
                    continue;
                c.Blend(x, y, Colors.Rgb(0, 0, 3, (byte)Math.Round(Math.Pow((t - 0.40) / 0.60, 1.8) * 170)));
            }
        }
    }

    private static SDL.Color Lerp(SDL.Color a, SDL.Color b, double t)
    {
        t = Math.Clamp(t, 0, 1);
        return Colors.Rgb(
            (byte)Math.Round(a.R + (b.R - a.R) * t),
            (byte)Math.Round(a.G + (b.G - a.G) * t),
            (byte)Math.Round(a.B + (b.B - a.B) * t),
            (byte)Math.Round(a.A + (b.A - a.A) * t));
    }
}
