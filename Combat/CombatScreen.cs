using SDL3;
using SlimeDungeon.Core;
using SlimeDungeon.Domain;
using SlimeDungeon.Graphics;
using SlimeDungeon.UI;

namespace SlimeDungeon.Combat;

public sealed class CombatScreen : IScreen
{
    private enum Phase { Menu, TargetSelect, SpellSelect, ItemSelect, RoundResolved, BattleEnd, BattleSummary, LevelUpSummary }
    private enum MenuCommand { Attack, Magic, Item, Flee }

    private readonly IScreen _dungeonScreen;
    private AudioService _audio = null!;
    private CombatEncounter _battle = null!;
    private Phase _phase = Phase.Menu;
    private int _cursor;
    private Func<ActionResult>? _pendingPlayerAction;
    private readonly List<string> _roundLog = new();
    private List<Slime> _postEnemies = new();
    private float _time;

    public CombatScreen(List<Slime> enemies, IScreen dungeonScreen, Domain.Element? dungeonElement, Rank dungeonRank)
    {
        _dungeonScreen = dungeonScreen;
        Enemies = enemies;
        DungeonElement = dungeonElement;
        DungeonRank = dungeonRank;
    }

    private List<Slime> Enemies { get; }
    private Domain.Element? DungeonElement { get; }
    private Rank DungeonRank { get; }

    public void OnEnter(GameContext ctx)
    {
        AssignDisplayLabels(Enemies);
        _audio = ctx.Audio;
        _battle = new CombatEncounter { Player = ctx.Player!, Enemies = Enemies, DungeonElement = DungeonElement, DungeonRank = DungeonRank };
        _battle.Log.Add($"{string.Join("・", Enemies.Select(e => e.DisplayLabel))}のスライムが現れた！");
        BeginRound();
    }

    /// <summary>
    /// Starts a round: figures out which alive enemies are individually faster than the player (they act
    /// immediately, before the player ever sees the command menu) and which are slower-or-equal (saved to
    /// act right after the player's turn in <see cref="ResolveRound"/>). Replaces the old "player vs. total
    /// enemy AGL" grouping with a proper per-combatant initiative order.
    /// </summary>
    private void BeginRound()
    {
        _roundLog.Clear();
        var logStart = _battle.Log.Count;

        var (before, after) = _battle.SplitEnemiesByInitiative();
        _postEnemies = after;

        foreach (var enemy in before)
        {
            if (_battle.BattleOver)
                break;
            PlayEnemyTurn(enemy);
        }

        _roundLog.AddRange(_battle.Log.Skip(logStart));

        if (_battle.BattleOver)
        {
            _phase = Phase.BattleEnd;
            return;
        }

        _phase = Phase.Menu;
        _cursor = 0;
    }

    /// <summary>
    /// A slime's turn, with the impact sound when it actually connects. Only on a landed blow: a round where
    /// everything fumbles should sound like nothing happened, because nothing did.
    /// </summary>
    private void PlayEnemyTurn(Slime enemy)
    {
        var result = _battle.EnemyTurn(enemy);
        if (result.Outcome == ActionOutcome.Hit && result.Amount > 0)
            _audio.Play(SoundId.WeaponHit);
    }

    /// <summary>Gives each enemy a stable label for the whole battle: the plain color name when it's the
    /// only one of that color, or "Color A"/"Color B"/... when duplicates would otherwise be indistinguishable
    /// in the log and target list.</summary>
    private static void AssignDisplayLabels(List<Slime> enemies)
    {
        foreach (var group in enemies.GroupBy(e => e.Color))
        {
            var members = group.ToList();
            if (members.Count == 1)
            {
                members[0].DisplayLabel = members[0].Color.ToString();
                continue;
            }

            for (var i = 0; i < members.Count; i++)
                members[i].DisplayLabel = $"{members[0].Color} {(char)('A' + i)}";
        }
    }

