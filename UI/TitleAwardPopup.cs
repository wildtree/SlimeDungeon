using SDL3;
using SlimeDungeon.Core;
using SlimeDungeon.Domain;

namespace SlimeDungeon.UI;

/// <summary>
/// Announces titles the guild has just recognised. Several can land at once — a single dungeon trip might tip
/// over a kill count and a chest count together — so they are listed rather than shown one at a time.
/// </summary>
public static class TitleAwardPopup
{
    public static void Draw(GameContext ctx, List<TitleDefinition> awarded)
    {
        if (awarded.Count == 0)
            return;

        var r = ctx.Renderer;
        var fonts = ctx.Fonts;

        // Several thresholds can fall on the same trip, and a save gaining title data for the first time can
        // trip a dozen at once — far more than 400px of screen holds. Show the first few and count the rest;
        // the full register is on the title screen.
        const int maxRows = 5;
        var shown = awarded.Take(maxRows).ToList();
        var hidden = awarded.Count - shown.Count;

        const float w = 380f;
        var h = 92f + shown.Count * 30f + (hidden > 0 ? 16f : 0f);
        var x = (640f - w) / 2f;
        var y = (400f - h) / 2f;

        r.FillRect(x + 5, y + 5, w, h, Colors.Rgb(8, 7, 10));
        r.FillRect(x, y, w, h, Colors.Rgb(30, 26, 20));
        r.DrawRect(x, y, w, h, Colors.Gold);
        r.DrawRect(x + 3, y + 3, w - 6, h - 6, Colors.Rgb(104, 88, 40));

        var cx = x + w / 2f;
        var heading = awarded.Count > 1 ? $"称号を{awarded.Count}個獲得！" : "称号を獲得！";
        DrawCentered(ctx, heading, cx, y + 14, 17, Colors.Gold);
        DrawCentered(ctx, "ギルドがあなたの働きを認めました", cx, y + 38, 10, Colors.Rgb(180, 172, 158));

        var ry = y + 58f;
        foreach (var title in shown)
        {
            r.DrawTexture(ctx.Sprites.RankSeal, x + 22, ry, 20, 20);
            fonts.DrawText(r.Handle, title.Name, x + 52, ry + 1, 14, Colors.Highlight);
            fonts.DrawText(r.Handle, title.Requirement, x + 52, ry + 17, 9, Colors.Rgb(160, 154, 142));
            ry += 30f;
        }

        if (hidden > 0)
        {
            DrawCentered(ctx, $"…ほか{hidden}件", cx, ry, 10, Colors.Rgb(190, 182, 166));
            ry += 16f;
        }

        ControlHints.DrawCentered(ctx, cx, y + h - 20, 9, Colors.Rgb(150, 145, 130), ControlHints.Confirm("続ける（称号はギルドで選べます）"));
    }

    private static void DrawCentered(GameContext ctx, string text, float centerX, float y, float size, SDL.Color color)
    {
        var (tw, _) = ctx.Fonts.Measure(text, size);
        ctx.Fonts.DrawText(ctx.Renderer.Handle, text, centerX - tw / 2f, y, size, color);
    }
}
