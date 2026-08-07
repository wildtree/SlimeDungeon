namespace SlimeDungeon.Domain;

/// <summary>The two things the lodging house sells.</summary>
public enum InnService { Room, Meal }

/// <summary>
/// The cheap inn, and the game's main use for surplus gold.
///
/// The guild used to have one "回復" entry that filled HP and MP for ten gold a level, instantly and with no day
/// spent. From about iron onwards that was a rounding error against a trip's takings, so recovery stopped being
/// a decision — and with nothing else to buy, gold piled up until every piece of gear was automatic.
///
/// The two services are priced so that what money buys here is *time*, never strength. A bed puts everything
/// back but costs a day off every contract being held. A meal at the bar is a third of the price, changes no
/// date at all, and only takes the edge off — which is exactly what you want on the way back out to a dungeon
/// with a deadline, and never enough to make the trip safe.
/// </summary>
public sealed record InnServiceDefinition(
    InnService Service,
    string Name,
    string Note,
    double HpFraction,
    double MpFraction,
    int DaysSpent,
    int GoldPerLevel);

public static class Inn
{
    public static readonly InnServiceDefinition[] Services =
    [
        new(InnService.Room, "宿泊する", "HPとMPが全快する。一晩かかる",
            HpFraction: 1.0, MpFraction: 1.0, DaysSpent: 1, GoldPerLevel: 50),
        new(InnService.Meal, "飲食する", "HPを半分ほど、MPを少し戻す。日は変わらない",
            HpFraction: 0.5, MpFraction: 0.25, DaysSpent: 0, GoldPerLevel: 15),
    ];

    /// <summary>
    /// Scaled by level rather than flat, so lodging keeps pace with income for the whole game. A flat price
    /// would be crushing at level 1 and free at level 40, which is the exact failure the old heal had.
    /// </summary>
    public static int Cost(InnServiceDefinition service, Player player) =>
        Math.Max(service.GoldPerLevel, service.GoldPerLevel * player.Level);

    /// <summary>How much this service would actually put back, given how hurt the character is right now.</summary>
    public static (int Hp, int Mp) Restores(InnServiceDefinition service, Player player)
    {
        var hp = Heal(service.HpFraction, player.Stats.Hp, player.Stats.MaxHp);
        var mp = Heal(service.MpFraction, player.Stats.Mp, player.Stats.MaxMp);
        return (hp, mp);
    }

    /// <summary>
    /// A share of the maximum, never more than the shortfall. The fractional services also carry a floor of one
    /// point per rank of the fraction, because a level-1 character's half of ten HP rounds down to five and a
    /// quarter of ten MP would otherwise round to two — small, but a meal that restores nothing at all reads as
    /// a bug rather than as a cheap meal.
    /// </summary>
    private static int Heal(double fraction, int current, int max)
    {
        if (fraction <= 0 || current >= max)
            return 0;
        var amount = Math.Max(1, (int)Math.Floor(max * fraction));
        return Math.Min(amount, max - current);
    }

    /// <summary>
    /// Whether this service would do anything. Refusing to take money for nothing matters here because the
    /// room is expensive enough to sting and the player may well be at full HP but short on magic.
    /// </summary>
    public static bool WouldHelp(InnServiceDefinition service, Player player)
    {
        var (hp, mp) = Restores(service, player);
        return hp > 0 || mp > 0;
    }

    /// <summary>Takes the payment, spends the day, restores what the service covers, and reports it.</summary>
    public static string Use(InnServiceDefinition service, Player player)
    {
        var cost = Cost(service, player);
        var (hp, mp) = Restores(service, player);

        player.Gold -= cost;
        player.Stats.Hp += hp;
        player.Stats.Mp += mp;
        player.DayCount += service.DaysSpent;

        var what = mp > 0 ? $"HP{hp} MP{mp}" : $"HP{hp}";
        var night = service.DaysSpent > 0 ? "一晩休んで" : "";
        var verb = service.Service == InnService.Room ? "泊まった" : "食事をした";
        return $"{cost}Gで{verb}。{night}{what}回復した";
    }
}
