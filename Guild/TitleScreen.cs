using SDL3;
using SlimeDungeon.Core;
using SlimeDungeon.Data;
using SlimeDungeon.Graphics;
using SlimeDungeon.UI;



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

        var input = ctx.Input;

        // A cursor list rather than the old lettered shortcuts, so the title screen can be driven from a
        // gamepad — which cannot press C or N — like everything else.
        var options = Options();
        _cursor = MenuNav.Move(input, _cursor, options.Length);

        if (MenuNav.Confirmed(input))
        {
            switch (options[_cursor])
            {
                case Option.Continue:
                    ctx.Player = SaveManager.Load();
                    if (ctx.Player is not null)
                        ctx.Screens.ChangeTo(new GuildScreen());
                    break;
                case Option.NewAdventurer:
                    ctx.Screens.ChangeTo(new NamingScreen());
                    break;
                case Option.Quit:
                    input.RequestQuit();
                    break;
            }
        }
    }

    private enum Option { Continue, NewAdventurer, Quit }

    private int _cursor;

    private static Option[] Options() => SaveManager.HasSave
        ? [Option.Continue, Option.NewAdventurer, Option.Quit]
        : [Option.NewAdventurer, Option.Quit];

    private static string OptionLabel(Option option) => option switch
    {
        Option.Continue => "続きから",
        Option.NewAdventurer => "冒険者登録",
        _ => "終了",
    };

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
        // Quantised pulse on the selected row: the font cache is keyed by colour, so a handful of discrete
        // steps keeps it from filling up with a new texture every frame the way a smooth fade would.
        var steps = new[] { 150, 190, 225, 255 };
        var index = (int)((Math.Sin(_time * 2.6) * 0.5 + 0.5) * (steps.Length - 1) + 0.5);
        var level = (byte)steps[Math.Clamp(index, 0, steps.Length - 1)];
        var selectedColor = Colors.Rgb(level, (byte)(level * 0.92), (byte)(level * 0.55));

        var options = Options();
        var y = 340f;
        for (var i = 0; i < options.Length; i++)
        {
            var label = OptionLabel(options[i]);
            var selected = i == _cursor;
            var (w, _) = ctx.Fonts.Measure(label, selected ? 15 : 13);
            ctx.Fonts.DrawText(r.Handle, label, (TitleArt.BackdropWidth - w) / 2f, y,
                selected ? 15 : 13, selected ? selectedColor : Colors.Rgb(122, 116, 104));
            y += 20;
        }

        var hint = $"[↑↓]選ぶ  [{MenuNav.ConfirmHint(ctx.Input)}]決定";
        var (hw, _) = ctx.Fonts.Measure(hint, 10);
        ctx.Fonts.DrawText(r.Handle, hint, TitleArt.BackdropWidth - hw - 12, 384, 10, Colors.Rgb(120, 112, 100));
    }
}
