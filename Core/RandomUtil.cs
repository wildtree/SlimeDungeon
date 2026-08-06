using SlimeDungeon.Domain;

namespace SlimeDungeon.Core;

/// <summary>Shared RNG plus the normal-distribution helpers the design spec relies on everywhere.</summary>
public static class RandomUtil
{
    public static Random Shared { get; } = new();

    /// <summary>Standard normal-distributed sample via Box-Muller.</summary>
    public static double NextGaussian(this Random rnd, double mean = 0, double stdev = 1)
    {
        var u1 = 1.0 - rnd.NextDouble();
        var u2 = rnd.NextDouble();
        var z = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
        return mean + stdev * z;
    }

    /// <summary>
    /// Samples a rank as N(medianRank, stdev), rounded, then clamped both to [medianRank-spread, medianRank+spread]
    /// and to the valid [H, SS] range. This is the "rank ± spread, median = medianRank, stdev = x" pattern used
    /// throughout the spec for loot/monster/spell rank rolls.
    /// </summary>
    public static Rank SampleRank(Rank median, double stdev, int spread)
    {
        var medianValue = (int)median;
        var sample = (int)Math.Round(Shared.NextGaussian(medianValue, stdev));
        sample = Math.Clamp(sample, medianValue - spread, medianValue + spread);
        return RankExtensions.Clamp(sample);
    }

}
