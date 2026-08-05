using SDL3;
using SlimeDungeon.Core;

namespace SlimeDungeon.UI;

/// <summary>
/// "Are you sure?", with the thing being asked about spelled out on the panel.
///
/// The screens that lead here all used to act on the confirm key immediately: one press accepted a contract,
/// bought a sword, or sold one. That is fine when the cursor is where you left it, and a disaster the moment
/// it is not — and on a board where every row looks like every other row, it very often is not. So the answer
/// is not "are you sure" on its own, which nobody reads: it is the item, its price, its rank, what it will
/// leave in your purse. The question is a chance to notice the cursor is on the wrong row.
/// </summary>
public static class ConfirmPopup
{
    /// <summary>One fact about the thing being confirmed: what it is on the left, its value on the right.</summary>
    public readonly record struct Line(string Label, string Value, SDL.Color? ValueColor = null);

    public const float Width = 300f;

    /// <summary>Centred on the artwork/menu half of the screen, not on the window — the status panel owns the
    /// right-hand 240 pixels on every screen this appears over.</summary>
    private const float CentreX = 200f;

    private const float Pad = 14f;
    private const float LineHeight = 17f;

    private static readonly SDL.Color Panel = Colors.Rgb(28, 24, 20);
    private static readonly SDL.Color LabelInk = Colors.Rgb(158, 150, 138);

    public static void Draw(GameContext ctx, string heading, IntPtr icon,
        IReadOnlyList<Line> lines, string question)
    {
        var r = ctx.Renderer;
        var fonts = ctx.Fonts;

        var h = Pad + 20f + 10f + lines.Count * LineHeight + 12f + 18f + 20f + Pad;
        var x = CentreX - Width / 2f;
        var y = (400f - h) / 2f;

        // Everything behind goes dim, so the eye has nowhere else to be. Without this the panel is just one
        // more box on a busy screen and reads as part of the list rather than as something blocking it.
        r.FillRect(0, 0, 400, 400, Colors.Rgb(0, 0, 0, 130));

        r.FillRect(x + 5, y + 6, Width, h, Colors.Rgb(6, 6, 10, 190));
        r.FillRect(x, y, Width, h, Panel);
        r.DrawRect(x, y, Width, h, Colors.Gold);

        var textX = x + Pad;
        if (icon != IntPtr.Zero)
        {
            r.DrawTexture(icon, textX, y + Pad, 18, 18);
            textX += 24f;
        }

        fonts.DrawText(r.Handle, heading, textX, y + Pad + 2, 13, Colors.Highlight);

        var ly = y + Pad + 30f;
        r.FillRect(x + Pad, ly - 6, Width - Pad * 2, 1, Colors.Rgb(74, 64, 50));

        foreach (var line in lines)
        {
            fonts.DrawText(r.Handle, line.Label, x + Pad, ly + 1, 10, LabelInk);
            var value = line.Value;
            var (vw, _) = fonts.Measure(value, 11);
            fonts.DrawText(r.Handle, value, x + Width - Pad - vw, ly, 11, line.ValueColor ?? Colors.White);
            ly += LineHeight;
        }

        ly += 10f;
        var (qw, _) = fonts.Measure(question, 12);
        fonts.DrawText(r.Handle, question, CentreX - qw / 2f, ly, 12, Colors.White);

        ControlHints.DrawCentered(ctx, CentreX, y + h - Pad - 10f, 10, Colors.Rgb(168, 160, 148),
            ControlHints.Confirm("はい"), ControlHints.Cancel("いいえ"));
    }
}
