using System.Text.Json.Serialization;
using SlimeDungeon.Core;

namespace SlimeDungeon.Domain;

/// <summary>
/// Stable identifiers for titles. Written to the save as names, not numbers, so the list can be reordered or
/// added to freely without a saved id starting to mean a different title. (Only this enum opts into string
/// form — the others are already in existing saves as integers and must stay that way.) Entries may be added
/// but must not be renamed or removed, or an old save would silently drop a title the player had earned.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<TitleId>))]
public enum TitleId
{
    Novice,

    SlimeKiller, SlimeHunter, SlimeMaster, SlimeBane,
    SeasonedSlayer, SlimeSlayer,

    Naturalist, Zoologist,

    HerbEnthusiast, HerbScholar, HerbGuardian,
    AntidoteAdept, AntidoteMaster,

    DungeonRegular, DungeonHabitue, DungeonDweller,

    Journeyman, Renowned, GuildLegend,

    Toughened, Veteran,

    Thrifty, MerchantsFriend,

    ApprenticeMage, Archmage,

    ChestRummager, TreasureHunter,

    Compounder, Alchemist,

    LongLived, YearSurvivor,

    NimbleFooted,

    GuildWorker, GuildBenefactor,

    BronzeCollector, IronCollector, CopperCollector, SilverCollector,
    MithrilCollector, AdamantiteCollector, OrichalcumCollector,

    DragonSlimeSlayer,

    OreGatherer, OreHoarder, ApprenticeSmith, MasterSmith,
    GemSeeker, GemCollector, Gemologist, JewellersFriend,

    WoodMania, ClothMania, StoneMania, LeatherMania, OakMania, JadeMania, ThickLeatherMania,
    EbonyMania, ObsidianMania, AncientWoodMania, CrystalMania, DivineWoodMania, StarstoneMania,

    Daring, Valiant, Fearless,
    Careful, Prudent, Circumspect,
}

/// <summary>Broad grouping, used to sort the title list into something readable.</summary>
public enum TitleCategory { Beginning, Slaying, Bestiary, Quests, Delving, Guild, Growth, Craft, Living, Forging, Outfitting }

/// <summary>
/// A title and the deed that earns it. <see cref="Requirement"/> is shown to the player for titles they have not
/// earned yet, so the list doubles as a list of things worth attempting.
/// </summary>
public sealed record TitleDefinition(
    TitleId Id,
    string Name,
    TitleCategory Category,
    string Requirement,
    Func<Player, bool> IsEarned);

public static class Titles
{
    /// <summary>Granted the moment a character is registered, so the card is never blank.</summary>
    public const TitleId Starting = TitleId.Novice;

    /// <summary>
    /// The collector title for each ore. Named for the slime rather than the metal — the guild's regulars call
    /// someone who has forged the lot a "ブロンズコレクター", not a "青銅コレクター".
    /// </summary>
    private static readonly Dictionary<Metal, TitleId> CollectorTitles = new()
    {
        [Metal.Bronze] = TitleId.BronzeCollector,
        [Metal.Iron] = TitleId.IronCollector,
        [Metal.Copper] = TitleId.CopperCollector,
        [Metal.Silver] = TitleId.SilverCollector,
        [Metal.Mithril] = TitleId.MithrilCollector,
        [Metal.Adamantite] = TitleId.AdamantiteCollector,
        [Metal.Orichalcum] = TitleId.OrichalcumCollector,
    };

    public static TitleId CollectorTitleFor(Metal metal) => CollectorTitles[metal];

    /// <summary>
    /// One per ore, built from the metal table so a new ore brings its title with it automatically. The
    /// condition is having *forged* every piece, not owning or wearing them: a sword and a wand can never be
    /// held at once, so requiring a complete set on the body would make these unearnable by construction.
    /// </summary>
    private static IEnumerable<TitleDefinition> CollectorDefinitions() =>
        Metals.All.Select(m => new TitleDefinition(
            CollectorTitles[m.Metal],
            $"{m.Name}コレクター",
            TitleCategory.Forging,
            $"{m.Name}の武器・防具をすべて作成する",
            p => p.HasCompletedSet(m.Metal)));

