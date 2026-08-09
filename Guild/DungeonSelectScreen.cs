using SlimeDungeon.Core;
using SlimeDungeon.Domain;
using SlimeDungeon.Dungeon;
using SlimeDungeon.UI;

namespace SlimeDungeon.Guild;

/// <summary>
/// The mouth of the dungeon: a place on the map now rather than a menu hanging off the guild counter.
///
/// It behaves like the three shops. The picture is what you see on arrival, confirm opens the list of dungeons
/// that will let you in, and the travel menu takes you back to town. Climbing the stairs out of a dungeon
/// returns you here rather than to the guild, so a second trip is one press away and reporting to the guild is
/// a decision rather than something that happens to you.
/// </summary>
public sealed class DungeonSelectScreen : IScreen
{
    private int _cursor = -1;

    /// <summary>False until asked for, so standing at the entrance shows the entrance.</summary>
    private bool _menuOpen;

    private readonly TravelMenu _travel = new();

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

        if (_travel.Update(ctx, Place.Dungeon))
            return;

        if (!_menuOpen)
        {
            if (MenuNav.MenuRequested(input) || MenuNav.Confirmed(input))
                _menuOpen = true;
            return;
        }

        if (MenuNav.Cancelled(input) || MenuNav.MenuRequested(input))
        {
            _menuOpen = false;
            return;
        }

        _cursor = MenuNav.Move(input, _cursor, ranks.Count);

        if (MenuNav.Confirmed(input))
        {
            var rank = ranks[_cursor];
            var element = DungeonGenerator.RollDungeonElement();
            var map = DungeonGenerator.Generate(rank, element);
            player.Counters.DungeonVisits++;

            // The line the trip is measured from. Nothing that happens underground is written until the stairs
            // are climbed, so this is the state a quit inside the dungeon falls back to — which is the whole
            // reason to commit it here rather than trusting the last visit to the guild.
            Data.SaveManager.Save(player);

            // Back out to this same spot, not to the guild.
            ctx.Screens.ChangeTo(new DungeonScreen(map, new DungeonSelectScreen()));
        }
    }

    public void Draw(GameContext ctx)
    {
        var r = ctx.Renderer;
        var fonts = ctx.Fonts;
        var player = ctx.Player!;

        // The picture letters "ダンジョン入口" across its own crest, so the heading is only for the fallback.
        if (!ShopRoom.DrawBackdrop(ctx, ctx.Sprites.DungeonEntranceBackdrop))
            fonts.DrawText(r.Handle, "ダンジョン入口", 20, 16, 18, Colors.White);

        if (!_menuOpen)
        {
            ShopRoom.DrawPrompt(ctx, "もぐりますか");
            StatusPanel.Draw(ctx, ShopRoom.Size, 0, 400);
            _travel.Draw(ctx, Place.Dungeon);
            return;
        }

        var ranks = AvailableRanks(player);
        var cursor = _cursor < 0 ? DefaultCursor(player, ranks) : _cursor;

        // The ladder runs to ten rows at SS, so the sheet is sized from the list and the note underneath it.
        const float rowStep = 19f;
        var top = ShopRoom.Draw(ctx, 24f + ranks.Count * rowStep + 22f + ShopRoom.FooterHeight);
        var x = ShopRoom.ContentX;

        fonts.DrawText(r.Handle, "挑戦するダンジョンを選ぶ", x, top, 12, Colors.Highlight);

        var labels = ranks.Select(rk => $"{rk.Label(),-2}ランク ダンジョン").ToArray();
        var maxWidth = MenuNav.MaxLabelWidth(ctx, labels, 12);
        var y = top + 24f;
        for (var i = 0; i < ranks.Count; i++)
        {
            MenuNav.DrawRow(ctx, x + 8, y, maxWidth + 12, 17, labels[i], 12, i == cursor);
            y += rowStep;
        }

        fonts.DrawText(r.Handle, "格下のダンジョンにもいつでも入れる（スライムも宝も相応に弱い）。",
            x, y + 4, 10, Colors.Border);

        ShopRoom.DrawFooter(ctx, null,
            ControlHints.Direction("選ぶ"), ControlHints.Confirm("もぐる"), ControlHints.Cancel("戻る"));

        StatusPanel.Draw(ctx, ShopRoom.Size, 0, 400);
        _travel.Draw(ctx, Place.Dungeon);
    }
}
