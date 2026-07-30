using SDL3;
using SlimeDungeon.Core;
using SlimeDungeon.Data;
using SlimeDungeon.Domain;

namespace SlimeDungeon.Guild;

/// <summary>Shown when the player's HP hits 0: archives the character to history and starts a fresh one.</summary>
public sealed class GameOverScreen : IScreen
{
    private bool _archived;

    public void OnEnter(GameContext ctx)
    {
        if (_archived || ctx.Player is not { } player)
            return;

        SaveManager.ArchiveToHistory(player);
        SaveManager.DeleteActive();
        _archived = true;
    }

    public void Update(GameContext ctx, float dt)
    {
        if (ctx.Input.WasPressed(SDL.Keycode.Return) || ctx.Input.WasPressed(SDL.Keycode.Space))
        {
            ctx.Player = null;
            ctx.Screens.ChangeTo(new TitleScreen());
        }
    }

    public void Draw(GameContext ctx)
    {
        var r = ctx.Renderer;
        r.Clear(Colors.Black);
        var name = ctx.Player?.Name ?? "";
        var rank = ctx.Player?.Rank.Label() ?? "";
        var level = ctx.Player?.Level ?? 0;

        ctx.Fonts.DrawText(r.Handle, $"{name}は力尽きた…", 200, 140, 20, Colors.White);
        ctx.Fonts.DrawText(r.Handle, $"到達ランク: {rank}  LV {level}", 220, 190, 13, Colors.Highlight);
        ctx.Fonts.DrawText(r.Handle, "Enterでタイトルへ", 250, 240, 12, Colors.Border);
    }
}
