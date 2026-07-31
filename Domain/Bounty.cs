using SlimeDungeon.Core;

namespace SlimeDungeon.Domain;

/// <summary>One line of a bounty claim: how many of a given slime, and what the guild pays for them.</summary>
public sealed record BountyLine(SlimeColor Color, Rank Rank, int Count)
{
    public int PerHead => Bounty.PerHead(Color, Rank);
    public int Total => PerHead * Count;
}

/// <summary>
/// Slimes carry no purse. Money for killing them comes from the guild, against the tally of what you brought
/// down — which is why it is collected over the counter rather than picked up off the floor, and why it is
/// worth reporting a whole trip's work at once.
/// </summary>
public static class Bounty
{
    /// <summary>
    /// The going rate per head. Rank follows the same 1.6 curve as everything else in combat, so a slime that
    /// is twice the trouble is worth twice the fee; species is a scarcity premium on top, since a guild pays
    /// for what is hard to find as much as for what is hard to kill.
    /// </summary>
    public static int PerHead(SlimeColor color, Rank rank) =>
        Math.Max(1, (int)Math.Round(3 * CombatMath.RankPower((int)rank) * SpeciesMultiplier(color)));

    public static double SpeciesMultiplier(SlimeColor color) => color switch
    {
        SlimeColor.Green => 1.0,
        SlimeColor.Red or SlimeColor.Blue or SlimeColor.Yellow or SlimeColor.Gray => 1.2,
        SlimeColor.Poison => 2.0,
        SlimeColor.Gold => 3.0,
        SlimeColor.White => 5.0,
        _ => 1.0,
    };

    public static string ColorLabel(SlimeColor color) => color switch
    {
        SlimeColor.Green => "グリーン",
        SlimeColor.Red => "レッド",
        SlimeColor.Blue => "ブルー",
        SlimeColor.Yellow => "イエロー",
        SlimeColor.Gray => "グレー",
        SlimeColor.Poison => "ポイズン",
        SlimeColor.Gold => "ゴールド",
        SlimeColor.White => "ホワイト",
        _ => color.ToString(),
    };
}
