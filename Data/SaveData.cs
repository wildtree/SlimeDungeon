using SlimeDungeon.Domain;

namespace SlimeDungeon.Data;

public sealed record HistoryEntry(string Name, Rank ReachedRank, int Level, int DaysSurvived, Dictionary<string, int> KillCounts);
