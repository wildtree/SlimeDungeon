using SlimeDungeon.Core;
using SlimeDungeon.Domain;

namespace SlimeDungeon.Data;

/// <summary>
/// One retired adventurer, as the guild remembers them. Gender, registration day and purse were added after
/// the fact, so entries written before that carry the defaults — the records screen shows a dash rather than
/// inventing a value.
/// </summary>
public sealed record HistoryEntry(
    string Name,
    Rank ReachedRank,
    int Level,
    int DaysSurvived,
    Dictionary<string, int> KillCounts)
{
    public Gender? Gender { get; init; }

    /// <summary>Absolute world-calendar day the adventurer registered; 0 for entries predating this field.</summary>
    public int StartDay { get; init; }

    /// <summary>Gold on them when they fell; -1 for entries predating this field.</summary>
    public int Gold { get; init; } = -1;

    public int TotalKills => KillCounts?.Values.Sum() ?? 0;
}
