using SDL3;
using SlimeDungeon.Core;
using SlimeDungeon.Graphics;

namespace SlimeDungeon.UI;

/// <summary>One entry on a hint line: the control's glyph, and what pressing it does here.</summary>
public readonly record struct ControlHint(HintIcon Icon, string Label);

/// <summary>
/// Draws the "what can I press" line at the bottom of a screen as icons followed by words.
///
/// The hints have been through three forms. First the physical key ("[X]決定"), which was wrong for anyone on
/// a gamepad and outright misleading where a letter meant different things on the two devices. Then the action
/// name ("[決定]"), which was correct but read as a label bracketing its own name. Now the control is a picture
/// and the word beside it says only what it does here — so a line reads "✓ 決定  — 戻る" rather than repeating
/// itself, and nothing on it names hardware the player may not be holding.
/// </summary>
public static class ControlHints
{
    /// <summary>Icon size relative to the text it sits beside. Slightly larger, so it holds its own optically.</summary>
    private const float IconScale = 1.15f;

    /// <summary>Gap between an icon and its word.</summary>
    private const float IconGap = 3f;

    /// <summary>Gap between one hint and the next.</summary>
    private const float EntryGap = 12f;

    public static ControlHint Direction(string label) => new(HintIcon.Direction, label);
    public static ControlHint Confirm(string label) => new(HintIcon.Confirm, label);
    public static ControlHint Cancel(string label) => new(HintIcon.Cancel, label);
    public static ControlHint Menu(string label) => new(HintIcon.Menu, label);
    public static ControlHint Travel(string label) => new(HintIcon.Travel, label);

    /// <summary>Total width of the line, for right-aligning or centring it.</summary>
    public static float Width(GameContext ctx, float size, params ControlHint[] hints)
    {
        var total = 0f;
        for (var i = 0; i < hints.Length; i++)
        {
            total += size * IconScale + IconGap;
            if (hints[i].Label.Length > 0)
                total += ctx.Fonts.Measure(hints[i].Label, size).Item1;
            if (i < hints.Length - 1)
                total += EntryGap;
        }
        return total;
    }

    /// <summary>Draws the line starting at <paramref name="x"/>, with <paramref name="y"/> the text's top.</summary>
    public static void Draw(GameContext ctx, float x, float y, float size, SDL.Color color, params ControlHint[] hints)
    {
        var r = ctx.Renderer;
        var iconSize = size * IconScale;

        // Nudged up a little: text is drawn from its top, and an icon centred on the glyph body rather than on
        // the line box sits better against kana.
        var iconY = y - (iconSize - size) * 0.5f - 1f;

        foreach (var hint in hints)
        {
            r.DrawTextureTinted(ctx.Sprites.HintIcon(hint.Icon), x, iconY, iconSize, iconSize, color);
            x += iconSize + IconGap;

            if (hint.Label.Length > 0)
            {
                ctx.Fonts.DrawText(r.Handle, hint.Label, x, y, size, color);
                x += ctx.Fonts.Measure(hint.Label, size).Item1;
            }

            x += EntryGap;
        }
    }

    /// <summary>The same line, centred on <paramref name="centerX"/>.</summary>
    public static void DrawCentered(GameContext ctx, float centerX, float y, float size, SDL.Color color,
        params ControlHint[] hints) =>
        Draw(ctx, centerX - Width(ctx, size, hints) / 2f, y, size, color, hints);
}
