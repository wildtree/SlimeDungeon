using SlimeDungeon.Core;

namespace SlimeDungeon.Domain;

/// <summary>
/// The rank lattice all offence is built on.
///
/// Damage is not derived from stats any more, it is derived from the *rank* of what you are swinging or
/// casting, on the same 1.6-per-rank curve slime HP follows. That one decision is what makes the whole balance
/// statable in a sentence: an attack of rank R kills a rank-R slime outright and cannot kill a rank R+1 one.
/// Everything else in combat is a step along that ladder — a critical hit, a favourable element and a second
/// weapon each move you one rank up, an unfavourable element moves you one rank down — so any combination can
/// be reasoned about by counting steps rather than by simulating numbers.
///
/// Magic used to scale off INT with an elemental multiplier on top, which is why it ran away with the game:
/// nothing capped it against the enemy it was pointed at. Here its ceiling is structural.
/// </summary>
public static class CombatMath
{
    /// <summary>Growth per rank. Must stay in step with <see cref="Slime"/>'s stat curve or the lattice breaks.</summary>
    public const double RankStep = 1.6;

    /// <summary>Rank-H slime HP, the unit the whole ladder is measured in.</summary>
    public const double SlimeBaseHp = 6.0;

    /// <summary>
    /// How far an attack overshoots the HP of the rank it is matched against. Anything from 1.0 up kills its
    /// own rank; it has to stay under <see cref="RankStep"/> (with the stat bonus included) or it would start
    /// killing the rank above and the ladder would collapse. 1.25 sits in the middle of that window.
    /// </summary>
    public const double KillMargin = 1.25;

    public const double RollMean = 50.0;
    public const double RollStdDev = 25.0;

    /// <summary>At or above this the blow lands as a critical: one rank harder.</summary>
    public const int CriticalThreshold = 70;

    /// <summary>At or below this the attack misses outright.</summary>
    public const int FailureThreshold = 30;

    /// <summary>
    /// The most STR/INT can add. Kept well under the 25% of headroom <see cref="KillMargin"/> leaves before the
    /// next rank's HP, so a strong adventurer hits harder without ever punching above the rank of their gear.
    /// </summary>
    public const double MaxStatBonus = 0.15;

    /// <summary>The curve itself. Takes a real rank so that half-steps and sub-rank-H values are expressible.</summary>
    public static double RankPower(double effectiveRank) => Math.Pow(RankStep, effectiveRank - 1);

    /// <summary>The HP a slime of this rank is built with — the number every attack is calibrated against.</summary>
    public static int SlimeHp(Rank rank) =>
        Math.Max(1, (int)Math.Round(SlimeBaseHp * RankPower((int)rank)));

    /// <summary>Damage before the target's defence, for an attack that lands at this effective rank.</summary>
    public static int AttackDamage(double effectiveRank, double statBonus) =>
        Math.Max(1, (int)Math.Round(SlimeBaseHp * KillMargin * RankPower(effectiveRank) * (1 + statBonus)));

    /// <summary>
    /// STR's (or INT's) bounded contribution. Deliberately gentle and capped: stats decide margins, ranks
    /// decide outcomes.
    /// </summary>
    public static double StatBonus(int stat) => Math.Clamp(stat / 400.0, 0, MaxStatBonus);

    public static AttackRoll Roll() => new(
        (int)Math.Round(Math.Clamp(RandomUtil.Shared.NextGaussian(RollMean, RollStdDev), 0, 100)));
}

/// <summary>
/// One swing's luck, on a 0–100 normal curve centred on 50. Shared by both sides of a fight — a slime can
/// fumble or land a critical exactly as the adventurer can.
/// </summary>
public readonly record struct AttackRoll(int Value)
{
    public bool IsCritical => Value >= CombatMath.CriticalThreshold;
    public bool IsFailure => Value <= CombatMath.FailureThreshold;

    /// <summary>How far up the ladder this roll moves the attack: one rank on a critical, none otherwise.</summary>
    public int RankSteps => IsCritical ? 1 : 0;
}
