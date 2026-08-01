using SDL3;
using SlimeDungeon.Core;

namespace SlimeDungeon.UI;

/// <summary>Shared up/down cursor navigation + confirm/cancel edge-detection for list-style menus.</summary>
public static class MenuNav
{
    public static int Move(InputManager input, int cursor, int count)
    {
        if (count <= 0)
            return 0;
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

    /// <summary>Button names for on-screen hints, matched to whatever the player is actually holding.</summary>
    public static string ConfirmHint(InputManager input) => input.GamepadConnected ? "A" : "X";
    public static string CancelHint(InputManager input) => input.GamepadConnected ? "B" : "Z";
    public static string MenuHint(InputManager input) => input.GamepadConnected ? "X" : "S";

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
