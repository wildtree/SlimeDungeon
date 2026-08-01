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
    public required AudioService Audio { get; init; }
    public required IntPtr Window { get; init; }

    public Player? Player;

    public bool ShowInventory;
    public bool ShowKillLog;

    /// <summary>
    /// Set by the dungeon screen while a dungeon is being explored, so that the inventory overlay — which
    /// knows nothing about dungeon sessions — can still make a map scroll do its job. Null anywhere else,
    /// which is exactly how the overlay knows there is no map to reveal.
    /// </summary>
    public Action? RevealFullMap;
}
