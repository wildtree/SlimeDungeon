using SlimeDungeon.Core;

namespace SlimeDungeon.Domain;

public enum QuestType { CollectHerb, CollectAntidote, DefeatSlime }

/// <summary>A guild quest: either "bring N of an item" (checked against the bag at report time)
/// or "defeat N of a slime species" (progress ticked live by combat while the quest is active).</summary>
public sealed class Quest
{
    public required string Title { get; init; }
    public required string Description { get; init; }
    public required QuestType Type { get; init; }
    public required Rank Rank { get; init; }
    public required int TargetCount { get; init; }
    public required int RewardGold { get; init; }
    /// <summary>Absolute world-calendar day the quest is due by. Settable so a pre-calendar save can be
    /// slid onto the calendar without changing how long the player has left.</summary>
    public required int DeadlineDay { get; set; }

    public SlimeColor? TargetSlimeColor { get; init; }
    public Rank? TargetItemRank { get; init; }

    /// <summary>How far along the quest is: slimes defeated, or items handed in so far.</summary>
    public int Progress { get; set; }

    public bool IsExpired(int currentDay) => currentDay > DeadlineDay;

    /// <summary>Collection quests are fulfilled by handing items over the counter, not by fighting.</summary>
    public bool IsCollection => Type is QuestType.CollectHerb or QuestType.CollectAntidote;

    /// <summary>Broad kind of job, for the board's first column.</summary>
    public string CategoryLabel => Type switch
    {
        QuestType.CollectHerb => "薬草採取",
        QuestType.CollectAntidote => "毒消し草採取",
        QuestType.DefeatSlime => "スライム討伐",
        _ => "依頼",
    };

    /// <summary>
    /// Just the specifics, for the board's detail column. The rank and the broad kind each have their own
    /// column, so this deliberately repeats neither — unlike <see cref="Description"/>, which is a full
    /// sentence and far too long to line up in a table.
    /// </summary>
    public string DetailLabel => Type switch
    {
        QuestType.CollectHerb or QuestType.CollectAntidote => $"{TargetCount}本",
        QuestType.DefeatSlime => TargetSlimeColor is { } c ? $"{SlimeNames.Of(c)} {TargetCount}体" : $"{TargetCount}体",
        _ => $"{TargetCount}",
    };

    public int Remaining => Math.Max(0, TargetCount - Progress);

    public bool IsComplete => Progress >= TargetCount;

    /// <summary>The item category this quest accepts, or null if it is not a collection job.</summary>
    private ItemCategory? DeliveryCategory => Type switch
    {
        QuestType.CollectHerb => ItemCategory.Herb,
        QuestType.CollectAntidote => ItemCategory.Antidote,
        _ => null,
    };

    /// <summary>
    /// Items the player is carrying that count toward this quest. Readied item slots count as well as the bag —
    /// a herb the player put in an item slot is still a herb, and not counting it would look like the delivery
    /// had gone missing.
    /// </summary>
    public int DeliverableInBag(Player player) =>
        DeliveryCategory is { } category
            ? player.CarriedItems.Count(i => i.Category == category && i.Rank == TargetItemRank)
            : 0;

    /// <summary>
    /// Hands over as much as the player is carrying, up to what is still owed, and banks it against
    /// <see cref="Progress"/>. Deliveries are accepted in instalments because the starting bag holds only
    /// three items, so a "collect 4" contract would otherwise be literally impossible to fulfil. Returns how
    /// many items were taken.
    /// </summary>
    public int Deliver(Player player)
    {
        if (!IsCollection)
            return 0;

        var wanted = Math.Min(Remaining, DeliverableInBag(player));
        if (wanted <= 0 || DeliveryCategory is not { } category)
            return 0;

        // Hand over what is loose in the bag first, so a readied item is only broken out of its slot when the
        // contract still needs it.
        var handed = player.Bag
            .Where(i => i.Category == category && i.Rank == TargetItemRank)
            .Concat(player.ReadiedItems.Where(i => i.Category == category && i.Rank == TargetItemRank))
            .Take(wanted)
            .ToList();

        foreach (var item in handed)
            player.ConsumeOne(item);

        Progress += handed.Count;
        return handed.Count;
    }

    /// <summary>
    /// Rank points earned by reporting this quest. Work at or above your own rank counts toward promotion;
    /// easy jobs below your rank pay gold but do not. (This used to award only for *above*-rank quests, which
    /// meant ordinary rank-appropriate work could never promote anyone.)
    /// </summary>
    public int RankPointsFor(Rank playerRank) => Rank >= playerRank ? 2 : 0;

    /// <summary>
    /// EXP paid on completion. Guild work is meant to be the main way an ordinary adventurer gets on in life,
    /// so a job is worth appreciably more than the slimes you kill along the way — otherwise taking contracts
    /// would be purely for pocket money and levelling would depend entirely on grinding dungeons.
    /// </summary>
    public int RewardExp => (int)Rank * (int)Rank * 4 + TargetCount * 2;

    // Both quest types now report the same way — against banked progress. Collection quests used to check the
    // bag contents at report time, which is why they needed the full amount carried in one trip.
}
