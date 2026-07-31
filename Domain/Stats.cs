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

        // A little more starting HP than the other stats. At 14 an above-rank slime — which an H dungeon turns
        // up about one time in six — took exactly half the bar per blow and killed in two, which is not a
        // mistake the player can see coming or answer. This leaves a pair genuinely threatening while giving
        // a careful beginner room to reach for a herb.
        var hp = Roll(15);
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

    /// <summary>Applies a level-up growth roll and fully heals HP/MP.</summary>
    public void ApplyLevelUpGrowth()
    {
        // A straight floor(current * pct%) rounds down to 0 for any stat under ~20 (DEX/AGL start around 5,
        // so a "10%" gain is well under half a point) — that reads as the character never growing at all.
        // The flat minimum guarantees visible progress while the percentage lets bigger stats gain more.
        int Grow(int current, double meanPct, int minGain)
        {
            var pct = RandomUtil.Shared.NextGaussian(meanPct, meanPct * 0.15);
            var amount = Math.Max(minGain, (int)Math.Floor(current * (pct / 100.0)));
            return current + amount;
        }

        // Survivability grows faster than offence on purpose: monsters get 1.6x stronger per rank, so if HP
        // only crept up ~10% a level the player could never safely settle into a higher rank band.
        MaxHp = Grow(MaxHp, 16, 2);
        MaxMp = Grow(MaxMp, 16, 2);
        Str = Grow(Str, 10, 1);
        Int = Grow(Int, 10, 1);
        Dex = Grow(Dex, 10, 1);
        Agl = Grow(Agl, 10, 1);

        Hp = MaxHp;
        Mp = MaxMp;
    }
}
