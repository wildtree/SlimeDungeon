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

        if (MenuNav.Cancelled(input))
        {
            // Cancel steps back one level: out of a department first, out of the shop only from the counter.
            if (_open is null)
                ctx.Screens.ChangeTo(new GuildScreen());
            else
                _open = null;
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

        if (selling)
            Sell(player);
        else
            Buy(player, StockOf(_open.Value)[_cursor]());
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

    private void Sell(Player player)
    {
        var item = player.Bag[_cursor];
        player.EarnGold(item.SellValue);
        player.Bag.RemoveAt(_cursor);
        _message = $"{item.Name}を{item.SellValue}Gで売却した";

        // The list just shrank under the cursor; without this, selling the last row throws on the next press.
        if (_cursor >= player.Bag.Count && _cursor > 0)
            _cursor--;
    }

    public void Draw(GameContext ctx)
    {
        var r = ctx.Renderer;
        r.Clear(Colors.Rgb(24, 20, 16));
        r.DrawTexture(ctx.Sprites.MenuBackdrop, 0, 0, SpriteFactory.MenuBackdropWidth, SpriteFactory.MenuBackdropHeight);
        var fonts = ctx.Fonts;
        var player = ctx.Player!;

        fonts.DrawText(r.Handle, "ショップ", 20, 16, 18, Colors.White);

        if (_open is null)
        {
            DrawCounter(ctx);
            return;
        }

        fonts.DrawText(r.Handle, DepartmentLabel(_open.Value), 120, 21, 13, Colors.Highlight);

        var y = 56f;
        if (_open == Department.Sell)
            DrawSellList(ctx, player, ref y);
        else
            DrawStock(ctx, StockOf(_open.Value), ref y);

        if (_message is not null)
            fonts.DrawText(r.Handle, _message, 20, 345, 11, Colors.Gold);
        fonts.DrawText(r.Handle,
            $"[{MenuNav.ConfirmHint(ctx.Input)}]決定  [{MenuNav.CancelHint(ctx.Input)}]売り場へ戻る", 20, 370, 10, Colors.Border);

        StatusPanel.Draw(ctx, 400, 0, 400);
    }

    private void DrawCounter(GameContext ctx)
    {
        var r = ctx.Renderer;
        var fonts = ctx.Fonts;

        fonts.DrawText(r.Handle, "いらっしゃい。何をお探しで？", 20, 48, 12, Colors.Highlight);

        var labels = Departments.Select(DepartmentLabel).ToArray();
        var maxWidth = MenuNav.MaxLabelWidth(ctx, labels, 15);
        var y = 96f;
        for (var i = 0; i < labels.Length; i++)
        {
            MenuNav.DrawRow(ctx, 40, y, maxWidth + 20, 26, labels[i], 15, i == _departmentCursor);
            fonts.DrawText(r.Handle, Blurb(Departments[i]), 40 + maxWidth + 44, y + 4, 10,
                i == _departmentCursor ? Colors.Highlight : Colors.Border);
            y += 32;
        }

        if (_message is not null)
            fonts.DrawText(r.Handle, _message, 20, 345, 11, Colors.Gold);
        fonts.DrawText(r.Handle,
            $"[{MenuNav.ConfirmHint(ctx.Input)}]選ぶ  [{MenuNav.CancelHint(ctx.Input)}]戻る", 20, 370, 10, Colors.Border);

        StatusPanel.Draw(ctx, 400, 0, 400);
    }

    private static string Blurb(Department department) => department switch
    {
        Department.Weapons => "剣と杖",
        Department.Armour => "兜・籠手・鎧・盾・靴",
        Department.Goods => "薬草、毒消し、爆竹、まきびし",
        _ => "持ち物を売る",
    };

    private void DrawStock(GameContext ctx, Func<Item>[] stock, ref float y)
    {
        var r = ctx.Renderer;
        var fonts = ctx.Fonts;

        var items = stock.Select(make => make()).ToArray();
        var labels = items.Select(i => $"{i.Name}  {i.Value}G").ToArray();
        var maxWidth = MenuNav.MaxLabelWidth(ctx, labels, 11);
        var scrollTop = Math.Clamp(_cursor - VisibleRows / 2, 0, Math.Max(0, labels.Length - VisibleRows));

        for (var i = scrollTop; i < Math.Min(labels.Length, scrollTop + VisibleRows); i++)
        {
            r.DrawTexture(ctx.Sprites.ItemIcon(items[i]), 20, y - 1, 14, 14);
            MenuNav.DrawRow(ctx, 40, y, maxWidth, 15, labels[i], 11, i == _cursor);
            y += 16;
        }

        if (labels.Length > VisibleRows)
            fonts.DrawText(r.Handle, $"[↑↓] {_cursor + 1}/{labels.Length}", 300, 40, 10, Colors.Border);
    }

    private void DrawSellList(GameContext ctx, Player player, ref float y)
    {
        var r = ctx.Renderer;
        var fonts = ctx.Fonts;

        if (player.Bag.Count == 0)
        {
            fonts.DrawText(r.Handle, "売る物がない", 20, y, 12, Colors.Border);
            return;
        }

        var labels = player.Bag.Select(i => $"{i.Name} x{i.Quantity}  {i.SellValue}G").ToArray();
        var maxWidth = MenuNav.MaxLabelWidth(ctx, labels, 11);
        var scrollTop = Math.Clamp(_cursor - VisibleRows / 2, 0, Math.Max(0, labels.Length - VisibleRows));

        for (var i = scrollTop; i < Math.Min(labels.Length, scrollTop + VisibleRows); i++)
        {
            r.DrawTexture(ctx.Sprites.ItemIcon(player.Bag[i]), 20, y - 1, 14, 14);
            MenuNav.DrawRow(ctx, 40, y, maxWidth, 15, labels[i], 11, i == _cursor);
            y += 16;
        }

        if (labels.Length > VisibleRows)
            fonts.DrawText(r.Handle, $"[↑↓] {_cursor + 1}/{labels.Length}", 300, 40, 10, Colors.Border);
    }
}
