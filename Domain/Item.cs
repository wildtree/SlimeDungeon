using System.Text.Json.Serialization;

namespace SlimeDungeon.Domain;

public enum EquipSlot { RightHand, LeftHand, Arm, Body, Head, Feet }

public enum ItemCategory { Weapon, Shield, Armor, Helmet, Gauntlet, Shoes, Bag, Herb, Antidote, Potion, Scroll, FullMapReveal }

public enum WeaponKind { Sword, Wand }

public enum PotionKind { Hp, Mp }

/// <summary>
/// A single flat item model covering every item category in the spec (equipment, consumables, scrolls, the bag).
/// Kind-specific data lives in optional fields rather than a class hierarchy, which keeps JSON save/load simple.
/// </summary>
public sealed class Item
{
    public required string Name { get; set; }
    public required ItemCategory Category { get; init; }
    public Rank Rank { get; init; } = Rank.H;

    /// <summary>Shop buy price.</summary>
    public int Value { get; init; }
    public int Quantity { get; set; } = 1;

    /// <summary>Shop sell price — a shop wouldn't buy back at the same price it sells for, so this is a
    /// fraction of <see cref="Value"/> (never less than 1G, so even junk is worth something).</summary>
    [JsonIgnore]
    public int SellValue => Math.Max(1, Value / 2);

    public WeaponKind WeaponKind { get; init; }

    /// <summary>Weapon: STR (Sword) or INT (Wand). Gauntlet: DEX. Shoes: AGL.</summary>
    public int StatBonus { get; init; }

    /// <summary>Shield/Armor/Helmet damage reduction.</summary>
    public int Def { get; init; }

    /// <summary>Bag: number of inventory slots it grants.</summary>
    public int BagCapacity { get; init; }

    /// <summary>Scroll: the spell it teaches.</summary>
    public SpellId SpellTaught { get; init; }

    /// <summary>Potion: which resource it restores.</summary>
    public PotionKind PotionKind { get; init; }

    /// <summary>Equippable categories occupy exactly one of the six equipment slots.
    /// Weapon/Shield may go in either hand (the player picks); everything else has one fixed slot.</summary>
    [JsonIgnore]
    public bool IsEquippable => Category is ItemCategory.Weapon or ItemCategory.Shield or ItemCategory.Armor
        or ItemCategory.Helmet or ItemCategory.Gauntlet or ItemCategory.Shoes;

    public bool CanEquipToSlot(EquipSlot slot) => Category switch
    {
        ItemCategory.Weapon or ItemCategory.Shield => slot is EquipSlot.RightHand or EquipSlot.LeftHand,
        ItemCategory.Armor => slot == EquipSlot.Body,
        ItemCategory.Helmet => slot == EquipSlot.Head,
        ItemCategory.Gauntlet => slot == EquipSlot.Arm,
        ItemCategory.Shoes => slot == EquipSlot.Feet,
        _ => false,
    };

    public Item Clone() => new()
    {
        Name = Name,
        Category = Category,
        Rank = Rank,
        Value = Value,
        Quantity = Quantity,
        WeaponKind = WeaponKind,
        StatBonus = StatBonus,
        Def = Def,
        BagCapacity = BagCapacity,
        SpellTaught = SpellTaught,
        PotionKind = PotionKind,
    };
}
