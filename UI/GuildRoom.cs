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
}