    private static readonly TitleDefinition[] Fixed =
    [
        new(TitleId.Novice, "駆け出し冒険者", TitleCategory.Beginning,
            "ギルドに登録する", _ => true),

        // Slaying — sheer numbers.
        new(TitleId.SlimeKiller, "スライム退治", TitleCategory.Slaying,
            "スライムを10体倒す", p => p.TotalSlimesDefeated >= 10),
        new(TitleId.SlimeHunter, "スライムハンター", TitleCategory.Slaying,
            "スライムを50体倒す", p => p.TotalSlimesDefeated >= 50),
        new(TitleId.SlimeMaster, "スライム狩りの達人", TitleCategory.Slaying,
            "スライムを200体倒す", p => p.TotalSlimesDefeated >= 200),
        new(TitleId.SlimeBane, "スライムの天敵", TitleCategory.Slaying,
            "スライムを1000体倒す", p => p.TotalSlimesDefeated >= 1000),

        // Slaying — quality of prey.
        new(TitleId.SeasonedSlayer, "熟練の討伐者", TitleCategory.Slaying,
            "Aランクのスライムを倒す", p => p.BestSlimeRankDefeated >= Rank.A),
        new(TitleId.SlimeSlayer, "スライムスレイヤー", TitleCategory.Slaying,
            "SSランクのスライムを倒す", p => p.BestSlimeRankDefeated >= Rank.SS),

        // Bestiary.
        new(TitleId.Naturalist, "見習い博物学者", TitleCategory.Bestiary,
            "4種類のスライムを倒す", p => p.DistinctSlimeColorsDefeated >= 4),
        // Was "全8種類" when eight was all there were. The metal species pushed the count to sixteen, so the
        // wording drops the "全" rather than the threshold — nobody should lose a title they already earned.
        new(TitleId.Zoologist, "スライム博物学者", TitleCategory.Bestiary,
            "8種類のスライムを倒す", p => p.DistinctSlimeColorsDefeated >= 8),

        // Gathering work.
        new(TitleId.HerbEnthusiast, "薬草マニア", TitleCategory.Quests,
            "薬草採取の依頼を5件こなす", p => p.Counters.HerbQuestsCompleted >= 5),
        new(TitleId.HerbScholar, "薬草博士", TitleCategory.Quests,
            "薬草採取の依頼を15件こなす", p => p.Counters.HerbQuestsCompleted >= 15),
        new(TitleId.HerbGuardian, "薬草の守護者", TitleCategory.Quests,
            "薬草採取の依頼を40件こなす", p => p.Counters.HerbQuestsCompleted >= 40),
        new(TitleId.AntidoteAdept, "毒消しの心得", TitleCategory.Quests,
            "毒消し草採取の依頼を5件こなす", p => p.Counters.AntidoteQuestsCompleted >= 5),
        new(TitleId.AntidoteMaster, "解毒の名手", TitleCategory.Quests,
            "毒消し草採取の依頼を20件こなす", p => p.Counters.AntidoteQuestsCompleted >= 20),

        // Delving. Raised from 10/50/150, which was set before it was clear how much of this game is simply
        // going back down the stairs. Delving is the only activity there is, and a hundred and fifty trips is
        // something a player passes without noticing — so these now sit against the same horizon as the clear
        // counts below rather than being over inside the first fortnight. Titles are never revoked, so nobody
        // loses one they already hold; only the pace of earning them changes.
        new(TitleId.DungeonRegular, "ダンジョン慣れ", TitleCategory.Delving,
            "ダンジョンに50回もぐる", p => p.Counters.DungeonVisits >= 50),
        new(TitleId.DungeonHabitue, "ダンジョンの常連", TitleCategory.Delving,
            "ダンジョンに250回もぐる", p => p.Counters.DungeonVisits >= 250),
        new(TitleId.DungeonDweller, "ダンジョンの住人", TitleCategory.Delving,
            "ダンジョンに750回もぐる", p => p.Counters.DungeonVisits >= 750),

        // Standing in the guild.
        new(TitleId.Journeyman, "一人前の冒険者", TitleCategory.Guild,
            "Dランクに昇格する", p => p.Rank >= Rank.D),
        new(TitleId.Renowned, "高名な冒険者", TitleCategory.Guild,
            "Bランクに昇格する", p => p.Rank >= Rank.B),
        new(TitleId.GuildLegend, "ギルドの伝説", TitleCategory.Guild,
            "SSランクに到達する", p => p.Rank >= Rank.SS),
        new(TitleId.GuildWorker, "ギルドの働き手", TitleCategory.Guild,
            "依頼を25件達成する", p => p.Counters.QuestsCompleted >= 25),
        new(TitleId.GuildBenefactor, "ギルドの功労者", TitleCategory.Guild,
            "依頼を100件達成する", p => p.Counters.QuestsCompleted >= 100),

        // Personal growth.
        new(TitleId.Toughened, "鍛えられた体", TitleCategory.Growth,
            "レベル10に到達する", p => p.Level >= 10),
        new(TitleId.Veteran, "歴戦の勇士", TitleCategory.Growth,
            "レベル30に到達する", p => p.Level >= 30),
        new(TitleId.ApprenticeMage, "魔法使いの卵", TitleCategory.Growth,
            "まほうを50回となえる", p => p.Counters.SpellsCast >= 50),
        new(TitleId.Archmage, "大魔導士", TitleCategory.Growth,
            "まほうを200回となえる", p => p.Counters.SpellsCast >= 200),

        // Trade and craft.
        new(TitleId.Thrifty, "小金持ち", TitleCategory.Craft,
            "通算1000Gを稼ぐ", p => p.Counters.GoldEarned >= 1000),
        new(TitleId.MerchantsFriend, "大商人の友", TitleCategory.Craft,
            "通算10000Gを稼ぐ", p => p.Counters.GoldEarned >= 10000),
        new(TitleId.Compounder, "調合師", TitleCategory.Craft,
            "ポーションを20個つくる", p => p.Counters.PotionsCrafted >= 20),
        new(TitleId.Alchemist, "錬金術師", TitleCategory.Craft,
            "ポーションを60個つくる", p => p.Counters.PotionsCrafted >= 60),
        new(TitleId.ChestRummager, "宝箱あさり", TitleCategory.Craft,
            "宝箱を50個あける", p => p.Counters.ChestsOpened >= 50),
        new(TitleId.TreasureHunter, "トレジャーハンター", TitleCategory.Craft,
            "宝箱を200個あける", p => p.Counters.ChestsOpened >= 200),

        // Simply staying alive.
        new(TitleId.LongLived, "長生きの冒険者", TitleCategory.Living,
            "登録から100日生き延びる", p => p.DaysSurvived >= 100),
        new(TitleId.YearSurvivor, "一年を越えた者", TitleCategory.Living,
            "登録から1年(360日)生き延びる", p => p.DaysSurvived >= 360),
        new(TitleId.NimbleFooted, "逃げ足の達人", TitleCategory.Living,
            "戦闘から30回逃げ切る", p => p.Counters.TimesFled >= 30),

        // The one fight in the game that has to be prepared for rather than simply survived.
        new(TitleId.DragonSlimeSlayer, "ドラゴンスライムスレイヤー", TitleCategory.Slaying,
            "ドラゴンスライムを討伐する", p => p.HasDefeated(SlimeColor.Dragon)),

        // Ore and the anvil. Counted as the ore drops and as pieces come off the anvil, not by what is still
        // owned — the whole point of both is that they get spent.
        new(TitleId.OreGatherer, "鉱石ひろい", TitleCategory.Forging,
            "金属の素材を10個集める", p => p.Counters.MaterialsGathered >= 10),
        new(TitleId.OreHoarder, "鉱脈の主", TitleCategory.Forging,
            "金属の素材を60個集める", p => p.Counters.MaterialsGathered >= 60),
        new(TitleId.ApprenticeSmith, "見習い鍛冶", TitleCategory.Forging,
            "鍛冶で武具を1つ作る", p => p.Counters.ItemsForged >= 1),
        new(TitleId.MasterSmith, "名工", TitleCategory.Forging,
            "鍛冶で武具を20個作る", p => p.Counters.ItemsForged >= 20),

        // Gems. Seen rather than held, since a stone handed to the client who ordered it is gone.
        new(TitleId.GemSeeker, "宝石さがし", TitleCategory.Forging,
            "宝石を1個手に入れる", p => p.Counters.GemsFound >= 1),
        new(TitleId.GemCollector, "宝石収集家", TitleCategory.Forging,
            "宝石を10個手に入れる", p => p.Counters.GemsFound >= 10),
        new(TitleId.Gemologist, "宝石鑑定家", TitleCategory.Forging,
            $"全{Gems.All.Length}種類の宝石を手に入れる", p => p.GemsSeen.Distinct().Count() >= Gems.All.Length),
        new(TitleId.JewellersFriend, "宝飾ギルドの友", TitleCategory.Forging,
            "宝石調達の依頼を5件こなす", p => p.Counters.GemQuestsCompleted >= 5),

        // Delving above and below your station. Both are counted only on a dungeon taken apart completely —
        // every chest opened, then out by the stairs — because "went in" says nothing about nerve either way.
        new(TitleId.Daring, "勇敢な冒険者", TitleCategory.Delving,
            "格上のダンジョンを100回攻略する", p => p.Counters.HigherRankDungeonsCleared >= 100),
        new(TitleId.Valiant, "勇猛な挑戦者", TitleCategory.Delving,
            "格上のダンジョンを500回攻略する", p => p.Counters.HigherRankDungeonsCleared >= 500),
        new(TitleId.Fearless, "不屈の勇者", TitleCategory.Delving,
            "格上のダンジョンを1000回攻略する", p => p.Counters.HigherRankDungeonsCleared >= 1000),

        new(TitleId.Careful, "慎重な冒険者", TitleCategory.Delving,
            "格下のダンジョンを100回攻略する", p => p.Counters.LowerRankDungeonsCleared >= 100),
        new(TitleId.Prudent, "用心深い探索者", TitleCategory.Delving,
            "格下のダンジョンを500回攻略する", p => p.Counters.LowerRankDungeonsCleared >= 500),
        new(TitleId.Circumspect, "石橋を叩く者", TitleCategory.Delving,
            "格下のダンジョンを1000回攻略する", p => p.Counters.LowerRankDungeonsCleared >= 1000),
    ];

