using SDL3;
using SlimeDungeon.Core;
using SlimeDungeon.Data;
using SlimeDungeon.Domain;
using SlimeDungeon.Graphics;
using SlimeDungeon.UI;

namespace SlimeDungeon.Guild;

/// <summary>
/// The guild hub. Only guild business is on the counter now — the shop, the smith, the alchemist, the inn and
/// the dungeon are all places of their own, reached from the travel menu.
/// </summary>
public sealed class GuildScreen : IScreen
{
    private int _cursor;
    private string? _message;
    private List<TitleDefinition> _newTitles = new();

    /// <summary>Which row of the award popup is highlighted. Starts on "変更しない", the last row.</summary>
    private int _titleCursor;

    /// <summary>A contract that ran out of days while you were away; cleared once acknowledged.</summary>
    private QuestFailure? _questFailure;

    private bool _menuOpen;
    private readonly FieldMagicMenu _magic = new();
    private readonly TravelMenu _travel = new();

    public void OnEnter(GameContext ctx)
    {
        var player = ctx.Player!;

        if (player.ActiveQuest is { } quest && quest.IsExpired(player.DayCount))
        {
            _questFailure = player.FailExpiredQuest(quest);
            ctx.Audio.Play(SoundId.QuestFailed);
        }

        QuestFactory.RefillExpiredBoardSlots(player);

        // Titles are settled up here rather than at each individual deed: the player always comes back to the
        // guild, so one check on arrival catches everything — kills, chests, crafting, days survived — and the
        // guild recognising your work on your return is where the announcement belongs anyway.
        _newTitles = Titles.Refresh(player);
        if (_newTitles.Count > 0)
        {
            ctx.Audio.Play(SoundId.TitleFanfare);
            // On "変更しない", so a stray confirm never silently replaces the title you chose to wear.
            _titleCursor = TitleAwardPopup.RowCount(_newTitles) - 1;
        }

        _cursor = 0;
        SaveManager.Save(player);
    }

    /// <summary>
    /// What the counter offers. The shop, the smith, the alchemist and the dungeon used to be on this list;
    /// they are places rather than errands, so they moved to the travel menu and left the counter with only
    /// guild business on it. The last three are the same entries the dungeon menu carries, so the things you
    /// reach for most — your pack, your record, a heal — are in the same place wherever you are.
    /// </summary>
    private enum Entry
    {
        Quest, Bounty, Titles, Trophies, Records,
        Items, Equipment, KillLog, Magic, Options,
    }

    private static Entry[] Entries =>
    [
        Entry.Quest, Entry.Bounty, Entry.Titles, Entry.Trophies, Entry.Records,
        Entry.Items, Entry.Equipment, Entry.KillLog, Entry.Magic, Entry.Options,
    ];

    private static string Label(Entry entry, Player player) => entry switch
    {
        Entry.Quest => "クエスト",
        // The pending total is on the label itself: gold from slimes is only collected here now, so leaving it
        // unclaimed has to be visible from the hub rather than something you have to go and look for.
        Entry.Bounty => player.PendingBountyTotal > 0 ? $"討伐報酬 ({player.PendingBountyTotal}G)" : "討伐報酬",
        Entry.Titles => "称号",
        Entry.Trophies => player.TrophyCases > 0
            ? $"展示室 ({player.Trophies.Count}/{player.TrophyCases})"
            : "展示室",
        Entry.Records => "冒険者の記録",
        Entry.Items => "アイテム",
        Entry.Equipment => "装備",
        Entry.KillLog => "討伐記録",
        Entry.Magic => "まほう",
        _ => "オプション",
    };

