using SDL3;
using SlimeDungeon.Core;
using SlimeDungeon.Domain;
using SlimeDungeon.Graphics;
using SlimeDungeon.UI;

namespace SlimeDungeon.Guild;

public sealed class ShopScreen : IScreen
{
    private enum Tab { Buy, Sell }

    private Tab _tab = Tab.Buy;
    private int _cursor;
    private string? _message;

    /// <summary>Shop equipment tops out at Rank B — anything above that (and every carry-capacity bag)
    /// only drops in dungeons, so there's always a reason to go adventuring instead of just shopping.</summary>
    private static readonly Rank[] ShopEquipmentRanks = { Rank.H, Rank.G, Rank.F, Rank.E, Rank.D, Rank.C, Rank.B };

    private static readonly Func<Item>[] Catalog = BuildCatalog();

    private const int VisibleRows = 15;

    private static Func<Item>[] BuildCatalog()
    {
        var list = new List<Func<Item>>
        {
            () => ItemFactory.CreateHerb(Rank.H),
            () => ItemFactory.CreateAntidoteHerb(Rank.H),
        };
        foreach (var rank in ShopEquipmentRanks)
        {
            list.Add(() => ItemFactory.CreateWeapon(rank, WeaponKind.Sword));
            list.Add(() => ItemFactory.CreateWeapon(rank, WeaponKind.Wand));
            list.Add(() => ItemFactory.CreateShield(rank));
            list.Add(() => ItemFactory.CreateArmor(rank));
            list.Add(() => ItemFactory.CreateHelmet(rank));
            list.Add(() => ItemFactory.CreateGauntlet(rank));
            list.Add(() => ItemFactory.CreateShoes(rank));
        }
        return list.ToArray();
    }

    public void Update(GameContext ctx, float dt)
    {
        var input = ctx.Input;
        var player = ctx.Player!;

        if (MenuNav.Cancelled(input))
        {
            ctx.Screens.ChangeTo(new GuildScreen());
            return;
        }

        if (input.WasPressed(SDL.Keycode.Left) || input.WasPressed(SDL.Keycode.Right) || input.WasPressed(SDL.Keycode.Tab))
        {
            _tab = _tab == Tab.Buy ? Tab.Sell : Tab.Buy;
            _cursor = 0;
        }

        var count = _tab == Tab.Buy ? Catalog.Length : player.Bag.Count;
        _cursor = MenuNav.Move(input, _cursor, count);

        if (!MenuNav.Confirmed(input) || count == 0)
            return;

        if (_tab == Tab.Buy)
        {
            var sample = Catalog[_cursor]();
            if (player.Gold < sample.Value)
            {
                _message = "所持金が足りない";
                return;
            }
            if (!player.BagHasRoom)
            {
                _message = "鞄がいっぱいだ";
                return;
            }

            player.Gold -= sample.Value;
            player.Bag.Add(sample);
            _message = $"{sample.Name}を購入した";
        }
        else
        {
            var item = player.Bag[_cursor];
            player.Gold += item.SellValue;
            player.Bag.RemoveAt(_cursor);
            _message = $"{item.Name}を{item.SellValue}Gで売却した";
            if (_cursor >= player.Bag.Count && _cursor > 0)
                _cursor--;
        }
    }

    public void Draw(GameContext ctx)
    {
        var r = ctx.Renderer;
        r.Clear(Colors.Rgb(24, 20, 16));
        r.DrawTexture(ctx.Sprites.MenuBackdrop, 0, 0, SpriteFactory.MenuBackdropWidth, SpriteFactory.MenuBackdropHeight);
        var fonts = ctx.Fonts;
        var player = ctx.Player!;

        fonts.DrawText(r.Handle, "ショップ", 20, 16, 18, Colors.White);
        fonts.DrawText(r.Handle, "[左右]購入/売却切替", 220, 20, 10, Colors.Border);

        var buyColor = _tab == Tab.Buy ? Colors.Highlight : Colors.White;
        var sellColor = _tab == Tab.Sell ? Colors.Highlight : Colors.White;
        fonts.DrawText(r.Handle, "購入", 20, 44, 12, buyColor);
        fonts.DrawText(r.Handle, "売却", 70, 44, 12, sellColor);

        var y = 70f;
        if (_tab == Tab.Buy)
        {
            var labels = Catalog.Select(make => { var item = make(); return $"{item.Name}  {item.Value}G"; }).ToArray();
            var maxWidth = MenuNav.MaxLabelWidth(ctx, labels, 11);
            var scrollTop = Math.Clamp(_cursor - VisibleRows / 2, 0, Math.Max(0, labels.Length - VisibleRows));
            for (var i = scrollTop; i < Math.Min(labels.Length, scrollTop + VisibleRows); i++)
            {
                MenuNav.DrawRow(ctx, 20, y, maxWidth, 15, labels[i], 11, i == _cursor);
                y += 15;
            }
            if (labels.Length > VisibleRows)
                fonts.DrawText(r.Handle, $"[↑↓]スクロール ({_cursor + 1}/{labels.Length})", 220, 44, 10, Colors.Border);
        }
        else
        {
            var labels = player.Bag.Select(item => $"{item.Name} x{item.Quantity}  {item.SellValue}G").ToArray();
            var maxWidth = MenuNav.MaxLabelWidth(ctx, labels, 11);
            for (var i = 0; i < labels.Length; i++)
            {
                MenuNav.DrawRow(ctx, 20, y, maxWidth, 15, labels[i], 11, i == _cursor);
                y += 15;
            }
            if (player.Bag.Count == 0)
                fonts.DrawText(r.Handle, "売る物がない", 20, y, 11, Colors.Border);
        }

        if (_message is not null)
            fonts.DrawText(r.Handle, _message, 20, 345, 11, Colors.Gold);
        fonts.DrawText(r.Handle, "[Esc]戻る", 20, 370, 11, Colors.Border);

        StatusPanel.Draw(ctx, 400, 0, 400);
    }
}
