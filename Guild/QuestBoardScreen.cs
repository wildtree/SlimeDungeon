using SDL3;
using SlimeDungeon.Core;
using SlimeDungeon.Domain;
using SlimeDungeon.Graphics;
using SlimeDungeon.UI;

namespace SlimeDungeon.Guild;

public sealed class QuestBoardScreen : IScreen
{
    private int _cursor;
    private string? _message;
    private LevelUpSummary? _levelUp;
    private RankUpSummary? _rankUp;

    public void Update(GameContext ctx, float dt)
    {
        var player = ctx.Player!;
        var input = ctx.Input;

        // Reporting a quest can trigger both a promotion and a level-up. They queue up, promotion first since
        // it is the rarer and larger event, and each waits for its own keypress.
        if (_rankUp is not null)
        {
            if (MenuNav.Confirmed(input))
                _rankUp = null;
            return;
        }

        if (_levelUp is not null)
        {
            if (MenuNav.Confirmed(input))
                _levelUp = null;
            return;
        }

        if (MenuNav.Cancelled(input))
        {
            ctx.Screens.ChangeTo(new GuildScreen());
            return;
        }

        if (player.ActiveQuest is { } active)
        {
            if (input.WasPressed(SDL.Keycode.C))
            {
                player.ApplyPenalty();
                player.ActiveQuest = null;
                _message = "クエストをキャンセルした";
                return;
            }

            if (MenuNav.Confirmed(input))
                ConfirmActiveQuest(player, active);
            return;
        }

        _cursor = MenuNav.Move(input, _cursor, player.OpenQuests.Count);

        if (MenuNav.Confirmed(input) && player.OpenQuests.Count > 0)
        {
            var quest = player.OpenQuests[_cursor];
            player.OpenQuests.RemoveAt(_cursor);
            player.ActiveQuest = quest;
            _message = $"「{quest.Title}」を受注した";
        }
    }

    /// <summary>
    /// One key does the sensible thing: for a collection quest it hands over whatever you are carrying, and if
    /// that finishes the contract it is reported in the same keypress. Handed-in items leave the bag
    /// immediately, so they are forfeited if the quest later expires or is cancelled.
    /// </summary>
    private void ConfirmActiveQuest(Player player, Quest active)
    {
        if (active.IsCollection && !active.IsComplete)
        {
            var handed = active.Deliver(player);
            if (handed == 0)
            {
                _message = $"納品できる物を持っていない（残り{active.Remaining}個）";
                return;
            }
            if (!active.IsComplete)
            {
                _message = $"{handed}個納品した（残り{active.Remaining}個）";
                return;
            }
            _message = $"{handed}個納品して条件を満たした！";
        }

        if (!active.IsComplete)
        {
            _message = "まだ達成条件を満たしていない";
            return;
        }

        var exp = active.RewardExp;
        var gold = active.RewardGold;
        _levelUp = ReportQuest(player, active);
        _message = $"クエスト達成！ 報酬 {gold}G ・ EXP {exp}";
    }

    private LevelUpSummary? ReportQuest(Player player, Quest quest)
    {
        // Collected items were already taken out of the bag as they were handed in, so there is nothing left
        // to deduct here — only the rewards to pay out.
        player.Gold += quest.RewardGold;
        _rankUp = player.AddRankPoints(quest.RankPointsFor(player.Rank));
        var levelUp = player.AddExp(quest.RewardExp);
        player.ActiveQuest = null;
        return levelUp;
    }

