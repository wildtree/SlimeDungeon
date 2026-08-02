using SDL3;
using SlimeDungeon.Core;
using SlimeDungeon.Domain;

namespace SlimeDungeon.UI;

/// <summary>
/// "Your bag is full — throw something away?" Shown after a fight when loot dropped with nowhere to put it.
///
/// The last row gives the find up instead, and that is where the cursor starts: the safe answer is the one
/// that leaves everything you are already carrying alone. Ore only started needing this when it moved out of
/// its weightless pouch and into the bag, which is the point — a good haul is now a decision.
/// </summary>
public static class OverflowPopup
{
    private const float PanelW = 400f;

    /// <summary>Rows: everything in the bag, then the row for abandoning the find.</summary>
    public static int RowCount(Player player) => player.Bag.Count + 1;

    /// <summary>The bag item at this row, or null for the last row, which means "leave it".</summary>
    public static Item? Chosen(Player player, int cursor) =>
        cursor >= 0 && cursor < player.Bag.Count ? player.Bag[cursor] : null;

    /// <summary>
    /// Where the thing is coming from. It changes what the dialog can honestly say: loot off a slime is
    /// already in your hands and the only question is what to do with it, whereas a chest has not been opened
    /// yet and declining leaves it sitting there.
    /// </summary>
    public enum Source { Loot, Chest }

    public static void Draw(GameContext ctx, Item incoming, int cursor, Source source = Source.Loot)
    {
        var r = ctx.Renderer;
        var fonts = ctx.Fonts;
        var player = ctx.Player!;

        var rows = player.Bag.Count + 1;
        var h = 108f + rows * 19f;
        var x = (640f - PanelW) / 2f;
        var y = (400f - h) / 2f;

        r.FillRect(0, 0, 640, 400, Colors.Rgb(0, 0, 0, 130));
        r.FillRect(x + 5, y + 5, PanelW, h, Colors.Rgb(6, 6, 10));
        r.FillRect(x, y, PanelW, h, Colors.Rgb(30, 26, 22));
        r.DrawRect(x, y, PanelW, h, Colors.Gold);
        r.DrawRect(x + 3, y + 3, PanelW - 6, h - 6, Colors.Rgb(104, 88, 40));

        var cx = x + PanelW / 2f;
        DrawCentered(ctx, "鞄がいっぱいです", cx, y + 12, 16, Colors.Gold);

        // What is waiting, drawn with its icon so it reads as a thing rather than a line of text.
        var note = source == Source.Chest ? "が入っています" : "を拾いました";
        r.DrawTexture(ctx.Sprites.ItemIcon(incoming), x + 24, y + 36, 16, 16);
        fonts.DrawText(r.Handle, $"{incoming.Name} {note}", x + 46, y + 38, 12, Colors.Highlight);
        DrawCentered(ctx, "どれか捨てますか？", cx, y + 58, 11, Colors.Rgb(190, 182, 166));

        var ry = y + 78f;
        for (var i = 0; i < player.Bag.Count; i++)
        {
            var item = player.Bag[i];
            var selected = i == cursor;
            if (selected)
                r.FillRect(x + 14, ry - 3, PanelW - 28, 18, Colors.Highlight);

            r.DrawTexture(ctx.Sprites.ItemIcon(item), x + 20, ry - 1, 14, 14);
            var label = item.Quantity > 1 ? $"{item.Name} x{item.Quantity}" : item.Name;
            fonts.DrawText(r.Handle, label, x + 40, ry, 11, selected ? Colors.Black : Colors.White);

            // What it is worth, so the choice can be made without leaving the dialog.
            var value = $"{item.SellValue}G";
            var (vw, _) = fonts.Measure(value, 9);
            fonts.DrawText(r.Handle, value, x + PanelW - 24 - vw, ry + 1, 9,
                selected ? Colors.Rgb(70, 58, 24) : Colors.Rgb(160, 154, 142));

            ry += 19f;
        }

        var giveUp = cursor == player.Bag.Count;
        if (giveUp)
            r.FillRect(x + 14, ry - 3, PanelW - 28, 18, Colors.Highlight);
        var giveUpLabel = source == Source.Chest
            ? "あきらめる（宝箱はそのまま残ります）"
            : $"{incoming.Name}をあきらめる";
        fonts.DrawText(r.Handle, giveUpLabel, x + 40, ry, 11,
            giveUp ? Colors.Black : Colors.Rgb(200, 170, 170));

        ControlHints.DrawCentered(ctx, cx, y + h - 18, 9, Colors.Rgb(150, 145, 130),
            ControlHints.Direction("選ぶ"), ControlHints.Confirm("決定"));
    }

    private static void DrawCentered(GameContext ctx, string text, float centerX, float y, float size, SDL.Color color)
    {
        var (tw, _) = ctx.Fonts.Measure(text, size);
        ctx.Fonts.DrawText(ctx.Renderer.Handle, text, centerX - tw / 2f, y, size, color);
    }
}
