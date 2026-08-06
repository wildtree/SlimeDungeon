using SlimeDungeon.Core;

namespace SlimeDungeon.Domain;

/// <summary>Procedural item generation: starting gear plus rank-scaled loot.</summary>
public static class ItemFactory
{
    public static Item WoodWand() => new() { Name = "木の杖", Category = ItemCategory.Weapon, WeaponKind = WeaponKind.Wand, StatBonus = 2, Value = 20 };
    // Starting armour. DEF is subtracted flat, so at the bottom of the ladder a single point of it is the
    // difference between a slime doing a third of your health and half of it — these numbers are load-bearing
    // in a way the higher-rank gear is not.
    public static Item LeatherHat() => new() { Name = "革の帽子", Category = ItemCategory.Helmet, Def = 2, Value = 15 };
    public static Item LeatherArmor() => new() { Name = "革の鎧", Category = ItemCategory.Armor, Def = 3, Value = 25 };
    public static Item WoodShoes() => new() { Name = "木の靴", Category = ItemCategory.Shoes, StatBonus = 1, Value = 15 };
    public static Item Bag() => new() { Name = "袋", Category = ItemCategory.Bag, BagCapacity = 3, Value = 10 };

    /// <summary>
    /// Equipment power per rank. This has to follow the same 1.6x-per-rank curve the monsters use: on a
    /// linear curve, armour peaked around 7 DEF while a rank-A monster hit for 107, so high-rank play was
    /// unsurvivable no matter how good your gear was. The 0.8 base is tuned so a full set of rank-appropriate
    /// armour absorbs roughly half of an incoming same-rank hit at every point on the ladder.
    /// </summary>
    private static int RankScaledBonus(Rank rank)
    {
        var scaled = 0.8 * Math.Pow(1.6, (int)rank - 1);
        return Math.Max(1, (int)Math.Round(RandomUtil.Shared.NextGaussian(scaled, scaled * 0.15)));
    }

    public static Item CreateWeapon(Rank rank, WeaponKind kind)
    {
        var name = kind == WeaponKind.Sword ? EquipmentNames.Sword(rank) : EquipmentNames.Wand(rank);
        return new Item { Name = name, Category = ItemCategory.Weapon, WeaponKind = kind, Rank = rank, StatBonus = RankScaledBonus(rank), Value = (int)rank * 15 };
    }

    public static Item CreateShield(Rank rank) =>
        new() { Name = EquipmentNames.Shield(rank), Category = ItemCategory.Shield, Rank = rank, Def = RankScaledBonus(rank), Value = (int)rank * 15 };

    public static Item CreateArmor(Rank rank) =>
        new() { Name = EquipmentNames.Armor(rank), Category = ItemCategory.Armor, Rank = rank, Def = RankScaledBonus(rank), Value = (int)rank * 18 };

    public static Item CreateHelmet(Rank rank) =>
        new() { Name = EquipmentNames.Helmet(rank), Category = ItemCategory.Helmet, Rank = rank, Def = RankScaledBonus(rank), Value = (int)rank * 12 };

    public static Item CreateGauntlet(Rank rank) =>
        new() { Name = EquipmentNames.Gauntlet(rank), Category = ItemCategory.Gauntlet, Rank = rank, StatBonus = RankScaledBonus(rank), Value = (int)rank * 12 };

    public static Item CreateShoes(Rank rank) =>
        new() { Name = EquipmentNames.Shoes(rank), Category = ItemCategory.Shoes, Rank = rank, StatBonus = RankScaledBonus(rank), Value = (int)rank * 12 };

    /// <summary>Bags only drop in dungeons (never sold) — capacity scales gently with rank so a lucky
    /// early find doesn't trivialize the bag-space economy.</summary>
    public static Item CreateBag(Rank rank) =>
        new() { Name = EquipmentNames.Bag(rank), Category = ItemCategory.Bag, Rank = rank, BagCapacity = 2 + (int)rank, Value = (int)rank * 8 };

    public static Item CreateHerb(Rank rank) =>
        new() { 
            Name = EquipmentNames.Herb(rank), Category = ItemCategory.Herb, Rank = rank, Value = (int)rank * 5 };

    public static Item CreateAntidoteHerb(Rank rank) =>
        new() { Name = EquipmentNames.Antidote(rank), Category = ItemCategory.Antidote, Rank = rank, Value = (int)rank * 5 };

    public static Item CreatePotion(Rank rank, PotionKind kind) =>
        new()
        {
            Name = (kind == PotionKind.Hp ? EquipmentNames.HPPotion(rank) : EquipmentNames.MPPotion(rank)),
            Category = ItemCategory.Potion,
            Rank = rank,
            PotionKind = kind,
            Value = (int)rank * (int)rank * 10,
        };

