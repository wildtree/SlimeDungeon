using SlimeDungeon.Core;

namespace SlimeDungeon.UI;

/// <summary>
/// Three slimes loitering in front of the title screen's archway, bobbing on their own out-of-step rhythms.
/// The only moving thing on that screen, and the only screen they appear on: the dungeon entrance is meant to
/// be empty, so nothing is waiting for you at the top of the stairs.
/// </summary>
public static class DoorwaySlimes
{
    /// <summary>
    /// Offset from the arch centre, size, and phase. The phases are deliberately not evenly spaced: three
    /// slimes hopping in a neat round would read as choreography, and these are supposed to be loitering.
    /// </summary>
    private static readonly (float OffsetX, float Size, float Phase, SlimeColor Color)[] Group =
    [
        (-1.9f, 1.0f, 0.0f, SlimeColor.Blue),
        (0.0f, 1.28f, 1.9f, SlimeColor.Green),
        (1.6f, 0.88f, 3.4f, SlimeColor.Red),
    ];

    /// <summary>
    /// Draws the group with its feet on <paramref name="baseY"/>, centred on <paramref name="centreX"/>.
    /// <paramref name="unit"/> is the middle slime's size divided by its own scale — in practice, just set it
    /// so the group looks right against the archway behind it.
    /// </summary>
    public static void Draw(GameContext ctx, float centreX, float baseY, float unit, float time)
    {
        var r = ctx.Renderer;
        foreach (var (offsetX, sizeScale, phase, color) in Group)
        {
            var (idle, hop) = ctx.Sprites.Slime(color);
            var cycle = Math.Sin(time * 2.2 + phase);

            // The squashed frame on the way up, the round one coming down, and a lift that peaks between them.
            var texture = cycle > 0 ? hop : idle;
            var lift = (float)Math.Abs(cycle) * unit * 0.2f;

            var size = unit * sizeScale;
            var x = centreX + offsetX * unit - size / 2f;
            var y = baseY - size - lift;
            r.DrawTexture(texture, x, y, size, size);
        }
    }
}
