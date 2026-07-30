using SDL3;
using SlimeDungeon.Core;
using SlimeDungeon.Data;
using SlimeDungeon.Graphics;

namespace SlimeDungeon.Guild;

public sealed class TitleScreen : IScreen
{
    private float _time;

    /// <summary>Slimes loitering in the doorway: x offset from the arch centre, size, and animation phase.</summary>
    private static readonly (int OffsetX, int Size, float Phase, SlimeColor Color)[] DoorwaySlimes =
    [
        (-44, 30, 0.0f, SlimeColor.Blue),
        (2, 42, 1.9f, SlimeColor.Green),
        (44, 26, 3.4f, SlimeColor.Red),
    ];

    public void Update(GameContext ctx, float dt)
    {
        _time += dt;

        if (ctx.Input.WasPressed(SDL.Keycode.Escape))
            ctx.Input.RequestQuit();

        if (SaveManager.HasSave)
        {
            if (ctx.Input.WasPressed(SDL.Keycode.C))
            {
                ctx.Player = SaveManager.Load();
                if (ctx.Player is not null)
                {
                    ctx.Screens.ChangeTo(new GuildScreen());
                    return;
                }
            }
            if (ctx.Input.WasPressed(SDL.Keycode.N))
                ctx.Screens.ChangeTo(new NamingScreen());
        }
        else if (ctx.Input.WasPressed(SDL.Keycode.Return) || ctx.Input.WasPressed(SDL.Keycode.Space))
        {
            ctx.Screens.ChangeTo(new NamingScreen());
        }
    }

    public void Draw(GameContext ctx)
    {
        var r = ctx.Renderer;
        r.Clear(Colors.Rgb(8, 8, 12));
        r.DrawTexture(ctx.Sprites.TitleBackdrop, 0, 0, TitleArt.BackdropWidth, TitleArt.BackdropHeight);

        DrawSlimesInDoorway(ctx);

        foreach (var (tx, ty) in TitleArt.TorchPositions)
            TorchFlame.Draw(r, tx, ty - 8, _time);

        // The wordmark drifts a couple of pixels, just enough to keep the screen from feeling frozen.
        var logoY = 8 + (float)Math.Sin(_time * 1.1) * 3f;
        r.DrawTexture(ctx.Sprites.TitleLogo, (TitleArt.BackdropWidth - TitleArt.LogoWidth) / 2f, logoY,
            TitleArt.LogoWidth, TitleArt.LogoHeight);

        DrawPrompt(ctx);
    }

    private void DrawSlimesInDoorway(GameContext ctx)
    {
        var r = ctx.Renderer;
        foreach (var (offsetX, size, phase, color) in DoorwaySlimes)
        {
            var (idle, hop) = ctx.Sprites.Slime(color);
            var cycle = Math.Sin(_time * 2.2 + phase);
            var texture = cycle > 0 ? hop : idle;
            var lift = (float)Math.Abs(cycle) * 6f;
            var x = TitleArt.ArchCenterX + offsetX - size / 2f;
            var y = TitleArt.FloorY - 18 - size - lift;
            r.DrawTexture(texture, x, y, size, size);
        }
    }

    private void DrawPrompt(GameContext ctx)
    {
        var r = ctx.Renderer;
        var text = SaveManager.HasSave ? "[C] 続きから      [N] 新規登録" : "[Enter] 冒険者登録";

        // Quantised pulse: the font cache is keyed by colour, so a handful of discrete steps keeps it from
        // filling up with a new texture every frame the way a smooth fade would.
        var steps = new[] { 130, 170, 210, 245 };
        var index = (int)((Math.Sin(_time * 2.6) * 0.5 + 0.5) * (steps.Length - 1) + 0.5);
        var level = (byte)steps[Math.Clamp(index, 0, steps.Length - 1)];
        var color = Colors.Rgb(level, (byte)(level * 0.92), (byte)(level * 0.55));

        var (w, _) = ctx.Fonts.Measure(text, 14);
        ctx.Fonts.DrawText(r.Handle, text, (TitleArt.BackdropWidth - w) / 2f, 356, 14, color);

        const string hint = "[Esc] 終了";
        var (hw, _) = ctx.Fonts.Measure(hint, 10);
        ctx.Fonts.DrawText(r.Handle, hint, TitleArt.BackdropWidth - hw - 12, 382, 10, Colors.Rgb(120, 112, 100));
    }
}
