using SDL3;
using SlimeDungeon.Core;
using SlimeDungeon.Domain;

namespace SlimeDungeon.UI;

/// <summary>
/// Announces titles the guild has just recognised, and asks whether to put one on the card.
///
/// This used to be a notice you pressed past, and then the title sat unworn until you thought to go and look
/// for the register. Several can land at once — a single dungeon trip might tip over a kill count and a chest
/// count together — so it is a list to choose from rather than a yes/no, with a row for leaving the card as it
/// is. The last row is that one, and it is where the cursor starts: earning a title should never change what
/// you are wearing without you saying so.
/// </summary>
public static class TitleAwardPopup
{
    /// <summary>Rows on screen at once. Beyond this the rest are counted rather than listed.</summary>
    private const int MaxRows = 5;

    /// <summary>How many rows the popup offers, including the "leave it alone" row at the end.</summary>
    public static int RowCount(List<TitleDefinition> awarded) => Math.Min(awarded.Count, MaxRows) + 1;

    /// <summary>
    /// What the chosen row means: the title to display, or null for the last row, which changes nothing.
    /// </summary>
    public static TitleId? Chosen(List<TitleDefinition> awarded, int cursor)
    {
        var shown = Math.Min(awarded.Count, MaxRows);
        return cursor >= 0 && cursor < shown ? awarded[cursor].Id : null;
    }

    public static void Draw(GameContext ctx, List<TitleDefinition> awarded, int cursor)
    {
        if (awarded.Count == 0)
            return;

        var r = ctx.Renderer;
        var fonts = ctx.Fonts;

        var shown = awarded.Take(MaxRows).ToList();
        var hidden = awarded.Count - shown.Count;

        const float w = 400f;
        var h = 116f + (shown.Count + 1) * 30f + (hidden > 0 ? 16f : 0f);
        var x = (640f - w) / 2f;
        var y = (400f - h) / 2f;

        r.FillRect(0, 0, 640, 400, Colors.Rgb(0, 0, 0, 120));
        r.FillRect(x + 5, y + 5, w, h, Colors.Rgb(8, 7, 10));
        r.FillRect(x, y, w, h, Colors.Rgb(30, 26, 20));
        r.DrawRect(x, y, w, h, Colors.Gold);
        r.DrawRect(x + 3, y + 3, w - 6, h - 6, Colors.Rgb(104, 88, 40));

        var cx = x + w / 2f;
        var heading = awarded.Count > 1 ? $"称号を{awarded.Count}個獲得！" : "称号を獲得！";
        DrawCentered(ctx, heading, cx, y + 12, 17, Colors.Gold);
        DrawCentered(ctx, awarded.Count > 1 ? "どの称号をギルドカードに掲げますか？" : "この称号をギルドカードに掲げますか？",
            cx, y + 36, 11, Colors.Rgb(190, 182, 166));

        var ry = y + 58f;
        for (var i = 0; i < shown.Count; i++)
        {
            var selected = i == cursor;
            if (selected)
                r.FillRect(x + 14, ry - 4, w - 28, 30, Colors.Highlight);

            r.DrawTexture(ctx.Sprites.RankSeal, x + 22, ry, 20, 20);
            fonts.DrawText(r.Handle, shown[i].Name, x + 52, ry + 1, 14,
                selected ? Colors.Black : Colors.Highlight);
            fonts.DrawText(r.Handle, shown[i].Requirement, x + 52, ry + 15, 9,
                selected ? Colors.Rgb(70, 58, 24) : Colors.Rgb(160, 154, 142));
            ry += 30f;
        }

        if (hidden > 0)
        {
            // Only the first few are offered. The rest are still earned and still choosable from the register.
            DrawCentered(ctx, $"…ほか{hidden}件（ギルドの称号一覧から選べます）", cx, ry, 9, Colors.Rgb(190, 182, 166));
            ry += 16f;
        }

        // The row that leaves things as they are, always last and always present.
        var keepSelected = cursor == shown.Count;
        if (keepSelected)
            r.FillRect(x + 14, ry - 4, w - 28, 30, Colors.Highlight);

        var current = ctx.Player?.DisplayedTitle is { } id ? Titles.NameOf(id) : "なし";
        fonts.DrawText(r.Handle, "変更しない", x + 52, ry + 1, 14, keepSelected ? Colors.Black : Colors.White);
        fonts.DrawText(r.Handle, $"今のまま「{current}」を掲げ続ける", x + 52, ry + 15, 9,
            keepSelected ? Colors.Rgb(70, 58, 24) : Colors.Rgb(160, 154, 142));

        ControlHints.DrawCentered(ctx, cx, y + h - 20, 9, Colors.Rgb(150, 145, 130),
            ControlHints.Direction("選ぶ"), ControlHints.Confirm("決定"));
    }

    private static void DrawCentered(GameContext ctx, string text, float centerX, float y, float size, SDL.Color color)
    {
        var (tw, _) = ctx.Fonts.Measure(text, size);
        ctx.Fonts.DrawText(ctx.Renderer.Handle, text, centerX - tw / 2f, y, size, color);
    }
}
