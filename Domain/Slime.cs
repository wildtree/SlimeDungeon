using SlimeDungeon.Core;

namespace SlimeDungeon.Domain;

public sealed class Slime
{
    public required SlimeColor Color { get; init; }
    public required Element Element { get; init; }
    public required Rank Rank { get; init; }
    public required Stats Stats { get; init; }

    /// <summary>Set once when a battle starts (e.g. "Green" or "Green A" when there are duplicates in the
    /// same encounter), so combat log lines and target lists can tell same-colored slimes apart.</summary>
    public string DisplayLabel { get; set; } = "";

    public bool IsPoison => Color == SlimeColor.Poison;

    /// <summary>
    /// White slimes are a different proposition from every other colour: steel simply slides off them, so they
    /// can only be brought down with magic. They would rather not fight at all.
    /// </summary>
    public bool IsWhite => Color == SlimeColor.White;

    /// <summary>Set when a slime has escaped the battle. It is neither a threat nor a source of EXP after that.</summary>
    public bool HasFled { get; set; }

    /// <summary>Still in the fight: not dead and not run away.</summary>
    public bool IsEngaged => !Stats.IsDead && !HasFled;

    /// <summary>
    /// The rank a white slime is built to, one above its own — but never past the top of the ladder, or an SS
    /// white would end up with defence scaled beyond anything that could be brought against its (capped) HP,
    /// and nothing in the game could kill it without a lucky critical.
    /// </summary>
    public Rank ToughenedRank => (Rank)Math.Min((int)Rank.SS, (int)Rank + 1);

    /// <summary>
    /// Damage subtracted from every hit. Ordinary slimes have none — their rank alone decides what can kill
    /// them. A white slime carries the defence of the rank above, which together with its HP is what makes it
    /// need magic a rank better than its own.
    /// </summary>
    public int Def => IsWhite ? Math.Max(1, (int)Math.Round(CombatMath.RankPower((int)ToughenedRank))) : 0;

    /// <summary>
    /// Per-rank stat multiplier. Doubling every rank (the original curve) made a rank+1 dungeon lethal at the
    /// bottom of the ladder while producing absurd numbers at the top, so growth is 1.6x per rank instead:
    /// gentle enough that your own rank is always comfortable and rank+1 is a stretch rather than a death
    /// sentence, still steep enough that the S/SS bands stay a genuine achievement (SS is ~69x rank H).
    /// </summary>
    private static double RankMultiplier(Rank rank) => Math.Pow(1.6, (int)rank - 1);

    private static Stats BaselineForRank(Rank rank)
    {
        var mult = RankMultiplier(rank);
        int Scale(double baseValue) => Math.Max(1, (int)Math.Round(baseValue * mult));

        // HP comes from CombatMath so that there is exactly one definition of "how tough is a rank-R slime",
        // which is what every attack in the game is calibrated against.
        var hp = CombatMath.SlimeHp(rank);
        var mp = Scale(4);
        return new Stats
        {
            MaxHp = hp,
            Hp = hp,
            MaxMp = mp,
            Mp = mp,
            // Set against a beginner's DEF of 3 from starting gear, this lands around 3 damage a hit: one
            // slime is a scratch, but a pair can take most of a level-1 adventurer's health bar in a couple
            // of rounds. Careless play should be able to get you killed.
            Str = Scale(6),
            Int = Scale(6),
            Dex = Scale(2),
            Agl = Scale(2),
        };
    }

    /// <summary>How often a dungeon's own element decides a slime's colour; the rest are spread evenly.</summary>
    public const double FavoredColorChance = 0.9;

    public static SlimeColor RollColor(Element? dungeonElement)
    {
        var rnd = RandomUtil.Shared;
        var favored = FavoredColorFor(dungeonElement);

        if (rnd.NextDouble() < FavoredColorChance)
            return favored;

        var others = Enum.GetValues<SlimeColor>().Where(c => c != favored).ToArray();
        return others[rnd.Next(others.Length)];
    }

    public static SlimeColor FavoredColorFor(Element? dungeonElement) =>
        dungeonElement is null or Element.None ? SlimeColor.Green : ColorForElement(dungeonElement.Value);

