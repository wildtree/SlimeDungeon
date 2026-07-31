using SDL3;
using SlimeDungeon.Combat;
using SlimeDungeon.Core;
using SlimeDungeon.Domain;
using SlimeDungeon.Graphics;
using SlimeDungeon.UI;

namespace SlimeDungeon.Dungeon;

public sealed class DungeonScreen : IScreen
{
    public const int TileSize = SpriteFactory.TileSize;
    public const int Border = 8;
    public const float MapAreaWidth = DungeonMap.Size * TileSize + Border * 2;

    private readonly DungeonSession _session;
    private readonly IScreen _guildScreen;
    private (int Dx, int Dy)? _pendingContinue;
    private bool _spellMenuOpen;
    private int _spellCursor;

    public DungeonScreen(DungeonMap map, IScreen guildScreen)
    {
        _session = new DungeonSession(map);
        _guildScreen = guildScreen;
    }

    public void Update(GameContext ctx, float dt)
    {
        var player = ctx.Player!;
        var input = ctx.Input;

        if (_session.MessageTimer > 0)
        {
            _session.MessageTimer -= dt;
            if (_session.MessageTimer <= 0)
                _session.Message = null;
        }

        if (_session.FullMapRevealTimer > 0)
            _session.FullMapRevealTimer = Math.Max(0, _session.FullMapRevealTimer - dt);

        if (UpdateSlimes(ctx, dt))
            return;

        if (_spellMenuOpen)
        {
            UpdateSpellMenu(ctx);
            return;
        }

        if (input.WasPressed(SDL.Keycode.C))
        {
            if (FieldUsableSpells(player).Count > 0)
            {
                _spellMenuOpen = true;
                _spellCursor = 0;
            }
            else
            {
                _session.ShowMessage("使えるまほうがない");
            }
            return;
        }

        if (input.WasPressed(SDL.Keycode.M))
        {
            var reveal = player.Bag.FirstOrDefault(i => i.Category == ItemCategory.FullMapReveal);
            if (reveal is not null)
            {
                player.Bag.Remove(reveal);
                _session.FullMapRevealTimer = DungeonSession.FullMapRevealSeconds;
                _session.ShowMessage("ダンジョン全体が見える！");
            }
        }

        if (input.WasPressed(SDL.Keycode.Space) && _session.IsOnStairs)
        {
            player.DayCount++;
            ctx.Screens.ChangeTo(_guildScreen);
            return;
        }

        if (_session.MoveCooldown > 0)
        {
            _session.MoveCooldown -= dt;
            return;
        }

        if (_pendingContinue is { } pending)
        {
            ContinueMove(pending.Dx, pending.Dy);
            return;
        }

        var (dx, dy, dir) = ReadDirection(input);
        if (dir is null)
            return;

        _session.Facing = dir.Value;
        BeginMove(ctx, dx, dy);
    }

    private static (int Dx, int Dy, Direction? Dir) ReadDirection(InputManager input)
    {
        // Accepts both a held key (continuous movement) and a bare press-edge, so a single
        // quick tap still advances one half-step even if its key-up lands in the same poll batch.
        bool Held(SDL.Keycode k) => input.IsDown(k) || input.WasPressed(k);

        if (Held(SDL.Keycode.Up)) return (0, -1, Direction.Up);
        if (Held(SDL.Keycode.Down)) return (0, 1, Direction.Down);
        if (Held(SDL.Keycode.Left)) return (-1, 0, Direction.Left);
        if (Held(SDL.Keycode.Right)) return (1, 0, Direction.Right);
        return (0, 0, null);
    }

    private const int SlimeChaseRadius = 4;

