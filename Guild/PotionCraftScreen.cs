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

    /// <summary>False until the player asks, so walking in shows the shop rather than a list of herbs.</summary>
    private bool _menuOpen;

    private readonly TravelMenu _travel = new();

    /// <summary>The potion just made, while the alchemist asks whether to ready it.</summary>
    private Item? _justMade;
    private static readonly EquipSlot[] PotionSlots = [EquipSlot.Item1, EquipSlot.Item2];
    private int _madeCursor;

    /// <summary>Herbs anywhere on the player, bag or readied item slot alike — a herb in an item slot is still
    /// stock the alchemist can work with, and hiding it would look like it had gone missing.</summary>
    private static List<Item> Herbs(Player player) =>
        player.CarriedItems.Where(i => i.Category == ItemCategory.Herb).ToList();

    public void Update(GameContext ctx, float dt)
    {
        var input = ctx.Input;
        var player = ctx.Player!;

        if (_justMade is { } made)
        {
            UpdateEquipOffer(player, made, input);
            return;
        }

        if (_travel.Update(ctx, Place.Pharmacy))
            return;

        if (!_menuOpen)
        {
            if (MenuNav.MenuRequested(input) || MenuNav.Confirmed(input))
            {
                _menuOpen = true;
                _message = null;
            }
            else if (MenuNav.Cancelled(input))
            {
                ctx.Screens.ChangeTo(new GuildScreen());
            }
            return;
        }

        // The menu key clears the counter from wherever you are, back to just the room.
        if (MenuNav.MenuRequested(input))
        {
            _menuOpen = false;
            _phase = Phase.SelectHerb;
            _cursor = 0;
            _message = null;
            return;
        }

        if (MenuNav.Cancelled(input))
        {
            if (_phase == Phase.SelectPotionType)
            {
                _phase = Phase.SelectHerb;
                _cursor = 0;
            }
            else
            {
                _menuOpen = false;
                _message = null;
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
        var potion = ItemFactory.CreatePotion(_selectedHerb.Rank, kind);
        player.Bag.Add(potion);
        player.Counters.PotionsCrafted++;
        _message = $"{(kind == PotionKind.Hp ? "HP" : "MP")}ポーションを作った";
        _phase = Phase.SelectHerb;
        _cursor = 0;
        _selectedHerb = null;

        // Ask straight away whether to ready it. A potion in the bag cannot be drunk in a fight at all, so
        // "made a potion" and "can actually use the potion" are two different states and the second one is
        // easy to forget to reach.
        _justMade = potion;
        // On "leave it in the bag": readying displaces whatever is already in that slot, and the slot is far
        // more likely to hold something the player put there deliberately than to be empty.
        _madeCursor = EquipOfferPopup.RowCount(PotionSlots) - 1;
    }

    /// <summary>
    /// Which slot to ready the new potion in, or neither. Cancelling does nothing at all and leaves the potion
    /// in the bag, which is where it already is — the offer only ever moves it out.
    /// </summary>
    private void UpdateEquipOffer(Player player, Item potion, InputManager input)
    {
        if (MenuNav.Cancelled(input))
        {
            _justMade = null;
            return;
        }

        _madeCursor = MenuNav.Move(input, _madeCursor, EquipOfferPopup.RowCount(PotionSlots));

        if (!MenuNav.Confirmed(input))
            return;

        if (EquipOfferPopup.Chosen(PotionSlots, _madeCursor) is { } slot)
        {
            player.Bag.Remove(potion);
            if (player.TryEquip(potion, slot, out var displaced))
            {
                if (displaced is not null)
                    player.Bag.Add(displaced);
                _message = displaced is not null
                    ? $"{displaced.Name}を外して{potion.Name}を{EquipOfferPopup.SlotLabel(slot)}に入れた"
                    : $"{potion.Name}を{EquipOfferPopup.SlotLabel(slot)}に入れた";
            }
            else
            {
                player.Bag.Add(potion);
                _message = $"{potion.Name}はそこには入らない";
            }
        }

        _justMade = null;
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
        var fonts = ctx.Fonts;
        var player = ctx.Player!;

        // The elf's shop letters its own name across the top of the picture, so ours is only drawn when the
        // illustration is missing and the old panelled wall is showing instead.
        if (!ShopRoom.DrawBackdrop(ctx, ctx.Sprites.PharmacyBackdrop))
            fonts.DrawText(r.Handle, "ポーション調合", 20, 16, 18, Colors.White);

        if (!_menuOpen)
            ShopRoom.DrawPrompt(ctx, "承りますわ");
        else if (_phase == Phase.SelectHerb)
            DrawHerbList(ctx, player);
        else
            DrawKindChoice(ctx);

        StatusPanel.Draw(ctx, ShopRoom.Size, 0, 400);
        _travel.Draw(ctx, Place.Pharmacy);

        if (_justMade is { } made)
            EquipOfferPopup.Draw(ctx, player, made, PotionSlots, _madeCursor);
    }

    /// <summary>
    /// Whatever herbs are on the character. Usually a handful, so the sheet normally sits low on the workbench
    /// and leaves the alchemist and her flasks in view.
    /// </summary>
    private void DrawHerbList(GameContext ctx, Player player)
    {
        var r = ctx.Renderer;
        var fonts = ctx.Fonts;

        var herbs = Herbs(player);
        var labels = herbs
            .Select(h => $"{h.Name} x{h.Quantity}  (加工費 {(int)h.Rank * (int)h.Rank * 10}G)")
            .ToArray();

        const float rowStep = 17f;
        var rows = Math.Max(labels.Length, 1);
        var top = ShopRoom.Draw(ctx, 24f + rows * rowStep + ShopRoom.FooterHeight);
        var x = ShopRoom.ContentX;

        fonts.DrawText(r.Handle, "どの薬草を加工しますか", x, top, 12, Colors.Highlight);

        var y = top + 26f;
        if (labels.Length == 0)
        {
            fonts.DrawText(r.Handle, "薬草を持っていない", x, y, 11, Colors.Border);
        }
        else
        {
            var maxWidth = MenuNav.MaxLabelWidth(ctx, labels, 11);
            for (var i = 0; i < labels.Length; i++)
            {
                r.DrawTexture(ctx.Sprites.ItemIcon(herbs[i]), x, y - 1, 14, 14);
                MenuNav.DrawRow(ctx, x + 22, y, maxWidth, 16, labels[i], 11, i == _cursor);
                y += rowStep;
            }
        }

        ShopRoom.DrawFooter(ctx, _message, ControlHints.Confirm("選ぶ"), ControlHints.Cancel("戻る"));
    }

    private void DrawKindChoice(GameContext ctx)
    {
        var r = ctx.Renderer;
        var fonts = ctx.Fonts;

        var top = ShopRoom.Draw(ctx, 28f + 2 * 22f + ShopRoom.FooterHeight);
        var x = ShopRoom.ContentX;

        fonts.DrawText(r.Handle, $"{_selectedHerb?.Name} をどちらに加工する？", x, top, 12, Colors.Highlight);

        var labels = new[] { "HPポーション", "MPポーション" };
        var maxWidth = MenuNav.MaxLabelWidth(ctx, labels, 12);
        var y = top + 30f;
        for (var i = 0; i < labels.Length; i++)
        {
            MenuNav.DrawRow(ctx, x + 8, y, maxWidth + 12, 19, labels[i], 12, i == _cursor);
            y += 22f;
        }

        ShopRoom.DrawFooter(ctx, _message, ControlHints.Confirm("決める"), ControlHints.Cancel("戻る"));
    }
}
