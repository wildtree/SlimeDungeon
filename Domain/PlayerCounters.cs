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

    /// <summary>Pieces of ore prised out of metal slimes, counted as they drop rather than as they are spent.</summary>
    public int MaterialsGathered { get; set; }

    /// <summary>Pieces that have come off the guild's anvil, counting a second bronze sword as a second piece.</summary>
    public int ItemsForged { get; set; }

    /// <summary>Stones cut out of gem slimes.</summary>
    public int GemsFound { get; set; }

    public int GemQuestsCompleted { get; set; }
    public int MetalQuestsCompleted { get; set; }

    /// <summary>
    /// Dungeons taken apart completely — every chest opened, then out by the stairs — above the adventurer's
    /// own rank. Deliberately stricter than <see cref="DungeonVisits"/>, which counts walking in: a trip that
    /// found the stairs early and left is not the same deed as one that swept the floor first.
    /// </summary>
    public int HigherRankDungeonsCleared { get; set; }

    /// <summary>The same, below their own rank — the careful half of the same habit.</summary>
    public int LowerRankDungeonsCleared { get; set; }
}
