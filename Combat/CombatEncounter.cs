using SlimeDungeon.Core;
using SlimeDungeon.Domain;

namespace SlimeDungeon.Combat;

public enum ActionOutcome { Hit, Miss, Fled, FleeFailed, NotEnoughMp, NoEffect }

public sealed record ActionResult(ActionOutcome Outcome, int Amount, string Message);

/// <summary>Pure battle logic: turn order, damage formulas, poison, rewards. No rendering/input here.</summary>
public sealed class CombatEncounter
{
    public required Player Player { get; init; }
    public required List<Slime> Enemies { get; init; }
    public Element? DungeonElement { get; init; }
    public Rank? DungeonRank { get; init; }

    public bool PlayerPoisoned { get; private set; }
    public List<string> Log { get; } = new();
    public bool BattleOver { get; private set; }
    public bool PlayerWon { get; private set; }
    public bool PlayerFled { get; private set; }
    public int GoldReward { get; private set; }
    public int ExpReward { get; private set; }

    /// <summary>Set on victory when the EXP award pushed the player up at least one level; null otherwise.</summary>
    public LevelUpSummary? LevelUp { get; private set; }

    public List<Slime> AliveEnemies => Enemies.Where(e => !e.Stats.IsDead).ToList();

    /// <summary>Sum of every (living or not) enemy's AGL — used only for the flee-chance formula, which the
    /// spec defines against the opposing side's combined AGL rather than any single enemy.</summary>
    public int EnemyAglSum => Enemies.Sum(e => e.Stats.Agl);

    /// <summary>
    /// Splits the currently-alive enemies into those faster than the player (act before the player's turn
    /// this round) and those slower-or-equal (act after) — a proper per-combatant initiative order, each
    /// compared individually against the player's own AGL, rather than the player vs. the enemies' AGL total.
    /// </summary>
    public (List<Slime> Before, List<Slime> After) SplitEnemiesByInitiative()
    {
        var alive = AliveEnemies;
        var playerAgl = Player.EffectiveAgl;
        var before = alive.Where(e => e.Stats.Agl > playerAgl).OrderByDescending(e => e.Stats.Agl).ToList();
        var after = alive.Where(e => e.Stats.Agl <= playerAgl).OrderByDescending(e => e.Stats.Agl).ToList();
        return (before, after);
    }

    private void AddLog(string msg) => Log.Add(msg);

    // ---- Physical / magic damage formulas -------------------------------------------------

    /// <summary>
    /// Attack power with a small spread, then armour subtracted outright. The old formula reduced damage by
    /// only |N(0, DEF/2)| — about 1.2 points for DEF 3 — which made armour almost decorative and let any
    /// above-rank monster one-shot the player. Subtracting DEF directly is what makes equipment matter.
    /// </summary>
    private static int PhysicalDamage(int attackerStr, int defenderDef)
    {
        var spread = Math.Clamp(RandomUtil.Shared.NextGaussian(1.0, 0.14), 0.65, 1.35);
        return Math.Max(0, (int)Math.Floor(attackerStr * spread) - defenderDef);
    }

    private static string MatchupNote(Matchup matchup) => matchup switch
    {
        Matchup.Advantage => "（効果的だ！）",
        Matchup.Disadvantage => "（効きが悪い）",
        _ => "",
    };

    private static int MagicDamage(int casterInt, int defenderDef, Matchup matchup)
    {
        var rnd = RandomUtil.Shared;
        switch (matchup)
        {
            case Matchup.Advantage:
            {
                var r = Math.Max(1.1, rnd.NextGaussian(1.2, 0.2));
                return Math.Max(0, (int)Math.Floor(casterInt * r));
            }
            case Matchup.Disadvantage:
            {
                var r = Math.Min(0.9, rnd.NextGaussian(0.8, 0.2));
                return Math.Max(0, (int)Math.Floor(casterInt * r));
            }
            default:
            {
                var r = rnd.NextGaussian(0, defenderDef / 2.0);
                return Math.Max(0, casterInt - (int)Math.Floor(Math.Abs(r)));
            }
        }
    }

    // ---- Player actions ---------------------------------------------------------------------

    public ActionResult PlayerAttack(Slime target)
    {
        var dmg = PhysicalDamage(Player.EffectiveStr, TargetDef(target));
        ApplyDamage(target, dmg);
        var msg = dmg > 0 ? $"{Player.Name}の攻撃！ {target.DisplayLabel}に{dmg}のダメージ" : $"{Player.Name}の攻撃は外れた";
        AddLog(msg);
        CheckVictory();
        return new ActionResult(ActionOutcome.Hit, dmg, msg);
    }

