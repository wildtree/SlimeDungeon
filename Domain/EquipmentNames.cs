namespace SlimeDungeon.Domain;

/// <summary>
/// Flavor names for rank-scaled equipment. The rank itself is no longer printed on the item (the
/// inventory screen shows the actual stat change instead), so each rank needs its own distinct name
/// to read as a clear step up from the last.
/// </summary>
public static class EquipmentNames
{
    private static readonly Dictionary<Rank, string> Swords = new()
    {
        [Rank.H] = "みじかい剣", [Rank.G] = "銅の剣", [Rank.F] = "鉄の剣", [Rank.E] = "鋼の剣",
        [Rank.D] = "業物の剣", [Rank.C] = "魔法の剣", [Rank.B] = "秘銀の剣",
        [Rank.A] = "ミスリルソード", [Rank.S] = "伝説の剣", [Rank.SS] = "神話の剣",
    };

    private static readonly Dictionary<Rank, string> Wands = new()
    {
        [Rank.H] = "木の杖", [Rank.G] = "銅の杖", [Rank.F] = "鉄の杖", [Rank.E] = "銀の杖",
        [Rank.D] = "賢者の杖", [Rank.C] = "魔導の杖", [Rank.B] = "秘銀の杖",
        [Rank.A] = "大魔導の杖", [Rank.S] = "伝説の杖", [Rank.SS] = "神話の杖",
    };

    private static readonly Dictionary<Rank, string> Shields = new()
    {
        [Rank.H] = "木の盾", [Rank.G] = "銅の盾", [Rank.F] = "鉄の盾", [Rank.E] = "鋼の盾",
        [Rank.D] = "大盾", [Rank.C] = "魔法の盾", [Rank.B] = "秘銀の盾",
        [Rank.A] = "ミスリルシールド", [Rank.S] = "伝説の盾", [Rank.SS] = "神話の盾",
    };

    private static readonly Dictionary<Rank, string> Armors = new()
    {
        [Rank.H] = "布の鎧", [Rank.G] = "革の鎧", [Rank.F] = "鎖帷子", [Rank.E] = "鋼の鎧",
        [Rank.D] = "重鎧", [Rank.C] = "魔法の鎧", [Rank.B] = "秘銀の鎧",
        [Rank.A] = "ミスリルアーマー", [Rank.S] = "伝説の鎧", [Rank.SS] = "神話の鎧",
    };

    private static readonly Dictionary<Rank, string> Helmets = new()
    {
        [Rank.H] = "布の帽子", [Rank.G] = "革の兜", [Rank.F] = "鉄兜", [Rank.E] = "鋼の兜",
        [Rank.D] = "重兜", [Rank.C] = "魔法の兜", [Rank.B] = "秘銀の兜",
        [Rank.A] = "ミスリルヘルム", [Rank.S] = "伝説の兜", [Rank.SS] = "神話の兜",
    };

    private static readonly Dictionary<Rank, string> Gauntlets = new()
    {
        [Rank.H] = "布の手甲", [Rank.G] = "革の籠手", [Rank.F] = "鉄の籠手", [Rank.E] = "鋼の籠手",
        [Rank.D] = "重籠手", [Rank.C] = "魔法の籠手", [Rank.B] = "秘銀の籠手",
        [Rank.A] = "ミスリルガントレット", [Rank.S] = "伝説の籠手", [Rank.SS] = "神話の籠手",
    };

    private static readonly Dictionary<Rank, string> ShoesNames = new()
    {
        [Rank.H] = "木の靴", [Rank.G] = "革靴", [Rank.F] = "鉄の靴", [Rank.E] = "鋼の靴",
        [Rank.D] = "韋駄天の靴", [Rank.C] = "魔法の靴", [Rank.B] = "秘銀の靴",
        [Rank.A] = "ミスリルシューズ", [Rank.S] = "伝説の靴", [Rank.SS] = "神話の靴",
    };

    /// <summary>The H entry matches the literal name of the starting bag, so a found H-rank bag and the one
    /// every adventurer registers with are not two names for the same thing.</summary>
    private static readonly Dictionary<Rank, string> Bags = new()
    {
        [Rank.H] = "袋", [Rank.G] = "革の袋", [Rank.F] = "旅人の背嚢", [Rank.E] = "大きな背嚢",
        [Rank.D] = "行商人のカバン", [Rank.C] = "魔法の袋", [Rank.B] = "秘銀の袋",
        [Rank.A] = "ミスリルバッグ", [Rank.S] = "伝説の袋", [Rank.SS] = "神話の袋",
    };

    public static string Sword(Rank rank) => Swords[rank];
    public static string Wand(Rank rank) => Wands[rank];
    public static string Shield(Rank rank) => Shields[rank];
    public static string Armor(Rank rank) => Armors[rank];
    public static string Helmet(Rank rank) => Helmets[rank];
    public static string Gauntlet(Rank rank) => Gauntlets[rank];
    public static string Shoes(Rank rank) => ShoesNames[rank];
    public static string Bag(Rank rank) => Bags[rank];

    /// <summary>Saves made before this flavor-name table existed still carry the old literal
    /// "◯◯(H)"-style names (Name is stored verbatim per item). Detect that exact old pattern and
    /// re-flavor it on load — anything else (hand-named starting gear, already-flavored names) is left
    /// untouched since it can't collide with the old auto-generated suffix.</summary>
    public static void MigrateStaleName(Item item)
    {
        // Bags kept their own dry auto-name ("Gランクの鞄") because they were missed when the rest of the gear
        // was given flavor names, so they need their own pattern check.
        if (item.Category == ItemCategory.Bag)
        {
            if (item.Name == $"{item.Rank.Label()}ランクの鞄")
                item.Name = Bag(item.Rank);
            return;
        }

        if (!item.IsEquippable || !item.Name.EndsWith($"({item.Rank.Label()})"))
            return;

        item.Name = item.Category switch
        {
            ItemCategory.Weapon when item.WeaponKind == WeaponKind.Sword => Sword(item.Rank),
            ItemCategory.Weapon => Wand(item.Rank),
            ItemCategory.Shield => Shield(item.Rank),
            ItemCategory.Armor => Armor(item.Rank),
            ItemCategory.Helmet => Helmet(item.Rank),
            ItemCategory.Gauntlet => Gauntlet(item.Rank),
            ItemCategory.Shoes => Shoes(item.Rank),
            _ => item.Name,
        };
    }
}
