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
    /// MP cost, on the same 1.6-per-rank ladder as everything else in combat.
    ///
    /// It used to be linear — three MP a rank, so thirty at the top — while the MP pool grew exponentially with
    /// level. Measured against the old growth curve that came out at six hundred thousand casts from a full
    /// bar; even against a sane pool, linear cost against exponential MP means magic gets cheaper every rank
    /// until it replaces plain attacks entirely. Matching the ladder holds it at a handful of casts per trip
    /// for the whole game, which is what makes choosing to spend one interesting.
    /// </summary>
    /// <param name="rank">
    /// The coefficient is set against the MP pool a character has at the level that rank is reached at — about
    /// ten times the same 1.6 curve — so a full bar buys roughly four casts at every rank in the game rather
    /// than two at the bottom and thousands at the top.
    /// </param>
    public static int MpCost(Rank rank) =>
        Math.Max(1, (int)Math.Round(2.5 * CombatMath.RankPower((int)rank)));

    /// <summary>Heal recovers exactly what an HP potion of the same rank would, flat minimum included.</summary>
    public static int HealAmount(Rank rank, int maxHp) => ConsumableEffects.PotionRestoreAmount(rank, maxHp);

    /// <summary>Cure success rate follows the same curve as antidotes (H=50% .. SS=100%).</summary>
    public static double CureSuccessRate(Rank rank) => ConsumableEffects.AntidoteSuccessRate(rank);
}
