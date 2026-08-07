using SlimeDungeon.Core;
using SlimeDungeon.Data;
using SlimeDungeon.Domain;
using SlimeDungeon.UI;

namespace SlimeDungeon.Guild;

/// <summary>
/// The cheap inn: a bed upstairs and a bar downstairs.
///
/// Recovery used to be one line on the guild counter, which put the single most important decision in the game —
/// whether a trip is worth making in this state — in the middle of a list of errands. It is a place of its own
/// now, reached from the travel menu like the shop and the smith, and built the same way: the room is what you
/// see on arrival, and the counter opens when you ask for it.
/// </summary>
public sealed class InnScreen : IScreen
{
    /// <summary>False until the player rings the bell, so walking in shows the inn rather than a price list.</summary>
    private bool _menuOpen;

    private int _cursor;
    private string? _message;

    private readonly TravelMenu _travel = new();

    public void Update(GameContext ctx, float dt)
    {
        var input = ctx.Input;
        var player = ctx.Player!;

        if (_travel.Update(ctx, Place.Inn))
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

        if (MenuNav.MenuRequested(input) || MenuNav.Cancelled(input))
        {
            _menuOpen = false;
            _message = null;
            return;
        }

        _cursor = MenuNav.Move(input, _cursor, Inn.Services.Length);

        if (!MenuNav.Confirmed(input))
            return;

        var service = Inn.Services[_cursor];

        if (!Inn.WouldHelp(service, player))
        {
            _message = "すでに元気だ";
            return;
        }

        var cost = Inn.Cost(service, player);
        if (player.Gold < cost)
        {
            _message = $"所持金が足りない（{cost}G）";
            return;
        }

        _message = Inn.Use(service, player);

        // Written out at once. A night advances the calendar past contract deadlines, and the guild otherwise
        // only saves on arrival — so quitting from here would undo a night that had already been paid for.
        SaveManager.Save(player);
    }

    public void Draw(GameContext ctx)
    {
        var r = ctx.Renderer;
        var fonts = ctx.Fonts;
        var player = ctx.Player!;

        // The picture letters "宿屋" on its own crest, so the heading is only for the fallback wall.
        if (!ShopRoom.DrawBackdrop(ctx, ctx.Sprites.HotelBackdrop))
            fonts.DrawText(r.Handle, "安宿", 20, 16, 18, Colors.White);

        if (!_menuOpen)
            ShopRoom.DrawPrompt(ctx, "ご用件をどうぞ");
        else
            DrawCounter(ctx, player);

        StatusPanel.Draw(ctx, ShopRoom.Size, 0, 400);
        _travel.Draw(ctx, Place.Inn);
    }

    private const float RowHeight = 34f;

    /// <summary>
    /// Both services, with the price and what each actually restores for this character right now. The figures
    /// are computed rather than described, because "半分ほど" means a different number every few levels and the
    /// decision being made is whether that number is worth the day.
    /// </summary>
    private void DrawCounter(GameContext ctx, Player player)
    {
        var r = ctx.Renderer;
        var fonts = ctx.Fonts;

        var top = ShopRoom.Draw(ctx, 24f + Inn.Services.Length * RowHeight + ShopRoom.FooterHeight);
        var x = ShopRoom.ContentX;

        fonts.DrawText(r.Handle, "いらっしゃい。お泊まりですか", x, top, 12, Colors.Highlight);
        var vitals = $"HP {player.Stats.Hp}/{player.Stats.MaxHp}  MP {player.Stats.Mp}/{player.Stats.MaxMp}";
        var (vw, _) = fonts.Measure(vitals, 10);
        fonts.DrawText(r.Handle, vitals, ShopRoom.SheetRight - ShopRoom.Pad - vw, top + 2, 10,
            Colors.Rgb(170, 165, 158));

        var y = top + 24f;
        for (var i = 0; i < Inn.Services.Length; i++)
        {
            var service = Inn.Services[i];
            var cost = Inn.Cost(service, player);
            var (hp, mp) = Inn.Restores(service, player);
            var selected = i == _cursor;
            var affordable = player.Gold >= cost;

            if (selected)
                r.FillRect(x - 4, y - 4, ShopRoom.ContentWidth + 8, RowHeight - 4, Colors.Highlight);

            var ink = selected ? Colors.Black : affordable ? Colors.White : Colors.Rgb(126, 120, 112);
            var sub = selected ? Colors.Rgb(60, 50, 20) : Colors.Rgb(158, 152, 144);

            fonts.DrawText(r.Handle, service.Name, x, y, 12, ink);

            var price = $"{cost}G";
            var (pw, _) = fonts.Measure(price, 12);
            fonts.DrawText(r.Handle, price, x + ShopRoom.ContentWidth - pw, y, 12,
                selected ? Colors.Black : affordable ? Colors.Gold : Colors.HpBar);

            var restores = hp > 0 || mp > 0
                ? (mp > 0 ? $"HP+{hp} MP+{mp}" : $"HP+{hp}")
                : "いまは効果なし";
            fonts.DrawText(r.Handle, $"{service.Note}　({restores})", x, y + 15, 9, sub);

            y += RowHeight;
        }

        ShopRoom.DrawFooter(ctx, _message,
            ControlHints.Direction("選ぶ"), ControlHints.Confirm("決定"), ControlHints.Cancel("戻る"));
    }
}
