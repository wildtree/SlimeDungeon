using SlimeDungeon.Core;
using SlimeDungeon.Data;
using SlimeDungeon.Domain;
using SlimeDungeon.Graphics;
using SlimeDungeon.UI;

namespace SlimeDungeon.Guild;

/// <summary>
/// The guild's display wall: somewhere to put the things that took a season to make.
///
/// Cases are bought with gold and nothing else, and mounting a piece is free and reversible. That asymmetry is
/// deliberate — the wall exists to absorb money that has nowhere else to go, so the cost has to be in the case
/// rather than in the trophy, or filling it would feel like paying twice for the same sword.
/// </summary>
public sealed class TrophyHallScreen : IScreen
{
    private enum Phase { Wall, PickTrophy }

    private Phase _phase = Phase.Wall;
    private int _cursor;
    private int _pickCursor;
    private string? _message;

    /// <summary>Which case the player is filling, while the bag list is up.</summary>
    private int _fillingCase;

    /// <summary>Rows on the wall: one per case owned, then the "buy another" row when there is room for one.</summary>
    private static int RowCount(Player player) =>
        player.TrophyCases + (player.TrophyCases < TrophyHall.MaxCases ? 1 : 0);

    private static bool IsBuyRow(Player player, int row) => row >= player.TrophyCases;

    /// <summary>What in the bag is worth mounting.</summary>
    private static List<Item> Displayable(Player player) =>
        player.Bag.Where(TrophyHall.CanDisplay).ToList();

    public void Update(GameContext ctx, float dt)
    {
        var input = ctx.Input;
        var player = ctx.Player!;

        if (_phase == Phase.PickTrophy)
        {
            UpdatePick(player, input);
            return;
        }

        if (MenuNav.Cancelled(input))
        {
            ctx.Screens.ChangeTo(new GuildScreen());
            return;
        }

        _cursor = MenuNav.Move(input, _cursor, RowCount(player));

        if (!MenuNav.Confirmed(input))
            return;

        if (IsBuyRow(player, _cursor))
        {
            BuyCase(player);
            return;
        }

        // A filled case gives its trophy back; an empty one asks what to put in it.
        if (_cursor < player.Trophies.Count)
        {
            TakeDown(player, _cursor);
            return;
        }

        var candidates = Displayable(player);
        if (candidates.Count == 0)
        {
            _message = "飾れる物を持っていない（鍛冶作の武具か宝石）";
            return;
        }

        _fillingCase = _cursor;
        _pickCursor = 0;
        _phase = Phase.PickTrophy;
    }

    private void BuyCase(Player player)
    {
        var cost = TrophyHall.CaseCost(player.TrophyCases);
        if (player.Gold < cost)
        {
            _message = $"所持金が足りない（{cost}G）";
            return;
        }

        player.Gold -= cost;
        player.TrophyCases++;
        _message = $"展示棚を{cost}Gで設えた（{player.TrophyCases}/{TrophyHall.MaxCases}）";
        SaveManager.Save(player);
    }

    private void TakeDown(Player player, int index)
    {
        if (!player.BagHasRoom)
        {
            _message = "鞄がいっぱいだ";
            return;
        }

        var item = player.Trophies[index];
        player.Trophies.RemoveAt(index);
        player.Bag.Add(item);
        _message = $"{item.Name}を下げた";
        SaveManager.Save(player);
    }

    private void UpdatePick(Player player, InputManager input)
    {
        var candidates = Displayable(player);

        if (MenuNav.Cancelled(input) || candidates.Count == 0)
        {
            _phase = Phase.Wall;
            return;
        }

        _pickCursor = MenuNav.Move(input, _pickCursor, candidates.Count);

        if (!MenuNav.Confirmed(input))
            return;

        var item = candidates[_pickCursor];
        player.Bag.Remove(item);

        // Mounted in case order rather than at the cursor's index: the list of trophies is dense and the cases
        // are drawn from it, so inserting anywhere else would leave a hole the wall has no way to render.
        player.Trophies.Add(item);
        _message = $"{item.Name}を飾った";
        _phase = Phase.Wall;
        SaveManager.Save(player);
    }

