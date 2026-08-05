using SDL3;
using SlimeDungeon.Core;
using SlimeDungeon.Domain;
using SlimeDungeon.Graphics;
using SlimeDungeon.UI;

namespace SlimeDungeon.Guild;

/// <summary>
/// The shop, behind a counter of four departments. Stocking seven ranks of seven kinds of gear in one list
/// meant scrolling past fifty-odd rows to find a herb; splitting it by what you came in for means every
/// department is a screenful you can actually read.
/// </summary>
public sealed class ShopScreen : IScreen
{
    private enum Department { Weapons, Armour, Goods, Sell }

    private static readonly Department[] Departments =
        [Department.Weapons, Department.Armour, Department.Goods, Department.Sell];

    private Department? _open;
    private int _departmentCursor;
    private int _cursor;
    private string? _message;

    /// <summary>False until the player asks for the counter, so walking in shows the shop rather than a list.</summary>
    private bool _menuOpen;

    private readonly TravelMenu _travel = new();

    /// <summary>
    /// The item the counter has been asked about, and whether the deal is a purchase or a sale. Held while the
    /// terms are on screen. Selling in particular used to be a single keypress that could put a just-forged
    /// weapon on the counter for a fraction of the ore it cost, with nothing between the cursor and the till.
    /// </summary>
    private Item? _pending;
    private bool _pendingIsSale;

    /// <summary>Shop equipment tops out at Rank B — anything above that (and every carry-capacity bag)
    /// only drops in dungeons, so there's always a reason to go adventuring instead of just shopping.</summary>
    private static readonly Rank[] ShopEquipmentRanks = { Rank.H, Rank.G, Rank.F, Rank.E, Rank.D, Rank.C, Rank.B };

    private const int VisibleRows = 15;

    private static readonly Func<Item>[] Weapons = BuildWeapons();
    private static readonly Func<Item>[] Armour = BuildArmour();
    private static readonly Func<Item>[] Goods = BuildGoods();

    /// <summary>Swords and wands — anything swung or pointed.</summary>
    private static Func<Item>[] BuildWeapons()
    {
        var list = new List<Func<Item>>();
        foreach (var rank in ShopEquipmentRanks)
        {
            list.Add(() => ItemFactory.CreateWeapon(rank, WeaponKind.Sword));
            list.Add(() => ItemFactory.CreateWeapon(rank, WeaponKind.Wand));
        }
        return list.ToArray();
    }

    /// <summary>Everything worn to keep a slime off you: helmet, gauntlet, armour, shield — and boots, which
    /// belong with what you put on rather than with the consumables.</summary>
    private static Func<Item>[] BuildArmour()
    {
        var list = new List<Func<Item>>();
        foreach (var rank in ShopEquipmentRanks)
        {
            list.Add(() => ItemFactory.CreateHelmet(rank));
            list.Add(() => ItemFactory.CreateGauntlet(rank));
            list.Add(() => ItemFactory.CreateArmor(rank));
            list.Add(() => ItemFactory.CreateShield(rank));
            list.Add(() => ItemFactory.CreateShoes(rank));
        }
        return list.ToArray();
    }

    /// <summary>The rest: herbs, antidotes, and the two thrown things.</summary>
    private static Func<Item>[] BuildGoods()
    {
        var list = new List<Func<Item>>
        {
            () => ItemFactory.CreateHerb(Rank.H),
            () => ItemFactory.CreateAntidoteHerb(Rank.H),
        };

        // Throwables are stocked across the same rank band as the gear, because unlike a herb their usefulness
        // is pinned to the rank of what you are fighting — a rank-H firecracker is no help in a rank-D dungeon.
        foreach (var rank in ShopEquipmentRanks)
        {
            list.Add(() => ItemFactory.CreateFirecracker(rank));
            list.Add(() => ItemFactory.CreateCaltrops(rank));
        }
        return list.ToArray();
    }

    private static Func<Item>[] StockOf(Department department) => department switch
    {
        Department.Weapons => Weapons,
        Department.Armour => Armour,
        _ => Goods,
    };

    private static string DepartmentLabel(Department department) => department switch
    {
        Department.Weapons => "武器",
        Department.Armour => "防具",
        Department.Goods => "アイテム",
        _ => "買取",
    };

