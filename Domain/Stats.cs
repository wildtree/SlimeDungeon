using System.Text.Json.Serialization;
using SlimeDungeon.Core;

namespace SlimeDungeon.Domain;

/// <summary>Core RPG stats. HP/MP track current+max; the rest are flat values.</summary>
public sealed class Stats
{
    public int MaxHp { get; set; }
    public int Hp { get; set; }
    public int MaxMp { get; set; }
    public int Mp { get; set; }
    public int Str { get; set; }
    public int Int { get; set; }
    public int Dex { get; set; }
    public int Agl { get; set; }

    [JsonIgnore]
    public bool IsDead => Hp <= 0;

    public static Stats Clone(Stats s) => new()
    {
        MaxHp = s.MaxHp,
        Hp = s.Hp,
        MaxMp = s.MaxMp,
        Mp = s.Mp,
        Str = s.Str,
        Int = s.Int,
        Dex = s.Dex,
        Agl = s.Agl,
    };

    /// <summary>Rolls the player's initial stats: base value, median = base, stdev = 1.5, floor, minimum 1.</summary>
    public static Stats RollInitial()
    {
        int Roll(double baseValue) => Math.Max(1, (int)Math.Floor(RandomUtil.Shared.NextGaussian(baseValue, 1.5)));

        // HP is the one figure the whole difficulty curve is anchored to, because growth is a fixed multiple per
        // rank band — so this number sets how many blows an adventurer survives at *every* rank, not just this
        // one. Fifteen came out at about two blows all game, which is past "tight" and into "unplayable";
        // thirty came out at four or five, which played as a walkover once armour was counted. Twenty-two puts a
        // same-rank fight at three to four blows: enough room to reach for a herb or run, not enough to stand
        // there trading hits, and a pack still kills you inside a round or two.
        //
        // MP is deliberately left where it was. It is spent by the cast rather than absorbed by the round, and
        // the spell costs are set against this figure to buy about four casts from a full bar at any rank.
        var hp = Roll(20);
        var mp = Roll(10);
        return new Stats
        {
            MaxHp = hp,
            Hp = hp,
            MaxMp = mp,
            Mp = mp,
            Str = Roll(10),
            Int = Roll(10),
            Dex = Roll(5),
            Agl = Roll(5),
        };
    }

    /// <summary>
    /// The level each rank is meant to be reached at, and therefore how many levels a rank band is worth.
    ///
    /// Growth used to be a flat percentage per level — 16% for HP and MP — which was the single largest balance
    /// fault in the game. Enemies only span ten ranks, a factor of 1.6^9 ≈ 69 from end to end; a flat 16% over
    /// ninety-nine levels is a factor of 1.9 <em>million</em>. Measured, that produced a level-100 adventurer
    /// with 28,000,000 HP against a rank-SS slime hitting for 515: fifty-four thousand blows to kill them. The
    /// inn was pointless, magic was free, and nothing could threaten anybody past about rank D.
    ///
    /// So growth is keyed to this table instead. A rank band is worth a fixed multiple whether it takes four
    /// levels or twenty, which is what keeps the player and the ladder in step for the whole climb.
    /// </summary>
    private static readonly (int Level, int Rank)[] RankLevels =
    [
        (1, 1), (5, 2), (10, 3), (20, 4), (30, 5), (40, 6), (50, 7), (60, 8), (80, 9), (100, 10),
    ];

    /// <summary>
    /// What HP is multiplied by across one rank band — deliberately the same 1.6 the enemy ladder climbs at.
    ///
    /// HP and MP used to share one rate of 1.75. Above the enemy's 1.6 it compounds: measured, a same-rank fight
    /// went from 4.3 blows survived at rank H to 7.1 at SS, so the climb quietly got safer the further it went.
    /// That is the opposite of what "適正" is supposed to mean. Matching 1.6 exactly holds the ratio flat, and
    /// the whole curve then sits wherever <see cref="RollInitial"/>'s HP base puts rank H.
    /// </summary>
    private const double HpPerRank = 1.6;

    /// <summary>
    /// MP sits a shade above it. It is not a buffer that soaks up a round the way HP is — it is spent by the
    /// cast, against spell costs that climb at the full 1.6, and the low ranks lose the most to the one-point
    /// minimum growth on a bar of nine. This much margin lands a full bar at four or five casts at every rank,
    /// which is what the spell costs were set against.
    /// </summary>
    private const double MpPerRank = 1.65;

    /// <summary>
    /// And what STR/INT/DEX/AGL are multiplied by. Far gentler on purpose. These feed
    /// <see cref="CombatMath.StatBonus"/>, which is a bounded margin rather than a damage source — so what they
    /// have to do is stay in a range where a weapon's "+8" is a visible fraction of them. At 1.22 a level-100
    /// adventurer has around 60 STR, and an orichalcum sword's +86 more than doubles it. Under the old curve
    /// they had 74,000 and the same sword was a rounding error.
    /// </summary>
    private const double StatPerRank = 1.22;

    /// <summary>
    /// Per-level growth for a multiple spread across whichever rank band <paramref name="newLevel"/> falls in.
    /// The bottom of the ladder is compressed — four levels from H to G — so those levels grow much harder than
    /// the twenty-level bands at the top, and the rank the character is fighting stays the thing that matters.
    /// </summary>
    private static double PerLevelRate(int newLevel, double perRank)
    {
        var levelsInBand = 10;
        for (var i = 1; i < RankLevels.Length; i++)
        {
            if (newLevel > RankLevels[i].Level)
                continue;
            levelsInBand = RankLevels[i].Level - RankLevels[i - 1].Level;
            break;
        }

        // Past the table's top the last band's pace simply continues.
        if (newLevel > RankLevels[^1].Level)
            levelsInBand = RankLevels[^1].Level - RankLevels[^2].Level;

        return Math.Pow(perRank, 1.0 / Math.Max(1, levelsInBand)) - 1.0;
    }

    /// <summary>Applies a level-up growth roll and fully heals HP/MP.</summary>
    public void ApplyLevelUpGrowth(int newLevel)
    {
        // A straight floor(current * rate) rounds down to 0 for any small stat, which reads as the character
        // never growing at all. The flat minimum of one point guarantees visible progress; it used to be two
        // for HP and MP, which at these gentler rates would have overwhelmed the curve at low levels.
        //
        // Rounding rather than flooring matters more than it looks. Flooring throws away half a point every
        // level, and because the next level's growth is a fraction of the result, that loss compounds: measured
        // over ninety-nine levels it turned a 1.6-per-rank curve into about 1.55, which is enough to drift a
        // same-rank fight from four blows survived down to two and a half by rank SS. Rounding is unbiased, so
        // the rates above are the rates the player actually gets.
        int Grow(int current, double rate)
        {
            var jittered = RandomUtil.Shared.NextGaussian(rate, rate * 0.15);
            var amount = Math.Max(1, (int)Math.Round(current * jittered));
            return current + amount;
        }

        var hp = PerLevelRate(newLevel, HpPerRank);
        var mp = PerLevelRate(newLevel, MpPerRank);
        var stat = PerLevelRate(newLevel, StatPerRank);

        MaxHp = Grow(MaxHp, hp);
        MaxMp = Grow(MaxMp, mp);
        Str = Grow(Str, stat);
        Int = Grow(Int, stat);
        Dex = Grow(Dex, stat);
        Agl = Grow(Agl, stat);

        Hp = MaxHp;
        Mp = MaxMp;
    }
}
