using SDL3;
using SlimeDungeon.Core;
using SlimeDungeon.Domain;
using SlimeDungeon.Graphics;

namespace SlimeDungeon.UI;

/// <summary>
/// The guild announcing a promotion. Presented as a full-screen moment with the receptionist rather than a
/// small toast, because a rank-up is rare and easy to miss — it used to happen with no notification at all.
/// </summary>
public static class RankUpPopup
{
    public static void Draw(GameContext ctx, RankUpSummary summary)
    {
        var r = ctx.Renderer;
        var fonts = ctx.Fonts;

        r.FillRect(0, 0, 640, 400, Colors.Rgb(14, 11, 9));
        GuildRoom.Draw(ctx, ctx.Player?.DayCount ?? GameCalendar.Today());

        // Right-hand panel carries the announcement itself.
        const float panelX = 404f;
        const float panelW = 232f;
        r.FillRect(panelX, 14, panelW, 268, Colors.Rgb(26, 22, 16));
        r.DrawRect(panelX, 14, panelW, 268, Colors.Gold);
        r.DrawRect(panelX + 3, 17, panelW - 6, 262, Colors.Rgb(104, 88, 40));

        var cx = panelX + panelW / 2f;

        DrawCentered(ctx, "ランクアップ！", cx, 30, 19, Colors.Gold);

        // The rank transition, with the new rank given the most weight on the screen.
        var fromText = summary.From.Label();
        var toText = summary.To.Label();
        var (fromW, _) = fonts.Measure(fromText, 26);
        var (arrowW, _) = fonts.Measure("→", 20);
        var (toW, _) = fonts.Measure(toText, 44);

        const float rowY = 76f;
        var totalW = fromW + 12 + arrowW + 14 + toW;
        var x = cx - totalW / 2f;

        fonts.DrawText(r.Handle, fromText, x, rowY + 20, 26, Colors.Rgb(150, 145, 135));
        x += fromW + 12;
        fonts.DrawText(r.Handle, "→", x, rowY + 26, 20, Colors.Rgb(190, 180, 150));
        x += arrowW + 14;
        fonts.DrawText(r.Handle, toText, x, rowY, 44, Colors.Gold);

        DrawCentered(ctx, $"{summary.To.Label()}ランク冒険者", cx, rowY + 66, 12, Colors.White);

        r.FillRect(panelX + 20, 172, panelW - 40, 1, Colors.Rgb(104, 88, 40));

        // What the promotion actually unlocks.
        DrawCentered(ctx, "解禁されたもの", cx, 182, 11, Colors.Highlight);
        if (summary.IsTop)
        {
            DrawCentered(ctx, "最高位に到達！", cx, 204, 13, Colors.Gold);
            DrawCentered(ctx, "すべての依頼とダンジョン", cx, 226, 11, Colors.White);
        }
        else
        {
            DrawCentered(ctx, $"依頼: {summary.Ceiling.Label()}ランクまで", cx, 204, 12, Colors.White);
            DrawCentered(ctx, $"ダンジョン: {summary.Ceiling.Label()}ランクまで", cx, 226, 12, Colors.White);
        }

        DrawCentered(ctx, "Enterで続ける", cx, 258, 10, Colors.Rgb(150, 145, 130));

        // The receptionist's line, in the same speech box style the registration desk uses.
        const float boxX = 16f, boxY = 300f, boxW = 608f, boxH = 84f;
        r.FillRect(boxX, boxY, boxW, boxH, Colors.Rgb(20, 16, 12));
        r.DrawRect(boxX, boxY, boxW, boxH, Colors.Border);
        fonts.DrawText(r.Handle, "受付嬢", boxX + 12, boxY + 8, 12, Colors.Highlight);

        var line1 = $"おめでとうございます！ {summary.To.Label()}ランクに昇格されました。";
        var line2 = summary.IsTop
            ? "ここが冒険者の頂点です。すべての依頼とダンジョンにご案内できます。"
            : $"これから{summary.Ceiling.Label()}ランクまでの依頼を受けられ、{summary.Ceiling.Label()}ランクまでのダンジョンに入れます。";

        fonts.DrawText(r.Handle, line1, boxX + 14, boxY + 30, 13, Colors.White);
        fonts.DrawText(r.Handle, line2, boxX + 14, boxY + 54, 12, Colors.White);
    }

    private static void DrawCentered(GameContext ctx, string text, float centerX, float y, float size, SDL.Color color)
    {
        var (w, _) = ctx.Fonts.Measure(text, size);
        ctx.Fonts.DrawText(ctx.Renderer.Handle, text, centerX - w / 2f, y, size, color);
    }
}