    /// <summary>
    /// Chance that a randomly entered dungeon is one where this colour is the favoured species — 0 for the
    /// colours that are never favoured anywhere. Elemental colours are wildly uneven: a Fire dungeon is full of
    /// Red slimes and every other dungeon has almost none, so the real wait for them is dominated by how long
    /// it takes a Fire dungeon to come up, not by their average rate.
    /// </summary>
    public static double FavoredDungeonChance(SlimeColor color, double elementDungeonChance)
    {
        if (color == SlimeColor.Green)
            return 1 - elementDungeonChance;

        var elements = new[] { Element.Fire, Element.Water, Element.Wind, Element.Earth };
        return elements.Any(e => ColorForElement(e) == color)
            ? elementDungeonChance / elements.Length
            : 0;
    }

    /// <summary>
    /// Chance that any one slime you meet is this colour, averaged over the kinds of dungeon that exist.
    /// Green is favoured in every featureless dungeon and so is overwhelmingly the most common; the four
    /// elemental colours are favoured only in their own dungeon; Poison, Gold and White are never favoured and
    /// only ever turn up in the leftover slice, which makes them roughly forty times rarer than Green.
    /// Quest generation needs this so it does not demand a pile of something you will almost never see.
    /// </summary>
    public static double AverageSpawnChance(SlimeColor color, double elementDungeonChance)
    {
        var colors = Enum.GetValues<SlimeColor>();
        var leftoverShare = (1 - FavoredColorChance) / (colors.Length - 1);
        var elements = new[] { Element.Fire, Element.Water, Element.Wind, Element.Earth };
        var perElement = elementDungeonChance / elements.Length;

        // Featureless dungeons favour Green.
        var total = (1 - elementDungeonChance) *
                    (color == SlimeColor.Green ? FavoredColorChance : leftoverShare);

        foreach (var element in elements)
            total += perElement * (ColorForElement(element) == color ? FavoredColorChance : leftoverShare);

        return total;
    }

    public static SlimeColor ColorForElement(Element element) => element switch
    {
        Element.Fire => SlimeColor.Red,
        Element.Water => SlimeColor.Blue,
        Element.Wind => SlimeColor.Yellow,
        Element.Earth => SlimeColor.Gray,
        _ => SlimeColor.Green,
    };

    public static Element ElementForColor(SlimeColor color) => color switch
    {
        SlimeColor.Red => Element.Fire,
        SlimeColor.Blue => Element.Water,
        SlimeColor.Yellow => Element.Wind,
        SlimeColor.Gray => Element.Earth,
        _ => Element.None,
    };

    public static Slime Create(SlimeColor color, Rank dungeonRankMedian, Element? dungeonElement)
    {
        var rank = RandomUtil.SampleRank(dungeonRankMedian, 0.5, 1);
        var element = ElementForColor(color);
        var stats = BaselineForRank(rank);

        // A white slime is built like the rank above it — that, and its defence, is what puts it out of reach
        // of magic of its own rank.
        if (color == SlimeColor.White)
        {
            stats.MaxHp = CombatMath.SlimeHp((Rank)Math.Min((int)Rank.SS, (int)rank + 1));
            stats.Hp = stats.MaxHp;
        }

        // A dungeon's element used to scale a slime's whole stat block by 1.2 or 0.8. That cannot apply to HP
        // any more — HP is the yardstick every attack is calibrated against, and a slime weakened below its
        // rank could be finished by a spell a rank too low, which would break the ladder. Applying it to
        // damage alone was worse than removing it: an elemental slime would die exactly as fast as before
        // while hitting 20% harder, which is a straight difficulty increase nobody asked for. So the elemental
        // character of a dungeon now lives entirely where the design puts it — in the matchup that moves an
        // attack up or down a rank — and a slime's own numbers come from its rank and nothing else.
        return new Slime { Color = color, Element = element, Rank = rank, Stats = stats, DisplayLabel = color.ToString() };
    }

    /// <summary>What a white slime is worth relative to any other slime of its rank — it is far harder to
    /// corner, and killing one at all means you brought the right magic.</summary>
    public const int WhiteExpMultiplier = 5;

    public int ExpValue(Element? dungeonElement)
    {
        var baseExp = (int)Rank * (int)Rank;
        if (dungeonElement is { } de && Element == de && de != Element.None)
            baseExp = (int)Math.Floor(baseExp * 1.1);
        if (IsWhite)
            baseExp *= WhiteExpMultiplier;
        return baseExp;
    }
}
