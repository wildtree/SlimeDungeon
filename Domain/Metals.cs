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
    public static readonly MetalDefinition[] All =
    [
        // Bronze reaches down to H. It used to start at G, which meant the whole forge — ore, smith, collector
        // titles — was invisible to a new adventurer: rank H can only enter H and G dungeons, and half of
        // those had nothing in them to find. A system nobody can stumble into may as well not be there.
        new(Metal.Bronze, "ブロンズ", "青銅", SlimeColor.Bronze, [Rank.H, Rank.G, Rank.F], Rank.F),
        new(Metal.Iron, "アイアン", "鉄", SlimeColor.Iron, [Rank.E, Rank.D], Rank.D),
        new(Metal.Copper, "カッパー", "銅", SlimeColor.Copper, [Rank.C], Rank.C),
        new(Metal.Silver, "シルバー", "銀", SlimeColor.Silver, [Rank.B], Rank.B),
        new(Metal.Mithril, "ミスリル", "ミスリル", SlimeColor.Mithril, [Rank.A], Rank.A),
        new(Metal.Adamantite, "アダマンタイト", "アダマンタイト", SlimeColor.Adamantite, [Rank.S], Rank.S),
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
    /// How often a slime in an ore-bearing dungeon turns out to be the metal one.
    ///
    /// Tuned against the rate a player actually *meets* one, which is roughly half the rate they are placed at:
    /// a trip is a walk to the stairs, not a sweep of the floor. 12% came out at under a third of trips and
    /// read as broken; 20% came out at nearly one trip in two and read as too common. This sits between them,
    /// at about one metal slime every three or four dungeons — rare enough to be a small event, frequent
    /// enough that nobody wonders whether the feature exists.
    /// </summary>
    public const double SlimeSpawnChance = 0.13;

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
