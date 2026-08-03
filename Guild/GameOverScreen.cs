using SDL3;
using SlimeDungeon.Core;
using SlimeDungeon.Data;
using SlimeDungeon.Domain;
using SlimeDungeon.UI;

namespace SlimeDungeon.Guild;

/// <summary>
/// Shown when the player's HP hits 0: archives the character to history and starts a fresh one.
///
/// The epitaph is the last thing anyone sees of a character, and permadeath means there is no going back for a
/// second look — so the whole career is stated here rather than left in the register. Everything is lit down to
/// match the graveyard behind it: bone and ash rather than white, so it reads as lettering cut into the scene
/// rather than as a menu dropped on top of a painting.
/// </summary>
public sealed class GameOverScreen : IScreen
{
    private bool _archived;

    /// <summary>
    /// The epitaph, taken once as the character is archived. Read from a snapshot rather than from
    /// <see cref="GameContext.Player"/> because that is cleared the moment the screen is dismissed, and the
    /// dead should not go anonymous halfway through the frame that says goodbye to them.
    /// </summary>
    private string _name = "";
    private string _rank = "";
    private string _title = "";
    private int _level;
    private int _gold;
    private int _days;
    private int _kills;

    public void OnEnter(GameContext ctx)
    {
        if (_archived || ctx.Player is not { } player)
            return;

        _name = player.Name;
        _rank = player.Rank.Label();
        _title = player.DisplayedTitle is { } id ? Titles.NameOf(id) : "なし";
        _level = player.Level;
        _gold = player.Gold;
        _days = player.DaysSurvived;
        _kills = player.TotalSlimesDefeated;

        SaveManager.ArchiveToHistory(player);
        SaveManager.DeleteActive();
        _archived = true;
    }

    public void Update(GameContext ctx, float dt)
    {
        if (MenuNav.Confirmed(ctx.Input))
        {
            // Cut the lament here rather than letting the title theme displace it. Walking away from the
            // grave is the player's decision, and the music should stop when they make it.
            ctx.Audio.StopMusic();
            ctx.Player = null;
            ctx.Screens.ChangeTo(new TitleScreen());
        }
    }

    // The epitaph goes above the headstone's crown and the record sits just under it, across the upper face of
    // the stone. Laid out in two columns and kept short on purpose: a single column of six ran far enough down
    // the stone to bury the R.I.P. carved into it, which is the one thing on the picture worth not covering.
    private const float CentreX = 320f;
    private const float EpitaphY = 34f;
    private const float SummaryTop = 84f;
    private const float RowHeight = 18f;

    /// <summary>Where each column's label ends and its value begins.</summary>
    private const float LeftDivider = 250f;
    private const float RightDivider = 408f;
    private const float Gutter = 7f;

    /// <summary>Bone, for the one line that has to carry weight. Deliberately short of white.</summary>
    private static readonly SDL.Color Epitaph = Colors.Rgb(216, 210, 198);

    /// <summary>Weathered lettering for the record itself: readable against the stone, never bright.</summary>
    private static readonly SDL.Color LabelColor = Colors.Rgb(148, 144, 136);
    private static readonly SDL.Color ValueColor = Colors.Rgb(198, 192, 180);

    public void Draw(GameContext ctx)
    {
        var r = ctx.Renderer;
        var fonts = ctx.Fonts;

        r.Clear(Colors.Black);
        if (ctx.Sprites.RipBackdrop != IntPtr.Zero)
            r.DrawTexture(ctx.Sprites.RipBackdrop, 0, 0, 640, 400);

        // The epitaph sits against the night sky, dark enough to carry light text on its own. The dropped
        // shadow is for where a bare branch crosses behind it.
        var line = $"{_name}は死んでしまった";
        var (lw, _) = fonts.Measure(line, 22);
        fonts.DrawText(r.Handle, line, CentreX - lw / 2f + 1, EpitaphY + 1, 22, Colors.Rgb(0, 0, 0, 190));
        fonts.DrawText(r.Handle, line, CentreX - lw / 2f, EpitaphY, 22, Epitaph);

        // A scrim behind the record, because it lies across the headstone and the grass either side of it —
        // one is pale, the other is not, and the text has to be readable over both without being lit up.
        const float scrimW = 380f;
        const float scrimH = 3 * RowHeight + 20f;
        r.FillRect(CentreX - scrimW / 2f, SummaryTop - 10f, scrimW, scrimH, Colors.Rgb(8, 8, 10, 170));

        // Two paired columns for the short facts, then the title on a line of its own — it is the only value
        // long enough to need the full width.
        var y = SummaryTop;
        Pair(LeftDivider, "到達ランク", _rank, y);
        Pair(RightDivider, "レベル", $"{_level}", y);
        y += RowHeight;
        Pair(LeftDivider, "所持金", $"{_gold}G", y);
        Pair(RightDivider, "討伐数", $"{_kills}体", y);
        y += RowHeight;
        Pair(LeftDivider, "生存日数", $"{_days}日", y);
        Pair(RightDivider, "称号", _title, y);

        // Labels right-aligned onto the divider, values left-aligned off it, so each column of values lines up
        // under itself however long the labels are.
        void Pair(float divider, string label, string value, float rowY)
        {
            var (labelW, _) = fonts.Measure(label, 11);
            fonts.DrawText(r.Handle, label, divider - Gutter - labelW, rowY + 1, 11, LabelColor);
            fonts.DrawText(r.Handle, value, divider + Gutter, rowY, 12, ValueColor);
        }

        ControlHints.DrawCentered(ctx, CentreX, 372, 10, Colors.Rgb(150, 146, 138),
            ControlHints.Confirm("タイトルへ"));
    }
}
