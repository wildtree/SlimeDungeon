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
}

/// <summary>Broad grouping, used to sort the title list into something readable.</summary>
public enum TitleCategory { Beginning, Slaying, Bestiary, Quests, Delving, Guild, Growth, Craft, Living, Forging }

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

        // Delving.
        new(TitleId.DungeonRegular, "ダンジョン慣れ", TitleCategory.Delving,
            "ダンジョンに10回もぐる", p => p.Counters.DungeonVisits >= 10),
        new(TitleId.DungeonHabitue, "ダンジョンの常連", TitleCategory.Delving,
            "ダンジョンに50回もぐる", p => p.Counters.DungeonVisits >= 50),
        new(TitleId.DungeonDweller, "ダンジョンの住人", TitleCategory.Delving,
            "ダンジョンに150回もぐる", p => p.Counters.DungeonVisits >= 150),

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
    ];

    /// <summary>
    /// Declared after <see cref="Fixed"/> and <see cref="CollectorDefinitions"/> on purpose: static field
    /// initialisers run in source order, so putting this first would build the list out of a null array.
    /// </summary>
    public static readonly TitleDefinition[] All = [.. Fixed, .. CollectorDefinitions()];

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
