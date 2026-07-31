namespace SlimeDungeon.Domain;

/// <summary>
/// Lifetime tallies of what an adventurer has actually done. Kept separate from the character sheet because
/// none of it affects play — it exists so titles can be awarded for a career's worth of small deeds, which the
/// stats and kill log alone could not describe.
/// </summary>
public sealed class PlayerCounters
{
    /// <summary>Trips into a dungeon, counted on entry.</summary>
    public int DungeonVisits { get; set; }

    public int QuestsCompleted { get; set; }
    public int HerbQuestsCompleted { get; set; }
    public int AntidoteQuestsCompleted { get; set; }
    public int SlayQuestsCompleted { get; set; }

    public int ChestsOpened { get; set; }
    public int PotionsCrafted { get; set; }
    public int SpellsCast { get; set; }
    public int BattlesWon { get; set; }
    public int TimesFled { get; set; }

    /// <summary>Gold taken in over the whole career, never reduced by spending.</summary>
    public int GoldEarned { get; set; }
}
