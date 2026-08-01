using SDL3;
using SlimeDungeon.Core;
using SlimeDungeon.Data;
using SlimeDungeon.Domain;
using SlimeDungeon.Graphics;
using SlimeDungeon.UI;

namespace SlimeDungeon.Guild;

/// <summary>
/// The guild's roll of past adventurers. Everyone who has died is in the register; this shows the ten the
/// guild thinks best of, ordered the way a guild would judge: the rank you reached first, then the level
/// behind it, then what you actually killed, then how long you lasted, and only then what you were carrying.
/// </summary>
public sealed class RecordsScreen : IScreen
{
    private const int TopN = 10;
    private const float RowHeight = 28f;

    private List<HistoryEntry> _ranked = new();
    private int _scroll;

    public void OnEnter(GameContext ctx) => _ranked = Ranked();

    /// <summary>
    /// Rank, then level, then kills, then days, then gold — each a tiebreaker for the one before it, in the
    /// order the guild was asked to weigh them.
    /// </summary>
    private static List<HistoryEntry> Ranked() =>
        SaveManager.LoadHistory()
            .OrderByDescending(e => (int)e.ReachedRank)
            .ThenByDescending(e => e.Level)
            .ThenByDescending(e => e.TotalKills)
            .ThenByDescending(e => e.DaysSurvived)
            .ThenByDescending(e => e.Gold)
            .Take(TopN)
            .ToList();

    public void Update(GameContext ctx, float dt)
    {
        var input = ctx.Input;

        if (MenuNav.Cancelled(input) || MenuNav.Confirmed(input))
        {
            ctx.Screens.ChangeTo(new GuildScreen());
            return;
        }

        const int visible = 6;
        if (MenuNav.Down(input) && _scroll < Math.Max(0, _ranked.Count - visible))
            _scroll++;
        if (MenuNav.Up(input) && _scroll > 0)
            _scroll--;
    }

    public void Draw(GameContext ctx)
    {
        var r = ctx.Renderer;
        var fonts = ctx.Fonts;

        r.Clear(Colors.Rgb(24, 20, 16));
        r.DrawTexture(ctx.Sprites.MenuBackdrop, 0, 0, SpriteFactory.MenuBackdropWidth, SpriteFactory.MenuBackdropHeight);

        fonts.DrawText(r.Handle, "冒険者の記録", 20, 12, 18, Colors.White);
        fonts.DrawText(r.Handle, $"ギルド登録者 {SaveManager.LoadHistory().Count}名", 20, 36, 10, Colors.Border);

        if (_ranked.Count == 0)
        {
            fonts.DrawText(r.Handle, "まだ記録がありません。", 20, 70, 12, Colors.Highlight);
            fonts.DrawText(r.Handle, $"[{MenuNav.CancelHint(ctx.Input)}]戻る", 20, 372, 10, Colors.Border);
            StatusPanel.Draw(ctx, 400, 0, 400);
            return;
        }

        // Two lines per adventurer: the headline on top, the details beneath.
        r.FillRect(16, 52, 380, 1, Colors.Rgb(96, 88, 72));

        const int visible = 6;
        var y = 58f;
        for (var i = _scroll; i < Math.Min(_ranked.Count, _scroll + visible); i++)
        {
            var e = _ranked[i];
            var place = i + 1;

            // The top three get a coloured numeral; everything else is plain.
            var placeColor = place switch
            {
                1 => Colors.Rgb(232, 194, 90),
                2 => Colors.Rgb(198, 202, 210),
                3 => Colors.Rgb(196, 142, 92),
                _ => Colors.Rgb(130, 124, 114),
            };
            fonts.DrawText(r.Handle, $"{place}", 22, y + 2, 15, placeColor);

            fonts.DrawText(r.Handle, e.Name, 46, y, 13, Colors.White);
            var (nameW, _) = fonts.Measure(e.Name, 13);
            fonts.DrawText(r.Handle, GenderLabel(e.Gender), 50 + nameW, y + 4, 9, Colors.Rgb(150, 145, 136));

            RightAligned(ctx, $"{e.ReachedRank.Label()}ランク", 260, y + 1, 12, Colors.Highlight);
            RightAligned(ctx, $"LV {e.Level}", 320, y + 1, 12, Colors.White);
            RightAligned(ctx, $"討伐 {e.TotalKills}", 392, y + 1, 12, Colors.White);

            var registered = e.StartDay > 0
                ? $"登録 {GameCalendar.FromDayNumber(e.StartDay).MonthName}{GameCalendar.FromDayNumber(e.StartDay).Day}日"
                : "登録 —";
            var gold = e.Gold >= 0 ? $"所持金 {e.Gold}G" : "所持金 —";
            fonts.DrawText(r.Handle, $"{registered}    {e.DaysSurvived}日生存    {gold}",
                46, y + 15, 9, Colors.Rgb(150, 145, 136));

            y += RowHeight;
        }

        if (_ranked.Count > visible)
            fonts.DrawText(r.Handle, $"[↑↓] {_scroll + 1}-{Math.Min(_ranked.Count, _scroll + visible)}/{_ranked.Count}",
                20, 344, 9, Colors.Border);

        fonts.DrawText(r.Handle,
            $"順位: 到達ランク > レベル > 討伐数 > 生存日数 > 所持金", 20, 358, 9, Colors.Border);
        fonts.DrawText(r.Handle, $"[{MenuNav.CancelHint(ctx.Input)}]戻る", 20, 374, 10, Colors.Border);

        StatusPanel.Draw(ctx, 400, 0, 400);
    }

    private static string GenderLabel(Gender? gender) => gender switch
    {
        Core.Gender.Male => "（男）",
        Core.Gender.Female => "（女）",
        _ => "",
    };

    private static void RightAligned(GameContext ctx, string text, float right, float y, float size, SDL.Color color)
    {
        var (w, _) = ctx.Fonts.Measure(text, size);
        ctx.Fonts.DrawText(ctx.Renderer.Handle, text, right - w, y, size, color);
    }

    public void OnExit(GameContext ctx) { }
}
