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

    public static Player CreateNew(string name, Gender gender)
    {
        var p = new Player { Name = name, Gender = gender };
        p.EquippedBag = ItemFactory.Bag();

        var wand = ItemFactory.WoodWand();
        p.Equipment[EquipSlot.RightHand] = wand;
        p.Equipment[EquipSlot.Head] = ItemFactory.LeatherHat();
        p.Equipment[EquipSlot.Body] = ItemFactory.LeatherArmor();
        p.Equipment[EquipSlot.Feet] = ItemFactory.WoodShoes();
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

    public void ApplyPenalty()
    {
        PenaltyCount++;
        if (PenaltyCount >= 3 && Rank != Rank.H)
        {
            PenaltyCount = 0;
            Rank = RankExtensions.Clamp((int)Rank - 1);
        }
    }
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
