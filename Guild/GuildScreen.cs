using SDL3;
using SlimeDungeon.Core;
using SlimeDungeon.Data;
using SlimeDungeon.Domain;
using SlimeDungeon.Graphics;
using SlimeDungeon.UI;

namespace SlimeDungeon.Guild;

/// <summary>The guild hub: quest board, shop, potion crafting, healing, and dungeon select all branch from here.</summary>
public sealed class GuildScreen : IScreen
{
    private int _cursor;
    private string? _message;

    public void OnEnter(GameContext ctx)
    {
        var player = ctx.Player!;

        if (player.ActiveQuest is { } quest && quest.IsExpired(player.DayCount))
        {
            player.ApplyPenalty();
            player.ActiveQuest = null;
        }

        QuestFactory.RefillExpiredBoardSlots(player);
        _cursor = 0;
        SaveManager.Save(player);
    }

    /// <summary>Full HP/MP restore costs more as the character levels up (and can afford it) — a flat
    /// 10G per level keeps it cheap early on without staying trivial once stats have grown a lot.</summary>
    private static int HealCost(Player player) => player.Level * 10;

    private static string[] BuildMenuLabels(Player player) =>
    [
        "クエスト",
        "ショップ",
        "ポーション調合",
        "ダンジョンへ",
        $"回復 ({HealCost(player)}G)",
        "討伐記録",
    ];

    public void Update(GameContext ctx, float dt)
    {
        var input = ctx.Input;
        var player = ctx.Player!;
        var labels = BuildMenuLabels(player);
        _cursor = MenuNav.Move(input, _cursor, labels.Length);

        if (!MenuNav.Confirmed(input))
            return;

        switch (_cursor)
        {
            case 0: ctx.Screens.ChangeTo(new QuestBoardScreen()); break;
            case 1: ctx.Screens.ChangeTo(new ShopScreen()); break;
            case 2: ctx.Screens.ChangeTo(new PotionCraftScreen()); break;
            case 3: ctx.Screens.ChangeTo(new DungeonSelectScreen()); break;
            case 4: HandleHeal(player); break;
            case 5: ctx.ShowKillLog = true; break;
        }
    }

    private void HandleHeal(Player player)
    {
        if (player.Stats.Hp >= player.Stats.MaxHp && player.Stats.Mp >= player.Stats.MaxMp)
        {
            _message = "すでに元気だ";
            return;
        }

        var cost = HealCost(player);
        if (player.Gold < cost)
        {
            _message = "所持金が足りない";
            return;
        }

        player.Gold -= cost;
        player.Stats.Hp = player.Stats.MaxHp;
        player.Stats.Mp = player.Stats.MaxMp;
        _message = $"{cost}Gで全快した";
    }

    public void Draw(GameContext ctx)
    {
        var r = ctx.Renderer;
        r.Clear(Colors.Rgb(24, 20, 16));
        var fonts = ctx.Fonts;
        var player = ctx.Player!;
        var labels = BuildMenuLabels(player);

        GuildRoom.Draw(ctx, player.DayCount);

        // The command list as a sheet of guild business lying on the counter, rather than a dark panel floating
        // over the scene. Drawn at runtime rather than baked, because the other screens that share this
        // backdrop (registration, promotion) have no menu on the counter.
        const float rowHeight = 23f;
        var maxWidth = MenuNav.MaxLabelWidth(ctx, labels, 13);
        var sheetX = 14f;
        var sheetW = Math.Max(168f, maxWidth + 34f);
        var sheetH = labels.Length * rowHeight + 34f;
        var sheetY = 396f - sheetH;

        var paper = Colors.Rgb(232, 218, 186);
        var paperEdge = Colors.Rgb(196, 178, 142);
        var paperShade = Colors.Rgb(214, 198, 164);
        var ink = Colors.Rgb(62, 46, 30);

        r.FillRect(sheetX + 3, sheetY + 3, sheetW, sheetH, Colors.Rgb(96, 62, 36));
        r.FillRect(sheetX, sheetY, sheetW, sheetH, paper);
        r.FillRect(sheetX, sheetY, sheetW, 1, Colors.Rgb(246, 236, 210));
        r.FillRect(sheetX, sheetY + sheetH - 2, sheetW, 2, paperShade);
        r.DrawRect(sheetX, sheetY, sheetW, sheetH, paperEdge);

        fonts.DrawText(r.Handle, "ご用件", sheetX + 10, sheetY + 7, 10, Colors.Rgb(122, 96, 64));
        r.FillRect(sheetX + 10, sheetY + 21, sheetW - 20, 1, paperEdge);

        var y = sheetY + 27f;
        for (var i = 0; i < labels.Length; i++)
        {
            var selected = i == _cursor;
            if (selected)
            {
                r.FillRect(sheetX + 6, y - 3, sheetW - 12, rowHeight - 2, Colors.Rgb(206, 168, 88));
                r.FillRect(sheetX + 6, y - 3, 3, rowHeight - 2, Colors.Rgb(148, 106, 44));
            }
            fonts.DrawText(r.Handle, labels[i], sheetX + 14, y, 13, ink);
            y += rowHeight;
        }

        if (_message is not null)
            fonts.DrawText(r.Handle, _message, sheetX + 2, sheetY - 16, 11, Colors.Gold);

        StatusPanel.Draw(ctx, 400, 0, 400);
    }
}