    public void Update(GameContext ctx, float dt)
    {
        var input = ctx.Input;
        _time += dt;

        switch (_phase)
        {
            case Phase.Menu:
                UpdateMenu(ctx, input);
                break;
            case Phase.TargetSelect:
                UpdateTargetSelect(ctx, input);
                break;
            case Phase.SpellSelect:
                UpdateSpellSelect(ctx, input);
                break;
            case Phase.ItemSelect:
                UpdateItemSelect(ctx, input);
                break;
            case Phase.RoundResolved:
                if (input.WasPressed(SDL.Keycode.Return) || input.WasPressed(SDL.Keycode.Space))
                    BeginRound();
                break;
            case Phase.BattleEnd:
                if (input.WasPressed(SDL.Keycode.Return) || input.WasPressed(SDL.Keycode.Space))
                {
                    if (_battle.Player.Stats.IsDead)
                        ctx.Screens.ChangeTo(new Guild.GameOverScreen());
                    else if (!_battle.PlayerFled)
                        _phase = Phase.BattleSummary;
                    else
                        ctx.Screens.ChangeTo(_dungeonScreen);
                }
                break;
            case Phase.BattleSummary:
                if (input.WasPressed(SDL.Keycode.Return) || input.WasPressed(SDL.Keycode.Space))
                {
                    if (_battle.LevelUp is not null)
                    {
                        ctx.Audio.Play(SoundId.LevelUpFanfare);
                        _phase = Phase.LevelUpSummary;
                    }
                    else
                    {
                        ctx.Screens.ChangeTo(_dungeonScreen);
                    }
                }
                break;
            case Phase.LevelUpSummary:
                if (input.WasPressed(SDL.Keycode.Return) || input.WasPressed(SDL.Keycode.Space))
                    ctx.Screens.ChangeTo(_dungeonScreen);
                break;
        }
    }

    private static readonly string[] MenuLabels = { "たたかう", "まほう", "アイテム", "にげる" };

    private void UpdateMenu(GameContext ctx, InputManager input)
    {
        if (input.WasPressed(SDL.Keycode.Down)) _cursor = (_cursor + 1) % MenuLabels.Length;
        if (input.WasPressed(SDL.Keycode.Up)) _cursor = (_cursor - 1 + MenuLabels.Length) % MenuLabels.Length;

        if (!input.WasPressed(SDL.Keycode.Return) && !input.WasPressed(SDL.Keycode.Space))
            return;

        switch ((MenuCommand)_cursor)
        {
            case MenuCommand.Attack:
                _phase = Phase.TargetSelect;
                _cursor = 0;
                break;
            case MenuCommand.Magic:
                if (_battle.Player.KnownSpells.Count == 0)
                {
                    _roundLog.Add("まほうを覚えていない");
                    return;
                }
                _phase = Phase.SpellSelect;
                _cursor = 0;
                break;
            case MenuCommand.Item:
                if (UsableItems(ctx).Count == 0)
                {
                    _roundLog.Add("アイテム欄に何も装備していない");
                    return;
                }
                _phase = Phase.ItemSelect;
                _cursor = 0;
                break;
            case MenuCommand.Flee:
                _pendingPlayerAction = () => _battle.PlayerFlee();
                ResolveRound();
                break;
        }
    }

    private void UpdateTargetSelect(GameContext ctx, InputManager input)
    {
        var alive = _battle.AliveEnemies;
        if (input.WasPressed(SDL.Keycode.Escape)) { _phase = Phase.Menu; _cursor = 0; return; }
        if (input.WasPressed(SDL.Keycode.Down)) _cursor = (_cursor + 1) % alive.Count;
        if (input.WasPressed(SDL.Keycode.Up)) _cursor = (_cursor - 1 + alive.Count) % alive.Count;

        if (input.WasPressed(SDL.Keycode.Return) || input.WasPressed(SDL.Keycode.Space))
        {
            var target = alive[_cursor];
            ctx.Audio.Play(SoundId.WeaponHit);
            _pendingPlayerAction = () => _battle.PlayerAttack(target);
            ResolveRound();
        }
    }

