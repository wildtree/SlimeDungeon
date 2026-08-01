using System.Runtime.InteropServices;
using SDL3;

namespace SlimeDungeon.Core;

/// <summary>
/// The three things the game asks of the player, whatever they are holding. Screens work in terms of these
/// rather than in keycodes, so the physical bindings live in exactly one place.
/// </summary>
public enum GameAction { Confirm, Cancel, Menu }

/// <summary>Tracks keyboard and gamepad state with edge-detection (pressed-this-frame vs held).</summary>
public sealed class InputManager
{
    private readonly HashSet<SDL.Keycode> _down = new();
    private readonly HashSet<SDL.Keycode> _pressedThisFrame = new();
    private readonly HashSet<SDL.GamepadButton> _padDown = new();
    private readonly HashSet<SDL.GamepadButton> _padPressedThisFrame = new();
    private readonly Dictionary<uint, IntPtr> _gamepads = new();
    private readonly Queue<char> _textInput = new();

    /// <summary>
    /// The binding's <see cref="SDL.GamepadButton"/> enum only names a handful of buttons and leaves the
    /// d-pad out, so these are SDL3's own values. They are anchored by the members the binding does define —
    /// South is 0 and Misc1 is 15 in SDL3's header, which fixes everything in between — and the constructor
    /// checks those two anchors at startup rather than trusting the assumption silently.
    /// </summary>
    public const SDL.GamepadButton DpadUp = (SDL.GamepadButton)11;
    public const SDL.GamepadButton DpadDown = (SDL.GamepadButton)12;
    public const SDL.GamepadButton DpadLeft = (SDL.GamepadButton)13;
    public const SDL.GamepadButton DpadRight = (SDL.GamepadButton)14;

    public InputManager()
    {
        // Read through locals so the compiler treats these as values to compare rather than as constants it
        // can fold away — the point is to catch the binding changing under us, not to be optimised out.
        int south = (int)SDL.GamepadButton.South, misc1 = (int)SDL.GamepadButton.Misc1;
        if (south != 0 || misc1 != 15)
            Console.Error.WriteLine(
                "gamepad button numbering is not what the d-pad constants assume; d-pad input may be wrong " +
                $"(South={south}, Misc1={misc1})");

        OpenConnectedGamepads();
    }

    /// <summary>
    /// Opens whatever is already plugged in. A controller connected before the game started may never produce
    /// a GamepadAdded event, so waiting for one means a pad that was there all along is simply never opened.
    /// </summary>
    private void OpenConnectedGamepads()
    {
        var ids = SDL.GetGamepads(out var count);
        if (ids is not null)
        {
            for (var i = 0; i < count && i < ids.Length; i++)
                Open(ids[i]);
        }

        // Reported so a pad that is plugged in but not working can be told apart from one SDL never saw.
        Console.Error.WriteLine(_gamepads.Count > 0
            ? $"gamepad: {_gamepads.Count} connected"
            : "gamepad: none detected (keyboard only)");
    }

    private void Open(uint id)
    {
        if (_gamepads.ContainsKey(id))
            return;

        var pad = SDL.OpenGamepad(id);
        if (pad != IntPtr.Zero)
            _gamepads[id] = pad;
    }

    public bool QuitRequested { get; private set; }

    /// <summary>True while any gamepad is connected, so on-screen hints can name the right buttons.</summary>
    public bool GamepadConnected => _gamepads.Count > 0;

    public void RequestQuit() => QuitRequested = true;

    public void BeginFrame()
    {
        _pressedThisFrame.Clear();
        _padPressedThisFrame.Clear();
    }

    public void HandleEvent(in SDL.Event ev)
    {
        switch ((SDL.EventType)ev.Type)
        {
            case SDL.EventType.Quit:
                QuitRequested = true;
                break;
            case SDL.EventType.KeyDown:
                if (!ev.Key.Repeat && _down.Add(ev.Key.Key))
                    _pressedThisFrame.Add(ev.Key.Key);
                break;
            case SDL.EventType.KeyUp:
                _down.Remove(ev.Key.Key);
                break;
            case SDL.EventType.TextInput:
                var s = Marshal.PtrToStringUTF8(ev.Text.Text);
                if (!string.IsNullOrEmpty(s))
                    foreach (var c in s)
                        _textInput.Enqueue(c);
                break;

            case SDL.EventType.GamepadAdded:
                Open(ev.GDevice.Which);
                break;
            case SDL.EventType.GamepadRemoved:
            {
                var id = ev.GDevice.Which;
                if (_gamepads.Remove(id, out var pad))
                    SDL.CloseGamepad(pad);
                break;
            }
            case SDL.EventType.GamepadButtonDown:
            {
                var button = (SDL.GamepadButton)ev.GButton.Button;
                if (_padDown.Add(button))
                    _padPressedThisFrame.Add(button);
                break;
            }
            case SDL.EventType.GamepadButtonUp:
                _padDown.Remove((SDL.GamepadButton)ev.GButton.Button);
                break;
        }
    }

    public bool IsDown(SDL.Keycode key) => _down.Contains(key);

    public bool WasPressed(SDL.Keycode key) => _pressedThisFrame.Contains(key);

    public bool WasPressed(SDL.GamepadButton button) => _padPressedThisFrame.Contains(button);

    // ---- Bindings ----------------------------------------------------------------------------

    /// <summary>
    /// The one place the physical bindings are written down. Enter and Escape are kept alongside the letter
    /// keys because they are what a hand reaches for by reflex, and nothing is lost by accepting both.
    /// </summary>
    public bool WasPressed(GameAction action) => action switch
    {
        GameAction.Confirm => WasPressed(SDL.Keycode.X) || WasPressed(SDL.Keycode.Return)
                              || WasPressed(SDL.GamepadButton.South),
        GameAction.Cancel => WasPressed(SDL.Keycode.Z) || WasPressed(SDL.Keycode.Escape)
                             || WasPressed(SDL.GamepadButton.East),
        GameAction.Menu => WasPressed(SDL.Keycode.S) || WasPressed(SDL.GamepadButton.West),
        _ => false,
    };

    /// <summary>Confirm while text is being typed. Letter keys are being consumed as text, so only Enter
    /// (or the pad) can mean "done" — otherwise typing an x in a name would submit the form.</summary>
    public bool TextEntryConfirmed() =>
        WasPressed(SDL.Keycode.Return) || WasPressed(SDL.GamepadButton.South);

    public bool TryDequeueChar(out char c) => _textInput.TryDequeue(out c);

    public void ClearTextInput() => _textInput.Clear();

    public void CloseGamepads()
    {
        foreach (var pad in _gamepads.Values)
            SDL.CloseGamepad(pad);
        _gamepads.Clear();
    }
}
