using SDL3;
using SlimeDungeon.Core;
using SlimeDungeon.Data;
using SlimeDungeon.Domain;
using SlimeDungeon.Graphics;
using SlimeDungeon.UI;

namespace SlimeDungeon.Guild;

/// <summary>
/// The bounty desk. Slimes carry no money, so this is where a trip's work turns into gold: the guild reads back
/// what you brought down, prices it by rank and by how uncommon the species is, and pays the lot in one go.
/// </summary>
public sealed class BountyScreen : IScreen
{
    private const int VisibleRows = 9;
    private const float RowHeight = 22f;

    private List<BountyLine> _lines = new();
    private int _paid = -1;
    private int _scroll;

    public void OnEnter(GameContext ctx) => _lines = ctx.Player!.PendingBounty;

    public void Update(GameContext ctx, float dt)
    {
        var input = ctx.Input;
        var player = ctx.Player!;

        if (MenuNav.Cancelled(input))
        {
            ctx.Screens.ChangeTo(new GuildScreen());
            return;
        }

        // Once paid, the only thing left to do is leave.
        if (_paid >= 0)
        {
            if (MenuNav.Confirmed(input))
                ctx.Screens.ChangeTo(new GuildScreen());
            return;
        }

        if (MenuNav.Down(input) && _scroll < Math.Max(0, _lines.Count - VisibleRows))
            _scroll++;
        if (MenuNav.Up(input) && _scroll > 0)
            _scroll--;

        if (!MenuNav.Confirmed(input) || _lines.Count == 0)
            return;

        _paid = player.ClaimBounty();
        SaveManager.Save(player);
    }

    public void Draw(GameContext ctx)
    {
        var r = ctx.Renderer;
        var fonts = ctx.Fonts;
        var player = ctx.Player!;

        r.Clear(Colors.Rgb(24, 20, 16));
        r.DrawTexture(ctx.Sprites.MenuBackdrop, 0, 0, SpriteFactory.MenuBackdropWidth, SpriteFactory.MenuBackdropHeight);

        fonts.DrawText(r.Handle, "討伐報酬", 20, 14, 18, Colors.White);

        if (_lines.Count == 0)
        {
            fonts.DrawText(r.Handle, "報告できる討伐がありません。", 20, 60, 12, Colors.Highlight);
            fonts.DrawText(r.Handle, "ダンジョンでスライムを倒してから来てください。", 20, 82, 10, Colors.Border);
            ControlHints.Draw(ctx, 20, 372, 10, Colors.Border, ControlHints.Cancel("戻る"));
            StatusPanel.Draw(ctx, 400, 0, 400);
            return;
        }

        var total = _lines.Sum(l => l.Total);
        var headCount = _lines.Sum(l => l.Count);
        fonts.DrawText(r.Handle, $"討伐 {headCount}体 / {_lines.Count}種", 20, 40, 11, Colors.Highlight);

        // Column headings, matching the rows below.
        fonts.DrawText(r.Handle, "スライム", 46, 60, 9, Colors.Border);
        fonts.DrawText(r.Handle, "ランク", 186, 60, 9, Colors.Border);
        fonts.DrawText(r.Handle, "数", 246, 60, 9, Colors.Border);
        RightAligned(ctx, "単価", 320, 60, 9, Colors.Border);
        RightAligned(ctx, "小計", 388, 60, 9, Colors.Border);
        r.FillRect(16, 74, 380, 1, Colors.Rgb(96, 88, 72));

        var y = 80f;
        foreach (var line in _lines.Skip(_scroll).Take(VisibleRows))
        {
            var (sprite, _) = ctx.Sprites.Slime(line.Color);
            r.DrawTexture(sprite, 22, y, 18, 18);
            fonts.DrawText(r.Handle, $"{Bounty.ColorLabel(line.Color)}", 46, y + 3, 11, Colors.White);
            fonts.DrawText(r.Handle, line.Rank.Label(), 186, y + 4, 10, Colors.Highlight);
            fonts.DrawText(r.Handle, $"{line.Count}", 246, y + 4, 10, Colors.White);
            RightAligned(ctx, $"{line.PerHead}G", 320, y + 4, 10, Colors.Rgb(180, 174, 160));
            RightAligned(ctx, $"{line.Total}G", 388, y + 3, 11, Colors.Gold);
            y += RowHeight;
        }

        if (_lines.Count > VisibleRows)
            ControlHints.Draw(ctx, 20, 292, 9, Colors.Border,
                ControlHints.Direction($"{_scroll + 1}-{Math.Min(_lines.Count, _scroll + VisibleRows)}/{_lines.Count}"));

        r.FillRect(16, 306, 380, 1, Colors.Rgb(96, 88, 72));
        RightAligned(ctx, $"合計 {total}G", 388, 314, 15, Colors.Gold);

        if (_paid >= 0)
        {
            fonts.DrawText(r.Handle, $"{_paid}Gを受け取った！", 20, 348, 13, Colors.Gold);
            ControlHints.Draw(ctx, 20, 372, 10, Colors.Border, ControlHints.Confirm("戻る"));
        }
        else
        {
            ControlHints.Draw(ctx, 20, 372, 10, Colors.Border, ControlHints.Confirm("報酬を受け取る"), ControlHints.Cancel("戻る"));
        }

        StatusPanel.Draw(ctx, 400, 0, 400);
    }

    private static void RightAligned(GameContext ctx, string text, float right, float y, float size, SDL.Color color)
    {
        var (w, _) = ctx.Fonts.Measure(text, size);
        ctx.Fonts.DrawText(ctx.Renderer.Handle, text, right - w, y, size, color);
    }

    public void OnExit(GameContext ctx) { }
}