    private void UpdateSpellSelect(GameContext ctx, InputManager input)
    {
        var spells = _battle.Player.KnownSpells;
        if (input.WasPressed(SDL.Keycode.Escape)) { _phase = Phase.Menu; _cursor = 0; return; }
        if (input.WasPressed(SDL.Keycode.Down)) _cursor = (_cursor + 1) % spells.Count;
        if (input.WasPressed(SDL.Keycode.Up)) _cursor = (_cursor - 1 + spells.Count) % spells.Count;

        if (!input.WasPressed(SDL.Keycode.Return) && !input.WasPressed(SDL.Keycode.Space))
            return;

        var spell = spells[_cursor];
        if (SpellDefinitions.MpCost(spell.Rank) > _battle.Player.Stats.Mp)
        {
            _roundLog.Add("MPが足りない");
            return;
        }

        // Restorative magic gets its own sound — a spell that mends should not land like one that burns.
        var effect = SpellDefinitions.All[spell.Id].Effect;
        ctx.Audio.Play(effect == SpellEffect.Attack ? SoundId.MagicAttack : SoundId.MagicHeal);

        // Attack spells hit the whole pack, so none of the spells need a target picked any more.
        _pendingPlayerAction = () => _battle.PlayerCastSpell(spell, null);
        ResolveRound();
    }

    private void UpdateItemSelect(GameContext ctx, InputManager input)
    {
        var items = UsableItems(ctx);
        if (input.WasPressed(SDL.Keycode.Escape) || items.Count == 0) { _phase = Phase.Menu; _cursor = 0; return; }
        if (_cursor >= items.Count) _cursor = items.Count - 1;
        if (input.WasPressed(SDL.Keycode.Down)) _cursor = (_cursor + 1) % items.Count;
        if (input.WasPressed(SDL.Keycode.Up)) _cursor = (_cursor - 1 + items.Count) % items.Count;

        if (!input.WasPressed(SDL.Keycode.Return) && !input.WasPressed(SDL.Keycode.Space))
            return;

        var item = items[_cursor];
        _pendingPlayerAction = () => _battle.PlayerUseItem(item, null);
        ResolveRound();
    }

    /// <summary>Spell rows advertise MP cost and, for attack spells, that they cover the whole pack — the
    /// reason to spend MP instead of just attacking.</summary>
    private static string SpellMenuLabel(LearnedSpell s)
    {
        var def = SpellDefinitions.All[s.Id];
        var scope = def.Effect == SpellEffect.Attack ? " 全体" : "";
        return $"{SpellDefinitions.NameOf(s.Id)}{scope} (MP{SpellDefinitions.MpCost(s.Rank)})";
    }

    /// <summary>
    /// Only what is readied in the two item slots. Digging through the pack mid-fight is not an option —
    /// that is precisely what buys the readied items their place outside the bag's capacity.
    /// </summary>
    private static List<Item> UsableItems(GameContext ctx) => ctx.Player!.ReadiedItems.ToList();

    /// <summary>Resolves the player's chosen action, then whichever alive enemies were slower than the
    /// player this round (computed back in <see cref="BeginRound"/>), skipping any that already died.</summary>
    private void ResolveRound()
    {
        var logStart = _battle.Log.Count;

        _pendingPlayerAction?.Invoke();
        _pendingPlayerAction = null;
        _battle.ApplyPlayerTurnEndPoisonTick();

        if (!_battle.BattleOver)
        {
            foreach (var enemy in _postEnemies)
            {
                if (_battle.BattleOver)
                    break;
                // Skip anything that died — or slipped away — since the round's initiative order was fixed.
                if (!enemy.IsEngaged)
                    continue;
                PlayEnemyTurn(enemy);
            }
        }

        _roundLog.AddRange(_battle.Log.Skip(logStart));
        _phase = _battle.BattleOver ? Phase.BattleEnd : Phase.RoundResolved;

        if (_battle.PlayerWon)
            TickQuestProgress();
    }