    /// <summary>
    /// One per material, built from <see cref="MaterialSets"/> so the pieces a title asks for are always the
    /// pieces that exist. The condition is having <em>worn</em> each of them at least once — a sword and a wand
    /// cannot be held at the same time, so anything phrased against the body at one moment is unearnable by
    /// construction, the same trap the smith's collector titles sidestep by counting what was forged.
    /// </summary>
    private static IEnumerable<TitleDefinition> MaterialDefinitions() =>
        MaterialSets.All.Select(set => new TitleDefinition(
            set.Title,
            $"{set.Material}マニア",
            TitleCategory.Outfitting,
            $"{set.Material}の武器・防具{set.Pieces.Length}種をすべて装備する",
            p => MaterialSets.HasWornAll(p, set)));

    /// <summary>
    /// Declared after <see cref="Fixed"/> and <see cref="CollectorDefinitions"/> on purpose: static field
    /// initialisers run in source order, so putting this first would build the list out of a null array.
    /// </summary>
    public static readonly TitleDefinition[] All =
        [.. Fixed, .. CollectorDefinitions(), .. MaterialDefinitions()];

    private static readonly Dictionary<TitleId, TitleDefinition> ById =
        All.ToDictionary(t => t.Id);

    public static TitleDefinition Get(TitleId id) => ById[id];

    public static string NameOf(TitleId id) => ById.TryGetValue(id, out var t) ? t.Name : id.ToString();

    /// <summary>
    /// Awards every title whose condition is now met and returns the ones that were newly granted. Safe to call
    /// as often as convenient: already-earned titles are skipped, so it only ever reports genuinely new ones.
    /// </summary>
    public static List<TitleDefinition> Refresh(Player player)
    {
        // Credit for what is already on the body. Equipping records itself, but a character who has been
        // wearing the same 革の鎧 since before any of this was tracked would otherwise never be credited for
        // it — and neither would the starting gear, which is placed directly rather than equipped.
        player.NoteWornGear();

        var gained = new List<TitleDefinition>();
        foreach (var title in All)
        {
            if (player.EarnedTitles.Contains(title.Id) || !title.IsEarned(player))
                continue;
            player.EarnedTitles.Add(title.Id);
            gained.Add(title);
        }

        // Without this a new character's card would sit blank until they went looking for the title screen.
        player.DisplayedTitle ??= player.EarnedTitles.Count > 0 ? player.EarnedTitles[0] : null;
        return gained;
    }
}
