using SlimeDungeon.Core;
using SlimeDungeon.Graphics;

namespace SlimeDungeon.UI;

/// <summary>
/// A shop interior and the sheet of business laid over it.
///
/// The three trade screens used to be lists of text on a plain panelled wall, where any layout at all was
/// readable because there was nothing behind it. A painted room is the opposite problem: bare text disappears
/// into it, and a panel big enough to fix that hides the painting the player came to look at. So the sheet is
/// sized to its contents and pinned to the bottom of the room — a short menu leaves the shopkeeper and most of
/// the shelves in view, and only a long list of stock climbs far enough to cover them. It never rises past
/// <see cref="TopLimit"/>, which keeps each shop's painted crest and signboard on screen whatever is open.
///
/// The pictures are square and sit in the same 400x400 slot as the guild room, immediately left of the status
/// panel, so nothing is stretched and nothing is cropped.
/// </summary>
public static class ShopRoom
{
    /// <summary>The square the artwork fills — everything right of this belongs to the status panel.</summary>
    public const float Size = 400f;

    public const float SheetLeft = 10f;
    public const float SheetRight = 390f;
    public const float SheetBottom = 390f;

    /// <summary>The highest the sheet may grow. Below every shop's crest and its hanging sign.</summary>
    public const float TopLimit = 74f;

    /// <summary>Breathing room between the sheet's edge and what is written on it.</summary>
    public const float Pad = 11f;

    public static float ContentX => SheetLeft + Pad;
    public static float ContentWidth => SheetRight - SheetLeft - Pad * 2;

    /// <summary>Room for the control hints and the reply line that every one of these screens ends with.</summary>
    public const float FooterHeight = 34f;

    /// <summary>
    /// The room itself, or the old panelled wall when no picture was supplied. Returns whether a painting was
    /// drawn, which is what tells the caller to leave off its own heading: each illustration has the shop's
    /// name lettered into it already, and ours would land on top of it.
    /// </summary>
    public static bool DrawBackdrop(GameContext ctx, IntPtr art)
    {
        var r = ctx.Renderer;
        r.Clear(Colors.Rgb(24, 20, 16));

        if (art == IntPtr.Zero)
        {
            r.DrawTexture(ctx.Sprites.MenuBackdrop, 0, 0,
                SpriteFactory.MenuBackdropWidth, SpriteFactory.MenuBackdropHeight);
            return false;
        }

        r.DrawTexture(art, 0, 0, Size, Size);
        return true;
    }

    /// <summary>
    /// The one line that shows while nothing is open: the room, and an invitation to say what you came for.
    /// Every shop starts here for the same reason the guild does — the picture is the thing worth looking at,
    /// and a menu that is always up means it is never seen.
    /// </summary>
    public static void DrawPrompt(GameContext ctx, string text)
    {
        var hint = ControlHints.Menu(text);
        var width = ControlHints.Width(ctx, 12, hint) + 20f;
        ctx.Renderer.FillRect(SheetLeft, 366f, width, 22f, Colors.Rgb(0, 0, 0, 150));
        ControlHints.Draw(ctx, SheetLeft + 10f, 370f, 12, Colors.Highlight, hint);
    }

    /// <summary>Where the sheet's top edge lands for a given amount of content, before it is drawn.</summary>
    public static float TopFor(float contentHeight) =>
        Math.Max(TopLimit, SheetBottom - (contentHeight + Pad * 2));

    /// <summary>
    /// Draws the sheet and hands back the y its contents start at. Deliberately not opaque: at this alpha the
    /// room is still faintly legible through it, which keeps the sheet reading as something lying on the
    /// counter rather than as a hole cut in the picture.
    /// </summary>
    public static float Draw(GameContext ctx, float contentHeight)
    {
        var r = ctx.Renderer;
        var top = TopFor(contentHeight);
        var w = SheetRight - SheetLeft;
        var h = SheetBottom - top;

        r.FillRect(SheetLeft + 3, top + 4, w, h, Colors.Rgb(0, 0, 0, 96));
        r.FillRect(SheetLeft, top, w, h, Colors.Rgb(16, 13, 10, 214));
        r.FillRect(SheetLeft, top, w, 1, Colors.Rgb(124, 100, 66));
        r.DrawRect(SheetLeft, top, w, h, Colors.Rgb(88, 72, 48));

        return top + Pad;
    }

    /// <summary>
    /// The reply line and the control hints, in the space <see cref="FooterHeight"/> reserved for them at the
    /// foot of the sheet. Fixed to the sheet's bottom rather than following the content, so the hints do not
    /// wander up and down the screen as a list grows and shrinks.
    /// </summary>
    public static void DrawFooter(GameContext ctx, string? message, params ControlHint[] hints)
    {
        if (message is not null)
            ctx.Fonts.DrawText(ctx.Renderer.Handle, message, ContentX, SheetBottom - 32f, 11, Colors.Gold);

        ControlHints.Draw(ctx, ContentX, SheetBottom - 16f, 10, Colors.Rgb(168, 160, 148), hints);
    }
}
