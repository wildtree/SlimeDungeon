namespace SlimeDungeon.Domain;

/// <summary>
/// Flavor names for rank-scaled equipment — everything the shop stocks and everything a chest coughs up. The
/// rank itself is no longer printed on the item (the inventory screen shows the actual stat change instead),
/// so each rank needs its own distinct name to read as a clear step up from the last.
///
/// Nothing here is made of metal, and that is the point. Metal weapons and armour come off the guild's anvil
/// and nowhere else, so if the shop also sold steel and mithril the forge would be an expensive way of buying
/// what was already on the shelf. What the merchant sells is wood and stone, worked better and better as the
/// ranks climb; what the smith makes is bronze through orichalcum. See <see cref="Forge"/>.
///
/// The materials run: 木 → 石 → 樫 → 翡翠 → 黒檀 → 黒曜石 → 古木 → 水晶 → 神木 → 星石, wood and stone
/// alternating so each rank reads as a different substance rather than as the same one with an adjective.
/// </summary>
public static class EquipmentNames
{
    /// <summary>Shop blades are one-handed and get longer, then heavier, as the ranks climb: 短剣, 剣, 長剣,
    /// 斧, 短槍. No two-handers and nothing thrown.</summary>
    private static readonly Dictionary<Rank, string> Swords = new()
    {
        [Rank.H] = "木の短剣", [Rank.G] = "石の短剣", [Rank.F] = "樫の剣", [Rank.E] = "翡翠の剣",
        [Rank.D] = "黒檀の長剣", [Rank.C] = "黒曜石の長剣", [Rank.B] = "古木の斧",
        [Rank.A] = "水晶の斧", [Rank.S] = "神木の短槍", [Rank.SS] = "星石の短槍",
    };

    /// <summary>The H entry is the literal name of the wand every adventurer registers with.</summary>
    private static readonly Dictionary<Rank, string> Wands = new()
    {
        [Rank.H] = "木の杖", [Rank.G] = "石の杖", [Rank.F] = "樫の杖", [Rank.E] = "翡翠の杖",
        [Rank.D] = "黒檀の杖", [Rank.C] = "黒曜石の杖", [Rank.B] = "古木の杖",
        [Rank.A] = "水晶の杖", [Rank.S] = "神木の杖", [Rank.SS] = "星石の杖",
    };

    private static readonly Dictionary<Rank, string> Shields = new()
    {
        [Rank.H] = "木の盾", [Rank.G] = "革の盾", [Rank.F] = "樫の盾", [Rank.E] = "石の盾",
        [Rank.D] = "黒檀の盾", [Rank.C] = "黒曜石の盾", [Rank.B] = "古木の盾",
        [Rank.A] = "水晶の盾", [Rank.S] = "神木の盾", [Rank.SS] = "星石の盾",
    };

    private static readonly Dictionary<Rank, string> Armors = new()
    {
        [Rank.H] = "布の鎧", [Rank.G] = "革の鎧", [Rank.F] = "樫の鎧", [Rank.E] = "厚革の鎧",
        [Rank.D] = "黒檀の鎧", [Rank.C] = "黒曜石の鎧", [Rank.B] = "古木の鎧",
        [Rank.A] = "水晶の鎧", [Rank.S] = "神木の鎧", [Rank.SS] = "星石の鎧",
    };

    private static readonly Dictionary<Rank, string> Helmets = new()
    {
        [Rank.H] = "布の帽子", [Rank.G] = "革の兜", [Rank.F] = "樫の兜", [Rank.E] = "厚革の兜",
        [Rank.D] = "黒檀の兜", [Rank.C] = "黒曜石の兜", [Rank.B] = "古木の兜",
        [Rank.A] = "水晶の兜", [Rank.S] = "神木の兜", [Rank.SS] = "星石の兜",
    };

    private static readonly Dictionary<Rank, string> Gauntlets = new()
    {
        [Rank.H] = "布の手甲", [Rank.G] = "革の籠手", [Rank.F] = "樫の籠手", [Rank.E] = "厚革の籠手",
        [Rank.D] = "黒檀の籠手", [Rank.C] = "黒曜石の籠手", [Rank.B] = "古木の籠手",
        [Rank.A] = "水晶の籠手", [Rank.S] = "神木の籠手", [Rank.SS] = "星石の籠手",
    };

