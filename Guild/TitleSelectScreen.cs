using SDL3;
using SlimeDungeon.Core;
using SlimeDungeon.Domain;
using SlimeDungeon.Graphics;
using SlimeDungeon.UI;

namespace SlimeDungeon.Guild;

/// <summary>
/// The guild's register of titles. Every title is listed, earned or not: the unearned ones show what would earn
/// them, so the list doubles as a set of things worth going after. Confirming an earned title puts it on the
/// guild card.
/// </summary>
public sealed class TitleSelectScreen : IScreen
{
    private int _cursor;
    private string? _message;

    private const int VisibleRows = 13;
    private const float RowHeight = 21f;

    /// <summary>Earned titles first (newest last, as awarded), then the rest grouped by category — so the
    /// things you can actually equip are at the top and never move around as you earn more.</summary>
    private static List<TitleDefinition> BuildList(Player player)
    {
        var earned = player.EarnedTitles.Select(Titles.Get).ToList();
        var rest = Titles.All
            .Where(t => !player.EarnedTitles.Contains(t.Id))
            .OrderBy(t => t.Category)
            .ThenBy(t => Array.IndexOf(Titles.All, t));
        return earned.Concat(rest).ToList();
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

        var list = BuildList(player);
        _cursor = MenuNav.Move(input, _cursor, list.Count);

        // Clearing the card entirely is a legitimate choice, so offer it on its own key.
        if (input.WasPressed(SDL.Keycode.X))
        {
            player.DisplayedTitle = null;
            _message = "称号を外しました";
            return;
        }

        if (!MenuNav.Confirmed(input) || list.Count == 0)
            return;

        var picked = list[_cursor];
        if (!player.EarnedTitles.Contains(picked.Id))
        {
            _message = "まだ獲得していません";
            return;
        }

        player.DisplayedTitle = picked.Id;
        _message = $"「{picked.Name}」を掲げました";
    }

    public void Draw(GameContext ctx)
    {
        var r = ctx.Renderer;
        var fonts = ctx.Fonts;
        var player = ctx.Player!;

        r.Clear(Colors.Rgb(24, 20, 16));
        r.DrawTexture(ctx.Sprites.MenuBackdrop, 0, 0, SpriteFactory.MenuBackdropWidth, SpriteFactory.MenuBackdropHeight);

        fonts.DrawText(r.Handle, "称号", 20, 14, 18, Colors.White);

        var list = BuildList(player);
        var earnedCount = player.EarnedTitles.Count;
        var summary = $"獲得 {earnedCount}/{Titles.All.Length}";
        var (sw, _) = fonts.Measure(summary, 11);
        fonts.DrawText(r.Handle, summary, 392 - sw, 20, 11, Colors.Highlight);

        var current = player.DisplayedTitle is { } id ? Titles.NameOf(id) : "なし";
        fonts.DrawText(r.Handle, $"掲げている称号: {current}", 20, 40, 11, Colors.Gold);

        r.FillRect(16, 56, 380, 1, Colors.Rgb(96, 88, 72));

        var scrollTop = Math.Clamp(_cursor - VisibleRows / 2, 0, Math.Max(0, list.Count - VisibleRows));
        var y = 62f;
        for (var i = scrollTop; i < Math.Min(list.Count, scrollTop + VisibleRows); i++)
        {
            var title = list[i];
            var owned = player.EarnedTitles.Contains(title.Id);
            var selected = i == _cursor;
            var displayed = player.DisplayedTitle == title.Id;

            if (selected)
                r.FillRect(16, y - 2, 380, RowHeight - 2, Colors.Highlight);
            else if ((i & 1) == 0)
                r.FillRect(16, y - 2, 380, RowHeight - 2, Colors.Rgb(30, 26, 22));

            // A marker in the gutter: which one is on the card, and which are still locked.
            var marker = displayed ? "★" : owned ? "・" : "×";
            var markerColor = selected
                ? Colors.Black
                : displayed ? Colors.Gold : owned ? Colors.White : Colors.Rgb(96, 92, 88);
            fonts.DrawText(r.Handle, marker, 22, y + 2, 10, markerColor);

            var nameColor = selected ? Colors.Black : owned ? Colors.White : Colors.Rgb(104, 100, 96);
            fonts.DrawText(r.Handle, title.Name, 40, y + 1, 12, nameColor);

            // An earned title reads as a record of the deed that won it. An unearned one keeps its condition
            // to itself — finding out what earns a title is meant to be part of the discovery, not a checklist.
            var note = owned ? title.Requirement : "？？？";
            var noteColor = selected
                ? Colors.Rgb(60, 50, 20)
                : owned ? Colors.Rgb(150, 145, 135) : Colors.Rgb(88, 84, 80);
            fonts.DrawText(r.Handle, note, 176, y + 3, 9, noteColor);

            y += RowHeight;
        }

        if (list.Count > VisibleRows)
            fonts.DrawText(r.Handle, $"({_cursor + 1}/{list.Count})", 340, 40, 9, Colors.Border);

        if (_message is not null)
            fonts.DrawText(r.Handle, _message, 20, 352, 11, Colors.Gold);
        fonts.DrawText(r.Handle, "[Enter]掲げる  [X]外す  [Esc]戻る", 20, 372, 10, Colors.Border);

        StatusPanel.Draw(ctx, 400, 0, 400);
    }

}
