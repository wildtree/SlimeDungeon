using System.Runtime.InteropServices;
using SDL3;

namespace SlimeDungeon.Core;

/// <summary>Tracks keyboard state with edge-detection (pressed-this-frame vs held).</summary>
public sealed class InputManager
{
    private readonly HashSet<SDL.Keycode> _down = new();
    private readonly HashSet<SDL.Keycode> _pressedThisFrame = new();
    private readonly Queue<char> _textInput = new();

    public bool QuitRequested { get; private set; }

    public void RequestQuit() => QuitRequested = true;

    public void BeginFrame()
    {
        _pressedThisFrame.Clear();
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
        }
    }

    public bool IsDown(SDL.Keycode key) => _down.Contains(key);

    public bool WasPressed(SDL.Keycode key) => _pressedThisFrame.Contains(key);

    public bool TryDequeueChar(out char c) => _textInput.TryDequeue(out c);

    public void ClearTextInput() => _textInput.Clear();
}