    private static readonly Dictionary<Rank, string> ShoesNames = new()
    {
        [Rank.H] = "木の靴", [Rank.G] = "革靴", [Rank.F] = "樫の靴", [Rank.E] = "厚革の靴",
        [Rank.D] = "黒檀の靴", [Rank.C] = "黒曜石の靴", [Rank.B] = "古木の靴",
        [Rank.A] = "水晶の靴", [Rank.S] = "神木の靴", [Rank.SS] = "星石の靴",
    };

    /// <summary>The H entry matches the literal name of the starting bag, so a found H-rank bag and the one
    /// every adventurer registers with are not two names for the same thing.</summary>
    private static readonly Dictionary<Rank, string> Bags = new()
    {
        [Rank.H] = "袋", [Rank.G] = "革の袋", [Rank.F] = "旅人の背嚢", [Rank.E] = "大きな背嚢",
        [Rank.D] = "行商人のカバン", [Rank.C] = "魔法の袋", [Rank.B] = "絹の袋",
        [Rank.A] = "竜革の鞄", [Rank.S] = "伝説の袋", [Rank.SS] = "神話の袋",
    };

    private static readonly Dictionary<Rank, string> Herbs = new()
    {
        [Rank.H] = "雑草", [Rank.G] = "野草", [Rank.F] = "青葉草", [Rank.E] = "薬草",
        [Rank.D] = "癒し草", [Rank.C] = "翠癒草", [Rank.B] = "金露草",
        [Rank.A] = "月光花", [Rank.S] = "星涙草", [Rank.SS] = "神樹の霊葉",
    };

    private static readonly Dictionary<Rank, string> Antidotes = new()
    {
        [Rank.H] = "苦草", [Rank.G] = "野苦草", [Rank.F] = "山ミント", [Rank.E] = "苦葉草",
        [Rank.D] = "毒消し草", [Rank.C] = "解毒草", [Rank.B] = "解呪草",
        [Rank.A] = "白聖草", [Rank.S] = "万毒蓮", [Rank.SS] = "神蛇花",
    };

    private static readonly Dictionary<Rank, string> HPPotions = new()
    {
        [Rank.H] = "傷薬", [Rank.G] = "薬湯", [Rank.F] = "治癒薬",
        [Rank.E] = "ポーション", [Rank.D] = "ミドルポーション", [Rank.C] = "ハイポーション",
        [Rank.B] = "王者の秘薬", [Rank.A] = "フェニックスの涙", [Rank.S] = "エリクサー",
        [Rank.SS] = "神命霊薬",
    };

    private static readonly Dictionary<Rank, string> MPPotions = new()
    {
        [Rank.H] = "ハーブティー", [Rank.G] = "薬茶", [Rank.F] = "魔力薬",
        [Rank.E] = "エーテルドリンク", [Rank.D] = "マナポーション", [Rank.C] = "エーテル",
        [Rank.B] = "ハイエーテル", [Rank.A] = "精霊蜜", [Rank.S] = "エーテル・オリジン",
        [Rank.SS] = "神霊甘露",
    };

    public static string Sword(Rank rank) => Swords[rank];
    public static string Wand(Rank rank) => Wands[rank];
    public static string Shield(Rank rank) => Shields[rank];
    public static string Armor(Rank rank) => Armors[rank];
    public static string Helmet(Rank rank) => Helmets[rank];
    public static string Gauntlet(Rank rank) => Gauntlets[rank];
    public static string Shoes(Rank rank) => ShoesNames[rank];
    public static string Bag(Rank rank) => Bags[rank];
    public static string Herb(Rank rank) => Herbs[rank];
    public static string Antidote(Rank rank) => Antidotes[rank];
    public static string HPPotion(Rank rank) => HPPotions[rank];
    public static string MPPotion(Rank rank) => MPPotions[rank];

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
