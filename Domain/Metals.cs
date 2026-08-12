using SlimeDungeon.Core;

namespace SlimeDungeon.Domain;

/// <summary>
/// The ores. Serialised by name on crafted gear and on the materials themselves, so entries may be appended
/// but never renamed.
/// </summary>
public enum Metal { Bronze, Iron, Copper, Silver, Mithril, Adamantite, Orichalcum }

/// <summary>
/// One ore: the slime that carries it, the ranks it turns up at, and the rank the gear forged from it comes
/// out at. Everything about a metal is stated once, here — the slime, the drop, the forge and the collector
/// title all read from this table rather than each keeping their own copy of the mapping.
/// </summary>
/// <param name="Name">
/// The canonical name, and the one everything is built from — the slime (ブロンズスライム), the forged gear
/// (ブロンズの剣) and the collector title (ブロンズコレクター) all read from this, so a metal is spelled the
/// same way in every corner of the game.
/// </param>
/// <param name="OreName">
/// What the raw material is called (青銅). Shown only at the forge, as a gloss on the name above — the shop's
/// gear already uses these kanji heavily (銅の剣, 鉄の盾, 銀の杖), so naming forged pieces this way would have
/// produced two completely different swords both called 鉄の剣.
/// </param>
public sealed record MetalDefinition(
    Metal Metal,
    string Name,
    string OreName,
    SlimeColor Slime,
    Rank[] Ranks,
    Rank GearRank);

public static class Metals
{
    /// <summary>
    /// The ladder. Gear comes out at the top of the band the ore is found in, so a metal is always worth
    /// roughly what the slimes carrying it were worth to fight.
    /// </summary>
    /// <summary>
    /// The ladder, one metal per rank from F upward.
    ///
    /// Each ore now sits at exactly the rank whose gear it makes, so "the metal for this rank" and "the gear you
    /// should be wearing at this rank" are the same sentence. Bronze used to span H through F and iron E through
    /// D, which put iron gear two ranks below where it was needed and left the top of the ladder crowded.
    ///
    /// Rank B has no ore of its own. There are seven metals and ten ranks, and B is where the shop's own ladder
    /// (古木) is strong enough to bridge the gap — better a visible gap in one place than an eighth metal
    /// invented to fill it.
    ///
    /// Bronze also turns up in H and G dungeons, but at <see cref="RareSpawnChance"/> rather than the ordinary
    /// rate: a new adventurer should hear that metal slimes exist without being able to farm one.
    /// </summary>
    public static readonly MetalDefinition[] All =
    [
        new(Metal.Bronze, "ブロンズ", "青銅", SlimeColor.Bronze, [Rank.H, Rank.G, Rank.F], Rank.F),
        new(Metal.Iron, "アイアン", "鉄", SlimeColor.Iron, [Rank.E], Rank.E),
        new(Metal.Copper, "カッパー", "銅", SlimeColor.Copper, [Rank.D], Rank.D),
        new(Metal.Silver, "シルバー", "銀", SlimeColor.Silver, [Rank.C], Rank.C),
        // Adamantite below mithril: the rarest ore in the world should be the one the last rank is built from.
        new(Metal.Adamantite, "アダマンタイト", "アダマンタイト", SlimeColor.Adamantite, [Rank.A], Rank.A),
        new(Metal.Mithril, "ミスリル", "ミスリル", SlimeColor.Mithril, [Rank.S], Rank.S),
        new(Metal.Orichalcum, "オリハルコン", "オリハルコン", SlimeColor.Orichalcum, [Rank.SS], Rank.SS),
    ];

    private static readonly Dictionary<Metal, MetalDefinition> ById = All.ToDictionary(m => m.Metal);
    private static readonly Dictionary<SlimeColor, MetalDefinition> BySlime = All.ToDictionary(m => m.Slime);

    public static MetalDefinition Get(Metal metal) => ById[metal];

    /// <summary>The ore a slime carries, or null if it is not a metal slime.</summary>
    public static MetalDefinition? ForSlime(SlimeColor color) =>
        BySlime.TryGetValue(color, out var m) ? m : null;

    public static bool IsMetalSlime(SlimeColor color) => BySlime.ContainsKey(color);

    /// <summary>The ore found at this dungeon rank, or null where none is.</summary>
    public static MetalDefinition? ForRank(Rank rank) =>
        All.FirstOrDefault(m => m.Ranks.Contains(rank));

    /// <summary>
    /// How often a slime in the ore's own rank of dungeon turns out to be the metal one.
    ///
    /// Tuned against the rate a player actually *meets* one, which is roughly half the rate they are placed at:
    /// a trip is a walk to the stairs, not a sweep of the floor. At 13% a metal slime turned up every three or
    /// four trips, which made a full seven-piece set a routine errand rather than a project. At 6% it is about
    /// one in seven or eight — still often enough that nobody wonders whether the feature exists.
    /// </summary>
    public const double SlimeSpawnChance = 0.06;

    /// <summary>
    /// The rate for a metal appearing outside its own rank — which is only bronze, in H and G dungeons. Low
    /// enough that a beginner meets one perhaps once in thirty trips: a story rather than a supply.
    /// </summary>
    public const double RareSpawnChance = 0.012;

    /// <summary>
    /// How often a metal slime turns up in a dungeon of this rank, which is the ordinary rate at the ore's own
    /// rank and the rare one anywhere else it reaches.
    /// </summary>
    public static double SpawnChanceAt(Rank rank) =>
        ForRank(rank) is { } ore && ore.GearRank == rank ? SlimeSpawnChance : RareSpawnChance;

    /// <summary>
    /// The share of a floor's slimes an ordinary trip actually fights. Measured by walking the shortest route
    /// to the stairs and counting what comes within chase range of it: consistently a little under half.
    ///
    /// Anything that reasons about "per trip" rates has to include this. Leaving it out is what made the ore
    /// commissions twice as optimistic as they should have been.
    /// </summary>
    public const double SlimesMetPerTrip = 0.47;

    /// <summary>How often killing one yields its ore.</summary>
    public const double MaterialDropChance = 0.5;

    /// <summary>
    /// What a metal slime is worth next to an ordinary one of its rank — armoured, uncommon, and carrying
    /// something the smith wants.
    /// </summary>
    public const int ExpMultiplier = 3;
}
