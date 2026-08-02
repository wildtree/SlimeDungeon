using System.Text.Json.Serialization;
using SlimeDungeon.Core;

namespace SlimeDungeon.Domain;

public sealed record KillRecord(SlimeColor Color, Rank Rank, int Count);

public sealed record LearnedSpell(SpellId Id, Rank Rank);

public sealed class Player
{
    public required string Name { get; set; }
    public required Gender Gender { get; set; }
    public Rank Rank { get; set; } = Rank.H;
    public int Level { get; set; } = 1;
    public int Exp { get; set; }
    public int ExpToNext { get; set; } = ExpCurve.TotalForLevel(2);
    public Stats Stats { get; set; } = Domain.Stats.RollInitial();
    public int Gold { get; set; }
    /// <summary>
    /// Absolute day number in the world calendar — day 1 is 元年 木の上月 1日, which is the Unix epoch. A new
    /// character is seeded from the real date, and it advances by one for every return from a dungeon. Quest
    /// deadlines are stored on the same scale, so all the comparisons against them stay plain arithmetic.
    /// </summary>
    public int DayCount { get; set; } = GameCalendar.Today();

    /// <summary>
    /// Calendar day this adventurer registered with the guild. Needed because <see cref="DayCount"/> became an
    /// absolute calendar day: without a starting point, "days survived" would report the day number itself
    /// (some twenty thousand) instead of the length of the career.
    /// </summary>
    public int StartDay { get; set; } = GameCalendar.Today();

    [JsonIgnore]
    public int DaysSurvived => Math.Max(1, DayCount - StartDay + 1);
    public int RankPoints { get; set; }
    public int PenaltyCount { get; set; }

    public Dictionary<EquipSlot, Item> Equipment { get; set; } = new();
    public Item? EquippedBag { get; set; }
    public List<Item> Bag { get; set; } = new();
    public List<LearnedSpell> KnownSpells { get; set; } = new();
    public Dictionary<string, int> KillCounts { get; set; } = new();
    public Quest? ActiveQuest { get; set; }
    public List<Quest> OpenQuests { get; set; } = new();

    public PlayerCounters Counters { get; set; } = new();

    /// <summary>
    /// Ore left with the guild's smith for safekeeping.
    ///
    /// Ore itself rides in the bag now — it competes for space, sells at the shop and settles commissions like
    /// anything else. This is the store you can hand it to instead, which frees the slot but takes it out of
    /// circulation: what is in here can be forged with and nothing else. It cannot be sold and it cannot be
    /// delivered, which is the price of not carrying it.
    /// </summary>
    public Dictionary<Metal, int> Materials { get; set; } = new();

    /// <summary>
    /// Every forge recipe this adventurer has ever completed, by id. Kept separately from what they are
    /// carrying because a collector title asks what you have made, not what you still own — you may sell a
    /// bronze helmet, and you may never wear the sword and the wand at the same time.
    /// </summary>
    public List<string> CraftedRecipes { get; set; } = new();

    /// <summary>Every title earned so far, in the order they were awarded. Titles are never lost.</summary>
    public List<TitleId> EarnedTitles { get; set; } = new();

    /// <summary>Which earned title shows on the guild card; null means none is displayed.</summary>
    public TitleId? DisplayedTitle { get; set; }

    /// <summary>Takes in gold, keeping the lifetime tally in step. Spending goes straight to <see cref="Gold"/>.</summary>
    public void EarnGold(int amount)
    {
        if (amount <= 0)
            return;
        Gold += amount;
        Counters.GoldEarned += amount;
    }

    [JsonIgnore]
    public int TotalSlimesDefeated => KillCounts.Values.Sum();

    [JsonIgnore]
    public int DistinctSlimeColorsDefeated => KillLog.Select(k => k.Color).Distinct().Count();

    /// <summary>Whether this species has ever been brought down, at any rank.</summary>
    public bool HasDefeated(SlimeColor color) => KillLog.Any(k => k.Color == color);

    /// <summary>Highest-ranked slime ever defeated, or null if none yet.</summary>
    [JsonIgnore]
    public Rank? BestSlimeRankDefeated
    {
        get
        {
            var ranks = KillLog.Select(k => k.Rank).ToList();
            return ranks.Count == 0 ? null : ranks.Max();
        }
    }

    public const int MaxKnownSpells = 4;

    [JsonIgnore]
    public bool IsAlive => !Stats.IsDead;