    private void TickQuestProgress()
    {
        var quest = _battle.Player.ActiveQuest;
        if (quest is null || quest.Type != QuestType.DefeatSlime)
            return;

        // Only what actually fell counts. A slime that ran away is not a slime you defeated, and now that
        // white ones flee on purpose the difference is a real one.
        foreach (var e in _battle.Defeated.Where(e => e.Color == quest.TargetSlimeColor))
            quest.Progress++;
    }

    public void Draw(GameContext ctx)
    {
        var r = ctx.Renderer;
        r.Clear(Colors.Rgb(24, 18, 30));

        var areaW = 400f;
        r.DrawTexture(ctx.Sprites.CombatBackdrop(DungeonElement), 0, 0, CombatArt.Width, CombatArt.Height);
        foreach (var (tx, ty) in CombatArt.TorchPositions)
            TorchFlame.Draw(r, tx, ty - 6, _time, 0.8f);
        DrawEnemies(ctx, areaW);
        DrawLog(ctx, areaW);

        switch (_phase)
        {
            case Phase.Menu:
                DrawMenu(ctx, MenuLabels, _cursor, "コマンド");
                break;
            case Phase.TargetSelect:
                DrawMenu(ctx, _battle.AliveEnemies.Select(e => $"{e.DisplayLabel}({e.Rank.Label()})").ToArray(), _cursor, "対象");
                break;
            case Phase.SpellSelect:
                DrawMenu(ctx, _battle.Player.KnownSpells.Select(SpellMenuLabel).ToArray(), _cursor, $"まほう (MP {_battle.Player.Stats.Mp}/{_battle.Player.Stats.MaxMp})");
                break;
            case Phase.ItemSelect:
                DrawMenu(ctx, UsableItems(ctx).Select(i => $"{i.Name} x{i.Quantity}").ToArray(), _cursor, "アイテム");
                break;
            case Phase.RoundResolved:
                ctx.Fonts.DrawText(r.Handle, "Enterで続ける", 12, 380, 11, Colors.Highlight);
                break;
            case Phase.BattleEnd:
                var msg = _battle.Player.Stats.IsDead ? "倒れてしまった…" : _battle.PlayerFled ? "戦闘から逃げ出した" : "戦闘に勝利した！";
                ctx.Fonts.DrawText(r.Handle, msg, 12, 360, 14, Colors.Highlight);
                ctx.Fonts.DrawText(r.Handle, "Enterで続ける", 12, 380, 11, Colors.Highlight);
                break;
        }

        StatusPanel.Draw(ctx, areaW, 0, 400);

        // Modal, so they go on top of the status panel too. The tally comes first, then the celebration.
        if (_phase == Phase.BattleSummary)
            BattleSummaryPopup.Draw(ctx, _battle.Defeated, _battle.Escaped, _battle.ExpReward);
        else if (_phase == Phase.LevelUpSummary && _battle.LevelUp is { } levelUp)
            LevelUpPopup.Draw(ctx, levelUp);
    }

    // Enemy block layout. The sprite's feet land on the backdrop's ground line, with the name and the two
    // gauges stacked underneath on the floor.
    private const float SpriteSize = 48f;
    private const float SpriteTop = CombatArt.GroundY - SpriteSize;
    private const float LabelY = CombatArt.GroundY + 4f;
    private const float HpBarY = CombatArt.GroundY + 18f;
    private const float MpBarY = CombatArt.GroundY + 27f;
    private const float NumbersY = CombatArt.GroundY + 36f;
    private const float GaugeLabelWidth = 9f;
    private const float GaugeBarWidth = 50f;
    private const float GaugeHeight = 6f;