    public void Update(GameContext ctx, float dt)
    {
        var input = ctx.Input;
        var player = ctx.Player!;

        // A deal on the counter answers before anything else — including travel, which would otherwise walk
        // out of the shop with a question still open.
        if (_pending is { } deal)
        {
            if (MenuNav.Confirmed(input))
            {
                if (_pendingIsSale)
                    Sell(player, deal);
                else
                    Buy(player, deal);
                _pending = null;
            }
            else if (MenuNav.Cancelled(input))
            {
                _pending = null;
                _message = null;
            }
            return;
        }

        if (_travel.Update(ctx, Place.Shop))
            return;

        // Until the counter is asked for there is nothing on screen but the shop itself.
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

        // The menu key puts the sheet down again from wherever you are, so the picture is never more than one
        // press away.
        if (MenuNav.MenuRequested(input))
        {
            _menuOpen = false;
            _open = null;
            _message = null;
            return;
        }

        if (MenuNav.Cancelled(input))
        {
            // Cancel steps back one level: out of a department first, off the counter only from the top.
            if (_open is null)
            {
                _menuOpen = false;
                _message = null;
            }
            else
            {
                _open = null;
            }
            return;
        }

        if (_open is null)
        {
            _departmentCursor = MenuNav.Move(input, _departmentCursor, Departments.Length);
            if (MenuNav.Confirmed(input))
            {
                _open = Departments[_departmentCursor];
                _cursor = 0;
                _message = null;
            }
            return;
        }

        var selling = _open == Department.Sell;
        var count = selling ? player.Bag.Count : StockOf(_open.Value).Length;
        _cursor = MenuNav.Move(input, _cursor, count);

        if (!MenuNav.Confirmed(input) || count == 0)
            return;

        // Confirm puts the deal on the counter rather than through the till. The stock rows are built fresh
        // every frame, so the purchase holds the very instance it will hand over, not an index into a list
        // that will have been rebuilt by the time the answer comes back.
        _pending = selling ? player.Bag[_cursor] : StockOf(_open.Value)[_cursor]();
        _pendingIsSale = selling;
        _message = null;
    }

    private void Buy(Player player, Item item)
    {
        if (player.Gold < item.Value)
        {
            _message = "所持金が足りない";
            return;
        }
        if (!player.BagHasRoom)
        {
            _message = "鞄がいっぱいだ";
            return;
        }

        player.Gold -= item.Value;
        player.Bag.Add(item);
        _message = $"{item.Name}を購入した";
    }

    /// <summary>
    /// A whole bag entry across the counter. <see cref="Item.SellValue"/> is the price of one, and a bag entry
    /// can be a stack of several — this used to remove the stack and pay for a single item, so a player selling
    /// five herbs was handing four of them over for nothing. The row and the confirmation both quote the total
    /// now, and the total is what is paid.
    /// </summary>
    private void Sell(Player player, Item item)
    {
        // Found by identity rather than by the cursor: the answer arrives a frame or more after the question,
        // and nothing guarantees the cursor is still on the same row of a list that shrinks as it is sold from.
        var index = player.Bag.IndexOf(item);
        if (index < 0)
            return;

        var total = SellTotal(item);
        player.EarnGold(total);
        player.Bag.RemoveAt(index);
        _message = $"{item.Name}を{total}Gで売却した";

        // The list just shrank under the cursor; without this, selling the last row throws on the next press.
        if (_cursor >= player.Bag.Count && _cursor > 0)
            _cursor--;
    }

    public void Draw(GameContext ctx)
    {
        var painted = ShopRoom.DrawBackdrop(ctx, ctx.Sprites.ShopBackdrop);
        var player = ctx.Player!;

        // The illustration letters "商店" on its own crest, so our heading only appears over the plain wall.
        if (!painted)
            ctx.Fonts.DrawText(ctx.Renderer.Handle, "ショップ", 20, 16, 18, Colors.White);

        if (!_menuOpen)
            ShopRoom.DrawPrompt(ctx, "いらっしゃいませ");
        else if (_open is null)
            DrawCounter(ctx);
        else if (_open == Department.Sell)
            DrawList(ctx, SellRows(ctx, player));
        else
            DrawList(ctx, StockRows(ctx, StockOf(_open.Value)));

        StatusPanel.Draw(ctx, ShopRoom.Size, 0, 400);
        _travel.Draw(ctx, Place.Shop);

        if (_pending is { } deal)
            DrawDealPopup(ctx, player, deal);
    }

    /// <summary>
    /// The four departments. Short enough that the sheet stays down on the counter and the shopkeeper is left
    /// looking over the top of it, which is the whole reason for sizing these panels to their contents.
    /// </summary>
    private void DrawCounter(GameContext ctx)
    {
        var r = ctx.Renderer;
        var fonts = ctx.Fonts;

        const float rowStep = 27f;
        var top = ShopRoom.Draw(ctx, 22f + Departments.Length * rowStep + ShopRoom.FooterHeight);
        var x = ShopRoom.ContentX;

        fonts.DrawText(r.Handle, "いらっしゃい。何をお探しで？", x, top, 12, Colors.Highlight);

        var labels = Departments.Select(DepartmentLabel).ToArray();
        var maxWidth = MenuNav.MaxLabelWidth(ctx, labels, 14);
        var y = top + 26f;
        for (var i = 0; i < labels.Length; i++)
        {
            MenuNav.DrawRow(ctx, x + 8, y, maxWidth + 12, 22, labels[i], 14, i == _departmentCursor);
            fonts.DrawText(r.Handle, Blurb(Departments[i]), x + maxWidth + 34, y + 3, 10,
                i == _departmentCursor ? Colors.Highlight : Colors.Border);
            y += rowStep;
        }

        ShopRoom.DrawFooter(ctx, _message, ControlHints.Confirm("選ぶ"), ControlHints.Cancel("戻る"));
    }

