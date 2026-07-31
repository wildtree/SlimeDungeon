using SlimeDungeon.Core;
using SlimeDungeon.Domain;
using SlimeDungeon.Dungeon;
using SlimeDungeon.Graphics;
using SlimeDungeon.UI;

namespace SlimeDungeon.Guild;

public sealed class DungeonSelectScreen : IScreen
{
    private int _cursor;

    private static List<Rank> AvailableRanks(Player player)
    {
        var ranks = new List<Rank> { player.Rank };
        if (player.Rank != Rank.SS)
            ranks.Add(RankExtensions.Clamp((int)player.Rank + 1));
        return ranks;
    }

    public void Update(GameContext ctx, float dt)
    {
        var input = ctx.Input;
        var player = ctx.Player!;
        var ranks = AvailableRanks(player);

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

        fonts.DrawText(r.Handle, "挑戦するダンジョンを選ぶ", 20, 16, 16, Colors.White);

        var labels = ranks.Select(rk => $"{rk.Label()}ランク ダンジョン").ToArray();
        var maxWidth = MenuNav.MaxLabelWidth(ctx, labels, 14);
        var y = 70f;
        for (var i = 0; i < ranks.Count; i++)
        {
            MenuNav.DrawRow(ctx, 20, y, maxWidth, 20, labels[i], 14, i == _cursor);
            y += 24;
        }

        fonts.DrawText(r.Handle, "12x12の一画面ダンジョン。入るたびに自動生成される。", 20, 200, 11, Colors.Border);
        fonts.DrawText(r.Handle, "[Esc]戻る", 20, 370, 11, Colors.Border);

        StatusPanel.Draw(ctx, 400, 0, 400);
    }
}
