using SlimeDungeon.Core;
using SlimeDungeon.Domain;
using SlimeDungeon.Dungeon;

namespace SlimeDungeon.Guild;

public static class QuestFactory
{
    public static List<Quest> CreateInitialQuests(int currentDay) => new()
    {
        new Quest
        {
            Title = "薬草採取",
            Description = "Hランクの薬草を3本納品する",
            Type = QuestType.CollectHerb,
            Rank = Rank.H,
            TargetItemRank = Rank.H,
            TargetCount = 3,
            RewardGold = 10,
            DeadlineDay = currentDay + 10,
        },
        new Quest
        {
            Title = "毒消し草採取",
            Description = "Hランクの毒消し草を3本納品する",
            Type = QuestType.CollectAntidote,
            Rank = Rank.H,
            TargetItemRank = Rank.H,
            TargetCount = 3,
            RewardGold = 10,
            DeadlineDay = currentDay + 10,
        },
        new Quest
        {
            Title = "グリーンスライム討伐",
            Description = "グリーンスライム(ランク不問)を10体退治する",
            Type = QuestType.DefeatSlime,
            Rank = Rank.H,
            TargetSlimeColor = SlimeColor.Green,
            TargetCount = 10,
            RewardGold = 50,
            DeadlineDay = currentDay + 14,
        },
        new Quest
        {
            Title = "ポイズンスライム討伐",
            Description = "ポイズンスライム(ランク不問)を1体退治する",
            Type = QuestType.DefeatSlime,
            Rank = Rank.H,
            TargetSlimeColor = SlimeColor.Poison,
            TargetCount = 1,
            RewardGold = 100,
            // Poison slimes are about one in seventy, so meeting even one takes a dozen dungeon trips. The
            // original two-week window made the game's highest-paying starter job a near-certain penalty.
            DeadlineDay = currentDay + 35,
        },
    };

    public static Quest CreateRandom(Rank playerRank, int currentDay)
    {
        var rnd = RandomUtil.Shared;
        var rank = RandomUtil.SampleRankUniform(playerRank, 1);
        var rankValue = (int)rank;
        // Generous deadlines: a missed job costs a penalty point and three of those demote you, which is a
        // lot of pressure for a game about taking things at your own pace.
        var deadline = currentDay + rnd.Next(8, 15);

        // A gem commission is drawn first and separately. It is not one of the guild's own jobs — a noble house
        // or a trading concern puts it up — so it does not take a turn in the ordinary rotation, it occasionally
        // displaces one. Only offered where the stone can actually be found, which is rank B and above.
        if (rank >= Gems.LowestRank && rnd.NextDouble() < GemQuestChance)
        {
            var gemQuest = TryGemQuest(rank, currentDay, rnd);
            if (gemQuest is not null)
                return gemQuest;
        }

        // Ore commissions likewise take a slot rather than a turn in the rotation, and only where the rank
        // actually has an ore in it — which is everywhere except the very top of nothing, since bronze now
        // reaches down to H.
        if (Metals.ForRank(rank) is not null && rnd.NextDouble() < MetalQuestChance)
            return MetalQuest(rank, currentDay, rnd);

        return (QuestType)rnd.Next(3) switch
        {
            QuestType.CollectHerb => new Quest
            {
                Title = "薬草採取",
                Description = $"{rank.Label()}ランクの薬草を{rnd.Next(2, 5)}本納品する",
                Type = QuestType.CollectHerb,
                Rank = rank,
                TargetItemRank = rank,
                TargetCount = rnd.Next(2, 5),
                RewardGold = rankValue * 15,
                DeadlineDay = deadline,
            },
            QuestType.CollectAntidote => new Quest
            {
                Title = "毒消し草採取",
                Description = $"{rank.Label()}ランクの毒消し草を{rnd.Next(2, 5)}本納品する",
                Type = QuestType.CollectAntidote,
                Rank = rank,
                TargetItemRank = rank,
                TargetCount = rnd.Next(2, 5),
                RewardGold = rankValue * 15,
                DeadlineDay = deadline,
            },
            // Slay quests set their own deadline from how rare the target is, so they get the current day.
            _ => RandomSlimeQuest(rank, currentDay, rnd),
        };
    }

    /// <summary>How often a board slot turns into an ore commission. Common enough to be part of the rotation.</summary>
    private const double MetalQuestChance = 0.2;