    public void LearnSpell(SpellId id, Rank rank)
    {
        KnownSpells.RemoveAll(s => s.Id == id);
        KnownSpells.Add(new LearnedSpell(id, rank));
    }

    public void ForgetSpell(SpellId id) => KnownSpells.RemoveAll(s => s.Id == id);

    [JsonIgnore]
    public int BagCapacity => EquippedBag?.BagCapacity ?? 0;
    [JsonIgnore]
    public bool BagHasRoom => Bag.Count < BagCapacity;

    /// <summary>The two consumable slots, in the order every menu lists them.</summary>
    public static readonly EquipSlot[] ItemSlots = { EquipSlot.Item1, EquipSlot.Item2 };

    /// <summary>Consumables readied in the item slots — the only ones reachable during a fight.</summary>
    [JsonIgnore]
    public IEnumerable<Item> ReadiedItems =>
        ItemSlots.Where(Equipment.ContainsKey).Select(s => Equipment[s]);

    /// <summary>
    /// Everything on the adventurer's person: what is in the bag plus what is readied. Readied items are not in
    /// the bag, so anything counting what the player is carrying (quest deliveries, crafting stock) has to look
    /// here or readying an item would appear to make it vanish.
    /// </summary>
    [JsonIgnore]
    public IEnumerable<Item> CarriedItems => Bag.Concat(ReadiedItems);

    /// <summary>
    /// Consumes one of <paramref name="item"/> wherever it is being carried, emptying its item slot or dropping
    /// it from the bag once the last one is gone. Returns false if the player is not carrying it at all.
    /// </summary>
    public bool ConsumeOne(Item item)
    {
        if (Bag.Contains(item))
        {
            if (--item.Quantity <= 0)
                Bag.Remove(item);
            return true;
        }

        foreach (var slot in ItemSlots)
        {
            if (!Equipment.TryGetValue(slot, out var readied) || !ReferenceEquals(readied, item))
                continue;
            if (--item.Quantity <= 0)
                Equipment.Remove(slot);
            return true;
        }

        return false;
    }

    [JsonIgnore]
    public int EffectiveStr => Stats.Str + Equipment.Values.Where(i => i.Category == ItemCategory.Weapon && i.WeaponKind == WeaponKind.Sword).Sum(i => i.StatBonus);
    [JsonIgnore]
    public int EffectiveInt => Stats.Int + Equipment.Values.Where(i => i.Category == ItemCategory.Weapon && i.WeaponKind == WeaponKind.Wand).Sum(i => i.StatBonus);
    [JsonIgnore]
    public int EffectiveDex => Stats.Dex + Equipment.Values.Where(i => i.Category == ItemCategory.Gauntlet).Sum(i => i.StatBonus);
    [JsonIgnore]
    public int EffectiveAgl => Stats.Agl + Equipment.Values.Where(i => i.Category == ItemCategory.Shoes).Sum(i => i.StatBonus);
    [JsonIgnore]
    public int TotalDef => Equipment.Values.Where(i => i.Category is ItemCategory.Armor or ItemCategory.Helmet or ItemCategory.Shield).Sum(i => i.Def);

    /// <summary>Whatever is in the hands, weapons only — a shield is not something you hit with.</summary>
    [JsonIgnore]
    public List<Item> EquippedWeapons =>
        new[] { EquipSlot.RightHand, EquipSlot.LeftHand }
            .Where(Equipment.ContainsKey)
            .Select(s => Equipment[s])
            .Where(i => i.Category == ItemCategory.Weapon)
            .ToList();

    [JsonIgnore]
    public bool IsDualWielding => EquippedWeapons.Count >= 2;

    /// <summary>
    /// The rank an armed attack lands at: the better weapon's own rank, plus one for fighting with a blade in
    /// each hand — which is exactly what lets a two-weapon adventurer finish off slimes a rank above their
    /// gear. Bare-handed comes out below rank H, so fists cannot reliably fell even the weakest slime.
    /// </summary>
    [JsonIgnore]
    public double WeaponAttackRank
    {
        get
        {
            var weapons = EquippedWeapons;
            if (weapons.Count == 0)
                return 0;
            var best = weapons.Max(w => (int)w.Rank);
            return weapons.Count >= 2 ? best + 1 : best;
        }
    }

    // ---- Ore and the forge -----------------------------------------------------------------------

    /// <summary>
    /// Every kind of stone this adventurer has ever held, whether or not they still have it. Kept because the
    /// gems themselves are spent — handed to whoever commissioned them, or sold — so counting what is in the
    /// bag could never tell you that all thirteen had been seen.
    /// </summary>
    public List<Gem> GemsSeen { get; set; } = new();

