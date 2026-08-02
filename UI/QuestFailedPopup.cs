using SDL3;
using SlimeDungeon.Core;
using SlimeDungeon.Domain;

namespace SlimeDungeon.UI;

/// <summary>
/// The notice waiting at the counter when a contract's deadline has passed.
///
/// This used to happen in complete silence: the quest vanished, a hidden counter ticked up, and three of them
/// later the character was demoted for reasons nothing on screen had ever mentioned. Everything a failure costs
/// is stated here, once, in the place the player is standing when it is applied.
/// </summary>
public static class QuestFailedPopup
{
    private const float PanelW = 380f;

    public static void Draw(GameContext ctx, QuestFailure failure)
    {
        var r = ctx.Renderer;
        var fonts = ctx.Fonts;

        // Rows are only drawn when they carry news, so the panel is sized to what actually happened.
        var rows = BuildRows(failure);
        var panelH = 108f + rows.Count * 20f + (failure.DemotedTo is null ? 0f : 26f);
        var x = (640f - PanelW) / 2f;
        var y = (400f - panelH) / 2f;

        r.FillRect(0, 0, 640, 400, Colors.Rgb(0, 0, 0, 140));

        r.FillRect(x + 6, y + 6, PanelW, panelH, Colors.Rgb(6, 6, 8));
        r.FillRect(x, y, PanelW, panelH, Colors.Rgb(34, 24, 24));
        r.DrawRect(x, y, PanelW, panelH, Colors.HpBar);
        r.DrawRect(x + 3, y + 3, PanelW - 6, panelH - 6, Colors.Rgb(110, 50, 50));

        var cx = x + PanelW / 2f;
        DrawCentered(ctx, "依頼失敗", cx, y + 14, 19, Colors.Rgb(240, 110, 110));
        DrawCentered(ctx, "期日までに達成できませんでした", cx, y + 40, 11, Colors.Rgb(200, 180, 180));

        DrawCentered(ctx, $"「{failure.QuestTitle}」（{failure.QuestRank.Label()}）", cx, y + 60, 12, Colors.White);

        r.FillRect(x + 24, y + 82, PanelW - 48, 1, Colors.Rgb(110, 70, 70));

        // The consequences, as a two-column ledger so the numbers line up under each other.
        var ry = y + 90f;
        foreach (var (label, value, color) in rows)
        {
            fonts.DrawText(r.Handle, label, x + 30, ry, 11, Colors.Rgb(200, 190, 190));
            var (vw, _) = fonts.Measure(value, 12);
            fonts.DrawText(r.Handle, value, x + PanelW - 30 - vw, ry - 1, 12, color);
            ry += 20f;
        }

        if (failure.DemotedTo is { } demoted)
        {
            ry += 2f;
            DrawCentered(ctx, $"ランクが {demoted.Label()} に下がりました", cx, ry, 13, Colors.Rgb(240, 110, 110));
            ry += 24f;
        }

        ControlHints.DrawCentered(ctx, cx, y + panelH - 20, 9, Colors.Rgb(170, 160, 160),
            ControlHints.Confirm("続ける"));
    }

    private static List<(string Label, string Value, SDL.Color Color)> BuildRows(QuestFailure failure)
    {
        var rows = new List<(string, string, SDL.Color)>();

        if (failure.RankPointsLost > 0)
            rows.Add(("昇格ポイント", $"-{failure.RankPointsLost}", Colors.Rgb(240, 150, 110)));

        if (failure.Fine > 0)
        {
            // When the purse could not cover it, say so rather than quietly charging less than was owed.
            var value = failure.CouldNotPayInFull
                ? $"-{failure.FinePaid}G（{failure.Fine}Gのうち）"
                : $"-{failure.FinePaid}G";
            rows.Add(("違約金", value, Colors.Rgb(240, 150, 110)));
        }

        // Only meaningful while the black marks are still accumulating; once one has cost a rank the counter
        // has reset and the demotion line below says the real news.
        if (failure.DemotedTo is null)
            rows.Add(("ペナルティ", $"{failure.PenaltyCount} / {Player.PenaltiesBeforeDemotion}",
                failure.PenaltyCount >= Player.PenaltiesBeforeDemotion - 1
                    ? Colors.Rgb(240, 110, 110)
                    : Colors.White));

        return rows;
    }

    private static void DrawCentered(GameContext ctx, string text, float centerX, float y, float size, SDL.Color color)
    {
        var (w, _) = ctx.Fonts.Measure(text, size);
        ctx.Fonts.DrawText(ctx.Renderer.Handle, text, centerX - w / 2f, y, size, color);
    }
}
