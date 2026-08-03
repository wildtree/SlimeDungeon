using SlimeDungeon.Core;
using SlimeDungeon.Guild;

namespace SlimeDungeon.UI;

/// <summary>Everywhere the travel menu will take you. The order is the order the rows are drawn in.</summary>
public enum Place { Guild, Shop, Smith, Pharmacy, Dungeon }

/// <summary>
/// Getting from one place to another.
///
/// The guild's counter used to be the way into the shop, the smith, the alchemist and the dungeon, which made
/// every trip out a trip back through the guild first. These are separate places on a map now, so moving
/// between them is its own control rather than an errand you ask the receptionist for.
///
/// The list always shows all five, in the same order, with the one you are standing in dimmed — the point of a
/// fixed list is that the hand learns "travel, down, down, confirm" and it means the same thing everywhere.
/// The cursor opens on the current place, so a stray confirm right after opening the menu goes nowhere.
/// </summary>
public sealed class TravelMenu
{
    private static readonly Place[] Places =
        [Place.Guild, Place.Shop, Place.Smith, Place.Pharmacy, Place.Dungeon];

    private static string Label(Place place) => place switch
    {
        Place.Guild => "冒険者ギルド",
        Place.Shop => "商店",
        Place.Smith => "鍛冶屋",
        Place.Pharmacy => "薬局",
        _ => "ダンジョン入口",
    };

    private static IScreen ScreenFor(Place place) => place switch
    {
        Place.Guild => new GuildScreen(),
        Place.Shop => new ShopScreen(),
        Place.Smith => new ForgeScreen(),
        Place.Pharmacy => new PotionCraftScreen(),
        _ => new DungeonSelectScreen(),
    };

    public bool IsOpen { get; private set; }
    private int _cursor;

    /// <summary>
    /// Handles the travel control and, while the menu is up, everything else. Returns true when it has taken
    /// the input, so the caller knows to stop — an open travel menu swallows the frame the way any modal does.
    /// </summary>
    public bool Update(GameContext ctx, Place here)
    {
        var input = ctx.Input;

        if (!IsOpen)
        {
            if (!MenuNav.TravelRequested(input))
                return false;

            IsOpen = true;
            _cursor = Array.IndexOf(Places, here);
            return true;
        }

        if (MenuNav.TravelRequested(input) || MenuNav.Cancelled(input))
        {
            IsOpen = false;
            return true;
        }

        _cursor = MenuNav.Move(input, _cursor, Places.Length);

        if (MenuNav.Confirmed(input))
        {
            var chosen = Places[_cursor];
            IsOpen = false;
            // Choosing where you already are is a way of closing the menu, not a journey.
            if (chosen != here)
                ctx.Screens.ChangeTo(ScreenFor(chosen));
        }

        return true;
    }

    private const float PanelW = 190f;
    private const float RowHeight = 24f;

    public void Draw(GameContext ctx, Place here)
    {
        if (!IsOpen)
            return;

        var r = ctx.Renderer;
        var fonts = ctx.Fonts;

        // Header, the rows themselves, and a clear band at the foot for the hints.
        var h = 38f + Places.Length * RowHeight + 24f;
        var x = (ShopRoom.Size - PanelW) / 2f;
        var y = (400f - h) / 2f;

        r.FillRect(x + 4, y + 5, PanelW, h, Colors.Rgb(0, 0, 0, 120));
        r.FillRect(x, y, PanelW, h, Colors.Rgb(18, 15, 12, 238));
        r.FillRect(x, y, PanelW, 1, Colors.Rgb(132, 106, 70));
        r.DrawRect(x, y, PanelW, h, Colors.Rgb(96, 78, 52));

        fonts.DrawText(r.Handle, "どこへ行きますか", x + 14, y + 11, 12, Colors.Highlight);
        r.FillRect(x + 14, y + 30, PanelW - 28, 1, Colors.Rgb(70, 58, 42));

        var rowY = y + 38f;
        for (var i = 0; i < Places.Length; i++)
        {
            var place = Places[i];
            var label = Label(place);
            var selected = i == _cursor;

            if (selected)
                r.FillRect(x + 10, rowY - 3, PanelW - 20, RowHeight - 4, Colors.Highlight);

            // The place you are standing in stays on the list so the rows never move, but it is greyed and
            // marked, so it reads as "you are here" rather than as somewhere to go.
            var ink = selected
                ? Colors.Black
                : place == here ? Colors.Rgb(126, 120, 112) : Colors.White;
            fonts.DrawText(r.Handle, label, x + 18, rowY, 12, ink);

            if (place == here)
            {
                const string mark = "現在地";
                var (w, _) = fonts.Measure(mark, 9);
                fonts.DrawText(r.Handle, mark, x + PanelW - 18 - w, rowY + 3, 9,
                    selected ? Colors.Black : Colors.Rgb(126, 120, 112));
            }

            rowY += RowHeight;
        }

        ControlHints.DrawCentered(ctx, x + PanelW / 2f, y + h - 16f, 9, Colors.Rgb(150, 142, 130),
            ControlHints.Confirm("移動"), ControlHints.Cancel("やめる"));
    }

    /// <summary>The hint that tells the player this control exists, for a screen's own hint line.</summary>
    public static ControlHint Hint => ControlHints.Travel("移動");
}
