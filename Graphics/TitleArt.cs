using SDL3;
using SlimeDungeon.Core;

namespace SlimeDungeon.Graphics;

/// <summary>
/// The title screen's two large pieces of art: the "SLIME DUNGEON" wordmark and the dungeon-entrance
/// backdrop behind it. Both are procedural pixel art like everything else in the game — the wordmark is
/// drawn from a hand-authored 7x9 bitmap alphabet, scaled up and then shaded as a single silhouette so the
/// outline, gradient and drips read as one connected blob of slime rather than per-letter decoration.
/// </summary>
public static class TitleArt
{
    public const int LogoWidth = 360;

    /// <summary>Tall enough for the two-tier wordmark plus the longest drip, its outline and its drop shadow.</summary>
    public const int LogoHeight = 156;

    public const int BackdropWidth = 640;
    public const int BackdropHeight = 400;

    private const int GlyphW = 7;
    private const int GlyphH = 9;

    /// <summary>7x9 letterforms for the ten distinct letters in "SLIME DUNGEON".</summary>
    private static readonly Dictionary<char, string[]> Alphabet = new()
    {
        ['S'] = [".#####.", "##...##", "##.....", "##.....", ".#####.", ".....##", ".....##", "##...##", ".#####."],
        ['L'] = ["##.....", "##.....", "##.....", "##.....", "##.....", "##.....", "##.....", "##.....", "#######"],
        ['I'] = ["#######", "..###..", "..###..", "..###..", "..###..", "..###..", "..###..", "..###..", "#######"],
        ['M'] = ["##...##", "###.###", "#######", "##.#.##", "##...##", "##...##", "##...##", "##...##", "##...##"],
        ['E'] = ["#######", "##.....", "##.....", "##.....", "#####..", "##.....", "##.....", "##.....", "#######"],
        ['D'] = ["#####..", "##..##.", "##...##", "##...##", "##...##", "##...##", "##...##", "##..##.", "#####.."],
        ['U'] = ["##...##", "##...##", "##...##", "##...##", "##...##", "##...##", "##...##", "##...##", ".#####."],
        ['N'] = ["##...##", "###..##", "####.##", "##.####", "##..###", "##...##", "##...##", "##...##", "##...##"],
        ['G'] = [".#####.", "##...##", "##.....", "##.....", "##..###", "##...##", "##...##", "##...##", ".#####."],
        ['O'] = [".#####.", "##...##", "##...##", "##...##", "##...##", "##...##", "##...##", "##...##", ".#####."],
    };

    // ---- Wordmark -------------------------------------------------------------

    public static PixelCanvas BuildLogo()
    {
        var c = new PixelCanvas(LogoWidth, LogoHeight);
        var mask = new bool[LogoWidth, LogoHeight];

        // "SLIME" rides above a larger "DUNGEON", the usual two-tier game-logo stack.
        const int topScale = 5;
        const int bottomScale = 6;
        const int letterGap = 4;

        var topWidth = WordWidth("SLIME", topScale, letterGap);
        var bottomWidth = WordWidth("DUNGEON", bottomScale, letterGap);

        const int topY = 4;
        var bottomY = topY + GlyphH * topScale + 8;

        StampWord(mask, "SLIME", (LogoWidth - topWidth) / 2, topY, topScale, letterGap);
        StampWord(mask, "DUNGEON", (LogoWidth - bottomWidth) / 2, bottomY, bottomScale, letterGap);

        var wordTop = topY;
        var wordBottom = bottomY + GlyphH * bottomScale;

        // Drips hang off the bottom edge, folded into the same mask so they get the same outline and
        // gradient as the letters — that is what makes them read as slime instead of stray blobs. Each x is
        // picked to sit under an actual letter stem; hanging one over a gap between stems reads as a
        // detached ornament instead of something running off the letter.
        var bottomLeft = (LogoWidth - bottomWidth) / 2;
        var pitch = GlyphW * bottomScale + letterGap;
        AddDrip(mask, bottomLeft + 14, wordBottom, 15);              // D, left stem
        AddDrip(mask, bottomLeft + pitch * 2 + 35, wordBottom, 23);  // N, right stem
        AddDrip(mask, bottomLeft + pitch * 4 + 20, wordBottom, 12);  // E, base bar
        AddDrip(mask, bottomLeft + pitch * 6 + 35, wordBottom, 19);  // N, right stem

        var shadow = Colors.Rgb(6, 12, 8, 190);
        var outline = Colors.Rgb(14, 34, 18);
        var bright = Colors.Rgb(168, 245, 138);
        var deep = Colors.Rgb(36, 136, 62);
        var specular = Colors.Rgb(232, 255, 214);

        // Drop shadow, then a 2px dilated outline, then the body — painted in that order so each layer
        // simply covers the one before it instead of needing per-pixel priority checks.
        ForEachMaskPixel(mask, (x, y) =>
        {
            for (var dy = 4; dy <= 6; dy++)
                for (var dx = 4; dx <= 6; dx++)
                    if (!InMask(mask, x + dx, y + dy))
                        c.Set(x + dx, y + dy, shadow);
        });

        ForEachMaskPixel(mask, (x, y) =>
        {
            for (var dy = -2; dy <= 2; dy++)
                for (var dx = -2; dx <= 2; dx++)
                    if (!InMask(mask, x + dx, y + dy))
                        c.Set(x + dx, y + dy, outline);
        });

        var span = Math.Max(1, wordBottom - wordTop);
        ForEachMaskPixel(mask, (x, y) =>
        {
            var t = Math.Clamp((y - wordTop) / (double)span, 0, 1);
            c.Set(x, y, Lerp(bright, deep, Math.Pow(t, 0.8)));
        });

        // Top-lit sheen: the first few pixels below each column's crown catch the light, which is what
        // sells the surface as wet.
        for (var x = 0; x < LogoWidth; x++)
        {
            var crown = -1;
            for (var y = 0; y < LogoHeight; y++)
            {
                if (!mask[x, y])
                {
                    crown = -1;
                    continue;
                }
                if (crown < 0)
                    crown = y;
                var depth = y - crown;
                if (depth < 4)
                    c.Set(x, y, Lerp(specular, bright, depth / 4.0));
            }
        }

        return c;
    }

