namespace SlimeDungeon.Core;

/// <summary>Single active screen with a pending-transition queue (transitions apply between frames).</summary>
public sealed class ScreenManager
{
    private IScreen? _current;
    private IScreen? _pending;

    public IScreen Current => _current ?? throw new InvalidOperationException("No screen set.");

    public void ChangeTo(IScreen next) => _pending = next;

    public void ApplyPendingTransition(GameContext ctx)
    {
        if (_pending is null)
            return;

        _current?.OnExit(ctx);
        _current = _pending;
        _pending = null;
        _current.OnEnter(ctx);
    }
}
