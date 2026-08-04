using SlimeDungeon.Core;

namespace SlimeDungeon.Graphics;

/// <summary>
/// The flicker on the painted title screen's two torches.
///
/// Deliberately not <see cref="TorchFlame"/>. That one draws the whole flame out of three stacked rectangles,
/// which suits the procedural backdrop it was written for — but the painting already has flames in it, drawn
/// far better than rectangles, and they are cold blue-white witchlight rather than fire. Stacking orange boxes
/// on top of them would replace the artwork rather than animate it.
///
/// So this adds light instead of shape: a soft halo and a brighter core, both breathing, laid over the flames
/// that are already there. The picture supplies the flame; this supplies the fact that it is burning.
/// </summary>
public static class FlameGlow
{
    /// <summary>
    /// The glow is one radial texture drawn at whatever size and tint a given flame wants. Authored large
    /// enough that scaling it up for the halo does not show the falloff's own steps.
    /// </summary>
    public const int TextureSize = 64;

    /// <summary>
    /// White, fading to nothing at the rim. The falloff is squared rather than linear: a linear ramp reads as
    /// a disc with a soft edge, and what is wanted is something with no edge at all.
    /// </summary>
    public static PixelCanvas BuildGlow()
    {
        var c = new PixelCanvas(TextureSize, TextureSize);
        const float radius = TextureSize / 2f;

        for (var y = 0; y < TextureSize; y++)
        {
            for (var x = 0; x < TextureSize; x++)
            {
                var dx = x + 0.5f - radius;
                var dy = y + 0.5f - radius;
                var d = (float)Math.Sqrt(dx * dx + dy * dy) / radius;
                if (d >= 1f)
                    continue;

                var falloff = (1f - d) * (1f - d);
                c.Set(x, y, Colors.Rgb(255, 255, 255, (byte)(falloff * 255)));
            }
        }

        return c;
    }

    /// <summary>Sampled from the artwork's own flames, so the light matches what it is sitting on.</summary>
    private static readonly SDL3.SDL.Color Halo = Colors.Rgb(104, 232, 226);
    private static readonly SDL3.SDL.Color Core = Colors.Rgb(206, 252, 246);

    /// <summary>
    /// Three sine terms at frequencies with no common multiple, so the brightness never settles into a beat.
    /// A single sine is instantly readable as a machine pulsing; three at these ratios is not.
    /// </summary>
    private static float Flicker(float time, float phase) =>
        0.5f
        + 0.28f * (float)Math.Sin(time * 6.1 + phase)
        + 0.14f * (float)Math.Sin(time * 11.3 + phase * 1.7)
        + 0.08f * (float)Math.Sin(time * 19.7 + phase * 2.9);

    /// <summary>
    /// Draws one flame's light, centred on the painted flame. <paramref name="phase"/> keeps the two torches
    /// off each other's rhythm — two lights flickering in unison look wired together.
    /// </summary>
    public static void Draw(Renderer r, IntPtr glow, float cx, float cy, float radius, float time, float phase)
    {
        if (glow == IntPtr.Zero)
            return;

        var flicker = Math.Clamp(Flicker(time, phase), 0f, 1f);

        // The flame licks upward as it brightens, and leans a little as it does. Without the lean the light
        // grows and shrinks on the spot, which reads as a lamp being dimmed rather than as something burning.
        var lift = radius * 0.14f * flicker;
        var lean = (float)Math.Sin(time * 3.7 + phase * 1.3) * radius * 0.06f;

        Blob(Halo, radius * (0.70f + 0.58f * flicker), (byte)(26 + 128 * flicker), 1f);
        Blob(Core, radius * (0.26f + 0.30f * flicker), (byte)(50 + 172 * flicker), 1.6f);

        void Blob(SDL3.SDL.Color colour, float rad, byte alpha, float leanScale)
        {
            var tint = Colors.Rgb(colour.R, colour.G, colour.B, alpha);
            r.DrawTextureTinted(glow, cx + lean * leanScale - rad, cy - lift - rad, rad * 2, rad * 2, tint);
        }
    }
}
