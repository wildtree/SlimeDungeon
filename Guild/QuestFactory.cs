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
        var colors = Enum.GetValues<SlimeColor>();
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
