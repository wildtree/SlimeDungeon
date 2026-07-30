using SlimeDungeon.Core;

namespace SlimeDungeon.Graphics;

/// <summary>
/// The animated part of a wall torch. The sconce itself is baked into whichever backdrop it belongs to; only
/// the flame is drawn per-frame, because that is the only part that moves.
/// </summary>
public static class TorchFlame
{
    /// <summary>
    /// Draws a three-layer flame sitting on top of <paramref name="baseY"/>. Each layer breathes on its own
    /// frequency so the silhouette never repeats on an obvious beat, which is what makes it read as fire
    /// rather than a pulsing rectangle.
    /// </summary>
    public static void Draw(Renderer r, float x, float baseY, float time, float scale = 1f)
    {
        var outerH = (20 + (float)Math.Sin(time * 7.3) * 4f) * scale;
        var midH = (13 + (float)Math.Sin(time * 11.1 + 1.3) * 3f) * scale;
        var coreH = (7 + (float)Math.Sin(time * 14.7 + 2.7) * 2f) * scale;
        var sway = (float)Math.Sin(time * 5.1) * 1.5f * scale;

        r.FillRect(x - 6 * scale + sway, baseY - outerH, 12 * scale, outerH, Colors.Rgb(196, 78, 22));
        r.FillRect(x - 4 * scale + sway * 1.4f, baseY - midH, 8 * scale, midH, Colors.Rgb(240, 152, 40));
        r.FillRect(x - 2 * scale + sway * 1.8f, baseY - coreH, 4 * scale, coreH, Colors.Rgb(255, 232, 150));
    }
}
