using SDL3;

namespace SlimeDungeon.Graphics;

/// <summary>
/// Loads hand-drawn artwork from disk, for the places where a painted illustration beats a procedural one.
///
/// Everything else in this game is generated from code, and for tiles and sprites that is the right call —
/// they are small, repetitive, and want to stay consistent. A guild interior is neither: it is one large
/// static picture whose only moving parts are the date and a line of dialogue, so drawing it with rectangles
/// buys nothing and costs a great deal of expressiveness.
///
/// Art is optional. When a file is missing the caller falls back to the procedural version, so the game runs
/// from a clean checkout with no assets at all.
/// </summary>
public static class ArtLoader
{
    /// <summary>The folder name looked for, both beside the executable and back up in the source tree.</summary>
    public const string AssetFolder = "assets";

    /// <summary>
    /// How far up from the executable to look for the project's own assets folder. bin/Debug/net10.0 is three
    /// levels down from the project directory; a couple to spare covers a runtime-identifier subfolder.
    /// </summary>
    private const int SourceSearchDepth = 5;

    /// <summary>
    /// Where a given asset actually is, or null. The copy beside the executable wins — that is what a built
    /// or published game uses. Failing that it walks back up towards the project directory, so during
    /// development a replaced picture is picked up on the next launch without even rebuilding.
    /// </summary>
    public static string? Resolve(string fileName)
    {
        var shipped = Path.Combine(AppContext.BaseDirectory, AssetFolder, fileName);
        if (File.Exists(shipped))
            return shipped;

        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (var i = 0; i < SourceSearchDepth && dir is not null; i++, dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, AssetFolder, fileName);
            if (File.Exists(candidate))
                return candidate;
        }

        return null;
    }

    /// <summary>
    /// Loads an image into a texture, or returns zero if it is not there or cannot be read. Any failure is
    /// reported once and then treated as "no artwork" — a missing or corrupt file should cost you the
    /// illustration, not the game.
    /// </summary>
    public static IntPtr TryLoad(IntPtr renderer, string fileName)
    {
        if (Resolve(fileName) is not { } path)
            return IntPtr.Zero;

        var surface = Image.Load(path);
        if (surface == IntPtr.Zero)
        {
            Console.Error.WriteLine($"could not read {path}: {SDL.GetError()}");
            return IntPtr.Zero;
        }

        var texture = SDL.CreateTextureFromSurface(renderer, surface);
        SDL.DestroySurface(surface);
        if (texture == IntPtr.Zero)
        {
            Console.Error.WriteLine($"could not upload {path}: {SDL.GetError()}");
            return IntPtr.Zero;
        }

        // Smooth, unlike the pixel-art sprites: a painted illustration scaled to the window should not have
        // its edges stair-stepped the way a deliberately blocky tile wants.
        SDL.SetTextureScaleMode(texture, SDL.ScaleMode.Linear);
        return texture;
    }
}