    /// <summary>
    /// Ore commissions, sized from the rate the ore actually drops rather than from a flat number.
    ///
    /// A dungeon in an ore's band puts a metal slime on the floor about 12% of the time and a killed one gives
    /// up its ore about half the time, across roughly six slimes a trip — so a trip is worth about a third of
    /// a piece. Both the count and the deadline are derived from that figure, so if the drop rate is ever
    /// retuned the contracts follow it instead of quietly becoming impossible.
    /// </summary>
    private static Quest MetalQuest(Rank rank, int currentDay, Random rnd)
    {
        var ore = Metals.ForRank(rank)!;

        var perTrip = Metals.SlimeSpawnChance * Metals.MaterialDropChance
                      * DungeonGenerator.AverageSlimesPerDungeon;

        // Two to four pieces: enough to be a job, few enough that the expected trip count stays in the range
        // an ordinary fortnight of play covers.
        var count = rnd.Next(2, 5);
        var expectedTrips = count / Math.Max(0.0001, perTrip);

        // Two and a half times the expected wait, plus slack. A missed deadline costs rank points and a fine
        // now, so a contract that is merely *usually* achievable is not good enough.
        var days = (int)Math.Ceiling(expectedTrips * 2.5) + rnd.Next(4, 9);
        var deadline = currentDay + Math.Clamp(days, 12, 60);

        // Better than herb work of the same rank, in proportion to costing several times the trips. A herb
        // contract of this rank pays rank*15; this lands around four times that.
        var reward = (int)rank * 25 + count * (int)rank * 12;

        return new Quest
        {
            Title = $"{ore.OreName}の採取",
            Description = $"{ore.Name}スライムから{ore.OreName}を{count}個集める",
            Type = QuestType.CollectMetal,
            Rank = rank,
            TargetMetal = ore.Metal,
            TargetCount = count,
            RewardGold = reward,
            DeadlineDay = deadline,
        };
    }

    /// <summary>
    /// How often a board slot at a gem-bearing rank turns out to be a commission rather than guild work.
    /// Uncommon on purpose: these pay several times what the guild does, and a board full of them would make
    /// every other job look like a waste of a fortnight.
    /// </summary>
    private const double GemQuestChance = 0.22;

    /// <summary>Who puts these up. Named only for flavour — the reward does not depend on which.</summary>
    private static readonly string[] GemClients =
    [
        "貴族のご令嬢", "オルド伯爵家", "宝飾ギルド", "豪商ドルトン", "王都の宝石商", "領主夫人", "辺境伯家",
    ];

    /// <summary>
    /// A commission for one particular stone. Always a single gem: they drop from roughly one slime in five
    /// hundred at the right rank in the right dungeon, so asking for two would be asking for a season's work.
    /// </summary>
    private static Quest? TryGemQuest(Rank rank, int currentDay, Random rnd)
    {
        // Every gem of this rank, regardless of element — the client wants what they want, and finding the
        // dungeon it can form in is the adventurer's problem.
        var candidates = Gems.All.Where(g => g.Rank == rank).ToArray();
        if (candidates.Length == 0)
            return null;

        var gem = candidates[rnd.Next(candidates.Length)];
        var client = GemClients[rnd.Next(GemClients.Length)];

        // Long. An aligned stone only forms in its own element's dungeon or a featureless one, so most of the
        // wait is for the right dungeon to come up at all, not for the slime to appear once you are in it.
        var deadline = currentDay + rnd.Next(40, 70);

        var elementNote = gem.Element == Domain.Element.None
            ? "無属性"
            : $"{SlimeNames.ElementLabel(gem.Element)}属性";

        return new Quest
        {
            Title = $"{gem.Name}の調達",
            Description = $"{client}の依頼。{gem.Name}（{elementNote}）を1個納品する",
            Type = QuestType.CollectGem,
            Rank = rank,
            TargetGem = gem.Gem,
            TargetItemRank = rank,
            Client = client,
            TargetCount = 1,
            // Several times an ordinary job of the rank. These are the paydays the whole system exists for.
            RewardGold = Gems.Value(gem.Gem) * 3,
            DeadlineDay = deadline,
        };
    }

