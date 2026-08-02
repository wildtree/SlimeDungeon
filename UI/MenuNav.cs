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

    /// <summary>
    /// What the on-screen hints call each control.
    ///
    /// These name the *action*, never the key. The hints used to print the physical button — X on a keyboard,
    /// A on a gamepad — which meant that whichever of the two you were not holding, the screen was telling you
    /// to press something that did not exist in front of you. Worse, X is confirm on the keyboard and the menu
    /// button on a pad, so a printed letter was actively ambiguous. Naming the action sidesteps all of it: the
    /// player learns "決定" once and it holds whatever they are playing with.
    /// </summary>
    public static class Hint
    {
        public const string Direction = "方向キー";
        public const string Confirm = "決定";
        public const string Cancel = "取消";
        public const string Menu = "メニュー";
    }

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
