using SDL3;
using SlimeDungeon.Core;
using SlimeDungeon.Domain;
using SlimeDungeon.Graphics;
using SlimeDungeon.UI;

namespace SlimeDungeon.Combat;

public sealed class CombatScreen : IScreen
{
    private enum Phase { Message, Menu, TargetSelect, SpellSelect, ItemSelect, BattleEnd, BattleSummary, LevelUpSummary, Overflow }
    private enum MenuCommand { Attack, Magic, Item, Flee }

    private readonly IScreen _dungeonScreen;
    private AudioService _audio = null!;
    private CombatEncounter _battle = null!;
    private Phase _phase = Phase.Menu;
    private int _cursor;

    /// <summary>
    /// What dismissing the message panel does. A round's results lead into the next round; everything else
    /// (the encounter itself, "MPが足りない") just hands the command menu back.
    /// </summary>
    private bool _messageEndsRound;
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
        _audio.Play(SoundId.Encounter);
        _battle = new CombatEncounter { Player = ctx.Player!, Enemies = Enemies, DungeonElement = DungeonElement, DungeonRank = DungeonRank };
        var opening = $"{string.Join("・", Enemies.Select(e => e.DisplayLabel))}のスライムが現れた！";
        _battle.Log.Add(opening);
        // Handed to the round rather than left in the battle log, so it opens the first message panel with
        // whatever the faster slimes do before the player has had a chance to move.
        BeginRound(opening);
    }

    /// <summary>
    /// Starts a round: figures out which alive enemies are individually faster than the player (they act
    /// immediately, before the player ever sees the command menu) and which are slower-or-equal (saved to
    /// act right after the player's turn in <see cref="ResolveRound"/>). Replaces the old "player vs. total
    /// enemy AGL" grouping with a proper per-combatant initiative order.
    /// </summary>
    private void BeginRound(string? opening = null)
    {
        _roundLog.Clear();
        if (opening is not null)
            _roundLog.Add(opening);

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

        // Anything that happened before the player's turn is read first — a slime that got in ahead of them
        // used to have its hit appear behind the command menu, where it was easy to miss entirely.
        if (_roundLog.Count > 0)
        {
            ShowMessage(endsRound: false);
            return;
        }

        _phase = Phase.Menu;
        _cursor = 0;
    }

    private void ShowMessage(bool endsRound)
    {
        _phase = Phase.Message;
        _messageEndsRound = endsRound;
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
            case Phase.Message:
                if (MenuNav.Confirmed(input))
                {
                    if (_messageEndsRound)
                    {
                        BeginRound();
                    }
                    else
                    {
                        _roundLog.Clear();
                        _phase = Phase.Menu;
                        _cursor = 0;
                    }
                }
                break;
            case Phase.BattleEnd:
                if (MenuNav.Confirmed(input))
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
                if (MenuNav.Confirmed(input))
                {
                    if (_battle.LevelUp is not null)
                    {
                        ctx.Audio.Play(SoundId.LevelUpFanfare);
                        _phase = Phase.LevelUpSummary;
                    }
                    else
                    {
                        LeaveBattle(ctx);
                    }
                }
                break;
            case Phase.LevelUpSummary:
                if (MenuNav.Confirmed(input))
                    LeaveBattle(ctx);
                break;
            case Phase.Overflow:
                UpdateOverflow(ctx, input);
                break;
        }
    }

    private int _overflowCursor;

    /// <summary>
    /// Leaves for the dungeon — unless something dropped that would not fit, in which case that is settled
    /// first. Loot is held back rather than discarded, so this is the last chance to make room for it.
    /// </summary>
    private void LeaveBattle(GameContext ctx)
    {
        if (_battle.Overflow.Count > 0)
        {
            _phase = Phase.Overflow;
            // On "give it up", so a stray confirm never throws away something already carried.
            _overflowCursor = OverflowPopup.RowCount(ctx.Player!);
            return;
        }

        ctx.Screens.ChangeTo(_dungeonScreen);
    }

    /// <summary>One find at a time: pick something to throw away for it, or let it go.</summary>
    private void UpdateOverflow(GameContext ctx, InputManager input)
    {
        var player = ctx.Player!;
        if (_battle.Overflow.Count == 0)
        {
            ctx.Screens.ChangeTo(_dungeonScreen);
            return;
        }

        _overflowCursor = MenuNav.Move(input, _overflowCursor, OverflowPopup.RowCount(player));
        if (!MenuNav.Confirmed(input))
            return;

        var incoming = _battle.Overflow[0];
        if (OverflowPopup.Chosen(player, _overflowCursor) is { } discarded)
        {
            player.Bag.Remove(discarded);
            player.Bag.Add(incoming);

            // Counted here rather than at the drop, so ore only ever counts once it is genuinely in hand.
            if (incoming is { Category: ItemCategory.Material, Metal: { } metal })
                player.Counters.MaterialsGathered++;
            else if (incoming is { Category: ItemCategory.Gemstone, Gem: { } gem })
                player.RecordGem(gem);
        }

        _battle.Overflow.RemoveAt(0);
        _overflowCursor = OverflowPopup.RowCount(player);

        if (_battle.Overflow.Count == 0)
            ctx.Screens.ChangeTo(_dungeonScreen);
    }

    private static readonly string[] MenuLabels = { "たたかう", "まほう", "アイテム", "にげる" };

    private void UpdateMenu(GameContext ctx, InputManager input)
    {
        if (MenuNav.Down(input)) _cursor = (_cursor + 1) % MenuLabels.Length;
        if (MenuNav.Up(input)) _cursor = (_cursor - 1 + MenuLabels.Length) % MenuLabels.Length;

        if (!MenuNav.Confirmed(input))
            return;

        switch ((MenuCommand)_cursor)
        {
            case MenuCommand.Attack:
                // Nothing to choose between when only one is left: asking which of one slime to hit is a
                // keypress that can only be answered one way.
                var alive = _battle.AliveEnemies;
                if (alive.Count == 1)
                {
                    AttackTarget(ctx, alive[0]);
                    break;
                }
                _phase = Phase.TargetSelect;
                _cursor = 0;
                break;
            case MenuCommand.Magic:
                if (_battle.Player.KnownSpells.Count == 0)
                {
                    _roundLog.Add("まほうを覚えていない");
                    ShowMessage(endsRound: false);
                    return;
                }
                _phase = Phase.SpellSelect;
                _cursor = 0;
                break;
            case MenuCommand.Item:
                if (UsableItems(ctx).Count == 0)
                {
                    _roundLog.Add("アイテム欄に何も装備していない");
                    ShowMessage(endsRound: false);
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
        if (MenuNav.Cancelled(input) || alive.Count == 0) { _phase = Phase.Menu; _cursor = 0; return; }

        // The slimes stand in a row, so left and right are what the hand reaches for. Up and down still work:
        // the cursor moved that way when this was a list, and there is no other meaning for them here.
        //
        // The list of living enemies shrinks as they are killed, so the cursor is clamped against it every
        // frame rather than only when a direction is pressed — that is what stops it pointing past the end of
        // a row that just got shorter.
        _cursor = Math.Clamp(_cursor, 0, alive.Count - 1);
        if (MenuNav.Right(input) || MenuNav.Down(input)) _cursor = (_cursor + 1) % alive.Count;
        if (MenuNav.Left(input) || MenuNav.Up(input)) _cursor = (_cursor - 1 + alive.Count) % alive.Count;

        if (MenuNav.Confirmed(input))
            AttackTarget(ctx, alive[_cursor]);
    }

    /// <summary>Swings at one slime. Shared by the target cursor and by the single-enemy shortcut.</summary>
    private void AttackTarget(GameContext ctx, Slime target)
    {
        ctx.Audio.Play(SoundId.WeaponHit);
        _pendingPlayerAction = () => _battle.PlayerAttack(target);
        ResolveRound();
    }

    private void UpdateSpellSelect(GameContext ctx, InputManager input)
    {
        var spells = _battle.Player.KnownSpells;
        if (MenuNav.Cancelled(input) || spells.Count == 0) { _phase = Phase.Menu; _cursor = 0; return; }
        _cursor = MenuNav.Move(input, _cursor, spells.Count);

        if (!MenuNav.Confirmed(input))
            return;

        var spell = spells[_cursor];
        if (SpellDefinitions.MpCost(spell.Rank) > _battle.Player.Stats.Mp)
        {
            _roundLog.Add("MPが足りない");
            ShowMessage(endsRound: false);
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
        // This one already carried a hand-written clamp, because the list visibly shrinks as items are used up
        // and someone had run off the end of it before. MenuNav.Move now does that for every menu in the game.
        if (MenuNav.Cancelled(input) || items.Count == 0) { _phase = Phase.Menu; _cursor = 0; return; }
        _cursor = MenuNav.Move(input, _cursor, items.Count);

        if (!MenuNav.Confirmed(input))
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

        if (_battle.BattleOver)
            _phase = Phase.BattleEnd;
        else if (_roundLog.Count > 0)
            ShowMessage(endsRound: true);
        else
            BeginRound();

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

    /// <summary>
    /// The two wall torches in the painted backdrop, measured off the artwork, and a radius a little wider
    /// than the flame so the light spills onto the brick around it.
    /// </summary>
    private static readonly (float X, float Y, float Radius, float Phase)[] PaintedTorches =
    [
        (61f, 96f, 17f, 0f),
        (344f, 96f, 17f, 2.4f),
    ];

    /// <summary>
    /// A wash of the dungeon's element over the painting. Very light — the point is that a fire dungeon and a
    /// water one do not look identical, not that either stops looking like the room it is. The procedural
    /// backdrop this replaced was built per-element from scratch, so dropping the cue entirely would have lost
    /// something the player uses: the element decides which of their spells is worth casting.
    /// </summary>
    private static SDL.Color ElementWash(Domain.Element? element) => element switch
    {
        Domain.Element.Fire => Colors.Rgb(255, 226, 214),
        Domain.Element.Water => Colors.Rgb(214, 230, 255),
        Domain.Element.Wind => Colors.Rgb(224, 246, 220),
        Domain.Element.Earth => Colors.Rgb(246, 234, 210),
        _ => Colors.White,
    };

    public void Draw(GameContext ctx)
    {
        var r = ctx.Renderer;
        r.Clear(Colors.Rgb(24, 18, 30));

        var areaW = 400f;
        var art = ctx.Sprites.BattleArtwork;
        if (art != IntPtr.Zero)
        {
            r.DrawTextureTinted(art, 0, 0, CombatArt.Width, CombatArt.Height, ElementWash(DungeonElement));
            foreach (var (tx, ty, radius, phase) in PaintedTorches)
                FlameGlow.Draw(r, ctx.Sprites.GlowSprite, tx, ty, radius, _time, phase, FlameGlow.Firelight);
        }
        else
        {
            r.DrawTexture(ctx.Sprites.CombatBackdrop(DungeonElement), 0, 0, CombatArt.Width, CombatArt.Height);
            foreach (var (tx, ty) in CombatArt.TorchPositions)
                TorchFlame.Draw(r, tx, ty - 6, _time, 0.8f);
        }

        DrawEnemies(ctx, areaW, art != IntPtr.Zero);

        // Top right, opposite the command panel: the foot of the screen belongs to the slimes and their gauges
        // now, and this used to sit right on top of them.
        if (_battle.PlayerPoisoned)
            ctx.Fonts.DrawText(r.Handle, "[毒]", areaW - 38, 12, 11, Colors.Rgb(190, 110, 220));

        switch (_phase)
        {
            case Phase.Menu:
                DrawMenu(ctx, MenuLabels, _cursor, "コマンド");
                break;
            case Phase.TargetSelect:
                DrawTargetCursor(ctx, areaW);
                DrawTargetPrompt(ctx);
                break;
            case Phase.SpellSelect:
                DrawMenu(ctx, _battle.Player.KnownSpells.Select(SpellMenuLabel).ToArray(), _cursor, $"まほう (MP {_battle.Player.Stats.Mp}/{_battle.Player.Stats.MaxMp})");
                break;
            case Phase.ItemSelect:
                DrawMenu(ctx, UsableItems(ctx).Select(i => $"{i.Name} x{i.Quantity}").ToArray(), _cursor, "アイテム");
                break;
            case Phase.Message:
                BattleMessagePopup.Draw(ctx, _roundLog, "続ける");
                break;
            case Phase.BattleEnd:
                var (msg, colour) = _battle.Player.Stats.IsDead
                    ? ("倒れてしまった…", Colors.HpBar)
                    : _battle.PlayerFled
                        ? ("戦闘から逃げ出した", Colors.Rgb(200, 196, 186))
                        : ("戦闘に勝利した！", Colors.Gold);
                BattleMessagePopup.DrawBanner(ctx, msg, colour, "続ける");
                break;
        }

        StatusPanel.Draw(ctx, areaW, 0, 400);

        // Modal, so they go on top of the status panel too. The tally comes first, then the celebration.
        if (_phase == Phase.BattleSummary)
            BattleSummaryPopup.Draw(ctx, _battle.Defeated, _battle.Escaped, _battle.ExpReward,
                _battle.MaterialsFound, _battle.GemsFound);
        else if (_phase == Phase.LevelUpSummary && _battle.LevelUp is { } levelUp)
            LevelUpPopup.Draw(ctx, levelUp);
        else if (_phase == Phase.Overflow && _battle.Overflow.Count > 0)
            OverflowPopup.Draw(ctx, _battle.Overflow[0], _overflowCursor);
    }

    // Enemy block layout. The sprite's feet land on the floor, with the name and the two gauges stacked
    // underneath. The painted backdrop's flagstones start much lower down the picture than the procedural
    // one's ground line, so the two have their own figures rather than sharing one.
    private const float SpriteSize = 48f;
    private const float PaintedGroundY = 326f;
    private const float GaugeLabelWidth = 10f;
    private const float GaugeBarWidth = 46f;
    private const float GaugeHeight = 7f;

    /// <summary>Gap between the bar and the maximum written beside it.</summary>
    private const float GaugeNumberGap = 4f;

    /// <summary>Where each living slime's centre line falls, left to right.</summary>
    private static float SlimeCentreX(int index, int count, float areaW) => areaW / (count + 1) * (index + 1);

    private void DrawEnemies(GameContext ctx, float areaW, bool painted)
    {
        var r = ctx.Renderer;
        var fonts = ctx.Fonts;
        var alive = _battle.AliveEnemies;

        var groundY = painted ? PaintedGroundY : CombatArt.GroundY;
        var spriteTop = groundY - SpriteSize;
        var labelY = groundY + 4f;
        var hpBarY = groundY + 18f;
        var mpBarY = groundY + 30f;

        var targeting = _phase == Phase.TargetSelect;

        for (var i = 0; i < alive.Count; i++)
        {
            var e = alive[i];
            var (idle, _) = ctx.Sprites.SlimeSprite(e);
            var center = SlimeCentreX(i, alive.Count, areaW);
            var selected = targeting && i == _cursor;

            r.DrawTexture(idle, center - SpriteSize / 2f, spriteTop, SpriteSize, SpriteSize);

            var (labelW, _) = fonts.Measure(e.DisplayLabel, 9);
            fonts.DrawText(r.Handle, e.DisplayLabel, center - labelW / 2f, labelY, 9,
                selected ? Colors.Highlight : Colors.White);

            DrawGauge(ctx, center, hpBarY, "H", e.Stats.Hp, e.Stats.MaxHp, Colors.HpBar);
            DrawGauge(ctx, center, mpBarY, "M", e.Stats.Mp, e.Stats.MaxMp, Colors.MpBar);
        }
    }

    /// <summary>
    /// One labelled gauge: the letter, the bar, and the maximum written beside it.
    ///
    /// It used to carry "12/40  6/6" on a line of its own underneath at 8pt, which at that size against a
    /// painted floor was barely legible and said twice over what the bar already shows. The bar is the current
    /// value; the only number it cannot convey is the scale it is measured against, so that is the only number
    /// printed.
    ///
    /// The <em>bar</em> is what is centred under the slime — not the label-bar-figure block. Centring the whole
    /// block let the width of the figure decide where the bar sat, so a slime with 6 MP and 10 HP had its two
    /// bars visibly out of line with each other, and no two slimes in a row agreed either. Bars that are meant
    /// to be compared at a glance have to start and end at the same place.
    /// </summary>
    private static void DrawGauge(GameContext ctx, float centreX, float y, string label, int current, int max, SDL.Color color)
    {
        var r = ctx.Renderer;
        var fonts = ctx.Fonts;

        var maxText = max.ToString();
        var (maxW, _) = fonts.Measure(maxText, 9);
        var barX = centreX - GaugeBarWidth / 2f;

        // A dark plate behind the lot: the flagstones under it are mottled, and grey-on-grey text at this size
        // disappears into them.
        var plateX = barX - GaugeLabelWidth - 2f;
        var plateW = GaugeLabelWidth + GaugeBarWidth + GaugeNumberGap + maxW + 4f;
        r.FillRect(plateX, y - 3, plateW, GaugeHeight + 6, Colors.Rgb(10, 10, 14, 190));

        fonts.DrawText(r.Handle, label, barX - GaugeLabelWidth, y - 2, 9, Colors.Rgb(186, 182, 176));

        r.FillRect(barX - 1, y - 1, GaugeBarWidth + 2, GaugeHeight + 2, Colors.Rgb(12, 12, 16));
        r.FillRect(barX, y, GaugeBarWidth, GaugeHeight, Colors.BarBg);

        // An almost-empty bar still shows a sliver, so "nearly dead" never looks like "dead".
        var frac = max > 0 ? Math.Clamp((float)current / max, 0f, 1f) : 0f;
        if (frac > 0)
            r.FillRect(barX, y, Math.Max(1f, GaugeBarWidth * frac), GaugeHeight, color);

        fonts.DrawText(r.Handle, maxText, barX + GaugeBarWidth + GaugeNumberGap, y - 2, 9,
            Colors.Rgb(224, 220, 212));
    }

    /// <summary>
    /// The attack cursor: the weapon in the player's hand, hanging over whichever slime is about to be hit.
    ///
    /// Picking a target used to mean reading a list of names in a panel and matching them back to the row of
    /// slimes on the floor — the game already draws the enemies, so making the player translate between the
    /// two was work for nothing. The icon points at the thing itself.
    /// </summary>
    private void DrawTargetCursor(GameContext ctx, float areaW)
    {
        var alive = _battle.AliveEnemies;
        if (alive.Count == 0)
            return;

        var index = Math.Clamp(_cursor, 0, alive.Count - 1);
        var centre = SlimeCentreX(index, alive.Count, areaW);
        var groundY = ctx.Sprites.BattleArtwork != IntPtr.Zero ? PaintedGroundY : CombatArt.GroundY;

        // Bobbing, because a cursor that does not move is easily read as part of the scenery.
        var bob = (float)Math.Sin(_time * 6.0) * 3f;
        const float size = 26f;
        var x = centre - size / 2f;
        var y = groundY - SpriteSize - size - 6f + bob;

        var weapon = _battle.Player.Equipment.GetValueOrDefault(EquipSlot.RightHand);
        var icon = weapon is not null ? ctx.Sprites.ItemIcon(weapon) : ctx.Sprites.HintIcon(HintIcon.Confirm);
        ctx.Renderer.DrawTexture(icon, x, y, size, size);
    }

    /// <summary>
    /// The command panel, hung from the top of the screen and only as wide as it needs to be.
    ///
    /// It used to be pinned across the bottom at a fixed 380 pixels, which was the full width of the battle
    /// area whether the list was "たたかう" or a single spell — and the bottom is now where the slimes stand.
    /// Hanging it from the ceiling puts it against the wall, where there is nothing to cover, and sizing it to
    /// its longest row keeps the room visible either side of it.
    /// </summary>
    /// <summary>
    /// Hung from the top left corner. It was briefly centred between the two wall torches to keep both of them
    /// visible, but a panel floating in the middle of the wall with nothing either side of it has nothing to
    /// sit against — a corner is where a menu belongs, and covering the left torch is the cheaper loss.
    /// </summary>
    private const float MenuLeft = 12f;
    private const float MenuTop = 10f;
    private const float MenuRowHeight = 20f;
    private const float MenuMinWidth = 100f;
    private const float MenuMaxWidth = 244f;
    private const float MenuPad = 10f;

    /// <summary>
    /// What the attack cursor is pointing at, in the corner the command list came from. Only a name and the
    /// controls — the choosing itself happens out on the floor now, so this is a caption, not a menu.
    /// </summary>
    private void DrawTargetPrompt(GameContext ctx)
    {
        var alive = _battle.AliveEnemies;
        if (alive.Count == 0)
            return;

        var r = ctx.Renderer;
        var fonts = ctx.Fonts;
        var target = alive[Math.Clamp(_cursor, 0, alive.Count - 1)];
        var name = $"{target.DisplayLabel} ({target.Rank.Label()})";

        ControlHint[] hints = [ControlHints.Direction("えらぶ"), ControlHints.Cancel("やめる")];
        var widest = Math.Max(fonts.Measure(name, 12).Item1, ControlHints.Width(ctx, 9, hints));
        var w = Math.Clamp(widest + MenuPad * 2 + 6f, MenuMinWidth, MenuMaxWidth);
        const float h = 62f;

        r.FillRect(MenuLeft + 3, MenuTop + 4, w, h, Colors.Rgb(4, 4, 8, 150));
        r.FillRect(MenuLeft, MenuTop, w, h, Colors.PanelBg);
        r.DrawRect(MenuLeft, MenuTop, w, h, Colors.Border);

        fonts.DrawText(r.Handle, "だれを攻撃する？", MenuLeft + MenuPad, MenuTop + 6, 10, Colors.Highlight);
        fonts.DrawText(r.Handle, name, MenuLeft + MenuPad, MenuTop + 23, 12, Colors.White);
        ControlHints.Draw(ctx, MenuLeft + MenuPad, MenuTop + 44, 9, Colors.Rgb(168, 160, 148), hints);
    }

    private void DrawMenu(GameContext ctx, IReadOnlyList<string> labels, int cursor, string title)
    {
        var r = ctx.Renderer;

        var widest = Math.Max(MenuNav.MaxLabelWidth(ctx, labels, 12), ctx.Fonts.Measure(title, 10).Item1);
        var w = Math.Clamp(widest + MenuPad * 2 + 6f, MenuMinWidth, MenuMaxWidth);
        var h = labels.Count * MenuRowHeight + 26f;
        const float x = MenuLeft;

        r.FillRect(x + 3, MenuTop + 4, w, h, Colors.Rgb(4, 4, 8, 150));
        r.FillRect(x, MenuTop, w, h, Colors.PanelBg);
        r.DrawRect(x, MenuTop, w, h, Colors.Border);

        ctx.Fonts.DrawText(r.Handle, title, x + MenuPad, MenuTop + 5, 10, Colors.Highlight);

        var rowWidth = w - MenuPad * 2;
        for (var i = 0; i < labels.Count; i++)
            MenuNav.DrawRow(ctx, x + MenuPad, MenuTop + 22 + i * MenuRowHeight, rowWidth, 18,
                labels[i], 12, i == cursor);
    }
}
