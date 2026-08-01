using System.Text.Json.Serialization;

namespace SlimeDungeon.Domain;

/// <summary>
/// Saves carry these as the dictionary keys of <see cref="Player.Equipment"/>, which System.Text.Json writes
/// by name — so entries may be added freely, but an existing name must never be changed or a save would load
/// with that piece of gear silently missing.
/// </summary>
public enum EquipSlot { RightHand, LeftHand, Arm, Body, Head, Feet, Item1, Item2 }

/// <summary>
/// Serialised as integers in existing saves, so entries may only ever be appended — inserting one would turn
/// every saved herb into a helmet.
/// </summary>
public enum ItemCategory
{
    Weapon, Shield, Armor, Helmet, Gauntlet, Shoes, Bag, Herb, Antidote, Potion, Scroll, FullMapReveal,

    /// <summary>Thrown, bursts, burns the whole pack.</summary>
    Firecracker,

    /// <summary>Scattered underfoot: hurts everything standing on it and slows what survives.</summary>
    Caltrops,
}

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

    /// <summary>Gear that occupies one of the six body slots.
    /// Weapon/Shield may go in either hand (the player picks); everything else has one fixed slot.</summary>
    [JsonIgnore]
    public bool IsEquippable => Category is ItemCategory.Weapon or ItemCategory.Shield or ItemCategory.Armor
        or ItemCategory.Helmet or ItemCategory.Gauntlet or ItemCategory.Shoes;

    /// <summary>
    /// Consumables that can be readied in one of the two item slots. Only readied items can be reached in the
    /// middle of a fight — rummaging through a pack while a slime is on you is not something the spec allows —
    /// and in exchange a readied item is off the bag's books, freeing a slot for loot.
    /// </summary>
    [JsonIgnore]
    public bool IsPocketable => Category is ItemCategory.Herb or ItemCategory.Potion or ItemCategory.Antidote
        or ItemCategory.Firecracker or ItemCategory.Caltrops;

    /// <summary>Thrown at the enemy rather than used on yourself — the offensive half of the item slots.</summary>
    [JsonIgnore]
    public bool IsThrowable => Category is ItemCategory.Firecracker or ItemCategory.Caltrops;

    /// <summary>
    /// True for anything the player can wear or carry in a slot of its own — body gear, a readied consumable,
    /// or the bag itself. The bag has no <see cref="EquipSlot"/> (it lives in <see cref="Player.EquippedBag"/>),
    /// but it is still equipment as far as the inventory screen is concerned, and leaving it out of this meant a
    /// bag picked up in a dungeon could only ever be thrown away.
    /// </summary>
    [JsonIgnore]
    public bool HasEquipSlot => IsEquippable || IsPocketable || Category == ItemCategory.Bag;

    public bool CanEquipToSlot(EquipSlot slot) => Category switch
    {
        ItemCategory.Weapon or ItemCategory.Shield => slot is EquipSlot.RightHand or EquipSlot.LeftHand,
        ItemCategory.Armor => slot == EquipSlot.Body,
        ItemCategory.Helmet => slot == EquipSlot.Head,
        ItemCategory.Gauntlet => slot == EquipSlot.Arm,
        ItemCategory.Shoes => slot == EquipSlot.Feet,
        ItemCategory.Herb or ItemCategory.Potion or ItemCategory.Antidote
            or ItemCategory.Firecracker or ItemCategory.Caltrops =>
            slot is EquipSlot.Item1 or EquipSlot.Item2,
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
