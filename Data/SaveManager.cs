using System.Text.Encodings.Web;
using System.Text.Json;
using SlimeDungeon.Domain;

namespace SlimeDungeon.Data;

/// <summary>
/// Plain JSON persistence in the per-user application data directory. Not encrypted yet (a later concern per
/// the design doc). Saves deliberately do not live beside the executable: that directory is build output, so a
/// rebuild that changes the target framework — or any clean of bin/ — takes the player's character with it.
/// </summary>
public static class SaveManager
{
    private const string VendorFolder = "WildTreeJP";
    private const string GameFolder = "SlimeDungeon";

    private static readonly string SavesDir = ResolveSaveDirectory();
    private static readonly string CharacterPath = Path.Combine(SavesDir, "character.json");
    private static readonly string HistoryPath = Path.Combine(SavesDir, "history.json");

    /// <summary>Where saves used to live, kept only so an existing character can be moved across once.</summary>
    private static readonly string LegacySavesDir = Path.Combine(AppContext.BaseDirectory, "saves");

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    static SaveManager() => MigrateLegacySaves();

    /// <summary>Exposed so the player can be told where their save actually lives.</summary>
    public static string SaveDirectory => SavesDir;

    /// <summary>
    /// The per-user data directory for this game. <see cref="Environment.SpecialFolder.LocalApplicationData"/>
    /// already resolves to AppData\Local on Windows and to $XDG_DATA_HOME (defaulting to ~/.local/share) on
    /// Linux; macOS gets its own conventional location instead, since .NET would otherwise put it under
    /// ~/.local/share there too.
    /// </summary>
    private static string ResolveSaveDirectory()
    {
        string root;
        if (OperatingSystem.IsMacOS())
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            root = Path.Combine(home, "Library", "Application Support");
        }
        else
        {
            root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        }

        // Some sandboxes report no home directory at all; fall back rather than throwing on startup.
        if (string.IsNullOrEmpty(root))
            root = AppContext.BaseDirectory;

        return Path.Combine(root, VendorFolder, GameFolder);
    }

    /// <summary>
    /// Moves a character saved by an older build out of the executable directory. Copies rather than moves, so
    /// a failure part-way through cannot destroy the only copy. Never throws: a migration problem must not stop
    /// the game from starting.
    /// </summary>
    private static void MigrateLegacySaves()
    {
        try
        {
            if (!Directory.Exists(LegacySavesDir) || File.Exists(CharacterPath))
                return;

            Directory.CreateDirectory(SavesDir);
            foreach (var name in new[] { "character.json", "history.json" })
            {
                var from = Path.Combine(LegacySavesDir, name);
                var to = Path.Combine(SavesDir, name);
                if (File.Exists(from) && !File.Exists(to))
                    File.Copy(from, to);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            Console.Error.WriteLine($"Could not migrate saves from '{LegacySavesDir}': {ex.Message}");
        }
    }

    public static bool HasSave => File.Exists(CharacterPath);

    public static void Save(Player player)
    {
        Directory.CreateDirectory(SavesDir);
        File.WriteAllText(CharacterPath, JsonSerializer.Serialize(player, Options));
    }

    public static Player? Load()
    {
        if (!File.Exists(CharacterPath))
            return null;
        try
        {
            var player = JsonSerializer.Deserialize<Player>(File.ReadAllText(CharacterPath), Options);
            if (player is not null)
            {
                foreach (var item in player.Equipment.Values)
                    EquipmentNames.MigrateStaleName(item);
                foreach (var item in player.Bag)
                    EquipmentNames.MigrateStaleName(item);
                // The bag is worn but lives outside the Equipment dictionary, so it needs its own pass.
                if (player.EquippedBag is { } worn)
                    EquipmentNames.MigrateStaleName(worn);
                MigrateExpToCumulative(player);
                MigrateDayCountToCalendar(player);
            }
            return player;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Saves written before EXP became a lifetime total stored <c>Exp</c> as progress within the current level
    /// (it was decremented back toward zero on every level-up). Detect that by checking whether
    /// <c>ExpToNext</c> matches the cumulative curve, and if not, fold the level's base total back in so the
    /// character keeps the progress they had rather than silently dropping a level's worth of EXP.
    /// </summary>
    private static void MigrateExpToCumulative(Player player)
    {
        var cumulative = ExpCurve.TotalForLevel(player.Level + 1);
        if (player.ExpToNext == cumulative)
            return;

        player.Exp = ExpCurve.TotalForLevel(player.Level) + player.Exp;
        player.ExpToNext = cumulative;
    }

    /// <summary>
    /// Saves written before the world calendar counted days from 1, whereas the calendar seeds a new character
    /// from the real date — which is a five-figure day number, since the epoch is 1970. Slide such a save onto
    /// the calendar and shift every quest deadline by the same amount, so days remaining are unchanged; without
    /// that shift the deadlines would all fall decades in the past and expire the moment the save loaded.
    /// </summary>
    private const int PreCalendarDayCeiling = 10_000;

    private static void MigrateDayCountToCalendar(Player player)
    {
        if (player.DayCount < PreCalendarDayCeiling)
        {
            var shift = GameCalendar.Today() - player.DayCount;
            var careerSoFar = player.DayCount;
            player.DayCount += shift;

            if (player.ActiveQuest is { } active)
                active.DeadlineDay += shift;
            foreach (var quest in player.OpenQuests)
                quest.DeadlineDay += shift;

            // Pre-calendar day counts started at 1, so the old value was itself the career length.
            player.StartDay = player.DayCount - careerSoFar + 1;
        }

        // Saves written between the calendar landing and StartDay existing have no registration day at all;
        // treat the career as starting today rather than reporting a twenty-thousand-day veteran.
        if (player.StartDay <= 0 || player.StartDay > player.DayCount)
            player.StartDay = player.DayCount;
    }

    public static void DeleteActive()
    {
        if (File.Exists(CharacterPath))
            File.Delete(CharacterPath);
    }

    public static void ArchiveToHistory(Player player)
    {
        Directory.CreateDirectory(SavesDir);
        var history = LoadHistory();
        history.Add(new HistoryEntry(player.Name, player.Rank, player.Level, player.DaysSurvived, player.KillCounts)
        {
            Gender = player.Gender,
            StartDay = player.StartDay,
            Gold = player.Gold,
        });
        File.WriteAllText(HistoryPath, JsonSerializer.Serialize(history, Options));
    }

    public static List<HistoryEntry> LoadHistory()
    {
        if (!File.Exists(HistoryPath))
            return new List<HistoryEntry>();
        try
        {
            return JsonSerializer.Deserialize<List<HistoryEntry>>(File.ReadAllText(HistoryPath), Options) ?? new List<HistoryEntry>();
        }
        catch (JsonException)
        {
            return new List<HistoryEntry>();
        }
    }
}
