namespace SlimeDungeon.Domain;

/// <summary>Dungeon/item/monster/spell rank, from lowest (H) to highest (SS).</summary>
public enum Rank
{
    H = 1,
    G = 2,
    F = 3,
    E = 4,
    D = 5,
    C = 6,
    B = 7,
    A = 8,
    S = 9,
    SS = 10,
}

public static class RankExtensions
{
    public const int Min = (int)Rank.H;
    public const int Max = (int)Rank.SS;

    public static Rank Clamp(int value) => (Rank)Math.Clamp(value, Min, Max);

    public static string Label(this Rank rank) => rank.ToString();
}
