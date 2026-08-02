using SDL3;
using SlimeDungeon.Core;
using SlimeDungeon.Domain;

namespace SlimeDungeon.UI;

/// <summary>What a chest held, one icon per line. This used to be a comma-separated string on the message line
/// in the corner of the dungeon, which is the least prominent place on the screen for the one moment a trip
/// actually pays out — and gave no sense of what the item was without reading its name.</summary>
public static class ChestPopup
{
    private const float PanelW = 300f;
    private const float RowHeight = 26f;
    private const float IconSize = 22f;

    public static void Draw(GameContext ctx, int gold, IReadOnlyList<Item> items)
    {
        var r = ctx.Renderer;
        var fonts = ctx.Fonts;

        var rows = (gold > 0 ? 1 : 0) + items.Count;
        var empty = rows == 0;
        var panelH = 74f + Math.Max(1, rows) * RowHeight;
        var x = (640f - PanelW) / 2f;
        var y = (400f - panelH) / 2f;

        r.FillRect(x + 6, y + 6, PanelW, panelH, Colors.Rgb(6, 6, 10));
        r.FillRect(x, y, PanelW, panelH, Colors.Rgb(30, 26, 20));
        r.DrawRect(x, y, PanelW, panelH, Colors.Gold);
        r.DrawRect(x + 3, y + 3, PanelW - 6, panelH - 6, Colors.Rgb(104, 88, 40));

        var cx = x + PanelW / 2f;

        // The open chest itself as the heading, so the dialog is identifiable before a word is read.
        r.DrawTexture(ctx.Sprites.ChestOpen, x + 14, y + 10, 26, 26);
        fonts.DrawText(r.Handle, empty ? "宝箱は空だった" : "宝箱をあけた！", x + 48, y + 16, 15, Colors.Gold);
        r.FillRect(x + 14, y + 42, PanelW - 28, 1, Colors.Rgb(90, 84, 60));

        var ry = y + 52f;
        if (empty)
        {
            DrawCentered(ctx, "…なにも入っていない", cx, ry + 4, 11, Colors.Rgb(150, 145, 130));
        }
        else
        {
            if (gold > 0)
            {
                r.DrawTexture(ctx.Sprites.GoldIcon, x + 24, ry, IconSize, IconSize);
                fonts.DrawText(r.Handle, "ゴールド", x + 54, ry + 5, 12, Colors.White);
                RightAligned(ctx, $"{gold} G", x + PanelW - 24, ry + 4, 13, Colors.Gold);
                ry += RowHeight;
            }

            foreach (var item in items)
            {
                r.DrawTexture(ctx.Sprites.ItemIcon(item), x + 24, ry, IconSize, IconSize);
                fonts.DrawText(r.Handle, item.Name, x + 54, ry + 5, 12, Colors.Highlight);
                if (item.Quantity > 1)
                    RightAligned(ctx, $"×{item.Quantity}", x + PanelW - 24, ry + 5, 12, Colors.White);
                ry += RowHeight;
            }
        }

        DrawCentered(ctx, $"[{MenuNav.Hint.Confirm}]閉じる", cx, y + panelH - 20, 9, Colors.Rgb(150, 145, 130));
    }

    private static void DrawCentered(GameContext ctx, string text, float centerX, float y, float size, SDL.Color color)
    {
        var (tw, _) = ctx.Fonts.Measure(text, size);
        ctx.Fonts.DrawText(ctx.Renderer.Handle, text, centerX - tw / 2f, y, size, color);
    }

    private static void RightAligned(GameContext ctx, string text, float right, float y, float size, SDL.Color color)
    {
        var (tw, _) = ctx.Fonts.Measure(text, size);
        ctx.Fonts.DrawText(ctx.Renderer.Handle, text, right - tw, y, size, color);
    }
}