    public void Update(GameContext ctx, float dt)
    {
        var input = ctx.Input;
        var player = ctx.Player!;

        // A failed contract is the first thing you hear about, ahead of any good news — it happened before you
        // walked through the door, and a title fanfare over the top of it would read very strangely.
        if (_questFailure is not null)
        {
            if (MenuNav.Confirmed(input))
                _questFailure = null;
            return;
        }

        // Newly awarded titles are acknowledged before anything else can be done — and answered, since the
        // popup now asks which of them to wear rather than just announcing them.
        if (_newTitles.Count > 0)
        {
            _titleCursor = MenuNav.Move(input, _titleCursor, TitleAwardPopup.RowCount(_newTitles));
            if (MenuNav.Confirmed(input))
            {
                if (TitleAwardPopup.Chosen(_newTitles, _titleCursor) is { } picked)
                {
                    player.DisplayedTitle = picked;
                    _message = $"「{Titles.NameOf(picked)}」を掲げました";
                    // Written straight away. The guild only saves on arrival, and the answer to this question
                    // is given after that — so without this, quitting from the counter would throw the choice
                    // away and the player would come back wearing whatever they had before.
                    SaveManager.Save(player);
                }
                _newTitles = new List<TitleDefinition>();
                _titleCursor = 0;
            }
            return;
        }

        if (_magic.IsOpen)
        {
            if (_magic.Update(ctx) is { } spellMessage)
                _message = spellMessage;
            return;
        }

        // Leaving for somewhere else is available from anywhere on this screen, open counter or not.
        if (_travel.Update(ctx, Place.Guild))
            return;

        // The counter is clear until asked for. The room is the thing worth looking at; the list of errands
        // sat on top of it permanently for no reason other than that it always had.
        if (!_menuOpen)
        {
            if (MenuNav.MenuRequested(input) || MenuNav.Confirmed(input))
            {
                _menuOpen = true;
                _message = null;
            }
            return;
        }

        if (MenuNav.Cancelled(input) || MenuNav.MenuRequested(input))
        {
            _menuOpen = false;
            return;
        }

        var entries = Entries;
        _cursor = MenuNav.Move(input, _cursor, entries.Length);

        if (!MenuNav.Confirmed(input))
            return;

        switch (entries[_cursor])
        {
            case Entry.Quest: ctx.Screens.ChangeTo(new QuestBoardScreen()); break;
            case Entry.Bounty: ctx.Screens.ChangeTo(new BountyScreen()); break;
            case Entry.Titles: ctx.Screens.ChangeTo(new TitleSelectScreen()); break;
            case Entry.Trophies: ctx.Screens.ChangeTo(new TrophyHallScreen()); break;
            case Entry.Records: ctx.Screens.ChangeTo(new RecordsScreen()); break;
            case Entry.Items: ctx.ShowItems = true; _menuOpen = false; break;
            case Entry.Equipment: ctx.ShowEquipment = true; _menuOpen = false; break;
            case Entry.KillLog: ctx.ShowKillLog = true; _menuOpen = false; break;
            case Entry.Magic:
                if (!_magic.TryOpen(player, out var reason))
                    _message = reason;
                break;
            case Entry.Options: ctx.ShowOptions = true; _menuOpen = false; break;
        }
    }


