namespace SlimeDungeon.Core;

public enum Direction { Down, Up, Left, Right }

public enum Gender { Male, Female }

public enum WalkFrame { A, B }

/// <summary>
/// Slime species. Written into kill records and quest targets by name, so entries may be appended freely but
/// must never be renamed or reordered.
///
/// The first eight are the ordinary spread. The metal ones after them do not take part in the usual colour
/// roll at all — each appears only in the rank band its ore belongs to, and is picked before the ordinary
/// roll runs, which is what keeps Poison, Gold and White as rare as they were before this list grew.
/// </summary>
public enum SlimeColor
{
    Green, Red, Blue, Yellow, Gray, Poison, Gold, White,

    Bronze, Iron, Copper, Silver, Mithril, Adamantite, Orichalcum,

    /// <summary>The mutant. Not a metal, not part of any roll — see DungeonGenerator.</summary>
    Dragon,
}
