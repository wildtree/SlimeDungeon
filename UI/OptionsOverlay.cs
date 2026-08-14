using SDL3;
using SlimeDungeon.Core;
using SlimeDungeon.Data;

namespace SlimeDungeon.UI;

/// <summary>
/// Volumes and controls, reachable from the guild counter and from inside a dungeon.
///
/// Built as an overlay rather than as a screen, which is what lets it open from both. A screen change would
/// throw away the dungeon session — the floor, the fog, the chests already opened — so the one place the
/// player is most likely to want to turn the music down is the one place a settings *screen* could never be
/// opened from. As an overlay it also freezes the dungeon while it is up, the same as the pack does.
/// </summary>
public sealed class OptionsOverlay
{
    private enum Row { Music, Sound, Confirm, Cancel, Menu, Travel }

    private static readonly Row[] Rows =
        [Row.Music, Row.Sound, Row.Confirm, Row.Cancel, Row.Menu, Row.Travel];

    private int _cursor;

    /// <summary>The action whose next press is being waited for, or null when nothing is being rebound.</summary>
    private GameAction? _capturing;

    /// <summary>Why the last attempted binding was refused, shown until something else happens.</summary>
    private string? _message;

    /// <summary>How much one press of left or right moves a slider.</summary>
    private const int VolumeStep = 5;

    private static GameAction? ActionOf(Row row) => row switch
    {
        Row.Confirm => GameAction.Confirm,
        Row.Cancel => GameAction.Cancel,
        Row.Menu => GameAction.Menu,
        Row.Travel => GameAction.Travel,
        _ => null,
    };

    public void Update(GameContext ctx, float dt)
    {
        var input = ctx.Input;
        var settings = ctx.Settings;

        if (_capturing is { } action)
        {
            UpdateCapture(ctx, action);
            return;
        }

        // Cancel anywhere outside a capture closes the panel — which is also why Escape stays a permanent
        // alias for Cancel: a player who has just bound Cancel to something unreachable can still get out.
        if (MenuNav.Cancelled(input))
        {
            Close(ctx);
            return;
        }

        _cursor = MenuNav.Move(input, _cursor, Rows.Length);
        var row = Rows[_cursor];

        if (row is Row.Music or Row.Sound)
        {
            var delta = (MenuNav.Right(input) ? VolumeStep : 0) - (MenuNav.Left(input) ? VolumeStep : 0);
            if (delta != 0)
            {
                if (row == Row.Music)
                    settings.MusicVolume = Math.Clamp(settings.MusicVolume + delta, 0, 100);
                else
                    settings.SoundVolume = Math.Clamp(settings.SoundVolume + delta, 0, 100);

                ctx.Audio.SetVolumes(settings.MusicVolume, settings.SoundVolume);

                // A sound on every step of the effects slider, so the level being set is audible while it is
                // being set rather than only the next time something happens to make a noise. A weapon hit is
                // the sample because it is the effect the player hears most and the one the mix is judged on.
                if (row == Row.Sound)
                    ctx.Audio.Play(SoundId.WeaponHit);

                _message = null;
            }
            return;
        }

        if (MenuNav.Confirmed(input) && ActionOf(row) is { } target)
        {
            _capturing = target;
            _message = null;
        }
    }

    /// <summary>
    /// Waiting for the key or button that should drive this command.
    ///
    /// Escape gets first refusal, so a capture entered by accident is always escapable — which matters more
    /// here than anywhere else in the game, since the alternative is a player stuck on a panel waiting for a
    /// keypress that will be swallowed whatever they do.
    /// </summary>
    private void UpdateCapture(GameContext ctx, GameAction action)
    {
        var input = ctx.Input;
        var settings = ctx.Settings;

        if (input.WasPressed(SDL.Keycode.Escape))
        {
            _capturing = null;
            _message = "割り当てを中止しました";
            return;
        }

        if (input.TryReadAnyKey(out var key))
        {
            if (InputManager.IsReserved(key))
            {
                _message = key is SDL.Keycode.Return
                    ? "ENTERは決定用に予約されています"
                    : "方向キー（H/J/K/L・矢印）には割り当てられません";
                return;
            }

            settings.Rebind(action, key);
            Finish(ctx, action);
            return;
        }

        if (input.TryReadAnyPadButton(out var button))
        {
            if (InputManager.IsReserved(button))
            {
                _message = "方向パッドには割り当てられません";
                return;
            }

            settings.Rebind(action, button);
            Finish(ctx, action);
        }
    }

    private void Finish(GameContext ctx, GameAction action)
    {
        _capturing = null;
        _message = $"{Settings.ActionLabel(action)}を割り当てました";
        SettingsManager.Save(ctx.Settings);
    }

    private void Close(GameContext ctx)
    {
        SettingsManager.Save(ctx.Settings);
        ctx.ShowOptions = false;
        _capturing = null;
        _message = null;
    }

    // ---- Drawing --------------------------------------------------------------------------------

    private const float Width = 336f;
    private const float RowHeight = 26f;