    /// <summary>
    /// Ticks every roaming slime's own movement timer: each wanders randomly on its own schedule until
    /// the player comes within sight and chase range, at which point it steps straight toward the player.
    /// Returns true if a slime just walked into the player, so the caller can bail out into combat.
    /// </summary>
    private bool UpdateSlimes(GameContext ctx, float dt)
    {
        var map = _session.Map;
        var playerTileX = _session.TileX;
        var playerTileY = _session.TileY;
        var (destX, destY) = PlayerDestinationTile();
        var rnd = RandomUtil.Shared;

        // Safety net: if the player and a slime have ended up sharing a tile by any route at all, resolve it
        // into a fight now. Without this, a pair that slipped past the checks below could stand on the same
        // square indefinitely, which is exactly the "walked onto a slime and nothing happened" symptom.
        var shared = map.SlimeAt(playerTileX, playerTileY);
        if (shared is not null)
        {
            map.Slimes.Remove(shared);
            StartEncounter(ctx, shared.Slime);
            return true;
        }

        foreach (var slime in map.Slimes.ToList())
        {
            if (slime.HopFrameTimer > 0)
            {
                slime.HopFrameTimer -= dt;
                if (slime.HopFrameTimer <= 0)
                    slime.HopFrame = false;
            }

            slime.MoveTimer -= dt;
            if (slime.MoveTimer > 0)
                continue;

            slime.MoveTimer = (float)(rnd.NextDouble() * 1.6 + 2.0);

            var (dx, dy) = ChooseSlimeMove(map, slime, playerTileX, playerTileY, rnd);
            if (dx == 0 && dy == 0)
                continue;

            var nx = slime.X + dx;
            var ny = slime.Y + dy;

            // A move already in flight reserves its destination tile. The player's own tile index does not
            // advance until the second half-step lands, so a slime could otherwise step onto the square the
            // player was walking into, and they would end up stacked with no encounter ever firing.
            if ((nx == playerTileX && ny == playerTileY) || (nx == destX && ny == destY))
            {
                map.Slimes.Remove(slime);
                StartEncounter(ctx, slime.Slime);
                return true;
            }

            if (map.IsWall(nx, ny) || map.SlimeAt(nx, ny) is not null)
                continue;

            slime.X = nx;
            slime.Y = ny;
            slime.HopFrame = true;
            slime.HopFrameTimer = 0.2f;
        }

        return false;
    }

    /// <summary>
    /// The tile the player will stand on once the current move finishes — their own tile when standing still.
    /// Derived from the half-step position rather than <c>TileX + dx</c>: because the tile index is
    /// <c>HalfX / 2</c>, a half-step right still reads as the old tile while a half-step left already reads as
    /// the new one, so adding the direction would give the wrong answer for leftward and upward moves.
    /// </summary>
    private (int X, int Y) PlayerDestinationTile()
    {
        if (_pendingContinue is not { } pending)
            return (_session.TileX, _session.TileY);
        return ((_session.HalfX + pending.Dx) / 2, (_session.HalfY + pending.Dy) / 2);
    }

    private static (int Dx, int Dy) ChooseSlimeMove(DungeonMap map, RoamingSlime slime, int playerTileX, int playerTileY, Random rnd)
    {
        var dxToPlayer = playerTileX - slime.X;
        var dyToPlayer = playerTileY - slime.Y;
        var dist2 = dxToPlayer * dxToPlayer + dyToPlayer * dyToPlayer;

        if (dist2 <= SlimeChaseRadius * SlimeChaseRadius && FieldOfView.HasLineOfSight(map, slime.X, slime.Y, playerTileX, playerTileY))
        {
            // Greedy chase: step along whichever axis has the larger gap; tie-break randomly.
            if (Math.Abs(dxToPlayer) > Math.Abs(dyToPlayer))
                return (Math.Sign(dxToPlayer), 0);
            if (Math.Abs(dyToPlayer) > Math.Abs(dxToPlayer))
                return (0, Math.Sign(dyToPlayer));
            return rnd.Next(2) == 0 ? (Math.Sign(dxToPlayer), 0) : (0, Math.Sign(dyToPlayer));
        }

        // Idle wandering: mostly stand still, occasionally shuffle one step in a random direction.
        if (rnd.NextDouble() < 0.4)
            return (0, 0);

        return rnd.Next(4) switch
        {
            0 => (0, -1),
            1 => (0, 1),
            2 => (-1, 0),
            _ => (1, 0),
        };
    }

    /// <summary>
    /// Starts a move from an aligned (full-tile) position. Looks ahead at the destination tile first,
    /// so a move only ever begins if it can run all the way through — the player never gets stuck
    /// resting at a half-step. Interactions (chest/slime) fire immediately without moving onto the tile.
    /// </summary>
    private void BeginMove(GameContext ctx, int dx, int dy)
    {
        var targetTileX = _session.TileX + dx;
        var targetTileY = _session.TileY + dy;

        if (targetTileX < 0 || targetTileY < 0 || targetTileX >= DungeonMap.Size || targetTileY >= DungeonMap.Size)
            return;

        if (_session.Map.IsWall(targetTileX, targetTileY))
            return;

        var chest = _session.Map.ChestAt(targetTileX, targetTileY);
        if (chest is not null && !chest.Opened)
        {
            if (!ctx.Player!.BagHasRoom)
            {
                _session.ShowMessage("荷物がいっぱいです");
                return;
            }
            OpenChest(ctx, chest);
            return;
        }

        var slime = _session.Map.SlimeAt(targetTileX, targetTileY);
        if (slime is not null)
        {
            _session.Map.Slimes.Remove(slime);
            StartEncounter(ctx, slime.Slime);
            return;
        }

        _session.HalfX += dx;
        _session.HalfY += dy;
        _session.Frame = _session.Frame == WalkFrame.A ? WalkFrame.B : WalkFrame.A;
        _session.MoveCooldown = DungeonSession.SecondsPerHalfStep;
        _pendingContinue = (dx, dy);
    }

