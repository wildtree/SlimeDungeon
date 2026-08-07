using SDL3;
using SlimeDungeon.Core;
using SlimeDungeon.Domain;
using SlimeDungeon.Graphics;
using SlimeDungeon.UI;

namespace SlimeDungeon.Guild;

/// <summary>
/// The alchemist. One herb in, one potion out — and which potion is decided by the herb, not by the customer.
///
/// This used to take a 薬草 and then ask whether to make it into an HP or an MP potion, which meant the whole
/// magical half of the shop ran on the same plant as the physical half and the 毒消し草 had no use here at all.
/// A healing herb makes a healing draught and an antidote herb makes a restorative one; the rank of the leaf
/// carries through to the potion and sets the fee, exactly as before.
/// </summary>
public sealed class PotionCraftScreen : IScreen
{
    private int _cursor;
    private string? _message;

    /// <summary>False until the player asks, so walking in shows the shop rather than a list of herbs.</summary>
    private bool _menuOpen;

    private readonly TravelMenu _travel = new();

    /// <summary>The potion just made, while the alchemist asks whether to ready it.</summary>
    private Item? _justMade;
    private static readonly EquipSlot[] PotionSlots = [EquipSlot.Item1, EquipSlot.Item2];
    private int _madeCursor;

    /// <summary>
    /// Everything the alchemist will work with, from anywhere on the character — bag or readied item slot alike.
    /// A herb in an item slot is still stock, and hiding it would look like it had gone missing.
    /// </summary>
    private static List<Item> Ingredients(Player player) =>
        player.CarriedItems
            .Where(i => i.Category is ItemCategory.Herb or ItemCategory.Antidote)
            .ToList();

    /// <summary>
    /// What a leaf turns into. The healing herb is the body's, the antidote herb is the blood's — one mends and
    /// the other clears, and clearing is what magic runs on.
    /// </summary>
    private static PotionKind KindFor(Item ingredient) =>
        ingredient.Category == ItemCategory.Herb ? PotionKind.Hp : PotionKind.Mp;

    /// <summary>The fee, unchanged: the square of the leaf's rank, which is what the potion comes out at.</summary>
    private static int CostOf(Item ingredient) => (int)ingredient.Rank * (int)ingredient.Rank * 10;

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
        if (MenuNav.MenuRequested(input) || MenuNav.Cancelled(input))
        {
            _menuOpen = false;
            _cursor = 0;
            _message = null;
            return;
        }

        var ingredients = Ingredients(player);
        _cursor = MenuNav.Move(input, _cursor, ingredients.Count);

        if (!MenuNav.Confirmed(input) || ingredients.Count == 0)
            return;

        var ingredient = ingredients[_cursor];
        var kind = KindFor(ingredient);
        var cost = CostOf(ingredient);

        if (player.Gold < cost)
        {
            _message = $"所持金が足りない（{cost}G）";
            return;
        }
        // Crafting takes the herb out and hands a potion back, so a herb consumed out of the bag is slot-neutral
        // and must not be blocked by a full bag. A stacked herb keeps its bag entry, and a readied herb never
        // occupied the bag in the first place, so both of those do need a spare slot for the potion.
        var freesBagSlot = player.Bag.Contains(ingredient) && ingredient.Quantity <= 1;
        if (!freesBagSlot && !player.BagHasRoom)
        {
            _message = "鞄がいっぱいだ";
            return;
        }

        player.Gold -= cost;
        player.ConsumeOne(ingredient);
        var potion = ItemFactory.CreatePotion(ingredient.Rank, kind);
        player.Bag.Add(potion);
        player.Counters.PotionsCrafted++;
        _message = $"{potion.Name}を作った";
        _cursor = 0;

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
        else
            DrawIngredientList(ctx, player);

        StatusPanel.Draw(ctx, ShopRoom.Size, 0, 400);
        _travel.Draw(ctx, Place.Pharmacy);

        if (_justMade is { } made)
            EquipOfferPopup.Draw(ctx, player, made, PotionSlots, _madeCursor);
    }

    /// <summary>
    /// Whatever leaves are on the character. Usually a handful, so the sheet normally sits low on the workbench
    /// and leaves the alchemist and her flasks in view.
    ///
    /// Each row names what it becomes rather than only what it is. There is no longer a second question to ask,
    /// so the outcome has to be on the row the cursor is on — otherwise the only way to find out what a 毒消し草
    /// makes would be to spend one.
    /// </summary>
    private void DrawIngredientList(GameContext ctx, Player player)
    {
        var r = ctx.Renderer;
        var fonts = ctx.Fonts;

        var ingredients = Ingredients(player);

        // Fixed columns rather than one padded string: the two leaf families have different name lengths, and
        // padding left the arrows and the fees stepping in and out down the list.
        var nameX = ShopRoom.ContentX + 22f;
        var arrowX = ShopRoom.ContentX + 140f;
        var feeRight = ShopRoom.SheetRight - ShopRoom.Pad;

        const float rowStep = 17f;
        var rows = Math.Max(ingredients.Count, 1);
        var top = ShopRoom.Draw(ctx, 24f + rows * rowStep + ShopRoom.FooterHeight);
        var x = ShopRoom.ContentX;

        fonts.DrawText(r.Handle, "どの薬草を加工しますか", x, top, 12, Colors.Highlight);

        var y = top + 26f;
        if (ingredients.Count == 0)
        {
            fonts.DrawText(r.Handle, "薬草も毒消し草も持っていない", x, y, 11, Colors.Border);
        }
        else
        {
            for (var i = 0; i < ingredients.Count; i++)
            {
                var item = ingredients[i];
                var selected = i == _cursor;
                var product = ItemFactory.CreatePotion(item.Rank, KindFor(item));
                var fee = $"{CostOf(item)}G";

                if (selected)
                    r.FillRect(x + 18, y - 2, ShopRoom.ContentWidth - 18, 16, Colors.Highlight);

                var ink = selected ? Colors.Black : Colors.White;
                var sub = selected ? Colors.Rgb(60, 50, 20) : Colors.Rgb(160, 200, 170);

                r.DrawTexture(ctx.Sprites.ItemIcon(item), x, y - 1, 14, 14);
                fonts.DrawText(r.Handle, item.Quantity > 1 ? $"{item.Name} x{item.Quantity}" : item.Name,
                    nameX, y, 11, ink);
                fonts.DrawText(r.Handle, $"→ {product.Name}", arrowX, y, 11, sub);

                var (fw, _) = fonts.Measure(fee, 10);
                fonts.DrawText(r.Handle, fee, feeRight - fw, y + 1, 10, selected ? Colors.Black : Colors.Gold);

                y += rowStep;
            }
        }

        ShopRoom.DrawFooter(ctx, _message, ControlHints.Confirm("加工する"), ControlHints.Cancel("戻る"));
    }
}
