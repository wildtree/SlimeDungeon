using SlimeDungeon.Core;
using SlimeDungeon.Domain;
using SlimeDungeon.Dungeon;
using SlimeDungeon.Graphics;
using SlimeDungeon.UI;

namespace SlimeDungeon.Guild;

public sealed class DungeonSelectScreen : IScreen
{
    private int _cursor = -1;

    /// <summary>
    /// Every dungeon the guild will let you through: one rank above your own as the stretch, your own rank,
    /// and everything below it. Easier dungeons hold weaker slimes and poorer chests, so there is nothing to
    /// exploit by dropping down — it is there for gathering quests, for hunting a colour that favours a
    /// particular element, or simply for a quiet trip. Listed hardest first.
    /// </summary>
    private static List<Rank> AvailableRanks(Player player)
    {
        var top = player.Rank == Rank.SS ? (int)Rank.SS : (int)player.Rank + 1;
        var ranks = new List<Rank>();
        for (var r = top; r >= (int)Rank.H; r--)
            ranks.Add(RankExtensions.Clamp(r));
        return ranks;
    }

    /// <summary>Where the cursor sits on arrival: the player's own rank, not the dangerous one above it.</summary>
    private static int DefaultCursor(Player player, List<Rank> ranks)
    {
        var own = ranks.IndexOf(player.Rank);
        return own >= 0 ? own : 0;
    }

    public void Update(GameContext ctx, float dt)
    {
        var input = ctx.Input;
        var player = ctx.Player!;
        var ranks = AvailableRanks(player);

        if (_cursor < 0)
            _cursor = DefaultCursor(player, ranks);

        if (MenuNav.Cancelled(input))
        {
            ctx.Screens.ChangeTo(new GuildScreen());
            return;
        }

        _cursor = MenuNav.Move(input, _cursor, ranks.Count);

        if (MenuNav.Confirmed(input))
        {
            var rank = ranks[_cursor];
            var element = DungeonGenerator.RollDungeonElement();
            var map = DungeonGenerator.Generate(rank, element);
            player.Counters.DungeonVisits++;
            ctx.Screens.ChangeTo(new DungeonScreen(map, new GuildScreen()));
        }
    }

    public void Draw(GameContext ctx)
    {
        var r = ctx.Renderer;
        r.Clear(Colors.Rgb(24, 20, 16));
        r.DrawTexture(ctx.Sprites.MenuBackdrop, 0, 0, SpriteFactory.MenuBackdropWidth, SpriteFactory.MenuBackdropHeight);
        var fonts = ctx.Fonts;
        var player = ctx.Player!;
        var ranks = AvailableRanks(player);
        var cursor = _cursor < 0 ? DefaultCursor(player, ranks) : _cursor;

        fonts.DrawText(r.Handle, "挑戦するダンジョンを選ぶ", 20, 16, 16, Colors.White);

        // The whole ladder up to rank+1 can be ten rows at SS, so the rows are tight enough that the longest
        // list still clears the notes at the foot of the screen.
        var labels = ranks.Select(rk => $"{rk.Label(),-2}ランク ダンジョン").ToArray();
        var maxWidth = MenuNav.MaxLabelWidth(ctx, labels, 14);
        var y = 52f;
        for (var i = 0; i < ranks.Count; i++)
        {
            MenuNav.DrawRow(ctx, 20, y, maxWidth, 20, labels[i], 14, i == cursor);
            y += 22;
        }

        y += 8;
        fonts.DrawText(r.Handle, "12x12の一画面ダンジョン。入るたびに自動生成される。", 20, y, 11, Colors.Border);
        fonts.DrawText(r.Handle, "格下のダンジョンにもいつでも入れる（スライムも宝も相応に弱い）。", 20, y + 16, 11, Colors.Border);
        fonts.DrawText(r.Handle, $"[{MenuNav.Hint.Direction}]選ぶ  [{MenuNav.Hint.Confirm}]もぐる  [{MenuNav.Hint.Cancel}]戻る", 20, 370, 11, Colors.Border);

        StatusPanel.Draw(ctx, 400, 0, 400);
    }
}
