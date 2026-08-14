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

    /// <summary>The pack. Separate from <see cref="ShowEquipment"/>: one screen is what you are carrying, the
    /// other is what you are wearing, and they were one list until they got long enough to be unusable.</summary>
    public bool ShowItems;

    public bool ShowEquipment;
    public bool ShowKillLog;

    /// <summary>
    /// Volumes and control bindings. An overlay rather than a screen for the same reason the pack is one: it
    /// has to be openable from inside a dungeon, and a screen change would throw the floor away.
    /// </summary>
    public bool ShowOptions;

    /// <summary>The player's own preferences, loaded at startup and written back as they are changed.</summary>
    public required Data.Settings Settings { get; init; }

    /// <summary>Any of the full-screen overlays is up, so the screen underneath takes no input and no time
    /// passes in it — which is what freezes the dungeon while a pack is being sorted through.</summary>
    public bool AnyOverlayOpen => ShowItems || ShowEquipment || ShowKillLog || ShowOptions;

    /// <summary>
    /// Set by the dungeon screen while a dungeon is being explored, so that the inventory overlay — which
    /// knows nothing about dungeon sessions — can still make a map scroll do its job. Null anywhere else,
    /// which is exactly how the overlay knows there is no map to reveal.
    /// </summary>
    public Action? RevealFullMap;
}
