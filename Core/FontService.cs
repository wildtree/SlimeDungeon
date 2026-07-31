using SDL3;

namespace SlimeDungeon.Core;

/// <summary>Loads a CJK-capable system font and renders/caches text as textures.</summary>
public sealed class FontService : IDisposable
{
    /// <summary>
    /// Well-known locations of a Japanese-capable font, tried in order of preference. Every string in this UI
    /// is Japanese, so a Latin-only fallback would render the whole game as blanks — the ASCII fallbacks at the
    /// end of each list exist only so the window still comes up with something legible enough to report the
    /// problem, not as a usable state.
    /// </summary>
    private static string[] CandidatePaths()
    {
        if (OperatingSystem.IsWindows())
            return
            [
                @"C:\Windows\Fonts\meiryo.ttc",
                @"C:\Windows\Fonts\YuGothM.ttc",
                @"C:\Windows\Fonts\msgothic.ttc",
                @"C:\Windows\Fonts\arial.ttf",
            ];

        if (OperatingSystem.IsMacOS())
            return
            [
                "/System/Library/Fonts/ヒラギノ角ゴシック W3.ttc",
                "/System/Library/Fonts/Hiragino Sans GB.ttc",
                "/Library/Fonts/Arial Unicode.ttf",
                "/System/Library/Fonts/Supplemental/Arial Unicode.ttf",
            ];

        // Linux and the other Unixes. Distributions disagree on both the directory and the file name, hence
        // the spread; the Debian/Ubuntu alternatives symlink is first because it is the one stable name.
        return
        [
            "/usr/share/fonts/truetype/fonts-japanese-gothic.ttf",
            "/usr/share/fonts/opentype/noto/NotoSansCJK-Regular.ttc",
            "/usr/share/fonts/opentype/noto/NotoSansCJKjp-Regular.otf",
            "/usr/share/fonts/google-noto-cjk/NotoSansCJK-Regular.ttc",
            "/usr/share/fonts/noto-cjk/NotoSansCJK-Regular.ttc",
            "/usr/share/fonts/truetype/vlgothic/VL-Gothic-Regular.ttf",
            "/usr/share/fonts/opentype/ipafont-gothic/ipag.ttf",
            "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf",
        ];
    }

    /// <summary>File names worth accepting wherever they turn up, for when none of the fixed paths hit.</summary>
    private static readonly string[] PreferredFontFiles =
    {
        "NotoSansCJK-Regular.ttc", "NotoSansCJKjp-Regular.otf", "NotoSansJP-Regular.ttf",
        "NotoSansJP[wght].ttf", "VL-Gothic-Regular.ttf", "ipag.ttf", "ipagp.ttf",
        "TakaoGothic.ttf", "DroidSansFallbackFull.ttf", "DejaVuSans.ttf",
    };