    private static int WordWidth(string word, int scale, int gap) =>
        word.Length * GlyphW * scale + (word.Length - 1) * gap;

    private static void StampWord(bool[,] mask, string word, int x, int y, int scale, int gap)
    {
        foreach (var ch in word)
        {
            var rows = Alphabet[ch];
            for (var gy = 0; gy < GlyphH; gy++)
                for (var gx = 0; gx < GlyphW; gx++)
                    if (rows[gy][gx] == '#')
                        FillMask(mask, x + gx * scale, y + gy * scale, scale, scale);
            x += GlyphW * scale + gap;
        }
    }

    /// <summary>
    /// A tapering runnel ending in a rounded bead, as if the letter above is melting. The runnel stays at
    /// least 6px wide because a 2px-wide neck disappears entirely under the 2px outline applied later.
    /// </summary>
    private static void AddDrip(bool[,] mask, int cx, int topY, int length)
    {
        const int topHalfWidth = 5;
        const int neckHalfWidth = 3;
        for (var i = 0; i < length; i++)
        {
            var t = i / (double)Math.Max(1, length - 1);
            var halfWidth = (int)Math.Round(topHalfWidth - (topHalfWidth - neckHalfWidth) * t);
            FillMask(mask, cx - halfWidth, topY + i, halfWidth * 2, 1);
        }

        var beadY = topY + length + 3;
        const int beadR = 6;
        for (var dy = -beadR; dy <= beadR; dy++)
            for (var dx = -beadR; dx <= beadR; dx++)
                if (dx * dx + dy * dy <= beadR * beadR)
                    FillMask(mask, cx + dx, beadY + dy, 1, 1);
    }

    private static void FillMask(bool[,] mask, int x, int y, int w, int h)
    {
        for (var yy = y; yy < y + h; yy++)
            for (var xx = x; xx < x + w; xx++)
                if (xx >= 0 && yy >= 0 && xx < LogoWidth && yy < LogoHeight)
                    mask[xx, yy] = true;
    }

    private static bool InMask(bool[,] mask, int x, int y) =>
        x >= 0 && y >= 0 && x < LogoWidth && y < LogoHeight && mask[x, y];

    private static void ForEachMaskPixel(bool[,] mask, Action<int, int> action)
    {
        for (var y = 0; y < LogoHeight; y++)
            for (var x = 0; x < LogoWidth; x++)
                if (mask[x, y])
                    action(x, y);
    }

    // ---- Backdrop -------------------------------------------------------------

    public const int ArchCenterX = 320;
    public const int ArchSpringY = 250;
    public const int ArchOpeningRadius = 72;
    public const int ArchFrameRadius = ArchOpeningRadius + 20;
    public const int FloorY = 340;

    /// <summary>Torch anchor points, shared with the title screen so its animated flames land on the
    /// baked sconces.</summary>
    public static readonly (int X, int Y)[] TorchPositions = [(112, 190), (528, 190)];