    /// <summary>
    /// A firecracker: readied in an item slot and thrown at the whole pack. It hits at its own rank on the
    /// same ladder everything else uses, so a rank-R firecracker clears a group of rank-R slimes outright —
    /// paid for by being consumed, by costing gold, and by occupying one of only two item slots. It carries
    /// Fire, so the elemental matchup moves it up or down a rank just as a spell would.
    /// </summary>
    public static Item CreateFirecracker(Rank rank) =>
        new()
        {
            Name = $"爆竹({rank.Label()})",
            Category = ItemCategory.Firecracker,
            Rank = rank,
            Value = (int)rank * (int)rank * 6,
        };

    /// <summary>
    /// Caltrops: weaker than a firecracker by a full rank and unable to finish anything of their own rank, but
    /// elementless (so never at a disadvantage) and they halve what survives them — the control option next to
    /// the firecracker's burst.
    /// </summary>
    public static Item CreateCaltrops(Rank rank) =>
        new()
        {
            Name = $"まきびし({rank.Label()})",
            Category = ItemCategory.Caltrops,
            Rank = rank,
            Value = (int)rank * (int)rank * 4,
        };

    /// <summary>
    /// Ore. It rides in the bag like everything else — it used to live in a weightless pouch of its own, which
    /// meant a good haul cost nothing to carry and there was never a decision to make about it.
    /// </summary>
    public static Item CreateMaterial(Metal metal)
    {
        var def = Metals.Get(metal);
        return new Item
        {
            Name = def.OreName,
            Category = ItemCategory.Material,
            Rank = def.GearRank,
            Metal = metal,
            // Worth roughly what a rank-appropriate herb is, so selling spare ore is a modest income rather
            // than a reason to skip the forge entirely.
            Value = (int)def.GearRank * 12,
        };
    }

    /// <summary>
    /// A stone cut out of a gem slime. It does nothing in a fight and nothing on the body: it is worth money,
    /// and it is what the commissions from the nobility ask for.
    /// </summary>
    public static Item CreateGem(Gem gem)
    {
        var def = Gems.Get(gem);
        return new Item
        {
            Name = def.Name,
            Category = ItemCategory.Gemstone,
            Rank = def.Rank,
            Gem = gem,
            Value = Gems.Value(gem),
        };
    }

    public static Item CreateScroll(Rank rank, SpellId spell) =>
        new() { Name = $"スクロール({SpellDefinitions.NameOf(spell)}・{rank.Label()})", Category = ItemCategory.Scroll, Rank = rank, SpellTaught = spell, Value = (int)rank * 20 };

    public static Item CreateFullMapReveal() =>
        new() { Name = "地図の巻物", Category = ItemCategory.FullMapReveal, Value = 30 };

    public static int RollGold(Rank dungeonRank)
    {
        var max = (int)dungeonRank * 20;
        return RandomUtil.Shared.Next(1, max + 1);
    }
}

public static class ConsumableEffects
{
    /// <summary>H..SS -> 5%..50% of max HP, in 5-point steps.</summary>
    public static double HerbHealFraction(Rank rank) => (int)rank * 0.05;

    /// <summary>Potions restore at double the herb's percentage.</summary>
    public static double PotionRestoreFraction(Rank rank) => (int)rank * 0.10;

    /// <summary>
    /// Percentage-only healing is worthless early on: a starting character has around 10 max HP, so a Rank H
    /// herb's 5% floors to 0 and the item literally does nothing. Every heal therefore also carries a flat
    /// rank-scaled minimum. The flat term dominates at low levels and the percentage takes over once max HP
    /// has grown, so high-rank consumables stay strong without early ones being dead weight.
    /// </summary>
    private static int Amount(Rank rank, int maxValue, double fraction, int flatPerRank) =>
        Math.Max(flatPerRank * (int)rank, (int)Math.Floor(maxValue * fraction));

    /// <summary>HP a herb of this rank restores for a character with <paramref name="maxHp"/> max HP.</summary>
    public static int HerbHealAmount(Rank rank, int maxHp) =>
        Amount(rank, maxHp, HerbHealFraction(rank), 5);

    /// <summary>HP or MP a potion of this rank restores — twice a herb, flat minimum included.</summary>
    public static int PotionRestoreAmount(Rank rank, int maxValue) =>
        Amount(rank, maxValue, PotionRestoreFraction(rank), 10);

    /// <summary>H -> 50% success, SS -> 100% success, linear in between.</summary>
    public static double AntidoteSuccessRate(Rank rank) => 0.5 + ((int)rank - 1) / 9.0 * 0.5;
}