    private static string[] FontDirectories()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return
        [
            "/usr/share/fonts",
            "/usr/local/share/fonts",
            "/Library/Fonts",
            "/System/Library/Fonts",
            Path.Combine(home, ".local", "share", "fonts"),
            Path.Combine(home, ".fonts"),
        ];
    }

    /// <summary>
    /// The first font this machine can offer, or null if it has none we recognise. Distributions rename and
    /// relocate font packages between releases, so a miss on every fixed path falls back to sweeping the
    /// standard font directories rather than giving up.
    /// </summary>
    private static string? FindFont()
    {
        foreach (var path in CandidatePaths())
        {
            if (File.Exists(path))
                return path;
        }

        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            MatchCasing = MatchCasing.CaseInsensitive,
        };

        foreach (var name in PreferredFontFiles)
        {
            foreach (var dir in FontDirectories())
            {
                if (!Directory.Exists(dir))
                    continue;
                try
                {
                    var hit = Directory.EnumerateFiles(dir, name, options).FirstOrDefault();
                    if (hit is not null)
                        return hit;
                }
                catch (IOException)
                {
                    // An unreadable or vanishing font directory is not a reason to stop looking in the others.
                }
                catch (UnauthorizedAccessException)
                {
                }
            }
        }

        return null;
    }

    private readonly Dictionary<float, IntPtr> _fontsBySize = new();
    private readonly Dictionary<string, CachedText> _cache = new();
    private readonly string _fontPath;
    private int _frame;

    private const int SweepIntervalFrames = 300;
    private const int MaxIdleFrames = 180;

    /// <summary>
    /// How many real pixels the window currently devotes to one logical pixel. The game draws into a fixed
    /// 640x400 space that SDL stretches to fill the window, so at a 2x window this is 2.
    /// </summary>
    private float _pixelScale = 1f;

    /// <summary>
    /// Glyphs are rasterised at <see cref="_pixelScale"/> times their logical size, so a 10pt label in a
    /// double-size window is drawn from a 20px rendering rather than a 10px one blown up. Beyond this the
    /// gain is invisible and the texture cache starts to cost real memory.
    /// </summary>
    private const float MaxPixelScale = 4f;

    /// <summary>
    /// Re-reads the window's current pixel scale; call once a frame. Text rendered before this reflects the
    /// old scale, which matters only for the single frame in which the window is resized.
    /// </summary>
    public void RefreshPixelScale(IntPtr renderer)
    {
        if (!SDL.GetCurrentRenderOutputSize(renderer, out var outW, out var outH) || outW <= 0 || outH <= 0)
            return;
        SDL.GetRenderLogicalPresentation(renderer, out var logicalW, out var logicalH, out _);
        if (logicalW <= 0 || logicalH <= 0)
            return;

        // Letterboxing fits the smaller of the two ratios, which is the scale actually applied to what we draw.
        var scale = Math.Min((float)outW / logicalW, (float)outH / logicalH);
        _pixelScale = Math.Clamp(scale, 1f, MaxPixelScale);
    }

    /// <summary>
    /// The font size to rasterise at for a given on-screen size. Rounded to whole pixels because TTF hinting
    /// works in whole pixels, and quantised so that dragging a window edge does not rebuild the entire glyph
    /// cache on every single frame of the drag.
    /// </summary>
    private float PixelSizeFor(float logicalSize) =>
        Math.Max(1f, MathF.Round(logicalSize * MathF.Round(_pixelScale * 4f) / 4f));

    private readonly record struct CachedText(IntPtr Texture, int Width, int Height, int LastUsedFrame);

    public FontService()
    {
        _fontPath = FindFont()
            ?? throw new InvalidOperationException(
                "文字表示に使えるフォントが見つかりませんでした。" +
                "Linuxでは fonts-noto-cjk / vlgothic などの日本語フォントを入れてください。");
        if (!TTF.Init())
            throw new InvalidOperationException($"TTF.Init failed: {SDL.GetError()}");
    }

    private IntPtr GetFont(float size)
    {
        if (_fontsBySize.TryGetValue(size, out var font))
            return font;

        font = TTF.OpenFont(_fontPath, size);
        if (font == IntPtr.Zero)
            throw new InvalidOperationException($"TTF.OpenFont failed: {SDL.GetError()}");

        // Light hinting stops stems being snapped hard onto the pixel grid at the many sizes this UI asks
        // for, which is most of what makes small Japanese text read as type rather than as lumps.
        TTF.SetFontHinting(font, TTF.HintingFlags.Light);
        _fontsBySize[size] = font;
        return font;
    }

    /// <summary>Draws text at (x, y) top-left, caching the rendered texture keyed by text+size+color.</summary>
    public void DrawText(IntPtr renderer, string text, float x, float y, float size, SDL.Color color)
    {
        if (string.IsNullOrEmpty(text))
            return;

        // Destination measured in logical units, texture rasterised at real pixels: the layout stays put
        // while the lettering itself gets re-rendered sharper as the window grows.
        var (w, h) = Measure(text, size);
        var tex = GetOrCreateTexture(renderer, text, size, color);
        var dst = new SDL.FRect { X = x, Y = y, W = w, H = h };
        SDL.RenderTexture(renderer, tex, IntPtr.Zero, dst);
    }

    /// <summary>Measures text without drawing it (for centering/layout).</summary>
    public (int Width, int Height) Measure(string text, float size)
    {
        if (string.IsNullOrEmpty(text))
            return (0, 0);
        var font = GetFont(size);
        TTF.GetStringSize(font, text, (UIntPtr)0, out var w, out var h);
        return (w, h);
    }

    private IntPtr GetOrCreateTexture(IntPtr renderer, string text, float size, SDL.Color color)
    {
        _frame++;
        var pixelSize = PixelSizeFor(size);
        var key = $"{pixelSize}{color.R},{color.G},{color.B},{color.A}{text}";
        if (_cache.TryGetValue(key, out var cached))
        {
            _cache[key] = cached with { LastUsedFrame = _frame };
            return cached.Texture;
        }

        var font = GetFont(pixelSize);
        var surface = TTF.RenderTextBlended(font, text, (UIntPtr)0, color);
        if (surface == IntPtr.Zero)
            throw new InvalidOperationException($"TTF.RenderTextBlended failed: {SDL.GetError()}");

        var texture = SDL.CreateTextureFromSurface(renderer, surface);
        SDL.GetTextureSize(texture, out var fw, out var fh);
        SDL.DestroySurface(surface);
        // Linear, unlike the pixel-art sprites. The glyph texture is close to its destination size but
        // rarely an exact multiple of it, and smoothing that last fraction is the difference between
        // clean type and stair-stepped edges. Sprites stay on nearest so the art keeps its hard edges.
        SDL.SetTextureScaleMode(texture, SDL.ScaleMode.Linear);

        var entry = new CachedText(texture, (int)fw, (int)fh, _frame);
        _cache[key] = entry;

        if (_frame % SweepIntervalFrames == 0)
            SweepStaleEntries();

        return texture;
    }

    private void SweepStaleEntries()
    {
        var stale = _cache.Where(kv => _frame - kv.Value.LastUsedFrame > MaxIdleFrames).Select(kv => kv.Key).ToList();
        foreach (var key in stale)
        {
            SDL.DestroyTexture(_cache[key].Texture);
            _cache.Remove(key);
        }
    }

    public void Dispose()
    {
        foreach (var c in _cache.Values)
            SDL.DestroyTexture(c.Texture);
        _cache.Clear();

        foreach (var f in _fontsBySize.Values)
            TTF.CloseFont(f);
        _fontsBySize.Clear();

        TTF.Quit();
    }
}
