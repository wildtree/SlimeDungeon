using SDL3;
using SlimeDungeon.Core;

namespace SlimeDungeon.UI;

/// <summary>
/// What just happened in the fight, as a panel rather than as text laid on the wall.
///
/// The round log used to be drawn straight onto the backdrop halfway up the screen. Against a flat procedural
/// wall that was legible; against a painted one of moss, brick and firelight it is not, and the lines have no
/// edge to tell the player where the report stops and the room begins. A panel also gives the beat its own
/// moment — the round resolves, you read it, you go on — instead of numbers quietly changing behind a menu.
/// </summary>
public static class BattleMessagePopup
{
    /// <summary>Centred on the battle half of the screen; the status panel owns the rest.</summary>
    private const float CentreX = 200f;

    private const float MaxWidth = 372f;
    private const float MinWidth = 190f;
    private const float Pad = 12f;
    private const float LineHeight = 17f;

    /// <summary>
    /// A round can produce a lot of lines when a big pack all acts — more than will fit. The tail is what
    /// matters (the newest events, and whatever killed something), so an overlong report shows its end.
    /// </summary>
    private const int MaxLines = 8;

    /// <summary>
    /// The panel's bottom edge: clear of the slimes' heads, so their sprites and health gauges are never
    /// covered by the report of what just happened to them. It grows upward from here into the wall, which is
    /// free — the command menu hangs there, but the two are never on screen at the same time.
    /// </summary>
    private const float Bottom = 268f;

    private static float TopFor(float height) => Math.Max(8f, Bottom - height);

    public static void Draw(GameContext ctx, IReadOnlyList<string> lines, string hint)
    {
        var r = ctx.Renderer;
        var fonts = ctx.Fonts;

        var shown = lines.Count > MaxLines ? lines.Skip(lines.Count - MaxLines).ToList() : lines.ToList();
        if (shown.Count == 0)
            return;

        var textWidth = MenuNav.MaxLabelWidth(ctx, shown, 11);
        var w = Math.Clamp(textWidth + Pad * 2, MinWidth, MaxWidth);
        var h = Pad + shown.Count * LineHeight + 6f + 16f + Pad;

        var x = CentreX - w / 2f;
        var y = TopFor(h);

        r.FillRect(x + 4, y + 5, w, h, Colors.Rgb(4, 4, 8, 170));
        r.FillRect(x, y, w, h, Colors.Rgb(22, 18, 16, 240));
        r.FillRect(x, y, w, 1, Colors.Rgb(126, 102, 66));
        r.DrawRect(x, y, w, h, Colors.Rgb(94, 76, 50));

        var ly = y + Pad;
        foreach (var line in shown)
        {
            fonts.DrawText(r.Handle, line, x + Pad, ly, 11, Colors.White);
            ly += LineHeight;
        }

        ControlHints.DrawCentered(ctx, CentreX, y + h - Pad - 4f, 10, Colors.Rgb(170, 162, 148),
            ControlHints.Confirm(hint));
    }

    /// <summary>The battle's closing line, which is one sentence and wants to be read as one.</summary>
    public static void DrawBanner(GameContext ctx, string message, SDL.Color colour, string hint)
    {
        var r = ctx.Renderer;
        var fonts = ctx.Fonts;

        var (tw, _) = fonts.Measure(message, 15);
        var w = Math.Clamp(tw + Pad * 3, MinWidth, MaxWidth);
        const float h = 62f;
        var x = CentreX - w / 2f;
        var y = TopFor(h);

        r.FillRect(x + 4, y + 5, w, h, Colors.Rgb(4, 4, 8, 170));
        r.FillRect(x, y, w, h, Colors.Rgb(22, 18, 16, 240));
        r.FillRect(x, y, w, 1, Colors.Rgb(126, 102, 66));
        r.DrawRect(x, y, w, h, Colors.Rgb(94, 76, 50));

        fonts.DrawText(r.Handle, message, CentreX - tw / 2f, y + 14, 15, colour);
        ControlHints.DrawCentered(ctx, CentreX, y + h - Pad - 4f, 10, Colors.Rgb(170, 162, 148),
            ControlHints.Confirm(hint));
    }
}