    public void Draw(GameContext ctx)
    {
        var r = ctx.Renderer;
        var fonts = ctx.Fonts;
        var settings = ctx.Settings;

        var h = 40f + Rows.Length * RowHeight + 44f;
        var x = 200f - Width / 2f;
        var y = (400f - h) / 2f;

        r.FillRect(0, 0, 400, 400, Colors.Rgb(0, 0, 0, 150));
        r.FillRect(x + 5, y + 6, Width, h, Colors.Rgb(6, 6, 10, 190));
        r.FillRect(x, y, Width, h, Colors.PanelBg);
        r.DrawRect(x, y, Width, h, Colors.Border);

        fonts.DrawText(r.Handle, "オプション", x + 14, y + 10, 14, Colors.Highlight);
        r.FillRect(x + 12, y + 32, Width - 24, 1, Colors.Rgb(70, 66, 60));

        var ry = y + 40f;
        for (var i = 0; i < Rows.Length; i++)
        {
            var row = Rows[i];
            var selected = i == _cursor;
            if (selected)
                r.FillRect(x + 8, ry - 2, Width - 16, RowHeight - 4, Colors.Rgb(58, 52, 40));

            var ink = selected ? Colors.Highlight : Colors.White;
            fonts.DrawText(r.Handle, RowLabel(row), x + 16, ry + 3, 12, ink);

            if (row is Row.Music or Row.Sound)
                DrawSlider(ctx, x + 130, ry + 6, 140,
                    row == Row.Music ? settings.MusicVolume : settings.SoundVolume, selected);
            else
                DrawBinding(ctx, x + 130, ry + 3, ActionOf(row)!.Value, settings, selected);

            ry += RowHeight;
        }

        // The footer changes with what is happening, because during a capture every other control is suspended
        // and saying otherwise would be a lie.
        var footerY = y + h - 30f;
        if (_message is not null)
            fonts.DrawText(r.Handle, _message, x + 14, footerY - 14, 10, Colors.Gold);

        if (_capturing is not null)
            fonts.DrawText(r.Handle, "割り当てたいキー／ボタンを押してください（ESCで中止）",
                x + 14, footerY, 10, Colors.Highlight);
        else
            ControlHints.Draw(ctx, x + 14, footerY, 10, Colors.Rgb(168, 160, 148),
                ControlHints.Direction("選ぶ／音量"),
                ControlHints.Confirm("キー変更"),
                ControlHints.Cancel("閉じる"));
    }

    private static string RowLabel(Row row) => row switch
    {
        Row.Music => "BGM音量",
        Row.Sound => "効果音音量",
        _ => Settings.ActionLabel(ActionOf(row)!.Value),
    };

    private static void DrawSlider(GameContext ctx, float x, float y, float w, int value, bool selected)
    {
        var r = ctx.Renderer;

        const float barH = 8f;
        r.FillRect(x - 1, y - 1, w + 2, barH + 2, Colors.Rgb(20, 18, 16));
        r.FillRect(x, y, w, barH, Colors.BarBg);

        var frac = Math.Clamp(value / 100f, 0f, 1f);
        if (frac > 0)
            r.FillRect(x, y, Math.Max(1f, w * frac), barH, selected ? Colors.Highlight : Colors.MpBar);

        // The handle, so a slider at zero still shows where it is rather than reading as an empty trough.
        var hx = x + w * frac;
        r.FillRect(hx - 2, y - 3, 4, barH + 6, selected ? Colors.White : Colors.Rgb(150, 145, 136));

        ctx.Fonts.DrawText(ctx.Renderer.Handle, $"{value,3}", x + w + 10, y - 4, 11,
            selected ? Colors.Highlight : Colors.White);
    }

    /// <summary>
    /// A command's key and its pad button side by side. Both are shown at once rather than on separate screens
    /// because they are one decision — the player who moves 決定 wants to know what the pad is doing too.
    /// </summary>
    private void DrawBinding(GameContext ctx, float x, float y, GameAction action, Settings settings, bool selected)
    {
        var fonts = ctx.Fonts;
        var handle = ctx.Renderer.Handle;

        if (_capturing == action)
        {
            fonts.DrawText(handle, "＞ 入力待ち …", x, y + 3, 12, Colors.Gold);
            return;
        }

        var ink = selected ? Colors.Highlight : Colors.White;
        fonts.DrawText(handle, KeyName(settings.KeyFor(action)), x, y + 3, 12, ink);
        fonts.DrawText(handle, "パッド", x + 76, y + 5, 9, Colors.Rgb(150, 145, 136));
        fonts.DrawText(handle, PadName(settings.PadFor(action)), x + 108, y + 3, 12, ink);
    }

    /// <summary>
    /// A printable name for a key. SDL will name most of them; the ones it will not are shown as their number
    /// rather than as a blank, so a binding is never invisible.
    /// </summary>
    private static string KeyName(SDL.Keycode key)
    {
        var name = SDL.GetKeyName(key);
        return string.IsNullOrEmpty(name) ? $"#{(uint)key}" : name;
    }

    /// <summary>
    /// The four face buttons by the letters printed on them, and everything else by SDL's own name. Deliberately
    /// positional — South is the bottom button whatever the pad calls it — since that is what the player's thumb
    /// actually knows.
    /// </summary>
    private static string PadName(SDL.GamepadButton button) => button switch
    {
        SDL.GamepadButton.South => "下(A/B)",
        SDL.GamepadButton.East => "右(B/A)",
        SDL.GamepadButton.West => "左(X/Y)",
        SDL.GamepadButton.North => "上(Y/X)",
        SDL.GamepadButton.Start => "START",
        SDL.GamepadButton.Back => "BACK",
        SDL.GamepadButton.LeftShoulder => "L",
        SDL.GamepadButton.RightShoulder => "R",
        _ => button.ToString(),
    };
}