    /// <summary>Finishes a move already committed to in <see cref="BeginMove"/> — always reaches the next full tile.</summary>
    private void ContinueMove(int dx, int dy)
    {
        _session.HalfX += dx;
        _session.HalfY += dy;
        _session.Frame = _session.Frame == WalkFrame.A ? WalkFrame.B : WalkFrame.A;
        _session.MoveCooldown = DungeonSession.SecondsPerHalfStep;
        _pendingContinue = null;
        _session.Fov.Recompute(_session.Map, _session.TileX, _session.TileY);
    }

    /// <summary>Spells usable outside of battle — currently just Heal (attack spells need a target, Cure has nothing to cure without an active battle's poison).</summary>
    private static List<LearnedSpell> FieldUsableSpells(Player player) =>
        player.KnownSpells.Where(s => SpellDefinitions.All[s.Id].Effect == SpellEffect.Heal).ToList();

    private void UpdateSpellMenu(GameContext ctx)
    {
        var player = ctx.Player!;
        var input = ctx.Input;
        var spells = FieldUsableSpells(player);

        if (MenuNav.Cancelled(input) || spells.Count == 0)
        {
            _spellMenuOpen = false;
            return;
        }

        _spellCursor = MenuNav.Move(input, _spellCursor, spells.Count);

        if (!MenuNav.Confirmed(input))
            return;

        var spell = spells[_spellCursor];
        var cost = SpellDefinitions.MpCost(spell.Rank);
        if (player.Stats.Mp < cost)
        {
            _session.ShowMessage("MPが足りない");
            _spellMenuOpen = false;
            return;
        }

        player.Stats.Mp -= cost;
        player.Counters.SpellsCast++;
        var amount = SpellDefinitions.HealAmount(spell.Rank, player.Stats.MaxHp);
        player.Stats.Hp = Math.Min(player.Stats.MaxHp, player.Stats.Hp + amount);
        _session.ShowMessage($"{SpellDefinitions.NameOf(spell.Id)}！ HPが{amount}回復した");
        _spellMenuOpen = false;
    }

    private void DrawSpellMenu(GameContext ctx)
    {
        var r = ctx.Renderer;
        var player = ctx.Player!;
        var spells = FieldUsableSpells(player);
        var labels = spells.Select(s => $"{SpellDefinitions.NameOf(s.Id)} (MP{SpellDefinitions.MpCost(s.Rank)})").ToArray();
        var maxWidth = MenuNav.MaxLabelWidth(ctx, labels, 12);

        var x = 12f;
        var y = 300f;
        var h = 20 * labels.Length + 20;
        r.FillRect(x - 4, y - 16, maxWidth + 40, h, Colors.PanelBg);
        r.DrawRect(x - 4, y - 16, maxWidth + 40, h, Colors.Border);
        ctx.Fonts.DrawText(r.Handle, "まほう", x, y - 14, 10, Colors.Highlight);

        for (var i = 0; i < labels.Length; i++)
            MenuNav.DrawRow(ctx, x, y + 6 + i * 20, maxWidth, 18, labels[i], 12, i == _spellCursor);
    }

    private void OpenChest(GameContext ctx, Chest chest)
    {
        var player = ctx.Player!;
        chest.Opened = true;

        if (chest.IsMimic)
        {
            _session.ShowMessage("宝箱の中からスライムが飛び出した！");
            var mimic = Slime.Create(SlimeColor.Gold, _session.Map.DungeonRank, _session.Map.DungeonElement);
            StartFixedEncounter(ctx, new List<Slime> { mimic });
            return;
        }

        player.Counters.ChestsOpened++;

        // The bag-has-room check happens before OpenChest is ever called (see BeginMove), so every
        // item here is guaranteed to fit.
        var parts = new List<string>();
        if (chest.Gold > 0)
        {
            player.EarnGold(chest.Gold);
            parts.Add($"{chest.Gold}G");
        }

        foreach (var item in chest.Items)
        {
            player.Bag.Add(item);
            parts.Add(item.Name);
        }

        _session.ShowMessage(parts.Count > 0 ? $"宝箱: {string.Join(", ", parts)}" : "宝箱は空だった");

        chest.Gold = 0;
        chest.Items.Clear();
    }

    /// <summary>Rank H dungeons cap packs at 2 slimes instead of 4 — a brand-new character still can't
    /// safely handle a full-size swarm even with the turn-order and damage adjustments already in place.</summary>
    private const int HRankDungeonMaxPackSize = 2;
    private const int MaxPackSize = 4;

