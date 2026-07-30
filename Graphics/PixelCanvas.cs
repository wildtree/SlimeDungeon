using SDL3;

namespace SlimeDungeon.Graphics;

/// <summary>
/// A small procedural-pixel-art canvas: draw into an indexed RGBA buffer, then bake to an SDL texture.
/// All sprites in the game are generated this way instead of loaded from image files.
/// </summary>
public sealed class PixelCanvas
{
    public int Width { get; }
    public int Height { get; }
    private readonly SDL.Color?[] _pixels;

    public PixelCanvas(int width, int height)
    {
        Width = width;
        Height = height;
        _pixels = new SDL.Color?[width * height];
    }

    public void Set(int x, int y, SDL.Color color)
    {
        if (x < 0 || y < 0 || x >= Width || y >= Height)
            return;
        _pixels[y * Width + x] = color;
    }

    public SDL.Color? Get(int x, int y)
    {
        if (x < 0 || y < 0 || x >= Width || y >= Height)
            return null;
        return _pixels[y * Width + x];
    }

    public void FillRect(int x, int y, int w, int h, SDL.Color color)
    {
        for (var yy = y; yy < y + h; yy++)
            for (var xx = x; xx < x + w; xx++)
                Set(xx, yy, color);
    }

    /// <summary>
    /// Composites a translucent color over what is already on the canvas, instead of replacing it the way
    /// <see cref="Set"/> does. Needed for shading passes (grime, moss, glow, vignette) that should tint the
    /// art underneath — writing an alpha color with <see cref="Set"/> would instead punch a see-through hole
    /// in the baked texture.
    /// </summary>
    public void Blend(int x, int y, SDL.Color color)
    {
        if (x < 0 || y < 0 || x >= Width || y >= Height || color.A == 0)
            return;
        var dst = _pixels[y * Width + x];
        if (dst is not { } under || under.A == 0)
        {
            _pixels[y * Width + x] = color;
            return;
        }

        var a = color.A / 255.0;
        _pixels[y * Width + x] = new SDL.Color
        {
            R = (byte)Math.Round(color.R * a + under.R * (1 - a)),
            G = (byte)Math.Round(color.G * a + under.G * (1 - a)),
            B = (byte)Math.Round(color.B * a + under.B * (1 - a)),
            A = Math.Max(under.A, color.A),
        };
    }

    public void BlendRect(int x, int y, int w, int h, SDL.Color color)
    {
        for (var yy = y; yy < y + h; yy++)
            for (var xx = x; xx < x + w; xx++)
                Blend(xx, yy, color);
    }

    public void FillCircle(int cx, int cy, double radius, SDL.Color color)
    {
        var r2 = radius * radius;
        var min = (int)Math.Floor(cy - radius);
        var max = (int)Math.Ceiling(cy + radius);
        for (var yy = min; yy <= max; yy++)
        {
            for (var xx = (int)Math.Floor(cx - radius); xx <= (int)Math.Ceiling(cx + radius); xx++)
            {
                var dx = xx + 0.5 - cx;
                var dy = yy + 0.5 - cy;
                if (dx * dx + dy * dy <= r2)
                    Set(xx, yy, color);
            }
        }
    }

    public void FillEllipse(int cx, int cy, double rx, double ry, SDL.Color color)
    {
        var min = (int)Math.Floor(cy - ry);
        var max = (int)Math.Ceiling(cy + ry);
        for (var yy = min; yy <= max; yy++)
        {
            for (var xx = (int)Math.Floor(cx - rx); xx <= (int)Math.Ceiling(cx + rx); xx++)
            {
                var dx = (xx + 0.5 - cx) / rx;
                var dy = (yy + 0.5 - cy) / ry;
                if (dx * dx + dy * dy <= 1.0)
                    Set(xx, yy, color);
            }
        }
    }

    /// <summary>Mirrors the whole canvas left-to-right. Lets a side-facing sprite be authored once and
    /// flipped for the opposite direction instead of maintaining two hand-tuned copies.</summary>
    public void FlipHorizontal()
    {
        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width / 2; x++)
            {
                var a = y * Width + x;
                var b = y * Width + (Width - 1 - x);
                (_pixels[a], _pixels[b]) = (_pixels[b], _pixels[a]);
            }
        }
    }

    /// <summary>
    /// Traces a 1px border around everything drawn so far. A dark outline is what keeps a small sprite
    /// readable against a dark dungeon floor — without it the figure dissolves into the background.
    /// </summary>
    public void AddOutline(SDL.Color outline)
    {
        var filled = new bool[Width, Height];
        for (var y = 0; y < Height; y++)
            for (var x = 0; x < Width; x++)
                filled[x, y] = _pixels[y * Width + x] is { A: > 0 };

        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                if (filled[x, y])
                    continue;
                var adjacent = false;
                for (var dy = -1; dy <= 1 && !adjacent; dy++)
                    for (var dx = -1; dx <= 1 && !adjacent; dx++)
                    {
                        var nx = x + dx;
                        var ny = y + dy;
                        if (nx >= 0 && ny >= 0 && nx < Width && ny < Height && filled[nx, ny])
                            adjacent = true;
                    }
                if (adjacent)
                    Set(x, y, outline);
            }
        }
    }

    public void MirrorLeftToRight()
    {
        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width / 2; x++)
            {
                var src = Get(x, y);
                if (src is { } color)
                    Set(Width - 1 - x, y, color);
            }
        }
    }

    /// <summary>Bakes the current pixel buffer into a static SDL texture (RGBA8888, nearest-neighbor sampling).</summary>
    public IntPtr ToTexture(IntPtr renderer)
    {
        var texture = SDL.CreateTexture(renderer, SDL.PixelFormat.ABGR8888, SDL.TextureAccess.Static, Width, Height);
        if (texture == IntPtr.Zero)
            throw new InvalidOperationException($"CreateTexture failed: {SDL.GetError()}");

        var bytes = new byte[Width * Height * 4];
        for (var i = 0; i < _pixels.Length; i++)
        {
            var c = _pixels[i] ?? new SDL.Color { R = 0, G = 0, B = 0, A = 0 };
            bytes[i * 4 + 0] = c.R;
            bytes[i * 4 + 1] = c.G;
            bytes[i * 4 + 2] = c.B;
            bytes[i * 4 + 3] = c.A;
        }

        SDL.UpdateTexture(texture, IntPtr.Zero, bytes, Width * 4);
        SDL.SetTextureBlendMode(texture, SDL.BlendMode.Blend);
        SDL.SetTextureScaleMode(texture, SDL.ScaleMode.Nearest);
        return texture;
    }
}