    public void RecordGem(Gem gem)
    {
        Counters.GemsFound++;
        if (!GemsSeen.Contains(gem))
            GemsSeen.Add(gem);
    }

    /// <summary>Ore in the bag, which is the only ore that can be sold or handed to a commission.</summary>
    public int CarriedMaterial(Metal metal) =>
        Bag.Where(i => i.Category == ItemCategory.Material && i.Metal == metal).Sum(i => i.Quantity);

    /// <summary>Ore left with the smith. Reachable by the forge and by nothing else.</summary>
    public int DepositedMaterial(Metal metal) => Materials.GetValueOrDefault(metal);

    /// <summary>Everything the forge could draw on: what is carried plus what is stored.</summary>
    public int AvailableMaterial(Metal metal) => CarriedMaterial(metal) + DepositedMaterial(metal);

    /// <summary>
    /// Takes ore into the bag, stacking onto a pile of the same metal if one is there. Returns false when
    /// there is no room and no existing stack to add to — the caller then has to decide what to throw away.
    /// </summary>
    public bool TryTakeMaterial(Metal metal)
    {
        var stack = Bag.FirstOrDefault(i => i.Category == ItemCategory.Material && i.Metal == metal);
        if (stack is null && !BagHasRoom)
            return false;

        if (stack is null)
            Bag.Add(ItemFactory.CreateMaterial(metal));
        else
            stack.Quantity++;

        Counters.MaterialsGathered++;
        return true;
    }

    /// <summary>Hands one piece of carried ore to the smith to keep. Returns false if none is being carried.</summary>
    public bool DepositMaterial(Metal metal)
    {
        var stack = Bag.FirstOrDefault(i => i.Category == ItemCategory.Material && i.Metal == metal);
        if (stack is null)
            return false;

        ConsumeOne(stack);
        Materials[metal] = DepositedMaterial(metal) + 1;
        return true;
    }

    public bool HasCrafted(string recipeId) => CraftedRecipes.Contains(recipeId);

    /// <summary>Whether the ore and the gold are both to hand, counting the smith's store as well as the bag.</summary>
    public bool CanForge(ForgeRecipe recipe) =>
        Gold >= recipe.GoldCost && recipe.Cost.All(c => AvailableMaterial(c.Key) >= c.Value);

    /// <summary>
    /// Spends ore on a piece of work: out of the smith's store first, then out of the bag. That order is
    /// deliberate — stored ore has no other use, so burning it first leaves whatever is still being carried
    /// free to be sold or handed to a commission.
    /// </summary>
    private void SpendMaterialForForge(Metal metal, int count)
    {
        var fromStore = Math.Min(count, DepositedMaterial(metal));
        if (fromStore > 0)
            Materials[metal] = DepositedMaterial(metal) - fromStore;

        var remaining = count - fromStore;
        while (remaining > 0)
        {
            var stack = Bag.FirstOrDefault(i => i.Category == ItemCategory.Material && i.Metal == metal);
            if (stack is null)
                break;
            ConsumeOne(stack);
            remaining--;
        }
    }

    /// <summary>
    /// Pays for a piece and hands it over. The gear goes straight into the bag, so the caller must have
    /// checked <see cref="BagHasRoom"/> — a piece forged with nowhere to put it would simply be lost.
    /// </summary>
    public Item Forge(ForgeRecipe recipe)
    {
        Gold -= recipe.GoldCost;
        foreach (var (metal, count) in recipe.Cost)
            SpendMaterialForForge(metal, count);

        var item = recipe.Create();
        Bag.Add(item);
        Counters.ItemsForged++;
        if (!CraftedRecipes.Contains(recipe.Id))
            CraftedRecipes.Add(recipe.Id);
        return item;
    }

    /// <summary>Whether every piece in one metal has been forged at least once — the collector's condition.</summary>
    public bool HasCompletedSet(Metal metal) =>
        Domain.Forge.ForSet(metal).All(r => HasCrafted(r.Id));

    /// <summary>
    /// The five slime-forged pieces worn at once. Anything short of all five and a dragon's blow is fatal, no
    /// matter how much HP has been accumulated — which is the whole point of the set.
    /// </summary>
    [JsonIgnore]
    public bool HasFullSlimeArmour => Domain.Forge.SlimeArmourPieces.All(HasSlimePieceEquipped);

    private bool HasSlimePieceEquipped(ForgePiece piece)
    {
        var id = Domain.Forge.SlimeSetPrefix + piece;
        return Equipment.Values.Any(i => i.ForgeId == id);
    }

