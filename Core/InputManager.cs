using System.Runtime.InteropServices;
using SDL3;

namespace SlimeDungeon.Core;

/// <summary>
/// The three things the game asks of the player, whatever they are holding. Screens work in terms of these
/// rather than in keycodes, so the physical bindings live in exactly one place.
/// </summary>
public enum GameAction { Confirm, Cancel, Menu, Travel }

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
    /// The d-pad directions. These were once raw numeric casts, on the belief that the binding did not name
    /// them; it does (as DPadUp and so on), so they are aliases now and cannot drift out of step with SDL.
    /// </summary>
    public const SDL.GamepadButton DpadUp = SDL.GamepadButton.DPadUp;
    public const SDL.GamepadButton DpadDown = SDL.GamepadButton.DPadDown;
    public const SDL.GamepadButton DpadLeft = SDL.GamepadButton.DPadLeft;
    public const SDL.GamepadButton DpadRight = SDL.GamepadButton.DPadRight;

    public InputManager()
    {
        OpenConnectedGamepads();
    }

    /// <summary>
    /// Opens whatever is already plugged in. A controller connected before the game started may never produce
    /// a GamepadAdded event, so waiting for one means a pad that was there all along is simply never opened.
    /// </summary>
    private void OpenConnectedGamepads()
    {
        Rescan();

        // Reported so a pad that is plugged in but not working can be told apart from one SDL never saw.
        Console.Error.WriteLine(_gamepads.Count > 0
            ? $"gamepad: {_gamepads.Count} connected"
            : "gamepad: none detected (keyboard only)");
    }

    /// <summary>Opens every gamepad SDL currently knows about that is not open already.</summary>
    private void Rescan()
    {
        var ids = SDL.GetGamepads(out var count);
        if (ids is null)
            return;

        for (var i = 0; i < count && i < ids.Length; i++)
            Open(ids[i]);
    }

    /// <summary>How often to look for pads that appeared without an event arriving to say so.</summary>
    private const float RescanInterval = 2f;

    private float _sinceRescan;

    /// <summary>
    /// Periodically re-opens whatever is plugged in.
    ///
    /// Opening used to happen exactly twice: at startup, and on a GamepadAdded event. That is one missed event
    /// away from a pad that Windows believes is connected and the game cannot see at all — which is precisely
    /// the state a Bluetooth pad lands in when the link drops and comes back, since the reconnection is handled
    /// down in the Bluetooth stack and does not always surface as a fresh SDL device. Two seconds is far below
    /// what a player would notice as a pause and far above anything that costs measurable time.
    /// </summary>
    private void UpdateGamepadRescan(float dt)
    {
        _sinceRescan += dt;
        if (_sinceRescan < RescanInterval)
            return;
        _sinceRescan = 0f;

        var before = _gamepads.Count;
        Rescan();
        if (_gamepads.Count != before)
            Console.Error.WriteLine($"gamepad: {_gamepads.Count} connected (found by rescan)");
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

    /// <summary>
    /// Forgets everything the pads were holding. The stick and the auto-repeat clock clear themselves on the
    /// next sample — a disconnected pad reports no axes — but the button set has no such pass over it.
    /// </summary>
    private void ReleaseAllPadInput()
    {
        _padDown.Clear();
        _padPressedThisFrame.Clear();
        _stickDirection = null;
        _stickPressedThisFrame = null;
        _heldFor.Clear();
        _repeatedThisFrame.Clear();
    }

    public void HandleEvent(in SDL.Event ev)
    {
        switch ((SDL.EventType)ev.Type)
        {
            case SDL.EventType.Quit:
                QuitRequested = true;
                break;
            case SDL.EventType.KeyDown:
            case SDL.EventType.KeyUp:
                var key = ev.Key.Key;
                if (key == SDL.Keycode.K)
                    key = SDL.Keycode.Up;
                if (key == SDL.Keycode.J)
                    key = SDL.Keycode.Down;
                if (key == SDL.Keycode.H)
                    key = SDL.Keycode.Left;
                if (key == SDL.Keycode.L)
                    key = SDL.Keycode.Right;
                if ((SDL.EventType)ev.Type == SDL.EventType.KeyDown) {
                    if (!ev.Key.Repeat && _down.Add(key))
                        _pressedThisFrame.Add(key);
                } else {
                    _down.Remove(key);
                }
                break;
            case SDL.EventType.TextInput:
                var s = Marshal.PtrToStringUTF8(ev.Text.Text);
                if (!string.IsNullOrEmpty(s))
                    foreach (var c in s)
                        _textInput.Enqueue(c);
                break;

            case SDL.EventType.GamepadAdded:
                Open(ev.GDevice.Which);
                Console.Error.WriteLine($"gamepad: #{ev.GDevice.Which} connected ({_gamepads.Count} total)");
                break;
            case SDL.EventType.GamepadRemoved:
            {
                var id = ev.GDevice.Which;
                if (_gamepads.Remove(id, out var pad))
                    SDL.CloseGamepad(pad);

                // A button held at the instant the link drops never gets its release event, so without this it
                // stays down for the rest of the session — and a direction stuck down walks the character
                // across the floor on their own, which reads as the game having lost its mind rather than as
                // the pad having disconnected. Bluetooth makes this the common case rather than a curiosity:
                // the link goes while the player's thumb is still on the stick.
                //
                // Every pad's state is cleared, not just this one's. Button events carry only the button, so
                // there is nothing to attribute held state to a particular pad with; on the two-pad case this
                // costs the other pad a spurious release, which is the harmless direction to be wrong in.
                ReleaseAllPadInput();

                Console.Error.WriteLine($"gamepad: #{id} disconnected ({_gamepads.Count} remaining)");
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

    // ---- The left stick ------------------------------------------------------------------------

    /// <summary>
    /// How far the left stick must be pushed to count. Sticks rest slightly off-centre and drift with age, so
    /// a raw reading would walk the player across the room on its own.
    /// </summary>
    private const short StickDeadZone = 12000;

    /// <summary>Where the stick is pointing right now, as a d-pad direction, or null when it is centred.</summary>
    private SDL.GamepadButton? _stickDirection;

    /// <summary>The direction the stick moved into on this frame, for edge detection.</summary>
    private SDL.GamepadButton? _stickPressedThisFrame;

    /// <summary>Raw axis values from the most recent sample, kept only so the diagnostic can show them.</summary>
    private short _stickX;
    private short _stickY;

    /// <summary>
    /// Reads the left stick and turns it into the d-pad direction it is pointing at.
    ///
    /// This has to be called once per frame *after* the event queue has been drained, because draining is what
    /// makes SDL refresh its gamepad state. It replaces an earlier arrangement where the stick was read two
    /// different ways — polled directly by the dungeon, and separately turned into held-button state by the
    /// axis-motion event — which is why the stick moved the character but did nothing in any menu: the menus
    /// ask <see cref="WasPressed(SDL.GamepadButton)"/>, and nothing was ever producing a press edge for it.
    /// Sampling in one place and deriving both "held" and "just pressed" from it makes the stick behave
    /// exactly like the d-pad everywhere, which is the only way it can be reasoned about.
    /// </summary>
    public void SampleSticks(float dt)
    {
        _stickPressedThisFrame = null;
        _stickX = 0;
        _stickY = 0;

        SDL.GamepadButton? direction = null;
        foreach (var pad in _gamepads.Values)
        {
            var x = SDL.GetGamepadAxis(pad, SDL.GamepadAxis.LeftX);
            var y = SDL.GetGamepadAxis(pad, SDL.GamepadAxis.LeftY);

            if (Math.Abs((int)x) < StickDeadZone && Math.Abs((int)y) < StickDeadZone)
                continue;

            _stickX = x;
            _stickY = y;

            // One direction at a time. The dungeon moves on a grid and menus move a row at a time, so a stick
            // held on the diagonal has to pick whichever way it is being pushed hardest rather than both.
            direction = Math.Abs((int)x) >= Math.Abs((int)y)
                ? (x < 0 ? DpadLeft : DpadRight)
                : (y < 0 ? DpadUp : DpadDown);
            break;
        }

        // Moving from centred to a direction, or straight from one direction to another, is a press.
        if (direction is not null && direction != _stickDirection)
            _stickPressedThisFrame = direction;

        _stickDirection = direction;

        UpdateDirectionRepeat(dt);
        UpdateGamepadRescan(dt);
    }

    // ---- Held-direction auto-repeat -------------------------------------------------------------

    /// <summary>How long a direction must be held before it starts repeating.</summary>
    private const float RepeatDelay = 0.4f;

    /// <summary>How often it repeats after that.</summary>
    private const float RepeatInterval = 0.12f;

    private static readonly SDL.GamepadButton[] Directions = [DpadUp, DpadDown, DpadLeft, DpadRight];

    private readonly Dictionary<SDL.GamepadButton, float> _heldFor = new();
    private readonly HashSet<SDL.GamepadButton> _repeatedThisFrame = new();

    /// <summary>
    /// Makes a held direction keep firing, the way a held arrow key does in any list. Without it a stick is
    /// nearly unusable in a menu of a dozen entries: a stick has no detent, so a player pushes it and waits
    /// for the cursor to run, where a d-pad invites tapping. Applied to the d-pad as well, since a menu that
    /// scrolls under one control and not the other is worse than either behaviour on its own.
    /// </summary>
    private void UpdateDirectionRepeat(float dt)
    {
        _repeatedThisFrame.Clear();

        foreach (var direction in Directions)
        {
            if (!IsDown(direction))
            {
                _heldFor.Remove(direction);
                continue;
            }

            // A direction that only just went down is already reporting a press of its own this frame; the
            // clock starts now and the first repeat is a whole RepeatDelay away.
            if (!_heldFor.TryGetValue(direction, out var held))
            {
                _heldFor[direction] = 0f;
                continue;
            }

            var now = held + dt;
            if (now >= RepeatDelay)
            {
                // Carry the remainder rather than resetting to the delay, so the repeat rate does not drift
                // with the frame rate.
                _repeatedThisFrame.Add(direction);
                now = RepeatDelay - RepeatInterval;
            }
            _heldFor[direction] = now;
        }
    }

    public bool IsDown(SDL.Keycode key) => _down.Contains(key);

    /// <summary>
    /// Held, from the d-pad or from the stick pushed the same way. Everything that asks about a direction goes
    /// through here, so neither the caller nor the player has to care which one is being used.
    /// </summary>
    public bool IsDown(SDL.GamepadButton button) =>
        _padDown.Contains(button) || _stickDirection == button;

    public bool WasPressed(SDL.Keycode key) => _pressedThisFrame.Contains(key);

    public bool WasPressed(SDL.GamepadButton button) =>
        _padPressedThisFrame.Contains(button) || _stickPressedThisFrame == button
        || _repeatedThisFrame.Contains(button);

    /// <summary>
    /// The direction the player is asking to walk. Kept as its own call because the dungeon wants a vector
    /// rather than four button queries, but it now reads the same sampled state as everything else.
    /// </summary>
    public (int Dx, int Dy) ReadStickDirection() => _stickDirection switch
    {
        DpadUp => (0, -1),
        DpadDown => (0, 1),
        DpadLeft => (-1, 0),
        DpadRight => (1, 0),
        _ => (0, 0),
    };

    // ---- Bindings ----------------------------------------------------------------------------

    /// <summary>
    /// The player's bindings. Supplied at startup and swapped in when the options screen changes them, so the
    /// physical mapping still lives in exactly one place — that place is now a file the player owns.
    /// </summary>
    public Data.Settings Bindings { get; set; } = new();

    /// <summary>
    /// Keys that cannot be rebound, and why.
    ///
    /// The four arrows are the direction pad (and H/J/K/L arrive as arrows — see <see cref="HandleEvent"/>), so
    /// binding a command to one would make it impossible to move without triggering it. Enter and Escape are
    /// reserved for a different reason: they are permanent aliases for Confirm and Cancel below, which is what
    /// guarantees a player who has bound themselves into a corner can always get back out — and they are the
    /// keys the rebinding screen itself is driven by.
    /// </summary>
    public static readonly SDL.Keycode[] ReservedKeys =
    [
        SDL.Keycode.Up, SDL.Keycode.Down, SDL.Keycode.Left, SDL.Keycode.Right,
        SDL.Keycode.Return, SDL.Keycode.Escape,
    ];

    public static bool IsReserved(SDL.Keycode key) => ReservedKeys.Contains(key);

    /// <summary>
    /// What the player asked for, from their own key or their own pad button — plus Enter and Escape, which
    /// answer for Confirm and Cancel whatever else has been bound. Those two are the escape hatch: the option
    /// screen can be reached and left with them no matter how the rest has been arranged.
    /// </summary>
    public bool WasPressed(GameAction action)
    {
        if (WasPressed(Bindings.KeyFor(action)) || WasPressed(Bindings.PadFor(action)))
            return true;

        return action switch
        {
            GameAction.Confirm => WasPressed(SDL.Keycode.Return),
            GameAction.Cancel => WasPressed(SDL.Keycode.Escape),
            _ => false,
        };
    }

    /// <summary>
    /// Any key pressed this frame, for the rebinding prompt. Returns the first in an unspecified order, which
    /// is the right answer for a screen that is waiting for one keypress and got two in the same frame.
    /// </summary>
    public bool TryReadAnyKey(out SDL.Keycode key)
    {
        foreach (var pressed in _pressedThisFrame)
        {
            key = pressed;
            return true;
        }
        key = default;
        return false;
    }

    /// <summary>
    /// Any pad button pressed this frame. Reads the raw button set rather than <see cref="WasPressed"/>, so the
    /// stick's synthesised d-pad presses and the auto-repeat cannot be captured as a binding.
    /// </summary>
    public bool TryReadAnyPadButton(out SDL.GamepadButton button)
    {
        foreach (var pressed in _padPressedThisFrame)
        {
            button = pressed;
            return true;
        }
        button = default;
        return false;
    }

    /// <summary>The d-pad, which is movement and therefore never a command.</summary>
    public static bool IsReserved(SDL.GamepadButton button) =>
        button is DpadUp or DpadDown or DpadLeft or DpadRight;

    /// <summary>Confirm while text is being typed. Letter keys are being consumed as text, so only Enter
    /// (or the pad) can mean "done" — otherwise typing an x in a name would submit the form.</summary>
    public bool TextEntryConfirmed() =>
        WasPressed(SDL.Keycode.Return) || WasPressed(SDL.GamepadButton.South);

    public bool TryDequeueChar(out char c) => _textInput.TryDequeue(out c);

    public void ClearTextInput() => _textInput.Clear();

    // ---- Diagnostics ---------------------------------------------------------------------------

    /// <summary>
    /// A live account of what SDL thinks is plugged in and what it is reporting, for the title screen's
    /// diagnostic panel. Worth having permanently: a controller that does nothing is otherwise
    /// indistinguishable from one SDL never opened, and there is no way to tell those apart from the outside.
    /// </summary>
    public IEnumerable<string> DescribeGamepads()
    {
        if (_gamepads.Count == 0)
        {
            yield return "検出なし — SDLはコントローラーを1台も開けていません";
            yield return "USBを挿し直すか、Windowsの「ゲームコントローラーの設定」で認識を確認してください";
            yield break;
        }

        foreach (var (id, pad) in _gamepads)
        {
            var name = SDL.GetGamepadName(pad);
            yield return $"#{id} {(string.IsNullOrEmpty(name) ? "(名前なし)" : name)} " +
                         $"{(SDL.GamepadConnected(pad) ? "接続中" : "切断")}";

            var lx = SDL.GetGamepadAxis(pad, SDL.GamepadAxis.LeftX);
            var ly = SDL.GetGamepadAxis(pad, SDL.GamepadAxis.LeftY);
            var rx = SDL.GetGamepadAxis(pad, SDL.GamepadAxis.RightX);
            var ry = SDL.GetGamepadAxis(pad, SDL.GamepadAxis.RightY);
            yield return $"  L stick X={lx,6} Y={ly,6}   R stick X={rx,6} Y={ry,6}";

            var held = Enum.GetValues<SDL.GamepadButton>()
                .Where(b => b >= 0 && b < SDL.GamepadButton.Count && SDL.GetGamepadButton(pad, b))
                .Select(b => b.ToString())
                .ToList();
            yield return $"  押されているボタン: {(held.Count > 0 ? string.Join(" ", held) : "なし")}";
        }

        yield return $"しきい値 {StickDeadZone} / 判定 {(_stickDirection?.ToString() ?? "中立")}";
    }

    public void CloseGamepads()
    {
        foreach (var pad in _gamepads.Values)
            SDL.CloseGamepad(pad);
        _gamepads.Clear();
    }
}
