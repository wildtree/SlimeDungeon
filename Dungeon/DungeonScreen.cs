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
    private readonly IScreen _exitScreen;
    private (int Dx, int Dy)? _pendingContinue;
    private readonly FieldMagicMenu _magic = new();

    /// <summary>The field menu: the same entries the guild counter carries at its foot.</summary>
    private enum MenuEntry { Items, Equipment, KillLog, Magic, Options }

    private static readonly MenuEntry[] MenuEntries =
        [MenuEntry.Items, MenuEntry.Equipment, MenuEntry.KillLog, MenuEntry.Magic, MenuEntry.Options];
    private bool _menuOpen;
    private int _menuCursor;

    /// <summary>The haul from the chest just opened, held while its dialog is up. Null when none is showing.</summary>
    private (int Gold, List<Item> Items)? _chestHaul;

    /// <summary>
    /// A chest standing open-lidded in front of a full bag, waiting on an answer about what to throw away.
    /// Nothing has been taken from it yet: declining leaves it untouched and still closed.
    /// </summary>
    private Chest? _chestSwap;
    private int _chestSwapCursor;

    public DungeonScreen(DungeonMap map, IScreen exitScreen)
    {
        _session = new DungeonSession(map);
        _exitScreen = exitScreen;
    }

    /// <summary>
    /// Publishes the map-reveal action for as long as this screen is the one being explored, so a map scroll
    /// can be used from the inventory like any other consumable. Combat swaps this screen out and back, which
    /// tears the hook down and rebuilds it — the scroll is correctly unusable while a fight is on.
    /// </summary>
    public void OnEnter(GameContext ctx) => ctx.RevealFullMap = RevealMap;

    public void OnExit(GameContext ctx) => ctx.RevealFullMap = null;

    private void RevealMap()
    {
        _session.FullMapRevealTimer = DungeonSession.FullMapRevealSeconds;
        _session.ShowMessage("ダンジョン全体が見える！");
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

        // Both chest dialogs are modal: nothing moves, and no slime takes a step, until they are answered.
        if (_chestSwap is not null)
        {
            UpdateChestSwap(ctx, input);
            return;
        }

        if (_chestHaul is not null)
        {
            if (MenuNav.Confirmed(input) || MenuNav.Cancelled(input))
                _chestHaul = null;
            return;
        }

        // Everything that puts something on screen to be answered stops the floor with it. The chest dialogs
        // above always did; the field menu and the spell list did not, so slimes went on walking — and closing
        // in — while the player was reading a list they had opened themselves. The item and record overlays
        // never had the problem, because the main loop stops calling this screen entirely while one is up.
        if (_magic.IsOpen)
        {
            if (_magic.Update(ctx) is { } spellMessage)
                _session.ShowMessage(spellMessage);
            return;
        }

        if (_menuOpen)
        {
            UpdateMenu(ctx);
            return;
        }

        // Below the modals with the slimes, because a map scroll's few seconds of sight are part of the floor's
        // clock too — reading the item list should not burn them. The message timer above is left where it is:
        // that is a line of text fading, not something happening in the dungeon.
        if (_session.FullMapRevealTimer > 0)
            _session.FullMapRevealTimer = Math.Max(0, _session.FullMapRevealTimer - dt);

        if (UpdateSlimes(ctx, dt))
            return;

        // Reachable only out here: the chest dialog above and combat (a different screen entirely) both
        // return before this line, which is what keeps the menu shut while either is up.
        if (MenuNav.MenuRequested(input))
        {
            _menuOpen = true;
            _menuCursor = 0;
            return;
        }

        // Kept as a shortcut, but the scroll is now usable from the inventory as well — it used to be reachable
        // only through this key, which nothing on screen mentions, while the item screen offered only "捨てる".
        if (input.WasPressed(SDL.Keycode.M))
        {
            var reveal = player.CarriedItems.FirstOrDefault(i => i.Category == ItemCategory.FullMapReveal);
            if (reveal is not null)
            {
                player.ConsumeOne(reveal);
                RevealMap();
            }
        }

        if (MenuNav.Confirmed(input) && _session.IsOnStairs)
        {
            player.DayCount++;
            RecordClear(player);
            // Written out here rather than left to the destination. The guild used to save on arrival and the
            // stairs always led to the guild; they lead back to the dungeon entrance now, so without this a
            // whole trip's loot and levels would be lost by quitting from anywhere but the counter.
            Data.SaveManager.Save(player);
            ctx.Screens.ChangeTo(_exitScreen);
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
        bool PadHeld(SDL.GamepadButton b) => input.IsDown(b) || input.WasPressed(b);

        if (Held(SDL.Keycode.Up) || PadHeld(InputManager.DpadUp)) return (0, -1, Direction.Up);
        if (Held(SDL.Keycode.Down) || PadHeld(InputManager.DpadDown)) return (0, 1, Direction.Down);
        if (Held(SDL.Keycode.Left) || PadHeld(InputManager.DpadLeft)) return (-1, 0, Direction.Left);
        if (Held(SDL.Keycode.Right) || PadHeld(InputManager.DpadRight)) return (1, 0, Direction.Right);

        // The stick, for players who never think to reach for the d-pad.
        var (sx, sy) = input.ReadStickDirection();
        if (sy < 0) return (0, -1, Direction.Up);
        if (sy > 0) return (0, 1, Direction.Down);
        if (sx < 0) return (-1, 0, Direction.Left);
        if (sx > 0) return (1, 0, Direction.Right);

        return (0, 0, null);
    }

    private const int SlimeChaseRadius = 4;

    /// <summary>How far the tremble throws the sprite at its peak, in pixels of a 32px tile.</summary>
    private const float ShiverAmplitude = 2.5f;

    /// <summary>
    /// Radians per second of the tremble — about 9Hz, which is the rate that reads as shivering rather than as
    /// either a slow rock or an unreadable blur.
    /// </summary>
    private const float ShiverRate = 60f;

    /// <summary>
    /// How far left or right to draw a slime that is about to move. Read straight off its move timer rather
    /// than kept as its own animation state: the countdown already says exactly how close the step is, and a
    /// second clock would only be something else to keep in sync with it.
    ///
    /// A slime whose chosen step turns out to be blocked shivers and then stays put. That is left alone on
    /// purpose — it looks like something bracing itself against a wall, which is closer to the truth than a
    /// slime that gives no sign at all.
    /// </summary>
    private static float ShiverOffset(RoamingSlime slime)
    {
        var progress = slime.ShiverProgress;
        if (progress <= 0)
            return 0f;

        var into = progress * RoamingSlime.ShiverSeconds;
        return (float)Math.Sin(into * ShiverRate) * ShiverAmplitude * progress;
    }

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

            slime.MoveTimer = RoamingSlime.NextDelay(rnd);

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
            // A full bag used to simply refuse, which meant walking all the way back with no idea what was in
            // it. Now it asks what to throw away — and if the answer is "nothing", the lid goes back down and
            // the chest is exactly as it was, to be come back for later.
            if (!ctx.Player!.BagHasRoom && chest.Items.Count > 0)
            {
                _chestSwap = chest;
                _chestSwapCursor = OverflowPopup.RowCount(ctx.Player!) - 1;
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
    private void UpdateMenu(GameContext ctx)
    {
        var input = ctx.Input;
        var player = ctx.Player!;

        if (MenuNav.Cancelled(input) || MenuNav.MenuRequested(input))
        {
            _menuOpen = false;
            return;
        }

        _menuCursor = MenuNav.Move(input, _menuCursor, MenuEntries.Length);
        if (!MenuNav.Confirmed(input))
            return;

        switch (MenuEntries[_menuCursor])
        {
            case MenuEntry.Items:
                ctx.ShowItems = true;
                _menuOpen = false;
                break;
            case MenuEntry.Equipment:
                ctx.ShowEquipment = true;
                _menuOpen = false;
                break;
            case MenuEntry.KillLog:
                ctx.ShowKillLog = true;
                _menuOpen = false;
                break;
            case MenuEntry.Magic:
                _menuOpen = false;
                if (!_magic.TryOpen(player, out var reason))
                    _session.ShowMessage(reason);
                break;
            case MenuEntry.Options:
                ctx.ShowOptions = true;
                _menuOpen = false;
                break;
        }
    }

    private static string MenuLabel(MenuEntry entry) => entry switch
    {
        MenuEntry.Items => "アイテム",
        MenuEntry.Equipment => "装備",
        MenuEntry.KillLog => "討伐記録",
        MenuEntry.Magic => "まほう",
        _ => "オプション",
    };

    private void DrawMenu(GameContext ctx)
    {
        var r = ctx.Renderer;
        var labels = MenuEntries.Select(MenuLabel).ToArray();
        var maxWidth = MenuNav.MaxLabelWidth(ctx, labels, 13);

        var w = maxWidth + 40;
        var h = labels.Length * 22 + 30;
        var x = 12f;
        var y = 396f - h;

        r.FillRect(x + 3, y + 3, w, h, Colors.Rgb(8, 7, 10));
        r.FillRect(x, y, w, h, Colors.PanelBg);
        r.DrawRect(x, y, w, h, Colors.Border);
        ctx.Fonts.DrawText(r.Handle, "メニュー", x + 10, y + 5, 10, Colors.Highlight);

        for (var i = 0; i < labels.Length; i++)
            MenuNav.DrawRow(ctx, x + 14, y + 24 + i * 22, maxWidth, 19, labels[i], 13, i == _menuCursor);
    }


    /// <summary>
    /// The answer to "your bag is full — throw something away?" in front of a chest. Cancelling and choosing
    /// "give it up" do the same thing, because they mean the same thing: the chest is left closed and full.
    /// </summary>
    private void UpdateChestSwap(GameContext ctx, InputManager input)
    {
        var player = ctx.Player!;
        var chest = _chestSwap!;

        if (MenuNav.Cancelled(input))
        {
            _chestSwap = null;
            _session.ShowMessage("宝箱はそのままにしておいた");
            return;
        }

        _chestSwapCursor = MenuNav.Move(input, _chestSwapCursor, OverflowPopup.RowCount(player));
        if (!MenuNav.Confirmed(input))
            return;

        if (OverflowPopup.Chosen(player, _chestSwapCursor) is not { } discarded)
        {
            // Gave it up. The chest keeps its contents and its closed lid, so it can be opened another time.
            _chestSwap = null;
            _session.ShowMessage("宝箱はそのままにしておいた");
            return;
        }

        player.Bag.Remove(discarded);
        _chestSwap = null;
        OpenChest(ctx, chest);
    }

    /// <summary>
    /// Counts a floor taken apart completely — every chest opened, mimics included, and then out by the stairs.
    ///
    /// Rank is compared against the adventurer's own at the moment they leave, which is what makes the two
    /// counters mean opposite things: going up is nerve, going down is caution. A dungeon of your own rank is
    /// neither and counts for neither. An empty floor with no chests on it is vacuously swept, which is correct
    /// — there was nothing left behind.
    /// </summary>
    private void RecordClear(Player player)
    {
        if (!_session.Map.Chests.All(c => c.Opened))
            return;

        var rank = _session.Map.DungeonRank;
        if (rank > player.Rank)
            player.Counters.HigherRankDungeonsCleared++;
        else if (rank < player.Rank)
            player.Counters.LowerRankDungeonsCleared++;
    }

    private void OpenChest(GameContext ctx, Chest chest)
    {
        var player = ctx.Player!;

        if (chest.IsMimic)
        {
            // A mimic is spent the moment it springs, whatever the bag looks like.
            chest.Opened = true;

            // No lid sound for a mimic — the encounter sting is the whole point of the surprise.
            _session.ShowMessage("宝箱の中からスライムが飛び出した！");
            var mimic = Slime.Create(SlimeColor.Gold, _session.Map.DungeonRank, _session.Map.DungeonElement);
            StartFixedEncounter(ctx, new List<Slime> { mimic });
            return;
        }

        ctx.Audio.Play(SoundId.ChestOpen);
        player.Counters.ChestsOpened++;

        var (gold, taken) = chest.TakeInto(player);

        // Shown as a dialog rather than a line in the corner: this is the payoff for the whole detour, and a
        // chest that turns out to be empty deserves to be read as clearly as one that is not.
        _chestHaul = (gold, taken);

        if (chest.Items.Count > 0)
            _session.ShowMessage($"鞄がいっぱいで{chest.Items.Count}個残した");
    }

    /// <summary>Rank H dungeons cap packs at 2 slimes instead of 4 — a brand-new character still can't
    /// safely handle a full-size swarm even with the turn-order and damage adjustments already in place.</summary>
    private const int HRankDungeonMaxPackSize = 2;
    private const int MaxPackSize = 4;

    /// <summary>Starts an encounter that must include the exact slime the player bumped into on the map
    /// (so the map icon's color always matches what shows up in battle), plus rolled companions to fill the pack.</summary>
    private void StartEncounter(GameContext ctx, Slime primarySlime)
    {
        // Nothing keeps a dragon slime company. It is always exactly one fight, on its own terms.
        if (primarySlime.IsDragon)
        {
            StartFixedEncounter(ctx, new List<Slime> { primarySlime });
            return;
        }

        var rnd = RandomUtil.Shared;
        var r = rnd.NextGaussian(0, 2);
        var maxPackSize = _session.Map.DungeonRank == Rank.H ? HRankDungeonMaxPackSize : MaxPackSize;
        var count = Math.Min((int)Math.Abs(r) + 1, maxPackSize);

        var slimes = new List<Slime> { primarySlime };
        for (var i = 1; i < count; i++)
        {
            var (color, gem) = Slime.Roll(_session.Map.DungeonElement, _session.Map.DungeonRank);
            var companion = Slime.Create(color, _session.Map.DungeonRank, _session.Map.DungeonElement, gem);
            slimes.Add(companion);
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

        if (_menuOpen)
            DrawMenu(ctx);
        if (_magic.IsOpen)
            _magic.Draw(ctx, 12, 240);

        // Modal, so these draw over the status panel as well as the map.
        if (_chestSwap is { Items.Count: > 0 } swapping)
            OverflowPopup.Draw(ctx, swapping.Items[0], _chestSwapCursor, OverflowPopup.Source.Chest);
        else if (_chestHaul is { } haul)
            ChestPopup.Draw(ctx, haul.Gold, haul.Items);
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
                        var (idle, hop) = sprites.SlimeSprite(slime.Slime);
                        r.DrawTexture(slime.HopFrame ? hop : idle, px + ShiverOffset(slime), py,
                            TileSize, TileSize);
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
