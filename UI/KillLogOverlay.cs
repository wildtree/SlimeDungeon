using SDL3;
using SlimeDungeon.Core;
using SlimeDungeon.Domain;
using SlimeDungeon.Graphics;

namespace SlimeDungeon.UI;

/// <summary>
/// The 's' overlay: a bestiary of every slime species with how many you have defeated. Rows are laid out in
/// fixed columns with a scaled-down sprite for each species — the old version was one interpolated string per
/// (species, rank) pair, so nothing lined up and it could run to dozens of rows.
/// </summary>
public sealed class KillLogOverlay
{
    private const float PanelX = 60f;
    private const float PanelY = 20f;
    private const float PanelW = 520f;
    private const float PanelH = 360f;

    // Column origins, measured from the panel's left edge.
    private const float IconX = PanelX + 18f;
    private const float NameX = PanelX + 48f;
    private const float ElementX = PanelX + 196f;
    private const float CountRightX = PanelX + 300f;
    private const float BreakdownX = PanelX + 316f;

    private const float IconSize = 22f;
    private const float RowHeight = 27f;

    public void Update(GameContext ctx, float dt)
    {
        if (MenuNav.Cancelled(ctx.Input) || MenuNav.MenuRequested(ctx.Input))
            ctx.ShowKillLog = false;
    }

    public void Draw(GameContext ctx)
    {
        var r = ctx.Renderer;
        var fonts = ctx.Fonts;
        var player = ctx.Player!;

        r.DrawTexture(ctx.Sprites.MenuBackdrop, PanelX, PanelY, PanelW, PanelH);
        r.DrawRect(PanelX, PanelY, PanelW, PanelH, Colors.Border);

        // Roll the per-(species, rank) records up into one row per species, keeping the rank spread as a
        // compact breakdown so no detail is lost.
        var byColor = player.KillLog
            .GroupBy(k => k.Color)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(k => k.Rank).ToList());

        var totalKills = player.KillLog.Sum(k => k.Count);
        var speciesFound = byColor.Count;
        var allColors = Enum.GetValues<SlimeColor>();

        fonts.DrawText(r.Handle, "討伐記録", PanelX + 16, PanelY + 12, 16, Colors.White);
        var summary = $"合計 {totalKills}体 ・ {speciesFound}/{allColors.Length}種";
        var (summaryW, _) = fonts.Measure(summary, 11);
        fonts.DrawText(r.Handle, summary, PanelX + PanelW - summaryW - 16, PanelY + 16, 11, Colors.Highlight);

        var y = PanelY + 38f;
        fonts.DrawText(r.Handle, "スライム", NameX, y, 9, Colors.Border);
        fonts.DrawText(r.Handle, "属性", ElementX, y, 9, Colors.Border);
        DrawRightAligned(ctx, "討伐数", CountRightX, y, 9, Colors.Border);
        fonts.DrawText(r.Handle, "ランク別", BreakdownX, y, 9, Colors.Border);
        y += 14f;
        r.FillRect(PanelX + 16, y, PanelW - 32, 1, Colors.Rgb(90, 84, 70));
        y += 6f;

        foreach (var color in allColors)
        {
            var found = byColor.TryGetValue(color, out var records);
            var count = found ? records!.Sum(k => k.Count) : 0;

            // Alternating banding, so the eye can track a row across to its count.
            if ((Array.IndexOf(allColors, color) & 1) == 0)
                r.FillRect(PanelX + 16, y - 3, PanelW - 32, RowHeight - 2, Colors.Rgb(30, 26, 22));

            var (idle, _) = ctx.Sprites.Slime(color);
            r.DrawTexture(idle, IconX, y - 2, IconSize, IconSize);

            // Undefeated species stay listed but dimmed, so the panel doubles as a checklist.
            var nameColor = found ? Colors.White : Colors.Rgb(96, 92, 88);
            fonts.DrawText(r.Handle, SlimeNames.FullName(color), NameX, y + 3, 12, nameColor);

            var element = Slime.ElementForColor(color);
            fonts.DrawText(r.Handle, SlimeNames.ElementLabel(element), ElementX, y + 4, 11,
                found ? ElementColor(element) : Colors.Rgb(84, 80, 76));

            DrawRightAligned(ctx, found ? $"{count}" : "-", CountRightX, y + 3, 13,
                found ? Colors.Gold : Colors.Rgb(84, 80, 76));

            if (found)
            {
                var breakdown = string.Join("  ", records!.Select(k => $"{k.Rank.Label()}:{k.Count}"));
                fonts.DrawText(r.Handle, breakdown, BreakdownX, y + 5, 9, Colors.Rgb(180, 176, 170));
            }
            else
            {
                fonts.DrawText(r.Handle, "未発見", BreakdownX, y + 5, 9, Colors.Rgb(84, 80, 76));
            }

            y += RowHeight;
        }

        fonts.DrawText(r.Handle, "[S/Esc]閉じる", PanelX + 16, PanelY + PanelH - 20, 10, Colors.Border);
    }

    private static SDL.Color ElementColor(Element element) => element switch
    {
        Element.Fire => Colors.Rgb(232, 120, 90),
        Element.Water => Colors.Rgb(110, 160, 235),
        Element.Wind => Colors.Rgb(140, 215, 130),
        Element.Earth => Colors.Rgb(200, 165, 110),
        _ => Colors.Rgb(190, 190, 195),
    };

    /// <summary>Right-aligns text so the counts form a clean column regardless of how many digits they have.</summary>
    private static void DrawRightAligned(GameContext ctx, string text, float rightX, float y, float size, SDL.Color color)
    {
        var (w, _) = ctx.Fonts.Measure(text, size);
        ctx.Fonts.DrawText(ctx.Renderer.Handle, text, rightX - w, y, size, color);
    }
}
