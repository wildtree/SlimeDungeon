using SlimeDungeon.Core;

namespace SlimeDungeon.Domain;

/// <summary>
/// The stones a gem slime can be carrying. Written into the save by name on both the gem items and the quests
/// that ask for them, so entries may be appended but never renamed.
/// </summary>
public enum Gem
{
    Diamond,
    Ruby, Sapphire, Emerald, Opal,
    Agate, Aquamarine, Peridot, Moonstone,
    Flamestone, Streamstone, Galestone, Earthstone,
}

/// <summary>One stone: what it is called, what it is aligned to, and how deep you have to go to find it.</summary>
public sealed record GemDefinition(Gem Gem, string Name, Element Element, Rank Rank);

/// <summary>
/// Gems, and the rules about where they turn up.
///
/// Unlike the ores, gems are not a crafting ladder — there is nothing to forge from them. They are the rare
/// find at the top of the game: a stone in the core of a slime, wanted by people who can pay for it. What they
/// are for is the commissions in <see cref="QuestFactory"/>, which is why the table carries a rank and an
/// element rather than any stats.
/// </summary>
public static class Gems
{
    public static readonly GemDefinition[] All =
    [
        new(Gem.Diamond, "ダイアモンド", Element.None, Rank.SS),

        new(Gem.Ruby, "ルビー", Element.Fire, Rank.S),
        new(Gem.Sapphire, "サファイヤ", Element.Water, Rank.S),
        new(Gem.Emerald, "エメラルド", Element.Wind, Rank.S),
        new(Gem.Opal, "オパール", Element.Earth, Rank.S),

        new(Gem.Agate, "メノウ", Element.Fire, Rank.A),
        new(Gem.Aquamarine, "アクアマリン", Element.Water, Rank.A),
        new(Gem.Peridot, "ペリドット", Element.Wind, Rank.A),
        new(Gem.Moonstone, "ムーンストーン", Element.Earth, Rank.A),

        new(Gem.Flamestone, "火焔石", Element.Fire, Rank.B),
        new(Gem.Streamstone, "水流石", Element.Water, Rank.B),
        new(Gem.Galestone, "風雷石", Element.Wind, Rank.B),
        new(Gem.Earthstone, "大地石", Element.Earth, Rank.B),
    ];

    private static readonly Dictionary<Gem, GemDefinition> ById = All.ToDictionary(g => g.Gem);

    public static GemDefinition Get(Gem gem) => ById[gem];

    public static string NameOf(Gem gem) => ById[gem].Name;

    /// <summary>The lowest rank any gem is found at — below this the whole system is out of reach.</summary>
    public static readonly Rank LowestRank = All.Min(g => g.Rank);

    /// <summary>
    /// Which stones a slime in this dungeon could be carrying.
    ///
    /// Two rules, both from the spec. A gem only ever appears at its own rank — you will not find a diamond in
    /// a B dungeon, however lucky you are. And an aligned stone only forms where its element is present or
    /// where nothing contends with it, so a water dungeon grows sapphires and nothing else, while a featureless
    /// one can grow any of them. A diamond has no alignment to satisfy and so turns up anywhere at its rank.
    /// </summary>
    public static IEnumerable<GemDefinition> Available(Rank dungeonRank, Element? dungeonElement)
    {
        var element = dungeonElement ?? Element.None;
        return All.Where(g => g.Rank == dungeonRank
                              && (g.Element == Element.None
                                  || element == Element.None
                                  || g.Element == element));
    }

    /// <summary>
    /// How often a slime in a dungeon that has gems in it turns out to be a gem slime. Rarer than the metals —
    /// there is no set to complete and no grind to pace, just a windfall when a commission is on the board.
    /// </summary>
    public const double SlimeSpawnChance = 0.05;

    /// <summary>How often killing one leaves the stone intact rather than in pieces.</summary>
    public const double DropChance = 0.4;

    /// <summary>
    /// What a gem is worth if sold rather than handed to the noble who asked for it. Steep, because these only
    /// exist from rank B up and the commissions they satisfy pay several times this again.
    /// </summary>
    public static int Value(Gem gem)
    {
        var rank = (int)Get(gem).Rank;
        return rank * rank * 40;
    }
}
