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

    /// <summary>
    /// The shelf being looked at inside the weapons or armour department, identified by its rank — which is
    /// also what its material is, one substance per rank.
    ///
    /// Both departments used to be one flat column: seven ranks times five armour pieces is thirty-five rows,
    /// scrolled fifteen at a time, with 黒檀の兜 and 黒檀の鎧 nowhere near each other because the list ran
    /// piece-first. Choosing the material first turns that into seven rows and then five, and the five are the
    /// ones that are actually alternatives to each other.
    /// </summary>
    private Rank? _material;
    private int _materialCursor;

    /// <summary>Weapons and armour are stocked in every rank and so need the shelf; goods and the sell list
    /// are short enough to read whole.</summary>
    private static bool HasShelves(Department department) =>
        department is Department.Weapons or Department.Armour;

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

    /// <summary>
    /// The piece just bought, while the shopkeeper asks whether to put it on. Only ever set for gear that beats
    /// what is already worn — see <see cref="EquipOfferPopup.IsUpgrade"/>. It is already in the bag by this
    /// point, so declining costs nothing and needs no undo.
    /// </summary>
    private Item? _justBought;
    private EquipSlot[] _boughtSlots = [];
    private int _boughtCursor;

    /// <summary>
    /// Shop equipment tops out at Rank B — anything above that (and every carry-capacity bag) only drops in
    /// dungeons, so there's always a reason to go adventuring instead of just shopping.
    /// </summary>
    private static readonly Rank[] ShopEquipmentRanks = { Rank.H, Rank.G, Rank.F, Rank.E, Rank.D, Rank.C, Rank.B };

    /// <summary>How far above their own standing an adventurer may look. Two shelves, then the shop's ceiling.</summary>
    private const int OverRankShelves = 2;

    /// <summary>
    /// What the merchant lays out for <em>this</em> adventurer: their own rank, everything below it, and two
    /// shelves above.
    ///
    /// The whole list used to be on show from the first day, at list price, and that was the single worst
    /// balance fault in the game: gear prices rise linearly with rank while gear power rises by 1.6 per rank,
    /// so a rank-G adventurer with a few hundred gold bought the rank-D 黒檀 set, walked into a G dungeon with
    /// 13 DEF against slimes hitting for 12, and took literally zero damage.
    ///
    /// The fix for that was to hide everything above your rank, which fixed the numbers and cost the player
    /// something worth having — the ability to look at what is coming, and to decide that this season's savings
    /// are going on one really good breastplate instead of a full set of what they are supposed to be wearing.
    /// So the shelves are back, and it is <see cref="PriceOf"/> that does the work now: reaching above your
    /// rank is a project rather than an afternoon.
    /// </summary>
    private static Rank[] StockedRanks(Player player) =>
        ShopEquipmentRanks.Where(r => (int)r <= (int)player.Rank + OverRankShelves).ToArray();

    /// <summary>
    /// What the merchant charges over list for each rank above the buyer's own. Gear one rank up is worth 1.6
    /// times what your own is, so anything at or near 1.6 would make buying up strictly correct and the whole
    /// ladder pointless; at four, a full set two ranks above costs sixteen times list, which measures out as a
    /// season of saving rather than a shopping trip. That is the "かなり頑張って" this is for.
    /// </summary>
    private const int OverRankPremium = 4;

    /// <summary>
    /// The asking price: list price, times the premium for anything above the buyer's rank.
    ///
    /// Deliberately not baked into <see cref="Item.Value"/>. Value is what the thing is actually worth — what
    /// it fetches back across the counter, at half, and what it is worth as loot. If the premium lived there,
    /// an over-rank purchase would sell back for eight times what a same-rank one does, and the shop would be a
    /// money press rather than a shop.
    /// </summary>
    public static int PriceOf(Item item, Player player)
    {
        var above = Math.Max(0, (int)item.Rank - (int)player.Rank);
        return item.Value * (int)Math.Pow(OverRankPremium, above);
    }

    private const int VisibleRows = 15;

    /// <summary>Swords and wands — anything swung or pointed.</summary>
    private static Func<Item>[] BuildWeapons(Rank[] ranks)
    {
        var list = new List<Func<Item>>();
        foreach (var rank in ranks)
        {
            var r = rank;
            list.Add(() => ItemFactory.CreateWeapon(r, WeaponKind.Sword));
            list.Add(() => ItemFactory.CreateWeapon(r, WeaponKind.Wand));
        }
        return list.ToArray();
    }

    /// <summary>Everything worn to keep a slime off you: helmet, gauntlet, armour, shield — and boots, which
    /// belong with what you put on rather than with the consumables.</summary>
    private static Func<Item>[] BuildArmour(Rank[] ranks)
    {
        var list = new List<Func<Item>>();
        foreach (var rank in ranks)
        {
            var r = rank;
            list.Add(() => ItemFactory.CreateHelmet(r));
            list.Add(() => ItemFactory.CreateGauntlet(r));
            list.Add(() => ItemFactory.CreateArmor(r));
            list.Add(() => ItemFactory.CreateShield(r));
            list.Add(() => ItemFactory.CreateShoes(r));
        }
        return list.ToArray();
    }

    /// <summary>The rest: herbs, antidotes, and the two thrown things.</summary>
    private static Func<Item>[] BuildGoods(Rank[] ranks)
    {
        var list = new List<Func<Item>>
        {
            () => ItemFactory.CreateHerb(Rank.H),
            () => ItemFactory.CreateAntidoteHerb(Rank.H),
        };

        // Throwables are stocked across the same rank band as the gear, because unlike a herb their usefulness
        // is pinned to the rank of what you are fighting — a rank-H firecracker is no help in a rank-D dungeon.
        foreach (var rank in ranks)
        {
            var r = rank;
            list.Add(() => ItemFactory.CreateFirecracker(r));
            list.Add(() => ItemFactory.CreateCaltrops(r));
        }
        return list.ToArray();
    }

    /// <summary>
    /// Built per visit rather than once at startup, because what is on the shelf now depends on who is standing
    /// at the counter. Cheap: a department is a handful of factory delegates.
    ///
    /// <paramref name="material"/> narrows it to the one shelf the player has opened. Goods pass null and get
    /// the whole department, which is what they were always shown as.
    /// </summary>
    private static Func<Item>[] StockOf(Department department, Player player, Rank? material)
    {
        var ranks = material is { } only ? [only] : StockedRanks(player);
        return department switch
        {
            Department.Weapons => BuildWeapons(ranks),
            Department.Armour => BuildArmour(ranks),
            _ => BuildGoods(ranks),
        };
    }

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

        // The "wear it?" offer answers first: it only ever appears immediately after a purchase, so nothing
        // else can be open behind it.
        if (_justBought is { } bought)
        {
            UpdateEquipOffer(player, bought, input);
            return;
        }

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

        // A shelf outside the window cannot be reached from the menu, but it could be left open across a rank
        // change; nothing should ever be sold off it.
        if (_material is { } shelf && !StockedRanks(player).Contains(shelf))
            _material = null;

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
            _material = null;
            _message = null;
            return;
        }

        if (MenuNav.Cancelled(input))
        {
            // Cancel steps back exactly one level: off a shelf, then out of the department, then off the
            // counter. Three presses from a sword back to the room, each of them undoing one choice.
            if (_material is not null)
                _material = null;
            else if (_open is not null)
                _open = null;
            else
            {
                _menuOpen = false;
                _message = null;
            }
            return;
        }

        if (_open is null)
        {
            _departmentCursor = MenuNav.Move(input, _departmentCursor, Departments.Length);
            if (MenuNav.Confirmed(input))
            {
                _open = Departments[_departmentCursor];
                _material = null;
                _materialCursor = 0;
                _cursor = 0;
                _message = null;
            }
            return;
        }

        // The material shelf, for the two departments that have one.
        if (HasShelves(_open.Value) && _material is null)
        {
            var shelves = StockedRanks(player);
            _materialCursor = MenuNav.Move(input, _materialCursor, shelves.Length);
            if (MenuNav.Confirmed(input) && shelves.Length > 0)
            {
                _material = shelves[_materialCursor];
                _cursor = 0;
                _message = null;
            }
            return;
        }

        var selling = _open == Department.Sell;
        var count = selling ? player.Bag.Count : StockOf(_open.Value, player, _material).Length;
        _cursor = MenuNav.Move(input, _cursor, count);

        if (!MenuNav.Confirmed(input) || count == 0)
            return;

        // Confirm puts the deal on the counter rather than through the till. The stock rows are built fresh
        // every frame, so the purchase holds the very instance it will hand over, not an index into a list
        // that will have been rebuilt by the time the answer comes back.
        _pending = selling ? player.Bag[_cursor] : StockOf(_open.Value, player, _material)[_cursor]();
        _pendingIsSale = selling;
        _message = null;
    }

    private void Buy(Player player, Item item)
    {
        var price = PriceOf(item, player);
        if (player.Gold < price)
        {
            _message = "所持金が足りない";
            return;
        }
        if (!player.BagHasRoom)
        {
            _message = "鞄がいっぱいだ";
            return;
        }

        player.Gold -= price;
        player.Bag.Add(item);
        _message = $"{item.Name}を購入した";

        // Straight into the offer, the same way the forge does — but only for weapons and armour, and only when
        // it is actually a step up. Both halves matter: a shop trip buys plenty of gear that is not an upgrade,
        // and readied consumables have two slots that are usually empty, so without the gear test every single
        // herb would have interrupted with "wear it?".
        var slots = EquipOfferPopup.Slots(item);
        if (item.IsEquippable && slots.Length > 0 && EquipOfferPopup.IsUpgrade(player, item))
        {
            _justBought = item;
            _boughtSlots = slots;
            // On the first slot: the player just paid for an upgrade, so wearing it is the expected answer.
            _boughtCursor = 0;
        }
    }

    /// <summary>
    /// Which slot to put the new piece in, or neither. Cancel and the last row both leave it in the bag, so the
    /// question can be escaped either way rather than only one.
    /// </summary>
    private void UpdateEquipOffer(Player player, Item bought, InputManager input)
    {
        if (MenuNav.Cancelled(input))
        {
            _justBought = null;
            return;
        }

        _boughtCursor = MenuNav.Move(input, _boughtCursor, EquipOfferPopup.RowCount(_boughtSlots));

        if (!MenuNav.Confirmed(input))
            return;

        if (EquipOfferPopup.Chosen(_boughtSlots, _boughtCursor) is { } slot)
        {
            player.Bag.Remove(bought);
            if (player.TryEquip(bought, slot, out var displaced))
            {
                if (displaced is not null)
                    player.Bag.Add(displaced);
                _message = displaced is not null
                    ? $"{displaced.Name}を外して{bought.Name}を装備した"
                    : $"{bought.Name}を装備した";
            }
            else
            {
                // Should not happen — the slots offered come from the item itself — but never lose the piece.
                player.Bag.Add(bought);
                _message = $"{bought.Name}はそこには装備できない";
            }
        }

        _justBought = null;
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
        else if (HasShelves(_open.Value) && _material is null)
            DrawShelves(ctx, player);
        else if (_open == Department.Sell)
            DrawList(ctx, SellRows(ctx, player));
        else
            DrawList(ctx, StockRows(ctx, player, StockOf(_open.Value, player, _material)));

        StatusPanel.Draw(ctx, ShopRoom.Size, 0, 400);
        _travel.Draw(ctx, Place.Shop);

        if (_justBought is { } bought)
            EquipOfferPopup.Draw(ctx, player, bought, _boughtSlots, _boughtCursor);
        else if (_pending is { } deal)
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

    /// <summary>One shelf: what the material is, who it is for, and what it costs to kit out in it.</summary>
    private readonly record struct Shelf(IntPtr Icon, string Name, string Note, bool Recommended, bool OverRank);

    /// <summary>
    /// The price span is read off the stock itself rather than restated here, so a change to what a helmet
    /// costs cannot leave the shelf quoting last week's figure. Weapons come out to a single price (a sword and
    /// a wand of a rank cost the same), armour to a range across its five pieces.
    /// </summary>
    private static Shelf[] Shelves(GameContext ctx, Department department, Player player)
    {
        var weapons = department == Department.Weapons;
        return StockedRanks(player).Select(rank =>
        {
            var pieces = StockOf(department, player, rank).Select(make => make()).ToArray();
            var low = pieces.Min(p => PriceOf(p, player));
            var high = pieces.Max(p => PriceOf(p, player));
            var price = low == high ? $"{low}G" : $"{low}〜{high}G";

            // The archetypal piece of each department, so the shelf reads as swords or as armour at a glance
            // rather than as whatever happened to be first in the build order.
            var sample = weapons ? ItemFactory.CreateWeapon(rank, WeaponKind.Sword) : ItemFactory.CreateArmor(rank);
            var name = weapons ? EquipmentNames.WeaponMaterial(rank) : EquipmentNames.ArmourMaterial(rank);

            return new Shelf(ctx.Sprites.ItemIcon(sample), name, $"{rank.Label()}ランク向け   {price}",
                rank == player.Rank, rank > player.Rank);
        }).ToArray();
    }

    /// <summary>
    /// The materials on offer, one row each. The player's own rank is marked, because with two shelves of
    /// over-rank goods below it the bottom row is no longer the answer — and working out which shelf you are
    /// supposed to be shopping from by reading prices is exactly what this screen exists to avoid.
    /// </summary>
    private void DrawShelves(GameContext ctx, Player player)
    {
        var r = ctx.Renderer;
        var fonts = ctx.Fonts;
        var shelves = Shelves(ctx, _open!.Value, player);

        const float rowStep = 24f;
        var top = ShopRoom.Draw(ctx, 22f + Math.Max(shelves.Length, 1) * rowStep + ShopRoom.FooterHeight);
        var x = ShopRoom.ContentX;

        fonts.DrawText(r.Handle, $"{DepartmentLabel(_open.Value)} — 素材を選んでください", x, top, 12, Colors.Highlight);

        var y = top + 24f;
        if (shelves.Length == 0)
        {
            fonts.DrawText(r.Handle, "品切れだ", x, y, 11, Colors.Border);
        }
        else
        {
            var nameWidth = MenuNav.MaxLabelWidth(ctx, shelves.Select(s => s.Name).ToArray(), 13);
            for (var i = 0; i < shelves.Length; i++)
            {
                var selected = i == _materialCursor;
                r.DrawTexture(shelves[i].Icon, x, y, 16, 16);
                MenuNav.DrawRow(ctx, x + 24, y, nameWidth + 10, 19, shelves[i].Name, 13, selected);

                var note = shelves[i].Note
                           + (shelves[i].Recommended ? "  ★" : "")
                           + (shelves[i].OverRank ? "  取寄" : "");
                fonts.DrawText(r.Handle, note, x + nameWidth + 46, y + 4, 10,
                    selected ? Colors.Highlight : Colors.Border);
                y += rowStep;
            }
        }

        ShopRoom.DrawFooter(ctx, _message, ControlHints.Confirm("選ぶ"), ControlHints.Cancel("売り場へ戻る"));
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
        var price = _pendingIsSale ? SellTotal(item) : PriceOf(item, player);
        var after = _pendingIsSale ? player.Gold + price : player.Gold - price;
        var affordable = _pendingIsSale || player.Gold >= price;

        var lines = new List<ConfirmPopup.Line>();

        // Only worth saying when there is more than one, but then it is essential: the price quoted is for the
        // whole stack, and the player needs to see that the whole stack is what leaves the bag.
        if (_pendingIsSale && item.Quantity > 1)
            lines.Add(new ConfirmPopup.Line("個数", $"{item.Quantity}個 (1個 {item.SellValue}G)"));

        // What the markup is and why, for anything above the buyer's standing. Without it the price looks
        // arbitrary; with it, it reads as a decision the player is being invited to make.
        if (!_pendingIsSale && item.Rank > player.Rank)
            lines.Add(new ConfirmPopup.Line("取寄品",
                $"{item.Rank.Label()}ランク向け（定価{item.Value}G）", Colors.Highlight));

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

    /// <summary>
    /// Over-rank rows say so. The price alone would look like a mistake next to the shelf below it — sixteen
    /// times list with nothing to explain it reads as a bug, where "取寄" reads as a merchant who has to send
    /// away for gear the guild does not let someone of your standing walk out with.
    /// </summary>
    private static Row[] StockRows(GameContext ctx, Player player, Func<Item>[] stock) =>
        stock.Select(make => make())
             .Select(i =>
             {
                 var price = PriceOf(i, player);
                 var note = i.Rank > player.Rank ? "  取寄" : "";
                 return new Row(ctx.Sprites.ItemIcon(i), $"{i.Name}  {price}G{note}");
             })
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

        fonts.DrawText(r.Handle, ListHeading(), x, top, 12, Colors.Highlight);

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
            ControlHints.Confirm("決定"),
            ControlHints.Cancel(_material is null ? "売り場へ戻る" : "素材を選び直す"));
    }

    /// <summary>
    /// The department, and the shelf within it once one is open. Standing in front of 黒檀 with a column of
    /// five pieces that never say 黒檀 anywhere would otherwise leave nothing on screen naming what you picked.
    /// </summary>
    private string ListHeading()
    {
        var department = DepartmentLabel(_open!.Value);
        if (_material is not { } rank)
            return department;

        var material = _open == Department.Weapons
            ? EquipmentNames.WeaponMaterial(rank)
            : EquipmentNames.ArmourMaterial(rank);
        return $"{department} — {material}（{rank.Label()}ランク向け）";
    }
}