    public void Draw(GameContext ctx)
    {
        var r = ctx.Renderer;
        var fonts = ctx.Fonts;
        var player = ctx.Player!;

        r.Clear(Colors.Rgb(24, 20, 16));
        r.DrawTexture(ctx.Sprites.MenuBackdrop, 0, 0,
            SpriteFactory.MenuBackdropWidth, SpriteFactory.MenuBackdropHeight);

        fonts.DrawText(r.Handle, "展示室", 20, 16, 18, Colors.White);
        fonts.DrawText(r.Handle, $"展示棚 {player.TrophyCases}/{TrophyHall.MaxCases}", 110, 22, 11, Colors.Highlight);
        fonts.DrawText(r.Handle, "鍛冶で打った武具と宝石を飾れます。飾った物はいつでも下げられます。",
            20, 40, 10, Colors.Border);

        var rows = RowCount(player);
        var y = 62f;
        for (var i = 0; i < rows; i++)
        {
            var selected = i == _cursor;
            if (selected)
                r.FillRect(16, y - 3, 372, 30, Colors.Highlight);

            var ink = selected ? Colors.Black : Colors.White;
            var sub = selected ? Colors.Rgb(60, 50, 20) : Colors.Rgb(158, 152, 144);

            if (IsBuyRow(player, i))
            {
                var cost = TrophyHall.CaseCost(player.TrophyCases);
                var affordable = player.Gold >= cost;
                fonts.DrawText(r.Handle, "展示棚を増やす", 24, y, 12, ink);
                var price = $"{cost}G";
                var (pw, _) = fonts.Measure(price, 12);
                fonts.DrawText(r.Handle, price, 380 - pw, y, 12,
                    selected ? Colors.Black : affordable ? Colors.Gold : Colors.HpBar);
                fonts.DrawText(r.Handle, "棚が増えるほど次の棚は高くつきます", 24, y + 14, 9, sub);
            }
            else if (i < player.Trophies.Count)
            {
                var item = player.Trophies[i];
                r.DrawTexture(ctx.Sprites.ItemIcon(item), 22, y, 16, 16);
                fonts.DrawText(r.Handle, item.Name, 44, y, 12, ink);
                fonts.DrawText(r.Handle, TrophyHall.Caption(item), 44, y + 14, 9, sub);
                fonts.DrawText(r.Handle, "決定で下げる", 300, y + 14, 9, sub);
            }
            else
            {
                fonts.DrawText(r.Handle, $"{i + 1}番の棚", 24, y, 12, selected ? Colors.Black : Colors.Border);
                fonts.DrawText(r.Handle, "空。決定で飾る物を選びます", 24, y + 14, 9, sub);
            }

            y += 32f;
        }

        if (_message is not null)
            fonts.DrawText(r.Handle, _message, 20, 345, 11, Colors.Gold);
        ControlHints.Draw(ctx, 20, 366, 10, Colors.Border,
            ControlHints.Direction("選ぶ"), ControlHints.Confirm("決定"), ControlHints.Cancel("戻る"));

        StatusPanel.Draw(ctx, 400, 0, 400);

        if (_phase == Phase.PickTrophy)
            DrawPick(ctx, player);
    }

    private void DrawPick(GameContext ctx, Player player)
    {
        var r = ctx.Renderer;
        var fonts = ctx.Fonts;
        var candidates = Displayable(player);

        const float w = 300f;
        var h = 40f + Math.Max(candidates.Count, 1) * 18f + 24f;
        const float x = 50f;
        var y = (400f - h) / 2f;

        r.FillRect(0, 0, 400, 400, Colors.Rgb(0, 0, 0, 130));
        r.FillRect(x + 5, y + 6, w, h, Colors.Rgb(6, 6, 10, 190));
        r.FillRect(x, y, w, h, Colors.Rgb(28, 24, 20));
        r.DrawRect(x, y, w, h, Colors.Gold);

        fonts.DrawText(r.Handle, $"{_fillingCase + 1}番の棚に何を飾りますか", x + 12, y + 10, 12, Colors.Highlight);
        r.FillRect(x + 12, y + 30, w - 24, 1, Colors.Rgb(74, 64, 50));

        var ly = y + 38f;
        for (var i = 0; i < candidates.Count; i++)
        {
            var item = candidates[i];
            var selected = i == _pickCursor;
            if (selected)
                r.FillRect(x + 8, ly - 3, w - 16, 17, Colors.Highlight);
            r.DrawTexture(ctx.Sprites.ItemIcon(item), x + 12, ly - 1, 14, 14);
            fonts.DrawText(r.Handle, item.Name, x + 32, ly, 11, selected ? Colors.Black : Colors.White);
            ly += 18f;
        }

        ControlHints.DrawCentered(ctx, x + w / 2f, y + h - 16f, 10, Colors.Rgb(168, 160, 148),
            ControlHints.Confirm("飾る"), ControlHints.Cancel("やめる"));
    }
}
