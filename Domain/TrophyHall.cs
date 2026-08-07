namespace SlimeDungeon.Domain;

/// <summary>
/// The guild's display wall, and the only thing in the game that gold buys purely for its own sake.
///
/// Every other use for money buys capability, which is a problem in a game where money piles up: the more there
/// is, the easier everything gets. A case on the wall does nothing at all. It costs a great deal, it rises
/// steeply with each one, and what it gives back is somewhere to put the sword you spent forty dungeon trips
/// gathering ore for — which for a collection is the point, and for the difficulty curve is harmless.
///
/// Cases are bought; what goes in them is not consumed. The gold is the sink, and charging the player their
/// trophies as well would make the wall something to avoid rather than something to fill.
/// </summary>
public static class TrophyHall
{
    /// <summary>One wall of the guild's hall. Eight is as many as fit above the fireplace.</summary>
    public const int MaxCases = 8;

    /// <summary>
    /// What the next case costs. Quadratic, so the first is affordable around the time iron gear is, and the
    /// eighth is a project for someone who has run out of other things to want. The whole wall comes to about
    /// 306,000 gold — several hundred dungeon trips at SS rates, which is the intended shape of a sink meant to
    /// absorb an endgame surplus rather than to be finished.
    /// </summary>
    public static int CaseCost(int casesOwned)
    {
        var next = casesOwned + 1;
        return 1200 * next * next;
    }

    /// <summary>
    /// What is worth putting on a wall: something the smith made, or a stone cut out of a gem slime. Ordinary
    /// shop gear is deliberately excluded — a display case for a leather cap would make the wall a storage
    /// chest, and there is already a bag for that.
    /// </summary>
    public static bool CanDisplay(Item item) =>
        item.ForgeId is not null || item.Category == ItemCategory.Gemstone;

    /// <summary>A line of caption under a mounted piece, saying what it is rather than repeating its name.</summary>
    public static string Caption(Item item) =>
        item.Category == ItemCategory.Gemstone
            ? $"{item.Rank.Label()}ランクの宝石"
            : $"{item.Rank.Label()}ランク・鍛冶作";
}