    public void Draw(GameContext ctx)
    {
        var r = ctx.Renderer;
        r.Clear(Colors.Rgb(24, 20, 16));
        r.DrawTexture(ctx.Sprites.MenuBackdrop, 0, 0, SpriteFactory.MenuBackdropWidth, SpriteFactory.MenuBackdropHeight);
        var fonts = ctx.Fonts;
        var player = ctx.Player!;

        fonts.DrawText(r.Handle, "クエスト", 20, 16, 18, Colors.White);

        var y = 50f;
        if (player.ActiveQuest is { } active)
        {
            fonts.DrawText(r.Handle, "受注中:", 20, y, 12, Colors.Highlight);
            y += 18;
            fonts.DrawText(r.Handle, $"{active.Title} ({active.Rank.Label()}) - {active.Description}", 20, y, 11, Colors.White);
            y += 16;
            fonts.DrawText(r.Handle, $"期限: {DeadlineText(active, player)}", 20, y, 11, DeadlineColor(active, player));
            y += 15;
            fonts.DrawText(r.Handle, $"報酬: {active.RewardGold}G / EXP {active.RewardExp}", 20, y, 11, Colors.White);
            y += 20;

            DrawProgress(ctx, active, player, 20, y);
            y += 30;

            var hint = active.IsCollection && !active.IsComplete
                ? "[Enter]納品する  [C]キャンセル  [Esc]戻る"
                : "[Enter]報告する  [C]キャンセル  [Esc]戻る";
            fonts.DrawText(r.Handle, hint, 20, y, 11, Colors.Border);
        }
        else
        {
            fonts.DrawText(r.Handle, "募集中のクエスト（Enterで受注）", 20, y, 12, Colors.Highlight);
            y += 20;
            DrawQuestTable(ctx, player, y);
            fonts.DrawText(r.Handle, "[Esc]戻る", 20, 370, 11, Colors.Border);
        }

        if (_message is not null)
            fonts.DrawText(r.Handle, _message, 20, 340, 11, Colors.Gold);

        StatusPanel.Draw(ctx, 400, 0, 400);

        // Promotion is announced first and fully replaces the screen; the level-up summary follows.
        if (_rankUp is { } rankUp)
            RankUpPopup.Draw(ctx, rankUp);
        else if (_levelUp is { } levelUp)
            LevelUpPopup.Draw(ctx, levelUp);
    }

    // Column origins for the quest table. Fixed positions rather than flowing text, so every field lines up
    // down the board and a row can be compared with the one above it at a glance.
    private const float ColIcon = 20f;
    private const float ColCategory = 40f;
    private const float ColRank = 108f;
    private const float ColDetail = 140f;
    private const float ColDeadline = 226f;
    private const float ColRewardRight = 392f;
    private const float TableRowHeight = 20f;

    /// <summary>
    /// The board as a table: kind of work (with an icon), rank, what exactly is wanted, when it is due, and
    /// what it pays. Previously each quest was two lines of free-flowing prose that repeated the rank and never
    /// lined up, so the four openings could not be compared without reading every word.
    /// </summary>
    private void DrawQuestTable(GameContext ctx, Player player, float top)
    {
        var r = ctx.Renderer;
        var fonts = ctx.Fonts;

        var headerColor = Colors.Rgb(150, 142, 128);
        fonts.DrawText(r.Handle, "種別", ColCategory, top, 9, headerColor);
        fonts.DrawText(r.Handle, "ランク", ColRank, top, 9, headerColor);
        fonts.DrawText(r.Handle, "内容", ColDetail, top, 9, headerColor);
        fonts.DrawText(r.Handle, "期日", ColDeadline, top, 9, headerColor);
        DrawRight(ctx, "報酬", ColRewardRight, top, 9, headerColor);

        var y = top + 13f;
        r.FillRect(16, y, 380, 1, Colors.Rgb(96, 88, 72));
        y += 5f;

        for (var i = 0; i < player.OpenQuests.Count; i++)
        {
            var q = player.OpenQuests[i];
            var selected = i == _cursor;

            if (selected)
                r.FillRect(16, y - 2, 380, TableRowHeight - 2, Colors.Highlight);
            else if ((i & 1) == 0)
                r.FillRect(16, y - 2, 380, TableRowHeight - 2, Colors.Rgb(30, 26, 22));

            var text = selected ? Colors.Black : Colors.White;

            var icon = q.IsCollection ? ctx.Sprites.QuestGatherIcon : ctx.Sprites.QuestSlayIcon;
            r.DrawTexture(icon, ColIcon, y + 1, 15, 15);

            fonts.DrawText(r.Handle, q.CategoryLabel, ColCategory, y + 2, 10, text);
            fonts.DrawText(r.Handle, q.Rank.Label(), ColRank, y + 1, 11,
                selected ? Colors.Black : RankColor(q.Rank, player.Rank));
            fonts.DrawText(r.Handle, q.DetailLabel, ColDetail, y + 2, 10, text);

            // The month and day without the era, which the days-remaining count disambiguates anyway — the
            // full date does not fit alongside the reward column.
            var due = GameCalendar.FromDayNumber(q.DeadlineDay);
            var left = q.DeadlineDay - player.DayCount;
            fonts.DrawText(r.Handle, $"{due.MonthName}{due.Day}日 (あと{left}日)", ColDeadline, y + 4, 8,
                selected ? Colors.Rgb(60, 50, 20) : DeadlineColor(q, player));

            DrawRight(ctx, $"{q.RewardGold}G / EXP{q.RewardExp}", ColRewardRight, y + 2, 9,
                selected ? Colors.Black : Colors.Gold);

            y += TableRowHeight;
        }

        // The rank colour is what tells you whether a job counts toward promotion, so say so once.
        y += 8;
        fonts.DrawText(r.Handle, "※ランクが金色の依頼は昇格ポイント(2pt)になります", 20, y, 9, Colors.Rgb(150, 142, 128));
    }

