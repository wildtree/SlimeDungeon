using SlimeDungeon.Core;

namespace SlimeDungeon.Domain;

public enum QuestType { CollectHerb, CollectAntidote, DefeatSlime, CollectGem, CollectMetal }

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

    /// <summary>The stone a gem commission is after. The rank comes from the gem, so this is the whole spec.</summary>
    public Gem? TargetGem { get; init; }

    /// <summary>The ore an ore commission is after.</summary>
    public Metal? TargetMetal { get; init; }

    /// <summary>
    /// Who wants it. Gem commissions do not come from the guild's usual clientele — a noble house or a trading
    /// concern puts them up, which is why they pay what they pay — and naming them is most of the flavour.
    /// </summary>
    public string? Client { get; init; }

    /// <summary>How far along the quest is: slimes defeated, or items handed in so far.</summary>
    public int Progress { get; set; }

    public bool IsExpired(int currentDay) => currentDay > DeadlineDay;

    /// <summary>Collection quests are fulfilled by handing items over the counter, not by fighting.</summary>
    public bool IsCollection =>
        Type is QuestType.CollectHerb or QuestType.CollectAntidote or QuestType.CollectGem or QuestType.CollectMetal;

    /// <summary>Broad kind of job, for the board's first column.</summary>
    public string CategoryLabel => Type switch
    {
        QuestType.CollectHerb => "薬草採取",
        QuestType.CollectAntidote => "毒消し草採取",
        QuestType.DefeatSlime => "スライム討伐",
        QuestType.CollectGem => "宝石調達",
        QuestType.CollectMetal => "鉱石採取",
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
        QuestType.CollectGem => TargetGem is { } g ? $"{Gems.NameOf(g)} {TargetCount}個" : $"{TargetCount}個",
        QuestType.CollectMetal => TargetMetal is { } m ? $"{Metals.Get(m).OreName} {TargetCount}個" : $"{TargetCount}個",
        QuestType.DefeatSlime => TargetSlimeColor is { } c ? $"{SlimeNames.Of(c)} {TargetCount}体" : $"{TargetCount}体",
        _ => $"{TargetCount}",
    };

    /// <summary>
    /// The contract compressed to one line for the guild card: what is wanted, how far along, and the unit —
    /// "苦草1/3本", "イエロー(H)0/2匹".
    ///
    /// The count is deliberately not <see cref="Progress"/> on its own. A collection contract banks nothing
    /// until it is reported, so a card reading off Progress would sit at 0/3 with three herbs already in the
    /// bag, which is the opposite of the question the player is asking it. What is carried counts towards the
    /// figure, capped at the target, so the line answers "can I go and report this yet".
    /// </summary>
    public string CardSummary(Player player)
    {
        var have = IsCollection
            ? Math.Min(TargetCount, Progress + DeliverableInBag(player))
            : Math.Min(TargetCount, Progress);

        return Type switch
        {
            QuestType.CollectHerb =>
                $"{EquipmentNames.Herb(TargetItemRank ?? Rank)}{have}/{TargetCount}本",
            QuestType.CollectAntidote =>
                $"{EquipmentNames.Antidote(TargetItemRank ?? Rank)}{have}/{TargetCount}本",
            QuestType.CollectGem =>
                $"{(TargetGem is { } g ? Gems.NameOf(g) : "宝石")}{have}/{TargetCount}個",
            QuestType.CollectMetal =>
                $"{(TargetMetal is { } m ? Metals.Get(m).OreName : "鉱石")}{have}/{TargetCount}個",
            QuestType.DefeatSlime =>
                $"{(TargetSlimeColor is { } c ? SlimeNames.Of(c) : "スライム")}({Rank.Label()}){have}/{TargetCount}匹",
            _ => $"{have}/{TargetCount}",
        };
    }

    public int Remaining => Math.Max(0, TargetCount - Progress);

    public bool IsComplete => Progress >= TargetCount;

    /// <summary>The item category this quest accepts, or null if it is not a collection job.</summary>
    private ItemCategory? DeliveryCategory => Type switch
    {
        QuestType.CollectHerb => ItemCategory.Herb,
        QuestType.CollectAntidote => ItemCategory.Antidote,
        QuestType.CollectGem => ItemCategory.Gemstone,
        QuestType.CollectMetal => ItemCategory.Material,
        _ => null,
    };

    /// <summary>
    /// Items the player is carrying that count toward this quest. Readied item slots count as well as the bag —
    /// a herb the player put in an item slot is still a herb, and not counting it would look like the delivery
    /// had gone missing.
    /// </summary>
    public int DeliverableInBag(Player player) => player.CarriedItems.Where(Accepts).Sum(i => i.Quantity);

    /// <summary>
    /// Whether one carried item counts towards this contract.
    ///
    /// Gems need more than the category and rank a herb needs: four different stones share rank S, so matching
    /// on rank alone would let a ruby settle a commission for a sapphire. The stone itself is the identity.
    /// </summary>
    private bool Accepts(Item item)
    {
        if (DeliveryCategory is not { } category || item.Category != category)
            return false;

        if (Type == QuestType.CollectGem)
            return item.Gem == TargetGem;

        // Likewise for ore: bronze and iron are both "material", so the metal itself is the identity.
        if (Type == QuestType.CollectMetal)
            return item.Metal == TargetMetal;

        return item.Rank == TargetItemRank;
    }

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
        if (wanted <= 0)
            return 0;

        // Hand over what is loose in the bag first, so a readied item is only broken out of its slot when the
        // contract still needs it. Ore stacks, so this counts pieces rather than entries.
        var handed = 0;
        foreach (var item in player.Bag.Where(Accepts).Concat(player.ReadiedItems.Where(Accepts)).ToList())
        {
            while (handed < wanted && item.Quantity > 0)
            {
                player.ConsumeOne(item);
                handed++;
            }
            if (handed >= wanted) break;
        }

        Progress += handed;
        return handed;
    }

    /// <summary>
    /// Rank points earned by reporting this quest. Work at or above your own rank counts toward promotion;
    /// easy jobs below your rank pay gold but do not. (This used to award only for *above*-rank quests, which
    /// meant ordinary rank-appropriate work could never promote anyone.)
    /// </summary>
    /// <summary>
    /// What finishing this contract is worth towards promotion, out of the ten a rank costs.
    ///
    /// This used to be a flat 2 for anything at or above your rank and nothing at all below it, which had two
    /// bad consequences. Below-rank work was strictly pointless — easier, worse paid, and no progress — so a
    /// third of the board was there to be ignored. And a job one rank above yours paid exactly the same as one
    /// at your own, so there was never a reason to take the harder of two similar postings. Three tiers fixes
    /// both: reaching up is the fast route, your own rank is the staple, and easy work still counts for
    /// something on a week when that is all you want to do.
    /// </summary>
    public int RankPointsFor(Rank playerRank) =>
        Rank > playerRank ? 3
        : Rank == playerRank ? 2
        : 1;

    /// <summary>
    /// EXP paid on completion. Guild work is meant to be the main way an ordinary adventurer gets on in life,
    /// so a job is worth appreciably more than the slimes you kill along the way — otherwise taking contracts
    /// would be purely for pocket money and levelling would depend entirely on grinding dungeons.
    /// </summary>
    public int RewardExp => (int)Rank * (int)Rank * 4 + TargetCount * 2;

    // Both quest types now report the same way — against banked progress. Collection quests used to check the
    // bag contents at report time, which is why they needed the full amount carried in one trip.
}
