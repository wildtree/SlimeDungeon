using SDL3;
using SlimeDungeon.Core;
using SlimeDungeon.Domain;

namespace SlimeDungeon.UI;

/// <summary>The post-battle level-up celebration: every stat's before/after, and how much EXP the next level
/// needs. Drawn last so it sits over the arena and the status panel alike.</summary>
public static class LevelUpPopup
{
    private const float PanelW = 372f;
    private const float PanelH = 262f;

    public static void Draw(GameContext ctx, LevelUpSummary s)
    {
        var r = ctx.Renderer;

        var x = (640f - PanelW) / 2f;
        var y = (400f - PanelH) / 2f;

        // Drop shadow, panel, then a double border in gold for a "reward" feel.
        r.FillRect(x + 6, y + 6, PanelW, PanelH, Colors.Rgb(6, 6, 10));
        r.FillRect(x, y, PanelW, PanelH, Colors.Rgb(26, 24, 34));
        r.DrawRect(x, y, PanelW, PanelH, Colors.Gold);
        r.DrawRect(x + 3, y + 3, PanelW - 6, PanelH - 6, Colors.Rgb(96, 84, 40));

        var cx = x + PanelW / 2f;
        var cy = y + 16f;

        DrawCentered(ctx, "CONGRATULATIONS!", cx, cy, 19, Colors.Gold);
        cy += 28f;
        DrawCentered(ctx, $"LEVEL {s.FromLevel}  →  {s.ToLevel}", cx, cy, 15, Colors.Highlight);
        cy += 26f;

        r.FillRect(x + 24, cy, PanelW - 48, 1, Colors.Rgb(90, 84, 60));
        cy += 10f;

        // Stat rows. HP/MP show their maximums, since that is what level-up growth actually raises.
        (string Label, int Before, int After)[] rows =
        [
            ("HP ", s.Before.MaxHp, s.After.MaxHp),
            ("MP ", s.Before.MaxMp, s.After.MaxMp),
            ("STR", s.Before.Str, s.After.Str),
            ("INT", s.Before.Int, s.After.Int),
            ("DEX", s.Before.Dex, s.After.Dex),
            ("AGL", s.Before.Agl, s.After.Agl),
        ];

        foreach (var (label, before, after) in rows)
        {
            DrawStatRow(ctx, x + 44, cy, label, before, after);
            cy += 17f;
        }

        cy += 6f;
        r.FillRect(x + 24, cy, PanelW - 48, 1, Colors.Rgb(90, 84, 60));
        cy += 9f;

        var remaining = Math.Max(0, s.ExpToNext - s.Exp);
        DrawCentered(ctx, $"EXP {s.Exp}  /  次のレベルまで {remaining}", cx, cy, 12, Colors.White);
        cy += 20f;
        DrawCentered(ctx, "Enterで続ける", cx, cy, 11, Colors.Rgb(150, 145, 130));
    }

    private static void DrawStatRow(GameContext ctx, float x, float y, string label, int before, int after)
    {
        var r = ctx.Renderer;
        var fonts = ctx.Fonts;
        var delta = after - before;

        fonts.DrawText(r.Handle, label, x, y, 12, Colors.Highlight);
        // Fixed columns rather than flowing text, so the arrows and deltas line up down the list.
        fonts.DrawText(r.Handle, before.ToString(), x + 40, y, 12, Colors.White);
        fonts.DrawText(r.Handle, "→", x + 84, y, 12, Colors.Rgb(140, 140, 150));
        fonts.DrawText(r.Handle, after.ToString(), x + 108, y, 12, Colors.White);

        var text = delta > 0 ? $"+{delta}" : delta.ToString();
        var color = delta > 0 ? Colors.Rgb(120, 230, 120) : Colors.Rgb(200, 200, 200);
        fonts.DrawText(r.Handle, text, x + 156, y, 12, color);
    }

    private static void DrawCentered(GameContext ctx, string text, float centerX, float y, float size, SDL.Color color)
    {
        var (w, _) = ctx.Fonts.Measure(text, size);
        ctx.Fonts.DrawText(ctx.Renderer.Handle, text, centerX - w / 2f, y, size, color);
    }
}