    public ActionResult PlayerCastSpell(LearnedSpell spell, Slime? target)
    {
        var def = SpellDefinitions.All[spell.Id];
        var cost = SpellDefinitions.MpCost(spell.Rank);
        if (Player.Stats.Mp < cost)
            return new ActionResult(ActionOutcome.NotEnoughMp, 0, "MPが足りない！");

        Player.Stats.Mp -= cost;
        Player.Counters.SpellsCast++;
        var effectiveInt = Player.EffectiveInt + (int)spell.Rank * 2;

        switch (def.Effect)
        {
            case SpellEffect.Attack:
            {
                // Attack magic always covers the whole pack. That, plus the elemental matchup multiplier, is
                // what it buys for its MP: a plain attack is free but hits one slime, so magic earns its keep
                // against groups and against colors it is strong into.
                var targets = AliveEnemies;
                var total = 0;
                AddLog($"{Player.Name}の{def.Name}！");
                foreach (var enemy in targets)
                {
                    var vsMonster = Domain.ElementExtensions.GetMatchup(def.Element, ElementForDefense(enemy));
                    var matchupAdj = CombineWithDungeon(vsMonster, def.Element);
                    var dmg = MagicDamage(effectiveInt, TargetDef(enemy), matchupAdj);
                    ApplyDamage(enemy, dmg);
                    total += dmg;
                    AddLog($"  {enemy.DisplayLabel}に{dmg}のダメージ{MatchupNote(matchupAdj)}");
                }
                CheckVictory();
                return new ActionResult(ActionOutcome.Hit, total, $"{def.Name}で{targets.Count}体に合計{total}のダメージ");
            }
            case SpellEffect.Heal:
            {
                var amount = SpellDefinitions.HealAmount(spell.Rank, Player.Stats.MaxHp);
                Player.Stats.Hp = Math.Min(Player.Stats.MaxHp, Player.Stats.Hp + amount);
                var msg = $"{Player.Name}の{def.Name}！ HPが{amount}回復した";
                AddLog(msg);
                return new ActionResult(ActionOutcome.Hit, amount, msg);
            }
            case SpellEffect.Cure:
            {
                var success = RandomUtil.Shared.NextDouble() < SpellDefinitions.CureSuccessRate(spell.Rank);
                if (success && PlayerPoisoned)
                {
                    PlayerPoisoned = false;
                    var msg = $"{Player.Name}の{def.Name}！ 毒が治った";
                    AddLog(msg);
                    return new ActionResult(ActionOutcome.Hit, 0, msg);
                }
                var failMsg = $"{Player.Name}の{def.Name}！ しかし効果がなかった";
                AddLog(failMsg);
                return new ActionResult(ActionOutcome.NoEffect, 0, failMsg);
            }
            default:
                return new ActionResult(ActionOutcome.NoEffect, 0, "");
        }
    }

    public ActionResult PlayerUseItem(Item item, Slime? target)
    {
        switch (item.Category)
        {
            case ItemCategory.Herb:
            {
                var amount = ConsumableEffects.HerbHealAmount(item.Rank, Player.Stats.MaxHp);
                Player.Stats.Hp = Math.Min(Player.Stats.MaxHp, Player.Stats.Hp + amount);
                RemoveOneFromBag(item);
                var msg = $"{item.Name}を使った。HPが{amount}回復した";
                AddLog(msg);
                return new ActionResult(ActionOutcome.Hit, amount, msg);
            }
            case ItemCategory.Potion:
            {
                var amount = ConsumableEffects.PotionRestoreAmount(
                    item.Rank,
                    item.PotionKind == PotionKind.Hp ? Player.Stats.MaxHp : Player.Stats.MaxMp);

                if (item.PotionKind == PotionKind.Hp)
                    Player.Stats.Hp = Math.Min(Player.Stats.MaxHp, Player.Stats.Hp + amount);
                else
                    Player.Stats.Mp = Math.Min(Player.Stats.MaxMp, Player.Stats.Mp + amount);

                RemoveOneFromBag(item);
                var msg = $"{item.Name}を使った。{(item.PotionKind == PotionKind.Hp ? "HP" : "MP")}が{amount}回復した";
                AddLog(msg);
                return new ActionResult(ActionOutcome.Hit, amount, msg);
            }
            case ItemCategory.Antidote:
            {
                var success = RandomUtil.Shared.NextDouble() < ConsumableEffects.AntidoteSuccessRate(item.Rank);
                RemoveOneFromBag(item);
                if (success && PlayerPoisoned)
                {
                    PlayerPoisoned = false;
                    var msg = $"{item.Name}を使った。毒が治った";
                    AddLog(msg);
                    return new ActionResult(ActionOutcome.Hit, 0, msg);
                }
                var failMsg = $"{item.Name}を使った。しかし効果がなかった";
                AddLog(failMsg);
                return new ActionResult(ActionOutcome.NoEffect, 0, failMsg);
            }
            default:
                return new ActionResult(ActionOutcome.NoEffect, 0, "使えない");
        }
    }

