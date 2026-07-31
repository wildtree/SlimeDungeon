using SDL3;
using SlimeDungeon.Core;
using SlimeDungeon.Domain;
using SlimeDungeon.Graphics;
using SlimeDungeon.UI;

namespace SlimeDungeon.Guild;

public sealed class PotionCraftScreen : IScreen
{
    private enum Phase { SelectHerb, SelectPotionType }

    private Phase _phase = Phase.SelectHerb;
    private int _cursor;
    private Item? _selectedHerb;
    private string? _message;

    /// <summary>Herbs anywhere on the player, bag or readied item slot alike — a herb in an item slot is still
    /// stock the alchemist can work with, and hiding it would look like it had gone missing.</summary>
    private static List<Item> Herbs(Player player) =>
        player.CarriedItems.Where(i => i.Category == ItemCategory.Herb).ToList();

    public void Update(GameContext ctx, float dt)
    {
        var input = ctx.Input;
        var player = ctx.Player!;

        if (MenuNav.Cancelled(input))
        {
            if (_phase == Phase.SelectPotionType)
            {
                _phase = Phase.SelectHerb;
                _cursor = 0;
            }
            else
            {
                ctx.Screens.ChangeTo(new GuildScreen());
            }
            return;
        }

        if (_phase == Phase.SelectHerb)
        {
            var herbs = Herbs(player);
            _cursor = MenuNav.Move(input, _cursor, herbs.Count);
            if (MenuNav.Confirmed(input) && herbs.Count > 0)
            {
                _selectedHerb = herbs[_cursor];
                _phase = Phase.SelectPotionType;
                _cursor = 0;
            }
            return;
        }

        _cursor = MenuNav.Move(input, _cursor, 2);
        if (!MenuNav.Confirmed(input) || _selectedHerb is null)
            return;

        var kind = _cursor == 0 ? PotionKind.Hp : PotionKind.Mp;
        var cost = (int)_selectedHerb.Rank * (int)_selectedHerb.Rank * 10;
        if (player.Gold < cost)
        {
            _message = "所持金が足りない";
            return;
        }
        // Crafting takes the herb out and hands a potion back, so a herb consumed out of the bag is slot-neutral
        // and must not be blocked by a full bag. A stacked herb keeps its bag entry, and a readied herb never
        // occupied the bag in the first place, so both of those do need a spare slot for the potion.
        var freesBagSlot = player.Bag.Contains(_selectedHerb) && _selectedHerb.Quantity <= 1;
        if (!freesBagSlot && !player.BagHasRoom)
        {
            _message = "鞄がいっぱいだ";
            return;
        }

        player.Gold -= cost;
        player.ConsumeOne(_selectedHerb);
        player.Bag.Add(ItemFactory.CreatePotion(_selectedHerb.Rank, kind));
        player.Counters.PotionsCrafted++;
        _message = $"{(kind == PotionKind.Hp ? "HP" : "MP")}ポーションを作った";
        _phase = Phase.SelectHerb;
        _cursor = 0;
        _selectedHerb = null;
    }

    private static void RemoveOne(List<Item> bag, Item item)
    {
        item.Quantity--;
        if (item.Quantity <= 0)
            bag.Remove(item);
    }

    public void Draw(GameContext ctx)
    {
        var r = ctx.Renderer;
        r.Clear(Colors.Rgb(24, 20, 16));
        r.DrawTexture(ctx.Sprites.MenuBackdrop, 0, 0, SpriteFactory.MenuBackdropWidth, SpriteFactory.MenuBackdropHeight);
        var fonts = ctx.Fonts;
        var player = ctx.Player!;

        fonts.DrawText(r.Handle, "ポーション調合", 20, 16, 18, Colors.White);

        var y = 60f;
        if (_phase == Phase.SelectHerb)
        {
            fonts.DrawText(r.Handle, "薬草を選ぶ:", 20, y, 12, Colors.Highlight);
            y += 20;
            var herbs = Herbs(player);
            var labels = herbs.Select(h => $"{h.Name} x{h.Quantity}  (加工費 {(int)h.Rank * (int)h.Rank * 10}G)").ToArray();
            var maxWidth = MenuNav.MaxLabelWidth(ctx, labels, 11);
            for (var i = 0; i < labels.Length; i++)
            {
                MenuNav.DrawRow(ctx, 20, y, maxWidth, 16, labels[i], 11, i == _cursor);
                y += 16;
            }
            if (herbs.Count == 0)
                fonts.DrawText(r.Handle, "薬草を持っていない", 20, y, 11, Colors.Border);
        }
        else
        {
            fonts.DrawText(r.Handle, $"{_selectedHerb?.Name} をどちらに加工する？", 20, y, 12, Colors.Highlight);
            y += 24;
            var labels = new[] { "HPポーション", "MPポーション" };
            var maxWidth = MenuNav.MaxLabelWidth(ctx, labels, 12);
            for (var i = 0; i < labels.Length; i++)
            {
                MenuNav.DrawRow(ctx, 20, y, maxWidth, 18, labels[i], 12, i == _cursor);
                y += 20;
            }
        }

        if (_message is not null)
            fonts.DrawText(r.Handle, _message, 20, 345, 11, Colors.Gold);
        fonts.DrawText(r.Handle, "[Esc]戻る", 20, 370, 11, Colors.Border);

        StatusPanel.Draw(ctx, 400, 0, 400);
    }
}