    private static string Blurb(Department department) => department switch
    {
        Department.Weapons => "剣と杖",
        Department.Armour => "兜・籠手・鎧・盾・靴",
        Department.Goods => "薬草、毒消し、爆竹、まきびし",
        _ => "持ち物を売る",
    };

    /// <summary>
    /// The deal, written out. The purse before and after is the point of it: the list quotes a price, but what
    /// the player actually needs to know at the moment of paying is whether they can still afford the potion
    /// they came in for afterwards.
    /// </summary>
    private void DrawDealPopup(GameContext ctx, Player player, Item item)
    {
        var price = _pendingIsSale ? SellTotal(item) : item.Value;
        var after = _pendingIsSale ? player.Gold + price : player.Gold - price;
        var affordable = _pendingIsSale || player.Gold >= price;

        var lines = new List<ConfirmPopup.Line>();

        // Only worth saying when there is more than one, but then it is essential: the price quoted is for the
        // whole stack, and the player needs to see that the whole stack is what leaves the bag.
        if (_pendingIsSale && item.Quantity > 1)
            lines.Add(new ConfirmPopup.Line("個数", $"{item.Quantity}個 (1個 {item.SellValue}G)"));

        lines.Add(new ConfirmPopup.Line(_pendingIsSale ? "買取価格" : "価格", $"{price}G", Colors.Gold));
        lines.Add(new ConfirmPopup.Line("所持金", $"{player.Gold}G"));
        lines.Add(new ConfirmPopup.Line(_pendingIsSale ? "売却後の所持金" : "購入後の所持金", $"{after}G",
            affordable ? Colors.White : Colors.HpBar));

        // Buying can fail for want of a slot as easily as for want of money, so both are on the panel.
        if (!_pendingIsSale)
            lines.Add(new ConfirmPopup.Line("鞄の空き", $"{player.Bag.Count}/{player.BagCapacity}",
                player.BagHasRoom ? Colors.White : Colors.HpBar));

        ConfirmPopup.Draw(ctx, item.Name, ctx.Sprites.ItemIcon(item), lines,
            _pendingIsSale ? "売却しますか？" : "購入しますか？");
    }

    /// <summary>One line of the department's list: what it looks like, and what it says.</summary>
    private readonly record struct Row(IntPtr Icon, string Label);

    private static Row[] StockRows(GameContext ctx, Func<Item>[] stock) =>
        stock.Select(make => make())
             .Select(i => new Row(ctx.Sprites.ItemIcon(i), $"{i.Name}  {i.Value}G"))
             .ToArray();

    /// <summary>What the whole bag entry fetches — one item's price times however many are stacked in it.</summary>
    private static int SellTotal(Item item) => item.SellValue * Math.Max(1, item.Quantity);

    private static Row[] SellRows(GameContext ctx, Player player) =>
        player.Bag.Select(i => new Row(ctx.Sprites.ItemIcon(i), $"{i.Name} x{i.Quantity}  {SellTotal(i)}G"))
                  .ToArray();

    /// <summary>
    /// Stock and the sell list are the same list with different words in it, so they share one drawing pass.
    /// A full department fills the sheet to its ceiling; a bag with three things in it does not.
    /// </summary>
    private void DrawList(GameContext ctx, Row[] rows)
    {
        var r = ctx.Renderer;
        var fonts = ctx.Fonts;

        const float rowStep = 16f;
        var shown = Math.Min(rows.Length, VisibleRows);
        var top = ShopRoom.Draw(ctx, 22f + Math.Max(shown, 1) * rowStep + ShopRoom.FooterHeight);
        var x = ShopRoom.ContentX;

        var heading = DepartmentLabel(_open!.Value);
        fonts.DrawText(r.Handle, heading, x, top, 12, Colors.Highlight);

        // The position counter goes on the heading line, right-aligned, where it does not steal a row.
        if (rows.Length > VisibleRows)
        {
            var counter = ControlHints.Direction($"{_cursor + 1}/{rows.Length}");
            var width = ControlHints.Width(ctx, 10, counter);
            ControlHints.Draw(ctx, ShopRoom.SheetRight - ShopRoom.Pad - width, top + 2, 10, Colors.Border, counter);
        }

        var y = top + 24f;
        if (rows.Length == 0)
        {
            fonts.DrawText(r.Handle, _open == Department.Sell ? "売る物がない" : "品切れだ", x, y, 11, Colors.Border);
        }
        else
        {
            var labelWidth = MenuNav.MaxLabelWidth(ctx, rows.Select(row => row.Label).ToArray(), 11);
            var scrollTop = Math.Clamp(_cursor - VisibleRows / 2, 0, Math.Max(0, rows.Length - VisibleRows));

            for (var i = scrollTop; i < Math.Min(rows.Length, scrollTop + VisibleRows); i++)
            {
                r.DrawTexture(rows[i].Icon, x, y - 1, 14, 14);
                MenuNav.DrawRow(ctx, x + 22, y, labelWidth, 15, rows[i].Label, 11, i == _cursor);
                y += rowStep;
            }
        }

        ShopRoom.DrawFooter(ctx, _message,
            ControlHints.Confirm("決定"), ControlHints.Cancel("売り場へ戻る"));
    }
}
