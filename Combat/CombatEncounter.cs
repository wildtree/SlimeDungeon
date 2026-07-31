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
    public int ExpReward { get; private set; }

    /// <summary>What was actually brought down, for the end-of-battle summary.</summary>
    public List<Slime> Defeated { get; } = new();

    /// <summary>Slimes that got away — white ones mostly. They are worth nothing.</summary>
    public List<Slime> Escaped { get; } = new();

    /// <summary>Set on victory when the EXP award pushed the player up at least one level; null otherwise.</summary>
    public LevelUpSummary? LevelUp { get; private set; }

    /// <summary>Enemies still in the fight. A slime that has run away is as gone as a dead one.</summary>
    public List<Slime> AliveEnemies => Enemies.Where(e => e.IsEngaged).ToList();

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

    // ---- Damage -----------------------------------------------------------------------------

    private static string MatchupNote(Matchup matchup) => matchup switch
    {
        Matchup.Advantage => "（効果的だ！）",
        Matchup.Disadvantage => "（効きが悪い）",
        _ => "",
    };

    /// <summary>How an elemental matchup moves an attack along the rank ladder.</summary>
    private static int MatchupSteps(Matchup matchup) => matchup switch
    {
        Matchup.Advantage => 1,
        Matchup.Disadvantage => -1,
        _ => 0,
    };

    // ---- Player actions ---------------------------------------------------------------------

    public ActionResult PlayerAttack(Slime target)
    {
        // Steel finds nothing to bite on a white slime. It still counts as having been attacked, which is the
        // only thing that will make one turn and fight.
        if (target.IsWhite)
        {
            var noEffect = $"{Player.Name}の攻撃！ しかし{target.DisplayLabel}には武器が効かない";
            AddLog(noEffect);
            TryWhiteCounter(target);
            return new ActionResult(ActionOutcome.NoEffect, 0, noEffect);
        }

        var roll = CombatMath.Roll();
        if (roll.IsFailure)
        {
            var missed = $"{Player.Name}の攻撃は外れた";
            AddLog(missed);
            return new ActionResult(ActionOutcome.Miss, 0, missed);
        }

        var effectiveRank = Player.WeaponAttackRank + roll.RankSteps;
        var dmg = Math.Max(0,
            CombatMath.AttackDamage(effectiveRank, CombatMath.StatBonus(Player.EffectiveStr)) - target.Def);
        ApplyDamage(target, dmg);

        var crit = roll.IsCritical ? "会心の一撃！ " : "";
        var msg = $"{crit}{Player.Name}の攻撃！ {target.DisplayLabel}に{dmg}のダメージ";
        AddLog(msg);
        CheckVictory();
        return new ActionResult(ActionOutcome.Hit, dmg, msg);
    }

    /// <summary>
    /// A white slime does not start fights, but it will hit back — about half the time — at anything that has
    /// just struck at it, whether or not the blow actually did anything.
    /// </summary>
    private const double WhiteCounterChance = 0.5;

    private void TryWhiteCounter(Slime slime)
    {
        if (BattleOver || !slime.IsEngaged || RandomUtil.Shared.NextDouble() >= WhiteCounterChance)
            return;

        AddLog($"{slime.DisplayLabel}の反撃！");
        StrikePlayer(slime);
    }

    public ActionResult PlayerCastSpell(LearnedSpell spell, Slime? target)
    {
        var def = SpellDefinitions.All[spell.Id];
        var cost = SpellDefinitions.MpCost(spell.Rank);
        if (Player.Stats.Mp < cost)
            return new ActionResult(ActionOutcome.NotEnoughMp, 0, "MPが足りない！");

        Player.Stats.Mp -= cost;
        Player.Counters.SpellsCast++;

        switch (def.Effect)
        {
            case SpellEffect.Attack:
            {
                // Attack magic still covers the whole pack — that is what it buys for its MP. What it no
                // longer does is scale without limit: its damage is fixed by the spell's own rank, so a spell
                // kills slimes of its rank and no higher unless the element or a critical carries it up one.
                var targets = AliveEnemies;
                AddLog($"{Player.Name}の{def.Name}！");

                // One roll for the whole casting. A fizzled spell costs the MP anyway, which is the price of
                // reaching for magic instead of a weapon.
                var roll = CombatMath.Roll();
                if (roll.IsFailure)
                {
                    var failed = "しかし まほうは失敗した！";
                    AddLog(failed);
                    return new ActionResult(ActionOutcome.Miss, 0, failed);
                }

                if (roll.IsCritical)
                    AddLog("会心のまほう！");

                var total = 0;
                foreach (var enemy in targets)
                {
                    var vsMonster = Domain.ElementExtensions.GetMatchup(def.Element, ElementForDefense(enemy));
                    var matchupAdj = CombineWithDungeon(vsMonster, def.Element);
                    var effectiveRank = (int)spell.Rank + MatchupSteps(matchupAdj) + roll.RankSteps;
                    var dmg = Math.Max(0,
                        CombatMath.AttackDamage(effectiveRank, CombatMath.StatBonus(Player.EffectiveInt)) - enemy.Def);
                    ApplyDamage(enemy, dmg);
                    total += dmg;
                    AddLog($"  {enemy.DisplayLabel}に{dmg}のダメージ{MatchupNote(matchupAdj)}");
                }

                // Anything white that survived the blast gets its chance to hit back.
                foreach (var enemy in targets.Where(e => e.IsWhite && e.IsEngaged).ToList())
                    TryWhiteCounter(enemy);

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

    /// <summary>How often a white slime succeeds in slipping away on its turn.</summary>
    private const double WhiteFleeChance = 0.6;

    public ActionResult EnemyTurn(Slime enemy)
    {
        if (!enemy.IsEngaged)
            return new ActionResult(ActionOutcome.NoEffect, 0, "");

        // A white slime has no interest in a fight and spends its turns looking for a way out.
        if (enemy.IsWhite)
        {
            if (RandomUtil.Shared.NextDouble() < WhiteFleeChance)
            {
                enemy.HasFled = true;
                Escaped.Add(enemy);
                var gone = $"{enemy.DisplayLabel}は逃げていった…";
                AddLog(gone);
                CheckVictory();
                return new ActionResult(ActionOutcome.Fled, 0, gone);
            }

            var hesitated = $"{enemy.DisplayLabel}は逃げ道を探している";
            AddLog(hesitated);
            return new ActionResult(ActionOutcome.NoEffect, 0, hesitated);
        }

        return StrikePlayer(enemy);
    }

    /// <summary>
    /// A slime's blow. Rank H dungeons used to halve incoming damage; that was a workaround for an older
    /// formula where armour barely mitigated anything. Slimes roll for a critical or a fumble on the same
    /// curve the player does — luck cuts both ways.
    /// </summary>
    private ActionResult StrikePlayer(Slime enemy)
    {
        var roll = CombatMath.Roll();
        if (roll.IsFailure)
        {
            var missed = $"{enemy.DisplayLabel}の攻撃は外れた";
            AddLog(missed);
            return new ActionResult(ActionOutcome.Miss, 0, missed);
        }

        var power = roll.IsCritical
            ? (int)Math.Round(enemy.Stats.Str * CombatMath.RankStep)
            : enemy.Stats.Str;
        var dmg = Math.Max(0, power - Player.TotalDef);
        Player.Stats.Hp = Math.Max(0, Player.Stats.Hp - dmg);

        var crit = roll.IsCritical ? "痛恨の一撃！ " : "";
        var msg = $"{crit}{enemy.DisplayLabel}の攻撃！ {dmg}のダメージ";

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

    /// <summary>
    /// The battle ends once nothing is left facing the player, whether it died or ran. Only the dead count
    /// for EXP and for the guild's bounty — a white slime that got away leaves you with nothing to show.
    /// </summary>
    private void CheckVictory()
    {
        if (BattleOver || Enemies.Any(e => e.IsEngaged))
            return;

        BattleOver = true;
        PlayerPoisoned = false;

        foreach (var e in Enemies.Where(e => e.Stats.IsDead))
        {
            Defeated.Add(e);
            Player.RecordKill(e.Color, e.Rank);
            ExpReward += e.ExpValue(DungeonElement);
        }

        PlayerWon = Defeated.Count > 0;
        if (PlayerWon)
            Player.Counters.BattlesWon++;

        // No purse comes off a slime. The tally goes to the guild, which pays for the trip's work at the desk.
        LevelUp = Player.AddExp(ExpReward);
        AddLog(Defeated.Count > 0 ? $"勝利！ EXP {ExpReward} 獲得" : "スライムは逃げ去った…");
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