    /// <summary>In hand, not merely owned — the only weapon that finds anything to bite on a dragon.</summary>
    [JsonIgnore]
    public bool WieldsSlimeSlayer => EquippedWeapons.Any(w => w.ForgeId == Domain.Forge.SlimeSetPrefix + ForgePiece.Sword);

    /// <summary>Likewise for magic: without this in hand, nothing cast at a dragon reaches it.</summary>
    [JsonIgnore]
    public bool WieldsSlimeWand => EquippedWeapons.Any(w => w.ForgeId == Domain.Forge.SlimeSetPrefix + ForgePiece.Wand);

    public static Player CreateNew(string name, Gender gender)
    {
        var p = new Player { Name = name, Gender = gender };
        p.EquippedBag = ItemFactory.Bag();

        var wand = ItemFactory.WoodWand();
        p.Equipment[EquipSlot.RightHand] = wand;
        p.Equipment[EquipSlot.Head] = ItemFactory.LeatherHat();
        p.Equipment[EquipSlot.Body] = ItemFactory.LeatherArmor();
        p.Equipment[EquipSlot.Feet] = ItemFactory.WoodShoes();

        // Registering is itself the first deed, so the card is never blank.
        Titles.Refresh(p);
        return p;
    }

    public bool TryEquip(Item item, EquipSlot slot, out Item? displaced)
    {
        displaced = null;
        if (!item.CanEquipToSlot(slot))
            return false;

        Equipment.TryGetValue(slot, out displaced);
        Equipment[slot] = item;
        return true;
    }

    /// <summary>
    /// Awards EXP and applies any level-ups it triggers. <see cref="Exp"/> is a lifetime running total that is
    /// never spent or reset, so <see cref="ExpToNext"/> is likewise the cumulative total at which the next
    /// level lands. Returns a summary of the whole gain when at least one level was earned (several levels at
    /// once collapse into one summary), or null when nothing changed.
    /// </summary>
    public LevelUpSummary? AddExp(int amount)
    {
        Exp += amount;

        var startLevel = Level;
        var before = Domain.Stats.Clone(Stats);

        while (Exp >= ExpCurve.TotalForLevel(Level + 1))
        {
            Level++;
            Stats.ApplyLevelUpGrowth();
        }
        ExpToNext = ExpCurve.TotalForLevel(Level + 1);

        if (Level == startLevel)
            return null;

        return new LevelUpSummary(startLevel, Level, before, Domain.Stats.Clone(Stats), Exp, ExpToNext);
    }

    public void RecordKill(SlimeColor color, Rank rank)
    {
        var key = $"{color}:{rank}";
        KillCounts[key] = KillCounts.GetValueOrDefault(key) + 1;
        UnclaimedKills[key] = UnclaimedKills.GetValueOrDefault(key) + 1;
    }

    /// <summary>
    /// Slimes felled since the last visit to the bounty desk, keyed exactly as <see cref="KillCounts"/> is.
    /// Kept separate from the lifetime record because this one gets emptied every time the guild pays out.
    /// </summary>
    public Dictionary<string, int> UnclaimedKills { get; set; } = new();

    /// <summary>The pending claim, worst slimes first so the headline of a trip is at the top.</summary>
    [JsonIgnore]
    public List<BountyLine> PendingBounty => UnclaimedKills
        .Select(kv =>
        {
            var parts = kv.Key.Split(':');
            return new BountyLine(Enum.Parse<SlimeColor>(parts[0]), Enum.Parse<Rank>(parts[1]), kv.Value);
        })
        .OrderByDescending(l => l.Total)
        .ThenByDescending(l => l.Rank)
        .ToList();

    [JsonIgnore]
    public int PendingBountyTotal => PendingBounty.Sum(l => l.Total);

    /// <summary>Hands over the tally and takes the fee. Returns what was paid.</summary>
    public int ClaimBounty()
    {
        var total = PendingBountyTotal;
        UnclaimedKills.Clear();
        if (total > 0)
            EarnGold(total);
        return total;
    }

    [JsonIgnore]
    public IEnumerable<KillRecord> KillLog => KillCounts.Select(kv =>
    {
        var parts = kv.Key.Split(':');
        return new KillRecord(Enum.Parse<SlimeColor>(parts[0]), Enum.Parse<Rank>(parts[1]), kv.Value);
    });

