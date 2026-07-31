using SlimeDungeon.Domain;
using SlimeDungeon.Graphics;

namespace SlimeDungeon.Core;

/// <summary>Bundles everything a screen needs: rendering, input, fonts, and screen transitions.</summary>
public sealed class GameContext
{
    public required Renderer Renderer { get; init; }
    public required InputManager Input { get; init; }
    public required FontService Fonts { get; init; }
    public required ScreenManager Screens { get; init; }
    public required SpriteFactory Sprites { get; init; }
    public required IntPtr Window { get; init; }

    public Player? Player;

    public bool ShowInventory;
    public bool ShowKillLog;
}
