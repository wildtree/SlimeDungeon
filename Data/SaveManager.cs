using System.Text.Encodings.Web;
using System.Text.Json;
using SlimeDungeon.Domain;

namespace SlimeDungeon.Data;

/// <summary>
/// Plain JSON persistence under &lt;exe dir&gt;/saves/. Not encrypted yet (a later concern per the design doc).
/// </summary>
public static class SaveManager
{
    private static readonly string SavesDir = Path.Combine(AppContext.BaseDirectory, "saves");
    private static readonly string CharacterPath = Path.Combine(SavesDir, "character.json");
    private static readonly string HistoryPath = Path.Combine(SavesDir, "history.json");

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

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
        history.Add(new HistoryEntry(player.Name, player.Rank, player.Level, player.DaysSurvived, player.KillCounts));
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