    private void DrawEnemies(GameContext ctx, float areaW)
    {
        var r = ctx.Renderer;
        var fonts = ctx.Fonts;
        var alive = _battle.AliveEnemies;
        var spacing = areaW / (alive.Count + 1);

        for (var i = 0; i < alive.Count; i++)
        {
            var e = alive[i];
            var (idle, _) = ctx.Sprites.Slime(e.Color);
            var center = spacing * (i + 1);

            r.DrawTexture(idle, center - SpriteSize / 2f, SpriteTop, SpriteSize, SpriteSize);

            var (labelW, _) = fonts.Measure(e.DisplayLabel, 9);
            fonts.DrawText(r.Handle, e.DisplayLabel, center - labelW / 2f, LabelY, 9, Colors.White);

            // The bars themselves are centred under the sprite and the "H"/"M" letters hang off to the left.
            // Centring the letter+bar pair as one block instead would push the bars visibly right of the slime.
            var barX = center - GaugeBarWidth / 2f;
            DrawGauge(ctx, barX, HpBarY, "H", e.Stats.Hp, e.Stats.MaxHp, Colors.HpBar);
            DrawGauge(ctx, barX, MpBarY, "M", e.Stats.Mp, e.Stats.MaxMp, Colors.MpBar);

            // Exact values are a bonus on top of the gauges: at high ranks slime HP runs into the thousands,
            // so they're only drawn when they actually fit inside this enemy's slot.
            var numbers = $"{e.Stats.Hp}/{e.Stats.MaxHp}  {e.Stats.Mp}/{e.Stats.MaxMp}";
            var (numbersW, _) = fonts.Measure(numbers, 8);
            if (numbersW <= spacing - 4)
                fonts.DrawText(r.Handle, numbers, center - numbersW / 2f, NumbersY, 8, Colors.Rgb(190, 190, 200));
        }
    }

    /// <summary>One compact labelled gauge. The dark backing and border keep it readable wherever it lands on
    /// the backdrop, and an almost-empty bar still shows a sliver so "nearly dead" never looks like "dead".</summary>
    private static void DrawGauge(GameContext ctx, float barX, float y, string label, int current, int max, SDL.Color color)
    {
        var r = ctx.Renderer;
        ctx.Fonts.DrawText(r.Handle, label, barX - GaugeLabelWidth, y - 2, 8, Colors.Rgb(170, 170, 180));

        r.FillRect(barX - 1, y - 1, GaugeBarWidth + 2, GaugeHeight + 2, Colors.Rgb(12, 12, 16));
        r.FillRect(barX, y, GaugeBarWidth, GaugeHeight, Colors.BarBg);

        var frac = max > 0 ? Math.Clamp((float)current / max, 0f, 1f) : 0f;
        if (frac > 0)
            r.FillRect(barX, y, Math.Max(1f, GaugeBarWidth * frac), GaugeHeight, color);
    }

    private void DrawLog(GameContext ctx, float areaW)
    {
        var r = ctx.Renderer;
        var lines = _roundLog.Count > 0 ? _roundLog : _battle.Log.TakeLast(1).ToList();
        var y = 180f;
        foreach (var line in lines.TakeLast(6))
        {
            ctx.Fonts.DrawText(r.Handle, line, 12, y, 11, Colors.White);
            y += 16;
        }

        if (_battle.PlayerPoisoned)
            ctx.Fonts.DrawText(r.Handle, "[毒]", areaW - 40, 8, 11, Colors.Rgb(160, 60, 200));
    }

    private void DrawMenu(GameContext ctx, IReadOnlyList<string> labels, int cursor, string title)
    {
        var r = ctx.Renderer;
        var x = 12f;
        var y = 300f;
        r.FillRect(x - 4, y - 16, 380, 20 * labels.Count + 20, Colors.PanelBg);
        r.DrawRect(x - 4, y - 16, 380, 20 * labels.Count + 20, Colors.Border);
        ctx.Fonts.DrawText(r.Handle, title, x, y - 14, 10, Colors.Highlight);

        var maxWidth = MenuNav.MaxLabelWidth(ctx, labels, 12);
        for (var i = 0; i < labels.Count; i++)
            MenuNav.DrawRow(ctx, x, y + 6 + i * 20, maxWidth, 18, labels[i], 12, i == cursor);
    }
}
