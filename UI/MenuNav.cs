using SDL3;
using SlimeDungeon.Core;

namespace SlimeDungeon.UI;

/// <summary>Shared up/down cursor navigation + confirm/cancel edge-detection for list-style menus.</summary>
public static class MenuNav
{
    /// <summary>
    /// Moves the cursor and — just as importantly — guarantees the result is a valid index into a list of
    /// <paramref name="count"/> items.
    ///
    /// It used to return the cursor untouched when no direction was pressed, which quietly made every caller
    /// unsafe: these lists shrink under a stationary cursor all the time (an item used up or thrown away, a
    /// quest handed in, a slime killed), and every caller feeds the result straight into a list indexer. Sit
    /// on the last item, consume it, press confirm, and the game came down with an IndexOutOfRange.
    /// </summary>
    public static int Move(InputManager input, int cursor, int count)
    {
        if (count <= 0)
            return 0;

        cursor = Math.Clamp(cursor, 0, count - 1);

        if (Down(input)) cursor = (cursor + 1) % count;
        if (Up(input)) cursor = (cursor - 1 + count) % count;
        return cursor;
    }

    /// <summary>Cursor movement, from the arrow keys or the pad's d-pad.</summary>
    public static bool Up(InputManager input) =>
        input.WasPressed(SDL.Keycode.Up) || input.WasPressed(InputManager.DpadUp);

    public static bool Down(InputManager input) =>
        input.WasPressed(SDL.Keycode.Down) || input.WasPressed(InputManager.DpadDown);

    public static bool Left(InputManager input) =>
        input.WasPressed(SDL.Keycode.Left) || input.WasPressed(InputManager.DpadLeft);

    public static bool Right(InputManager input) =>
        input.WasPressed(SDL.Keycode.Right) || input.WasPressed(InputManager.DpadRight);

    public static bool Confirmed(InputManager input) => input.WasPressed(GameAction.Confirm);

    public static bool Cancelled(InputManager input) => input.WasPressed(GameAction.Cancel);

    public static bool MenuRequested(InputManager input) => input.WasPressed(GameAction.Menu);

    // On-screen hints live in ControlHints, which draws each control as a small icon. There is deliberately no
    // string form of a control's name here any more: hints went from naming the key ("[X]") to naming the
    // action ("[決定]") to showing the control itself, and leaving a text version lying around would only
    // invite the next screen to go back to spelling one out.

    /// <summary>
    /// Widest rendered width across every label in a list, at the given font size — used so a selection
    /// highlight bar can be sized once for the whole list instead of resizing as the cursor moves between
    /// labels of different lengths.
    /// </summary>
    public static float MaxLabelWidth(GameContext ctx, IEnumerable<string> labels, float fontSize)
    {
        var max = 0f;
        foreach (var label in labels)
        {
            var (w, _) = ctx.Fonts.Measure(label, fontSize);
            if (w > max)
                max = w;
        }
        return max;
    }

    /// <summary>
    /// Draws one selectable row: a solid highlight bar (sized to <paramref name="barWidth"/>, not this
    /// row's own text width) with dark text when selected, plain text otherwise. Replaces the old "> "
    /// prefix convention, which misaligned every row as the cursor moved between labels of different widths.
    /// </summary>
    public static void DrawRow(GameContext ctx, float x, float y, float barWidth, float rowHeight, string label, float fontSize, bool selected)
    {
        if (selected)
        {
            ctx.Renderer.FillRect(x - 4, y - 2, barWidth + 8, rowHeight, Colors.Highlight);
            ctx.Fonts.DrawText(ctx.Renderer.Handle, label, x, y, fontSize, Colors.Black);
        }
        else
        {
            ctx.Fonts.DrawText(ctx.Renderer.Handle, label, x, y, fontSize, Colors.White);
        }
    }
}
