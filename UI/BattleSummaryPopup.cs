using SDL3;
using SlimeDungeon.Core;
using SlimeDungeon.Domain;

namespace SlimeDungeon.UI;

/// <summary>
/// The tally at the end of a fight: what was brought down, and what it was worth in experience. The battle log
/// scrolls past a line at a time and is easy to lose track of, so the trip's actual result gets stated once,
/// plainly, before the screen goes back to the dungeon. Anything that escaped is listed too — it is the
/// difference between a white slime you cornered and one you did not.
/// </summary>
public static class BattleSummaryPopup
{
    private const float PanelW = 340f;
    private const int MaxRows = 6;

    public static void Draw(GameContext ctx, IReadOnlyList<Slime> defeated, IReadOnlyList<Slime> escaped, int exp,
        IReadOnlyList<Metal>? materials = null)
    {
        var r = ctx.Renderer;
        var fonts = ctx.Fonts;

        var lines = Group(defeated);
        var shown = lines.Take(MaxRows).ToList();
        var hidden = lines.Count - shown.Count;
        var ore = materials is { Count: > 0 } ? materials : null;

        var panelH = 92f + shown.Count * 22f + (hidden > 0 ? 16f : 0f) + (escaped.Count > 0 ? 18f : 0f)
                     + (ore is null ? 0f : 18f);
        var x = (640f - PanelW) / 2f;
        var y = (400f - panelH) / 2f;

        r.FillRect(x + 6, y + 6, PanelW, panelH, Colors.Rgb(6, 6, 10));
        r.FillRect(x, y, PanelW, panelH, Colors.Rgb(26, 24, 34));
        r.DrawRect(x, y, PanelW, panelH, Colors.Gold);
        r.DrawRect(x + 3, y + 3, PanelW - 6, panelH - 6, Colors.Rgb(96, 84, 40));

        var cx = x + PanelW / 2f;
        DrawCentered(ctx, defeated.Count > 0 ? "戦闘終了" : "取り逃がした", cx, y + 14, 17, Colors.Gold);

        var ry = y + 42f;
        r.FillRect(x + 20, ry - 8, PanelW - 40, 1, Colors.Rgb(90, 84, 60));

        foreach (var (color, rank, count) in shown)
        {
            var (sprite, _) = ctx.Sprites.Slime(color);
            r.DrawTexture(sprite, x + 24, ry, 18, 18);
            fonts.DrawText(r.Handle, $"{Bounty.ColorLabel(color)}スライム", x + 48, ry + 3, 11, Colors.White);
            fonts.DrawText(r.Handle, rank.Label(), x + 190, ry + 4, 10, Colors.Highlight);

            // Counts right-aligned so a column of them can be read at a glance.
            var tally = $"×{count}";
            var (tw, _) = fonts.Measure(tally, 11);
            fonts.DrawText(r.Handle, tally, x + PanelW - 28 - tw, ry + 3, 11, Colors.White);
            ry += 22f;
        }

        if (defeated.Count == 0)
        {
            DrawCentered(ctx, "一体も倒せなかった", cx, ry, 10, Colors.Rgb(150, 145, 130));
            ry += 22f;
        }

        if (hidden > 0)
        {
            DrawCentered(ctx, $"…ほか{hidden}種", cx, ry, 10, Colors.Rgb(190, 182, 166));
            ry += 16f;
        }

        if (escaped.Count > 0)
        {
            DrawCentered(ctx, $"逃げられた: {escaped.Count}体", cx, ry, 10, Colors.Rgb(170, 150, 150));
            ry += 18f;
        }

        // Ore never goes into the bag, so unless it is said here the player has no way of knowing a drop
        // happened until they wander into the forge and notice the numbers have changed.
        if (ore is not null)
        {
            var haul = string.Join("、", ore
                .GroupBy(m => m)
                .Select(g => g.Count() > 1 ? $"{Metals.Get(g.Key).Name}×{g.Count()}" : Metals.Get(g.Key).Name));
            DrawCentered(ctx, $"素材入手: {haul}", cx, ry, 10, Colors.Gold);
            ry += 18f;
        }

        r.FillRect(x + 20, ry + 2, PanelW - 40, 1, Colors.Rgb(90, 84, 60));
        DrawCentered(ctx, $"経験値 {exp}", cx, ry + 10, 15, Colors.ExpBar);

        DrawCentered(ctx, $"[{MenuNav.Hint.Confirm}]続ける", cx, y + panelH - 18, 9, Colors.Rgb(150, 145, 130));
    }

    /// <summary>Collapses the kill list into one row per species and rank, worst first.</summary>
    private static List<(SlimeColor Color, Rank Rank, int Count)> Group(IReadOnlyList<Slime> defeated) =>
        defeated
            .GroupBy(s => (s.Color, s.Rank))
            .Select(g => (g.Key.Color, g.Key.Rank, g.Count()))
            .OrderByDescending(t => t.Rank)
            .ThenByDescending(t => t.Item3)
            .ToList();

    private static void DrawCentered(GameContext ctx, string text, float centerX, float y, float size, SDL.Color color)
    {
        var (tw, _) = ctx.Fonts.Measure(text, size);
        ctx.Fonts.DrawText(ctx.Renderer.Handle, text, centerX - tw / 2f, y, size, color);
    }
}
