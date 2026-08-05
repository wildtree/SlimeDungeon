using SDL3;
using SlimeDungeon.Core;
using SlimeDungeon.Data;
using SlimeDungeon.Graphics;
using SlimeDungeon.UI;



namespace SlimeDungeon.Guild;

public sealed class TitleScreen : IScreen
{
    private float _time;

    // Where the slimes stand, and where the menu goes, on each of the two title screens. The painted one has
    // its arch a little left of centre and a carved plaque waiting for the menu; the procedural one is
    // symmetrical and has no plaque, so the two sets of numbers are simply different pictures' furniture.
    private const float PaintedArchX = 322f;
    private const float PaintedFloorY = 306f;
    private const float PaintedSlimeUnit = 30f;
    private const float PaintedMenuY = 330f;

    /// <summary>
    /// The two witchlight torches in the painting, measured off the artwork itself rather than guessed: the
    /// centre of each flame, and a radius a little wider than the flame so the halo spills past it. The phases
    /// are unrelated numbers on purpose — the pair must not flicker in step.
    /// </summary>
    private static readonly (float X, float Y, float Radius, float Phase)[] PaintedTorches =
    [
        (120f, 175f, 18f, 0f),
        (518f, 170f, 18f, 2.4f),
    ];

    private const float DrawnFloorY = TitleArt.FloorY - 18f;

    /// <summary>Chosen so the middle slime comes out the 42px it was before these three moved to a shared
    /// helper — the drawn arch is smaller than the painted one and was tuned against it.</summary>
    private const float DrawnSlimeUnit = 32f;
    private const float DrawnMenuY = 340f;

    /// <summary>
    /// Whether the controller readout is showing. Lives on the title screen because that is the one place
    /// reachable before a character is at risk, and it is the screen you are already on when you notice the
    /// pad is not doing anything.
    /// </summary>
    private bool _showPadInfo;

    public void Update(GameContext ctx, float dt)
    {
        _time += dt;

        var input = ctx.Input;

        if (input.WasPressed(SDL.Keycode.F1))
            _showPadInfo = !_showPadInfo;

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

        var art = ctx.Sprites.TitleArtwork;
        var painted = art != IntPtr.Zero;

        if (painted)
        {
            // The painting carries its own wordmark and its own arch, so none of the drawn furniture goes on
            // top of it. Its torches are painted too — those get light laid over them rather than replaced.
            r.DrawTexture(art, 0, 0, TitleArt.BackdropWidth, TitleArt.BackdropHeight);

            foreach (var (tx, ty, radius, phase) in PaintedTorches)
                FlameGlow.Draw(r, ctx.Sprites.GlowSprite, tx, ty, radius, _time, phase, FlameGlow.Witchlight);

            DoorwaySlimes.Draw(ctx, PaintedArchX, PaintedFloorY, PaintedSlimeUnit, _time);
        }
        else
        {
            r.DrawTexture(ctx.Sprites.TitleBackdrop, 0, 0, TitleArt.BackdropWidth, TitleArt.BackdropHeight);
            DoorwaySlimes.Draw(ctx, TitleArt.ArchCenterX, DrawnFloorY, DrawnSlimeUnit, _time);

            foreach (var (tx, ty) in TitleArt.TorchPositions)
                TorchFlame.Draw(r, tx, ty - 8, _time);

            // The wordmark drifts a couple of pixels, just enough to keep the screen from feeling frozen.
            var logoY = 8 + (float)Math.Sin(_time * 1.1) * 3f;
            r.DrawTexture(ctx.Sprites.TitleLogo, (TitleArt.BackdropWidth - TitleArt.LogoWidth) / 2f, logoY,
                TitleArt.LogoWidth, TitleArt.LogoHeight);
        }

        DrawPrompt(ctx, painted ? PaintedMenuY : DrawnMenuY);

        if (_showPadInfo)
            DrawPadInfo(ctx);
    }

    /// <summary>
    /// The live controller readout. Deliberately raw — axis numbers rather than a verdict — because the point
    /// is to be able to tell "SDL never opened the pad" apart from "SDL opened it and the stick reads zero"
    /// apart from "the stick reads fine and the game is ignoring it", which are three different faults with
    /// three different fixes and look identical from the outside.
    /// </summary>
    private static void DrawPadInfo(GameContext ctx)
    {
        var r = ctx.Renderer;
        var lines = ctx.Input.DescribeGamepads().ToList();

        const float x = 16f;
        const float w = TitleArt.BackdropWidth - 32f;
        // Header, the lines themselves, then a strip at the bottom for the close hint — without that last
        // allowance the hint was drawn straight over the final line of the readout.
        var h = 32f + lines.Count * 13f + 18f;
        const float y = 150f;

        r.FillRect(x, y, w, h, Colors.Rgb(8, 10, 16, 235));
        r.DrawRect(x, y, w, h, Colors.Rgb(120, 150, 190));

        ctx.Fonts.DrawText(r.Handle, "ゲームパッド診断", x + 10, y + 7, 12, Colors.Rgb(150, 200, 240));

        var ly = y + 26f;
        foreach (var line in lines)
        {
            ctx.Fonts.DrawText(r.Handle, line, x + 10, ly, 10, Colors.Rgb(210, 214, 222));
            ly += 13f;
        }

        ctx.Fonts.DrawText(r.Handle, "[F1]閉じる", x + 10, y + h - 14, 9, Colors.Rgb(120, 130, 145));
    }

    private void DrawPrompt(GameContext ctx, float menuY)
    {
        var r = ctx.Renderer;
        // Quantised pulse on the selected row: the font cache is keyed by colour, so a handful of discrete
        // steps keeps it from filling up with a new texture every frame the way a smooth fade would.
        var steps = new[] { 150, 190, 225, 255 };
        var index = (int)((Math.Sin(_time * 2.6) * 0.5 + 0.5) * (steps.Length - 1) + 0.5);
        var level = (byte)steps[Math.Clamp(index, 0, steps.Length - 1)];
        var selectedColor = Colors.Rgb(level, (byte)(level * 0.92), (byte)(level * 0.55));

        var options = Options();
        var y = menuY;
        for (var i = 0; i < options.Length; i++)
        {
            var label = OptionLabel(options[i]);
            var selected = i == _cursor;
            var (w, _) = ctx.Fonts.Measure(label, selected ? 15 : 13);
            ctx.Fonts.DrawText(r.Handle, label, (TitleArt.BackdropWidth - w) / 2f, y,
                selected ? 15 : 13, selected ? selectedColor : Colors.Rgb(122, 116, 104));
            y += 19;
        }

        // F1 is named as a literal key on purpose: it is a keyboard-only diagnostic with no gamepad equivalent
        // and no action icon, so there is nothing ambiguous about spelling it out.
        var color = Colors.Rgb(120, 112, 100);
        ControlHint[] hints = [ControlHints.Direction("選ぶ"), ControlHints.Confirm("決定")];
        const string diag = "[F1]パッド診断";
        var (dw, _) = ctx.Fonts.Measure(diag, 10);
        // ControlHints.Width measures only up to the last label, so without this the F1 note is drawn flush
        // against 決定 with nothing between them.
        const float gap = 10f;
        var width = ControlHints.Width(ctx, 10, hints) + gap + dw;
        var hx = TitleArt.BackdropWidth - width - 12;
        ControlHints.Draw(ctx, hx, 384, 10, color, hints);
        ctx.Fonts.DrawText(r.Handle, diag, TitleArt.BackdropWidth - dw - 12, 384, 10, color);
    }
}
