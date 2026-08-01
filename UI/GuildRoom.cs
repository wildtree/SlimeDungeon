using SlimeDungeon.Core;
using SlimeDungeon.Domain;
using SlimeDungeon.Graphics;

namespace SlimeDungeon.UI;

/// <summary>
/// Draws the guild interior together with the lettering that belongs to its fixtures. The nameplate and the
/// date slate are baked into the backdrop art, but their text has to be rendered with the font at runtime — so
/// every screen showing this room goes through here, otherwise the ones that only drew the backdrop would
/// display a blank plaque and an unwritten board.
/// </summary>
public static class GuildRoom
{
    public static void Draw(GameContext ctx, int dayNumber)
    {
        var r = ctx.Renderer;
        var fonts = ctx.Fonts;

        r.DrawTexture(ctx.Sprites.GuildBackdrop, 0, 0, SpriteFactory.GuildBackdropSize, SpriteFactory.GuildBackdropSize);

        // The lettering below is positioned against the procedural room's carved sign and slate. A painted
        // room has its own signage in the artwork, so overlaying ours would land text on empty wall.
        if (ctx.Sprites.GuildBackdropIsArtwork)
        {
            DrawDateOnly(ctx, dayNumber);
            return;
        }

        // Hall name, painted into the carved recess with a shadow so it reads as cut into the timber.
        var (signX, signY, signW, signH) = SpriteFactory.GuildSign;
        const string signText = "ギルド";
        var (signTextW, signTextH) = fonts.Measure(signText, 16);
        var tx = signX + (signW - signTextW) / 2f;
        var ty = signY + (signH - signTextH) / 2f;
        fonts.DrawText(r.Handle, signText, tx + 1, ty + 1, 16, Colors.Rgb(62, 40, 22));
        fonts.DrawText(r.Handle, signText, tx, ty, 16, Colors.Rgb(232, 206, 152));

        // Today's date, chalked on the slate.
        var (boardX, boardY, boardW, _) = SpriteFactory.GuildDateBoard;
        var today = GameCalendar.FromDayNumber(dayNumber);
        var era = $"新暦{GameCalendar.YearLabel(today.Year)}";
        var date = $"{today.MonthName}{today.Day}日";
        var (eraW, _) = fonts.Measure(era, 9);
        var (dateW, _) = fonts.Measure(date, 12);
        fonts.DrawText(r.Handle, era, boardX + (boardW - eraW) / 2f, boardY + 8, 9, Colors.Rgb(168, 176, 166));
        fonts.DrawText(r.Handle, date, boardX + (boardW - dateW) / 2f, boardY + 23, 12, Colors.Rgb(232, 236, 226));
    }

    /// <summary>
    /// The date, for a painted room that has no slate of ours to write on. Drawn as a small plate in the top
    /// corner so it reads as an overlay rather than as part of the illustration.
    /// </summary>
    private static void DrawDateOnly(GameContext ctx, int dayNumber)
    {
        var r = ctx.Renderer;
        var fonts = ctx.Fonts;
        var today = GameCalendar.FromDayNumber(dayNumber);
        var text = $"新暦{GameCalendar.YearLabel(today.Year)} {today.MonthName}{today.Day}日";
        var (w, _) = fonts.Measure(text, 11);

        var x = SpriteFactory.GuildBackdropSize - w - 22f;
        r.FillRect(x - 8, 8, w + 16, 22, Colors.Rgb(0, 0, 0, 150));
        r.DrawRect(x - 8, 8, w + 16, 22, Colors.Rgb(120, 100, 70));
        fonts.DrawText(r.Handle, text, x, 12, 11, Colors.Rgb(240, 232, 210));
    }
}