    /// <summary>
    /// 10 rank points to advance one rank (a rank-appropriate quest is worth 2; a below-rank quest is worth 0).
    /// Returns a summary when a promotion happened, so the guild can actually announce it — it used to change
    /// <see cref="Rank"/> silently and players sailed straight past their own promotion.
    /// </summary>
    public RankUpSummary? AddRankPoints(int points)
    {
        var from = Rank;
        RankPoints += points;
        while (RankPoints >= 10 && Rank != Rank.SS)
        {
            RankPoints -= 10;
            Rank = RankExtensions.Clamp((int)Rank + 1);
        }
        return Rank != from ? new RankUpSummary(from, Rank) : null;
    }

    /// <summary>How many failures it takes to lose a rank.</summary>
    public const int PenaltiesBeforeDemotion = 3;

    public void ApplyPenalty()
    {
        PenaltyCount++;
        if (PenaltyCount >= PenaltiesBeforeDemotion && Rank != Rank.H)
        {
            PenaltyCount = 0;
            Rank = RankExtensions.Clamp((int)Rank - 1);
        }
    }

    /// <summary>
    /// A contract whose deadline has run out. Costs more than handing it back does: the guild loses face for an
    /// adventurer who simply never came back, where someone who returns and says they cannot finish it only
    /// takes the black mark. So this is the expensive path, and cancelling early is deliberately the cheap one.
    /// </summary>
    public QuestFailure FailExpiredQuest(Quest quest)
    {
        var rankBefore = Rank;

        // The rank points a failure costs: exactly what finishing it would have been worth, so a job that
        // could have promoted you sets you back by the same amount. A below-rank job earns nothing towards
        // promotion, but dropping one is still worth a point of embarrassment.
        var pointsLost = Math.Max(1, quest.RankPointsFor(Rank));
        pointsLost = Math.Min(pointsLost, RankPoints);
        RankPoints -= pointsLost;

        // Half of what the job paid, which scales with the contract rather than needing a table of its own.
        var fine = quest.RewardGold / 2;
        var paid = Math.Min(fine, Gold);
        Gold -= paid;

        ApplyPenalty();
        ActiveQuest = null;

        return new QuestFailure(
            quest.Title,
            quest.Rank,
            pointsLost,
            fine,
            paid,
            PenaltyCount,
            Rank != rankBefore ? Rank : null);
    }
}

/// <summary>
/// What a missed deadline cost, for the notice the guild puts in front of you on your way back in. Every field
/// is something the player would otherwise have to notice for themselves by comparing two screens.
/// </summary>
/// <param name="Fine">What was owed.</param>
/// <param name="FinePaid">What could actually be taken — less than <paramref name="Fine"/> if the purse was short.</param>
/// <param name="PenaltyCount">Black marks now standing, out of <see cref="Player.PenaltiesBeforeDemotion"/>.</param>
/// <param name="DemotedTo">The new rank if this failure cost one, null otherwise.</param>
public sealed record QuestFailure(
    string QuestTitle,
    Rank QuestRank,
    int RankPointsLost,
    int Fine,
    int FinePaid,
    int PenaltyCount,
    Rank? DemotedTo)
{
    public bool CouldNotPayInFull => FinePaid < Fine;
}

/// <summary>A snapshot of everything that changed across one level-up (or a multi-level jump), for the
/// post-battle summary popup.</summary>
public sealed record LevelUpSummary(int FromLevel, int ToLevel, Stats Before, Stats After, int Exp, int ExpToNext);

/// <summary>A guild promotion, for the announcement screen.</summary>
public sealed record RankUpSummary(Rank From, Rank To)
{
    /// <summary>Highest rank of quest and dungeon now open: the guild lets you reach one rank above your own.</summary>
    public Rank Ceiling => RankExtensions.Clamp((int)To + 1);

    public bool IsTop => To == Rank.SS;
}

public static class ExpCurve
{
    /// <summary>
    /// EXP that must be earned *while at* <paramref name="level"/> in order to leave it. Quadratic growth
    /// (the original 20*level^2) meant reaching level 5 on rank H slimes took roughly 600 kills — about a
    /// hundred dungeon trips. This near-linear curve keeps levels arriving at a steady, unhurried pace
    /// instead of stalling out.
    /// </summary>
    public static int Step(int level) => 20 + 14 * (level - 1);

    /// <summary>Lifetime EXP total at which <paramref name="level"/> is reached. Level 1 sits at 0.</summary>
    public static int TotalForLevel(int level)
    {
        var total = 0;
        for (var k = 1; k < level; k++)
            total += Step(k);
        return total;
    }
}
