using SlimeDungeon.Core;

namespace SlimeDungeon.Domain;

public enum SpellId { Fire, Water, Thunder, Stone, Heal, Cure }

public enum SpellEffect { Attack, Heal, Cure }

public sealed record SpellDef(SpellId Id, string Name, Element Element, SpellEffect Effect);

public static class SpellDefinitions
{
    public static readonly IReadOnlyDictionary<SpellId, SpellDef> All = new Dictionary<SpellId, SpellDef>
    {
        [SpellId.Fire] = new(SpellId.Fire, "ファイヤー", Element.Fire, SpellEffect.Attack),
        [SpellId.Water] = new(SpellId.Water, "ウォーター", Element.Water, SpellEffect.Attack),
        [SpellId.Thunder] = new(SpellId.Thunder, "サンダー", Element.Wind, SpellEffect.Attack),
        [SpellId.Stone] = new(SpellId.Stone, "ストーン", Element.Earth, SpellEffect.Attack),
        [SpellId.Heal] = new(SpellId.Heal, "ヒール", Element.Water, SpellEffect.Heal),
        [SpellId.Cure] = new(SpellId.Cure, "キュア", Element.Wind, SpellEffect.Cure),
    };

    public static string NameOf(SpellId id) => All[id].Name;

    public static SpellId RandomSpell() => (SpellId)RandomUtil.Shared.Next(Enum.GetValues<SpellId>().Length);

    /// <summary>
    /// MP cost scales with rank. Deliberately steep relative to a starting MP pool of ~10: attack magic now
    /// hits the whole pack, so it has to be a considered choice with a handful of casts per trip rather than
    /// a button that replaces plain attacks outright.
    /// </summary>
    public static int MpCost(Rank rank) => (int)rank * 3;

    /// <summary>Heal recovers exactly what an HP potion of the same rank would, flat minimum included.</summary>
    public static int HealAmount(Rank rank, int maxHp) => ConsumableEffects.PotionRestoreAmount(rank, maxHp);

    /// <summary>Cure success rate follows the same curve as antidotes (H=50% .. SS=100%).</summary>
    public static double CureSuccessRate(Rank rank) => ConsumableEffects.AntidoteSuccessRate(rank);
}