    public ActionResult PlayerFlee()
    {
        double chance;
        if (Player.EffectiveAgl > EnemyAglSum)
        {
            chance = 1.0;
        }
        else
        {
            var diff = Player.EffectiveAgl - EnemyAglSum;
            chance = Math.Clamp(0.5 + diff * 0.05, 0.1, 0.9);
        }

        if (RandomUtil.Shared.NextDouble() < chance)
        {
            PlayerFled = true;
            BattleOver = true;
            PlayerPoisoned = false;
            Player.Counters.TimesFled++;
            AddLog($"{Player.Name}は逃げ出した！");
            return new ActionResult(ActionOutcome.Fled, 0, "逃げ出した！");
        }

        AddLog($"{Player.Name}は逃げられなかった！");
        return new ActionResult(ActionOutcome.FleeFailed, 0, "逃げられなかった！");
    }

    // ---- Enemy turn --------------------------------------------------------------------------

    public ActionResult EnemyTurn(Slime enemy)
    {
        // Rank H dungeons used to halve incoming damage. That was a workaround for the old formula, where
        // armour barely mitigated anything and an above-rank monster could one-shot you; now that DEF is
        // subtracted directly it only made a beginner's first fights completely harmless.
        var dmg = PhysicalDamage(enemy.Stats.Str, Player.TotalDef);
        Player.Stats.Hp = Math.Max(0, Player.Stats.Hp - dmg);
        var msg = $"{enemy.DisplayLabel}の攻撃！ {dmg}のダメージ";

        if (enemy.IsPoison && dmg >= 1 && !PlayerPoisoned)
        {
            var r = RandomUtil.Shared.NextGaussian(0.5, 0.25);
            if (r * (int)enemy.Rank / 10.0 > 0.5)
            {
                PlayerPoisoned = true;
                msg += "。毒を浴びた！";
            }
        }

        AddLog(msg);
        CheckDefeat();
        return new ActionResult(ActionOutcome.Hit, dmg, msg);
    }

    public void ApplyPlayerTurnEndPoisonTick()
    {
        if (!PlayerPoisoned || BattleOver)
            return;

        var dmg = (int)Math.Ceiling(Player.Stats.Hp * 0.1);
        Player.Stats.Hp = Math.Max(0, Player.Stats.Hp - dmg);
        AddLog($"毒のダメージ！ {dmg}");
        CheckDefeat();
    }

    // ---- End-of-battle bookkeeping -----------------------------------------------------------

    private void CheckVictory()
    {
        if (BattleOver || Enemies.Any(e => !e.Stats.IsDead))
            return;

        BattleOver = true;
        PlayerWon = true;
        PlayerPoisoned = false;
        Player.Counters.BattlesWon++;

        foreach (var e in Enemies)
        {
            Player.RecordKill(e.Color, e.Rank);
            ExpReward += e.ExpValue(DungeonElement);
        }
        GoldReward = Enemies.Sum(e => (int)e.Rank * 3);
        Player.EarnGold(GoldReward);
        LevelUp = Player.AddExp(ExpReward);
        AddLog($"勝利！ EXP {ExpReward} 獲得、{GoldReward}G 獲得");
        if (LevelUp is { } up)
            AddLog($"レベルが{up.ToLevel}に上がった！");
    }

    private void CheckDefeat()
    {
        if (Player.Stats.IsDead)
        {
            BattleOver = true;
            PlayerPoisoned = false;
            AddLog($"{Player.Name}は倒れてしまった…");
        }
    }

    private int TargetDef(Slime target) => 0;

    private Element ElementForDefense(Slime target) => target.Element;

    /// <summary>
    /// Combines the spell-vs-monster matchup with the dungeon's own ambient elemental bias (a dungeon
    /// amplifies the spell element that beats it and dampens the one it beats, independent of the target).
    /// Conflicting signals (one favorable, one unfavorable) cancel out to neutral.
    /// </summary>
    private Matchup CombineWithDungeon(Matchup vsMonster, Element spellElement)
    {
        var vsDungeon = Matchup.Neutral;
        if (DungeonElement is { } de && de != Element.None)
        {
            if (Domain.ElementExtensions.StrongSpellElementIn(de) == spellElement) vsDungeon = Matchup.Advantage;
            else if (Domain.ElementExtensions.WeakElementIn(de) == spellElement) vsDungeon = Matchup.Disadvantage;
        }

        if (vsMonster == vsDungeon) return vsMonster;
        if (vsMonster == Matchup.Neutral) return vsDungeon;
        if (vsDungeon == Matchup.Neutral) return vsMonster;
        return Matchup.Neutral;
    }

    private static void ApplyDamage(Slime target, int dmg) =>
        target.Stats.Hp = Math.Max(0, target.Stats.Hp - dmg);

    /// <summary>Spends one of a consumable, whether it was readied in an item slot or loose in the bag.</summary>
    private void RemoveOneFromBag(Item item) => Player.ConsumeOne(item);
}