    /// <summary>Starts an encounter that must include the exact slime the player bumped into on the map
    /// (so the map icon's color always matches what shows up in battle), plus rolled companions to fill the pack.</summary>
    private void StartEncounter(GameContext ctx, Slime primarySlime)
    {
        var rnd = RandomUtil.Shared;
        var r = rnd.NextGaussian(0, 2);
        var maxPackSize = _session.Map.DungeonRank == Rank.H ? HRankDungeonMaxPackSize : MaxPackSize;
        var count = Math.Min((int)Math.Abs(r) + 1, maxPackSize);

        var slimes = new List<Slime> { primarySlime };
        for (var i = 1; i < count; i++)
        {
            var color = Slime.RollColor(_session.Map.DungeonElement);
            slimes.Add(Slime.Create(color, _session.Map.DungeonRank, _session.Map.DungeonElement));
        }

        StartFixedEncounter(ctx, slimes);
    }

    private void StartFixedEncounter(GameContext ctx, List<Slime> slimes)
    {
        ctx.Screens.ChangeTo(new CombatScreen(slimes, this, _session.Map.DungeonElement, _session.Map.DungeonRank));
    }

    public void Draw(GameContext ctx)
    {
        var r = ctx.Renderer;
        r.Clear(Colors.DungeonBg);

        DrawBorder(r);
        DrawTiles(ctx);
        DrawPlayer(ctx);

        StatusPanel.Draw(ctx, MapAreaWidth, 0, 400);

        if (_session.Message is not null)
        {
            r.FillRect(4, 400 - 20, MapAreaWidth - 8, 18, Colors.Rgb(0, 0, 0, 180));
            ctx.Fonts.DrawText(r.Handle, _session.Message, 8, 400 - 19, 11, Colors.White);
        }

        if (_session.FullMapRevealTimer > 0)
            ctx.Fonts.DrawText(r.Handle, $"全体表示 {_session.FullMapRevealTimer:0.0}s", 8, 4, 10, Colors.Highlight);

        if (_spellMenuOpen)
            DrawSpellMenu(ctx);
    }

    private void DrawBorder(Renderer r)
    {
        r.FillRect(0, 0, MapAreaWidth, Border, Colors.Border);
        r.FillRect(0, DungeonMap.Size * TileSize + Border, MapAreaWidth, Border, Colors.Border);
        r.FillRect(0, 0, Border, DungeonMap.Size * TileSize + Border * 2, Colors.Border);
        r.FillRect(DungeonMap.Size * TileSize + Border, 0, Border, DungeonMap.Size * TileSize + Border * 2, Colors.Border);
    }

    private void DrawTiles(GameContext ctx)
    {
        var r = ctx.Renderer;
        var map = _session.Map;
        var sprites = ctx.Sprites;
        var revealAll = _session.FullMapRevealTimer > 0;
        var (wallTex, floorTex, stairsTex) = sprites.TileSet(map.DungeonElement);

        for (var x = 0; x < DungeonMap.Size; x++)
        {
            for (var y = 0; y < DungeonMap.Size; y++)
            {
                var visible = revealAll || _session.Fov.IsVisible(x, y);
                var seen = revealAll || _session.Fov.WasSeen(x, y);
                if (!seen)
                    continue;

                var px = Border + x * TileSize;
                var py = Border + y * TileSize;

                var isStairs = (x, y) == map.StairsPos;
                var tex = map.Tiles[x, y] == TileType.Wall ? wallTex : isStairs ? stairsTex : floorTex;
                r.DrawTexture(tex, px, py, TileSize, TileSize);

                var chest = map.ChestAt(x, y);
                if (chest is not null)
                    r.DrawTexture(chest.Opened ? sprites.ChestOpen : sprites.ChestClosed, px, py, TileSize, TileSize);

                if (!visible)
                    r.FillRect(px, py, TileSize, TileSize, Colors.Rgb(0, 0, 0, 150));

                if (visible)
                {
                    var slime = map.SlimeAt(x, y);
                    if (slime is not null)
                    {
                        var (idle, hop) = sprites.Slime(slime.Slime.Color);
                        r.DrawTexture(slime.HopFrame ? hop : idle, px, py, TileSize, TileSize);
                    }
                }
            }
        }

        for (var x = 0; x < DungeonMap.Size; x++)
        {
            for (var y = 0; y < DungeonMap.Size; y++)
            {
                if (!(revealAll || _session.Fov.WasSeen(x, y)))
                {
                    var px = Border + x * TileSize;
                    var py = Border + y * TileSize;
                    r.FillRect(px, py, TileSize, TileSize, Colors.Black);
                }
            }
        }
    }

    private void DrawPlayer(GameContext ctx)
    {
        var player = ctx.Player!;
        var tex = ctx.Sprites.PlayerSprite(player.Gender, _session.Facing, _session.Frame);
        var px = Border + _session.HalfX * (TileSize / 2);
        var py = Border + _session.HalfY * (TileSize / 2);
        ctx.Renderer.DrawTexture(tex, px, py, TileSize, TileSize);
    }
}
