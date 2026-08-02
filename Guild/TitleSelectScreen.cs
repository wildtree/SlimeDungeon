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

    /// <summary>Space the title's name has before the requirement column starts at x=176.</summary>
    private const float NameColumnWidth = 132f;

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
        // The list carries one extra row at the top for taking the card down. Clearing it was a hidden key
        // before — X once, then D and the pad's north button — and a hidden key can only be explained by
        // printing its name, which is exactly what we are trying to stop doing. As a row it needs no key of
        // its own and no hint: you move to it and confirm, like everything else on the screen.
        _cursor = MenuNav.Move(input, _cursor, list.Count + 1);

        if (!MenuNav.Confirmed(input))
            return;

        if (_cursor == 0)
        {
            player.DisplayedTitle = null;
            _message = "称号を外しました";
            return;
        }

        if (list.Count == 0)
            return;

        var picked = list[_cursor - 1];
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

        // Row 0 is "take the card down"; the titles follow after it.
        var rowCount = list.Count + 1;
        var scrollTop = Math.Clamp(_cursor - VisibleRows / 2, 0, Math.Max(0, rowCount - VisibleRows));
        var y = 62f;
        for (var i = scrollTop; i < Math.Min(rowCount, scrollTop + VisibleRows); i++)
        {
            var selected = i == _cursor;

            if (selected)
                r.FillRect(16, y - 2, 380, RowHeight - 2, Colors.Highlight);
            else if ((i & 1) == 0)
                r.FillRect(16, y - 2, 380, RowHeight - 2, Colors.Rgb(30, 26, 22));

            if (i == 0)
            {
                var none = player.DisplayedTitle is null;
                fonts.DrawText(r.Handle, none ? "★" : "・", 22, y + 2, 10,
                    selected ? Colors.Black : none ? Colors.Gold : Colors.White);
                fonts.DrawText(r.Handle, "称号を外す", 40, y + 1, 12, selected ? Colors.Black : Colors.White);
                fonts.DrawText(r.Handle, "ギルドカードを空欄にする", 176, y + 3, 9,
                    selected ? Colors.Rgb(60, 50, 20) : Colors.Rgb(150, 145, 135));
                y += RowHeight;
                continue;
            }

            var title = list[i - 1];
            var owned = player.EarnedTitles.Contains(title.Id);
            var displayed = player.DisplayedTitle == title.Id;

            // A marker in the gutter: which one is on the card, and which are still locked.
            var marker = displayed ? "★" : owned ? "・" : "×";
            var markerColor = selected
                ? Colors.Black
                : displayed ? Colors.Gold : owned ? Colors.White : Colors.Rgb(96, 92, 88);
            fonts.DrawText(r.Handle, marker, 22, y + 2, 10, markerColor);

            // An unearned title gives away nothing at all — not its name and not what earns it. The register
            // says how many there are to find and no more; discovering that a thing is even possible is meant
            // to be part of playing, rather than a checklist handed over on day one.
            var name = owned ? title.Name : "？？？";
            var nameColor = selected ? Colors.Black : owned ? Colors.White : Colors.Rgb(104, 100, 96);

            // "ドラゴンスライムスレイヤー" is long enough to run into the requirement column beside it, so a
            // name that does not fit is set a little smaller rather than allowed to collide.
            var nameSize = 12f;
            while (nameSize > 9f && fonts.Measure(name, nameSize).Item1 > NameColumnWidth)
                nameSize -= 0.5f;
            fonts.DrawText(r.Handle, name, 40, y + 1 + (12f - nameSize) * 0.5f, nameSize, nameColor);

            var note = owned ? title.Requirement : "？？？";
            var noteColor = selected
                ? Colors.Rgb(60, 50, 20)
                : owned ? Colors.Rgb(150, 145, 135) : Colors.Rgb(88, 84, 80);
            fonts.DrawText(r.Handle, note, 176, y + 3, 9, noteColor);

            y += RowHeight;
        }

        if (rowCount > VisibleRows)
            fonts.DrawText(r.Handle, $"({_cursor + 1}/{rowCount})", 340, 40, 9, Colors.Border);

        if (_message is not null)
            fonts.DrawText(r.Handle, _message, 20, 352, 11, Colors.Gold);
        ControlHints.Draw(ctx, 20, 372, 10, Colors.Border,
            ControlHints.Direction("選ぶ"), ControlHints.Confirm("掲げる"), ControlHints.Cancel("戻る"));

        StatusPanel.Draw(ctx, 400, 0, 400);
    }

}