    public void Draw(GameContext ctx)
    {
        var r = ctx.Renderer;
        r.Clear(Colors.Rgb(24, 20, 16));
        var fonts = ctx.Fonts;
        var player = ctx.Player!;
        var labels = Entries.Select(e => Label(e, player)).ToArray();

        GuildRoom.Draw(ctx, player.DayCount);

        if (!_menuOpen)
        {
            // Nothing over the room but the prompt to open the counter, and the way out to everywhere else.
            var prompt = ControlHints.Menu("ご用件をどうぞ");
            var travel = TravelMenu.Hint;
            r.FillRect(10, 366, ControlHints.Width(ctx, 12, prompt, travel) + 20, 22, Colors.Rgb(0, 0, 0, 150));
            ControlHints.Draw(ctx, 20, 370, 12, Colors.Highlight, prompt, travel);

            if (_message is not null)
                DrawReceptionistSay(ctx, _message, 14f, 356f);

            StatusPanel.Draw(ctx, 400, 0, 400);
            _travel.Draw(ctx, Place.Guild);
            // Same order the input handler acknowledges them in: the bad news is on top.
            if (_questFailure is { } failed)
                QuestFailedPopup.Draw(ctx, failed);
            else if (_newTitles.Count > 0)
                TitleAwardPopup.Draw(ctx, _newTitles, _titleCursor);
            return;
        }

        // The command list as a sheet of guild business lying on the counter, rather than a dark panel floating
        // over the scene. Drawn at runtime rather than baked, because the other screens that share this
        // backdrop (registration, promotion) have no menu on the counter.
        const float rowHeight = 20f;
        var maxWidth = MenuNav.MaxLabelWidth(ctx, labels, 12);
        var sheetX = 14f;
        var sheetW = Math.Max(168f, maxWidth + 34f);
        var sheetH = labels.Length * rowHeight + 34f;
        var sheetY = 396f - sheetH;

        var paper = Colors.Rgb(232, 218, 186);
        var paperEdge = Colors.Rgb(196, 178, 142);
        var paperShade = Colors.Rgb(214, 198, 164);
        var ink = Colors.Rgb(62, 46, 30);

        r.FillRect(sheetX + 3, sheetY + 3, sheetW, sheetH, Colors.Rgb(96, 62, 36));
        r.FillRect(sheetX, sheetY, sheetW, sheetH, paper);
        r.FillRect(sheetX, sheetY, sheetW, 1, Colors.Rgb(246, 236, 210));
        r.FillRect(sheetX, sheetY + sheetH - 2, sheetW, 2, paperShade);
        r.DrawRect(sheetX, sheetY, sheetW, sheetH, paperEdge);

        fonts.DrawText(r.Handle, "ご用件", sheetX + 10, sheetY + 7, 10, Colors.Rgb(122, 96, 64));
        r.FillRect(sheetX + 10, sheetY + 21, sheetW - 20, 1, paperEdge);

        var y = sheetY + 27f;
        for (var i = 0; i < labels.Length; i++)
        {
            var selected = i == _cursor;
            if (selected)
            {
                r.FillRect(sheetX + 6, y - 3, sheetW - 12, rowHeight - 2, Colors.Rgb(206, 168, 88));
                r.FillRect(sheetX + 6, y - 3, 3, rowHeight - 2, Colors.Rgb(148, 106, 44));
            }
            fonts.DrawText(r.Handle, labels[i], sheetX + 14, y, 12, ink);
            y += rowHeight;
        }

        if (_message is not null)
            DrawReceptionistSay(ctx, _message, sheetX, sheetY);

        StatusPanel.Draw(ctx, 400, 0, 400);

        if (_magic.IsOpen)
            _magic.Draw(ctx, 210, 150);

        _travel.Draw(ctx, Place.Guild);

        if (_questFailure is { } expired)
            QuestFailedPopup.Draw(ctx, expired);
        else if (_newTitles.Count > 0)
            TitleAwardPopup.Draw(ctx, _newTitles, _titleCursor);
    }

    /// <summary>
    /// The reply to whatever the player just asked for, as a speech bubble on the wall beside the receptionist.
    /// It used to be bare gold text laid straight over the backdrop, which was unreadable against the timber and
    /// her vest — and these lines are hers, so a bubble says who is speaking as well as making it legible.
    /// </summary>
    private static void DrawReceptionistSay(GameContext ctx, string text, float sheetX, float sheetY)
    {
        var r = ctx.Renderer;
        var fonts = ctx.Fonts;

        const float fontSize = 11f;
        var (textW, textH) = fonts.Measure(text, fontSize);

        var w = textW + 20f;
        var h = textH + 12f;
        var x = sheetX;
        // Sits just above the command sheet, so it is read on the way to the menu rather than hunted for. The
        // sheet's top moves with the number of commands, hence anchoring to it instead of a fixed line.
        var y = sheetY - 12f - h;

        var fill = Colors.Rgb(250, 246, 236);
        var edge = Colors.Rgb(146, 122, 92);
        var ink = Colors.Rgb(54, 40, 26);

        r.FillRect(x + 2, y + 3, w, h, Colors.Rgb(150, 132, 106));
        r.FillRect(x, y, w, h, fill);
        r.DrawRect(x, y, w, h, edge);

        // Tail pointing right, toward the receptionist standing behind the counter. Drawn as an outline
        // triangle with the fill inset over it — the renderer only fills rectangles, so it is built column by
        // column. It starts a pixel inside the bubble so the border does not cut across the join.
        const float tailW = 13f;
        var tailCy = y + h * 0.62f;
        for (var i = 0f; i < tailW; i++)
        {
            var half = (tailW - i) * 0.5f + 1f;
            r.FillRect(x + w - 2 + i, tailCy - half, 1, half * 2, edge);
        }
        for (var i = 0f; i < tailW - 2; i++)
        {
            var half = (tailW - 2 - i) * 0.5f;
            r.FillRect(x + w - 2 + i, tailCy - half, 1, half * 2, fill);
        }

        fonts.DrawText(r.Handle, text, x + 10, y + 6, fontSize, ink);
    }
}