    /// <summary>Quests above your own rank are the ones that advance you, so they are worth spotting.</summary>
    private static SDL.Color RankColor(Rank questRank, Rank playerRank) => questRank switch
    {
        _ when questRank > playerRank => Colors.Rgb(255, 200, 110),
        _ when questRank == playerRank => Colors.White,
        _ => Colors.Rgb(140, 136, 130),
    };

    private static void DrawRight(GameContext ctx, string text, float rightX, float y, float size, SDL.Color color)
    {
        var (w, _) = ctx.Fonts.Measure(text, size);
        ctx.Fonts.DrawText(ctx.Renderer.Handle, text, rightX - w, y, size, color);
    }

    /// <summary>Deadline as a world-calendar date plus the days left, which is what actually drives the
    /// decision to accept a job.</summary>
    private static string DeadlineText(Quest q, Player player)
    {
        var left = q.DeadlineDay - player.DayCount;
        var suffix = left switch
        {
            < 0 => "期限切れ",
            0 => "本日まで",
            _ => $"あと{left}日",
        };
        return $"{GameCalendar.FormatShort(q.DeadlineDay)}（{suffix}）";
    }

    private static SDL.Color DeadlineColor(Quest q, Player player) => (q.DeadlineDay - player.DayCount) switch
    {
        < 0 => Colors.HpBar,
        <= 3 => Colors.Rgb(240, 170, 90),
        _ => Colors.White,
    };

    /// <summary>
    /// Progress as a labelled bar plus counts. Collection quests also show how many of the required item are
    /// in the bag right now, so it is obvious how much this trip to the counter will hand over.
    /// </summary>
    private static void DrawProgress(GameContext ctx, Quest q, Player player, float x, float y)
    {
        var r = ctx.Renderer;
        var fonts = ctx.Fonts;

        var label = q.IsCollection ? "納品済み" : "討伐数";
        fonts.DrawText(r.Handle, label, x, y, 11, Colors.Highlight);

        var counts = $"{q.Progress} / {q.TargetCount}";
        fonts.DrawText(r.Handle, counts, x + 66, y, 12, q.IsComplete ? Colors.Gold : Colors.White);

        const float barX = 140f;
        const float barW = 180f;
        r.FillRect(barX - 1, y + 1, barW + 2, 12, Colors.Rgb(12, 12, 16));
        r.FillRect(barX, y + 2, barW, 10, Colors.BarBg);
        var frac = q.TargetCount > 0 ? Math.Clamp(q.Progress / (float)q.TargetCount, 0f, 1f) : 0f;
        if (frac > 0)
            r.FillRect(barX, y + 2, Math.Max(1f, barW * frac), 10, q.IsComplete ? Colors.Gold : Colors.ExpBar);

        if (q.IsComplete)
        {
            fonts.DrawText(r.Handle, "達成！報告できます", barX + barW + 10, y + 1, 11, Colors.Gold);
            return;
        }

        fonts.DrawText(r.Handle, $"残り{q.Remaining}", barX + barW + 10, y + 1, 11, Colors.White);

        if (q.IsCollection)
        {
            var inBag = q.DeliverableInBag(player);
            var text = inBag > 0
                ? $"鞄に{inBag}個 → 納品できます"
                : "鞄に該当する物がありません";
            fonts.DrawText(r.Handle, text, x, y + 16, 10, inBag > 0 ? Colors.Rgb(140, 220, 140) : Colors.Border);
        }
    }
}