    public static PixelCanvas BuildBackdrop()
    {
        var c = new PixelCanvas(BackdropWidth, BackdropHeight);

        DrawCaveWall(c);
        DrawArch(c);
        DrawFloor(c);
        foreach (var (tx, ty) in TorchPositions)
        {
            DrawSconce(c, tx, ty);
            DrawTorchGlow(c, tx, ty - 16);
        }
        DrawMoss(c);
        DrawVignette(c);

        return c;
    }

    private static void DrawCaveWall(PixelCanvas c)
    {
        var top = Colors.Rgb(26, 27, 38);
        var bottom = Colors.Rgb(13, 13, 19);
        for (var y = 0; y < BackdropHeight; y++)
            c.FillRect(0, y, BackdropWidth, 1, Lerp(top, bottom, y / (double)BackdropHeight));

        // Running-bond brickwork, kept low-contrast so the wordmark stays the brightest thing on screen.
        const int bw = 44, bh = 22;
        var mortar = Colors.Rgb(9, 9, 14);
        var rnd = new Random(20260726);
        for (var row = 0; row * bh < FloorY + bh; row++)
        {
            var y = row * bh;
            var offset = row % 2 == 0 ? 0 : -bw / 2;
            for (var x = offset; x < BackdropWidth; x += bw)
            {
                var shade = 30 + rnd.Next(-6, 9);
                var face = Colors.Rgb((byte)shade, (byte)(shade + 1), (byte)(shade + 10), 90);
                c.BlendRect(x + 1, y + 1, bw - 2, bh - 2, face);
                c.FillRect(x, y, bw, 1, mortar);
                c.FillRect(x, y, 1, bh, mortar);
            }
        }
    }

    private static void DrawArch(PixelCanvas c)
    {
        var stoneLight = Colors.Rgb(74, 74, 88);
        var stoneMid = Colors.Rgb(58, 58, 70);
        var stoneDark = Colors.Rgb(40, 40, 50);
        const int frameR = ArchFrameRadius;

        // Frame first as a solid arch, then the opening punched out of it.
        c.FillCircle(ArchCenterX, ArchSpringY, frameR, stoneMid);
        c.FillRect(ArchCenterX - frameR, ArchSpringY, frameR * 2, FloorY - ArchSpringY, stoneMid);

        // Voussoir seams radiating out from the springline, plus a wider keystone at the crown.
        for (var deg = 8; deg <= 172; deg += 16)
        {
            var rad = deg * Math.PI / 180.0;
            for (var rr = ArchOpeningRadius; rr <= frameR; rr++)
            {
                var px = (int)Math.Round(ArchCenterX + Math.Cos(rad) * rr);
                var py = (int)Math.Round(ArchSpringY - Math.Sin(rad) * rr);
                c.Set(px, py, stoneDark);
            }
        }
        c.FillRect(ArchCenterX - 9, ArchSpringY - frameR, 18, 20, stoneLight);
        c.FillRect(ArchCenterX - 9, ArchSpringY - frameR, 1, 20, stoneDark);
        c.FillRect(ArchCenterX + 8, ArchSpringY - frameR, 1, 20, stoneDark);

        // Pillar courses below the springline.
        for (var y = ArchSpringY + 4; y < FloorY; y += 20)
        {
            c.FillRect(ArchCenterX - frameR, y, 20, 1, stoneDark);
            c.FillRect(ArchCenterX + ArchOpeningRadius, y, 20, 1, stoneDark);
        }

        // The opening: near-black at the crown easing to a faint blue at floor level, so anything drawn
        // standing in the doorway still reads as a silhouette against depth.
        var voidTop = Colors.Rgb(4, 4, 7);
        var voidBottom = Colors.Rgb(17, 20, 32);
        var openingTop = ArchSpringY - ArchOpeningRadius;
        for (var y = openingTop; y < FloorY; y++)
        {
            var t = (y - openingTop) / (double)(FloorY - openingTop);
            var color = Lerp(voidTop, voidBottom, t * t);
            if (y < ArchSpringY)
            {
                var dy = ArchSpringY - y;
                var halfWidth = (int)Math.Sqrt(Math.Max(0, ArchOpeningRadius * ArchOpeningRadius - dy * dy));
                c.FillRect(ArchCenterX - halfWidth, y, halfWidth * 2, 1, color);
            }
            else
            {
                c.FillRect(ArchCenterX - ArchOpeningRadius, y, ArchOpeningRadius * 2, 1, color);
            }
        }

        // Two shallow steps spilling out of the doorway.
        c.FillRect(ArchCenterX - ArchOpeningRadius - 14, FloorY - 10, (ArchOpeningRadius + 14) * 2, 6, Colors.Rgb(52, 50, 58));
        c.FillRect(ArchCenterX - ArchOpeningRadius - 26, FloorY - 4, (ArchOpeningRadius + 26) * 2, 6, Colors.Rgb(62, 60, 68));
    }