    /// <summary>
    /// Slay quests are built around how often the target actually appears. Previously the colour was picked
    /// uniformly and the count was a flat 3-10 regardless, which made most of the board unacceptable: Poison,
    /// Gold and White turn up on roughly 1.4% of slimes, so "defeat 8 Poison slimes" needed something like
    /// sixty dungeon trips inside a two-week deadline. Now the count, the deadline and the reward all scale
    /// with rarity, and rare targets are asked for less often so the board is not dominated by them.
    /// </summary>
    private static Quest RandomSlimeQuest(Rank rank, int currentDay, Random rnd)
    {
        var color = PickTargetColor(rnd);
        var perSlime = Slime.AverageSpawnChance(color, DungeonGenerator.ElementDungeonChance);
        var perVisit = perSlime * DungeonGenerator.AverageSlimesPerDungeon;

        // Aim for something achievable in a handful of visits, so a rare target means "bring me one".
        var count = Math.Max(1, (int)Math.Round(perVisit * rnd.Next(2, 5)));

        // Expected visits to fill the order. For an elemental colour the average rate badly understates the
        // wait: they only swarm in their own element's dungeon and the dungeon's element is rolled on entry,
        // so the real cost is waiting for that dungeon to come up. Take whichever is worse.
        var favoredDungeonChance = Slime.FavoredDungeonChance(color, DungeonGenerator.ElementDungeonChance);
        var waitForFavoredDungeon = favoredDungeonChance > 0 ? 1 / favoredDungeonChance : 0;
        var expectedVisits = Math.Max(count / Math.Max(0.0001, perVisit), waitForFavoredDungeon);

        // Widened generously on top of that, because a missed deadline costs a penalty point.
        var days = (int)Math.Ceiling(expectedVisits * 2.5) + rnd.Next(3, 7);
        var deadline = currentDay + Math.Clamp(days, 8, 45);

        // Rarer prey is worth more per head.
        var rarityBonus = (int)Math.Round(1.0 / Math.Max(0.02, perSlime));
        var name = SlimeNames.FullName(color);

        return new Quest
        {
            Title = $"{name}討伐",
            Description = $"{name}(ランク不問)を{count}体退治する",
            Type = QuestType.DefeatSlime,
            Rank = rank,
            TargetSlimeColor = color,
            TargetCount = count,
            RewardGold = (int)rank * 10 + count * 3 + rarityBonus,
            DeadlineDay = deadline,
        };
    }

    /// <summary>
    /// Weights the target colour by the square root of its spawn rate. Uniform picking meant seven of every
    /// eight slay quests wanted an off-element colour, so the board felt full of jobs not worth taking; the
    /// square root keeps rare species showing up occasionally without them crowding it out.
    /// </summary>
    private static SlimeColor PickTargetColor(Random rnd)
    {
        // Only the ordinary species. A metal slime is far too scarce to build a quota around, and the guild
        // does not put a bounty on the dragon — that one is not work, it is a decision.
        var colors = Slime.OrdinaryColors;
        var weights = colors
            .Select(c => Math.Sqrt(Slime.AverageSpawnChance(c, DungeonGenerator.ElementDungeonChance)))
            .ToArray();

        var roll = rnd.NextDouble() * weights.Sum();
        for (var i = 0; i < colors.Length; i++)
        {
            roll -= weights[i];
            if (roll <= 0)
                return colors[i];
        }
        return colors[0];
    }

    public static void RefillExpiredBoardSlots(Player player)
    {
        for (var i = 0; i < player.OpenQuests.Count; i++)
        {
            var quest = player.OpenQuests[i];
            if (quest.IsExpired(player.DayCount) || IsUnreasonable(quest))
                player.OpenQuests[i] = CreateRandom(player.Rank, player.DayCount);
        }

        while (player.OpenQuests.Count < 4)
            player.OpenQuests.Add(CreateRandom(player.Rank, player.DayCount));
    }

    /// <summary>
    /// Weeds out slay quests asking for more of a species than it is reasonable to find. Boards saved before
    /// slay quests became rarity-aware can still hold things like "defeat 9 Poison slimes", which needs about
    /// a hundred dungeon trips; those would sit there unacceptable until they timed out.
    /// </summary>
    private static bool IsUnreasonable(Quest quest)
    {
        if (quest.Type != QuestType.DefeatSlime || quest.TargetSlimeColor is not { } color)
            return false;

        var perVisit = Slime.AverageSpawnChance(color, DungeonGenerator.ElementDungeonChance)
                       * DungeonGenerator.AverageSlimesPerDungeon;
        var mostWeWouldAsk = Math.Max(1, (int)Math.Round(perVisit * 4));
        return quest.TargetCount > mostWeWouldAsk;
    }
}
