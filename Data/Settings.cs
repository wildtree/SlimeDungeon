using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using SDL3;
using SlimeDungeon.Core;

namespace SlimeDungeon.Data;

/// <summary>
/// Everything the player has set for themselves rather than earned: how loud the game is, and which buttons do
/// what.
///
/// Kept in its own file beside the save rather than inside it. These are preferences of the person playing, not
/// facts about a character — an adventurer dying should not reset the volume, and a player with two characters
/// should not have to set their controls up twice.
/// </summary>
public sealed class Settings
{
    /// <summary>0–100, as shown on the sliders.</summary>
    public int MusicVolume { get; set; } = 70;

    public int SoundVolume { get; set; } = 80;

    /// <summary>
    /// Keyboard bindings, stored as raw SDL keycode numbers.
    ///
    /// Numbers rather than names because <see cref="SDL.Keycode"/> is a large sparse enum whose members are not
    /// all named — a key with no name would serialise to a bare number anyway, and half the file being names
    /// and half numbers is worse than all of it being numbers. The action side is an enum key, which System.Text
    /// .Json writes by name, so the file still reads as "which action" even if not "which key".
    /// </summary>
    public Dictionary<GameAction, uint> Keys { get; set; } = new();

    public Dictionary<GameAction, int> PadButtons { get; set; } = new();

    /// <summary>
    /// What the game ships with. Also the fallback for any action missing from a settings file — adding a fifth
    /// action later must not leave it unbound for everyone who already has a file.
    /// </summary>
    public static readonly IReadOnlyDictionary<GameAction, SDL.Keycode> DefaultKeys =
        new Dictionary<GameAction, SDL.Keycode>
        {
            [GameAction.Confirm] = SDL.Keycode.X,
            [GameAction.Cancel] = SDL.Keycode.Z,
            [GameAction.Menu] = SDL.Keycode.S,
            [GameAction.Travel] = SDL.Keycode.A,
        };

    public static readonly IReadOnlyDictionary<GameAction, SDL.GamepadButton> DefaultPadButtons =
        new Dictionary<GameAction, SDL.GamepadButton>
        {
            [GameAction.Confirm] = SDL.GamepadButton.South,
            [GameAction.Cancel] = SDL.GamepadButton.East,
            [GameAction.Menu] = SDL.GamepadButton.West,
            // Sits alongside Menu rather than anywhere near Confirm or Cancel, because leaving a room by
            // accident is a worse mistake than opening a menu by accident.
            [GameAction.Travel] = SDL.GamepadButton.North,
        };

    public SDL.Keycode KeyFor(GameAction action) =>
        Keys.TryGetValue(action, out var raw) ? (SDL.Keycode)raw : DefaultKeys[action];

    public SDL.GamepadButton PadFor(GameAction action) =>
        PadButtons.TryGetValue(action, out var raw) ? (SDL.GamepadButton)raw : DefaultPadButtons[action];

    private void Bind(GameAction action, SDL.Keycode key) => Keys[action] = (uint)key;

    private void Bind(GameAction action, SDL.GamepadButton button) => PadButtons[action] = (int)button;

    /// <summary>
    /// Gives a command a key, trading rather than overwriting.
    ///
    /// If the key is already doing something else, the two commands swap: assigning 決定 to キャンセル's key
    /// leaves キャンセル holding the key 決定 just gave up. Overwriting would leave one command with no key at
    /// all, and a player who cannot cancel is a player who has to close the window.
    /// </summary>
    public void Rebind(GameAction action, SDL.Keycode key)
    {
        var previous = KeyFor(action);
        if (ActionUsing(key) is { } holder && holder != action)
            Bind(holder, previous);
        Bind(action, key);
    }

    public void Rebind(GameAction action, SDL.GamepadButton button)
    {
        var previous = PadFor(action);
        if (ActionUsing(button) is { } holder && holder != action)
            Bind(holder, previous);
        Bind(action, button);
    }

    /// <summary>Which action is already using this key, if any. Drives the swap when a key is reused.</summary>
    public GameAction? ActionUsing(SDL.Keycode key) =>
        Actions.Cast<GameAction?>().FirstOrDefault(a => KeyFor(a!.Value) == key);

    public GameAction? ActionUsing(SDL.GamepadButton button) =>
        Actions.Cast<GameAction?>().FirstOrDefault(a => PadFor(a!.Value) == button);

    public static readonly GameAction[] Actions =
        [GameAction.Confirm, GameAction.Cancel, GameAction.Menu, GameAction.Travel];

    public static string ActionLabel(GameAction action) => action switch
    {
        GameAction.Confirm => "決定",
        GameAction.Cancel => "キャンセル",
        GameAction.Menu => "メニュー",
        _ => "移動",
    };

    public void ResetToDefaults()
    {
        Keys.Clear();
        PadButtons.Clear();
        MusicVolume = 70;
        SoundVolume = 80;
    }
}

/// <summary>Loads and stores <see cref="Settings"/> alongside the save files.</summary>
public static class SettingsManager
{
    private static readonly string Path =
        System.IO.Path.Combine(SaveManager.SaveDirectory, "settings.json");

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter<GameAction>() },
    };

    /// <summary>
    /// Never throws. A corrupt or unreadable preferences file means the player plays with the defaults for one
    /// session, which is a far better outcome than a game that will not start.
    /// </summary>
    public static Settings Load()
    {
        try
        {
            if (!File.Exists(Path))
                return new Settings();
            return JsonSerializer.Deserialize<Settings>(File.ReadAllText(Path), Options) ?? new Settings();
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"Could not read settings, using defaults: {ex.Message}");
            return new Settings();
        }
    }

    public static void Save(Settings settings)
    {
        try
        {
            Directory.CreateDirectory(SaveManager.SaveDirectory);
            File.WriteAllText(Path, JsonSerializer.Serialize(settings, Options));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"Could not write settings: {ex.Message}");
        }
    }
}