    private static void DrawFloor(PixelCanvas c)
    {
        var slab = Colors.Rgb(44, 42, 52);
        var slabAlt = Colors.Rgb(38, 36, 46);
        var seam = Colors.Rgb(20, 19, 25);

        c.FillRect(0, FloorY, BackdropWidth, BackdropHeight - FloorY, slab);

        // Rows get wider toward the viewer, a cheap stand-in for perspective.
        var y = FloorY;
        var rowHeight = 12;
        var row = 0;
        while (y < BackdropHeight)
        {
            c.FillRect(0, y, BackdropWidth, rowHeight, row % 2 == 0 ? slab : slabAlt);
            c.FillRect(0, y, BackdropWidth, 1, seam);
            var slabWidth = 70 + row * 14;
            for (var x = (row % 2 == 0 ? 0 : slabWidth / 2); x < BackdropWidth; x += slabWidth)
                c.FillRect(x, y, 1, rowHeight, seam);
            y += rowHeight;
            rowHeight += 4;
            row++;
        }
    }

    private static void DrawSconce(PixelCanvas c, int x, int y)
    {
        var iron = Colors.Rgb(46, 42, 44);
        var ironLight = Colors.Rgb(66, 60, 62);
        var wood = Colors.Rgb(70, 48, 32);

        c.FillRect(x - 3, y, 6, 26, wood);          // handle
        c.FillRect(x - 9, y - 6, 18, 8, iron);      // cup
        c.FillRect(x - 9, y - 6, 18, 2, ironLight);
        c.FillRect(x - 2, y + 26, 4, 8, iron);      // bracket into the wall
        c.FillRect(x - 7, y + 32, 14, 4, ironLight);
    }

    /// <summary>A soft warm pool of light on the wall around a torch. Baked rather than animated because the
    /// renderer's rect fills are opaque — the flicker at runtime comes from the flame shape instead.</summary>
    private static void DrawTorchGlow(PixelCanvas c, int cx, int cy)
    {
        const int radius = 74;
        for (var dy = -radius; dy <= radius; dy++)
        {
            for (var dx = -radius; dx <= radius; dx++)
            {
                var dist = Math.Sqrt(dx * dx + dy * dy);
                if (dist > radius)
                    continue;
                var falloff = 1 - dist / radius;
                var alpha = (byte)Math.Round(falloff * falloff * 74);
                c.Blend(cx + dx, cy + dy, Colors.Rgb(255, 168, 74, alpha));
            }
        }
    }

    /// <summary>Darkens the frame edges so the eye is pulled to the lit doorway in the middle.</summary>
    private static void DrawVignette(PixelCanvas c)
    {
        var cx = BackdropWidth / 2.0;
        var cy = BackdropHeight / 2.0;
        var maxDist = Math.Sqrt(cx * cx + cy * cy);

        for (var y = 0; y < BackdropHeight; y++)
        {
            for (var x = 0; x < BackdropWidth; x++)
            {
                var dx = x - cx;
                var dy = y - cy;
                var t = Math.Sqrt(dx * dx + dy * dy) / maxDist;
                if (t < 0.45)
                    continue;
                var alpha = (byte)Math.Round(Math.Pow((t - 0.45) / 0.55, 1.7) * 150);
                c.Blend(x, y, Colors.Rgb(0, 0, 4, alpha));
            }
        }
    }

    private static void DrawMoss(PixelCanvas c)
    {
        var rnd = new Random(913);
        var moss = Colors.Rgb(38, 62, 40, 170);
        var mossLight = Colors.Rgb(54, 84, 52, 150);

        // Clinging along the arch base and creeping over the nearest floor seam.
        for (var i = 0; i < 240; i++)
        {
            var x = rnd.Next(BackdropWidth);
            var near = Math.Abs(x - ArchCenterX);
            if (near > ArchOpeningRadius + 60 && rnd.NextDouble() < 0.7)
                continue;
            var y = FloorY - rnd.Next(0, 16);
            c.BlendRect(x, y, rnd.Next(1, 4), rnd.Next(1, 3), rnd.NextDouble() < 0.4 ? mossLight : moss);
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
