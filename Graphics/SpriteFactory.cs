using SDL3;
using SlimeDungeon.Core;
using SlimeDungeon.Domain;

namespace SlimeDungeon.Graphics;

/// <summary>
/// Builds every 32x32 sprite texture the game uses, procedurally, once at startup.
/// There are no external image assets — everything below is pixel art authored in code.
/// </summary>
public sealed class SpriteFactory : IDisposable
{
    public const int TileSize = 32;

    public const int GuildBackdropSize = 400;

    /// <summary>Drop a 400x400 illustration here to replace the procedurally drawn guild room.</summary>
    public const string GuildArtFile = "guild.png";

    /// <summary>The graveyard shown when a character dies. Optional, like the guild room.</summary>
    public const string RipArtFile = "rip.png";

    /// <summary>
    /// The three shops the player can walk into. Square illustrations like the guild's, drawn in the same
    /// 400x400 slot to the left of the status panel, and equally optional — without them the screens fall
    /// back to the plain panelled room they used before.
    /// </summary>
    public const string ShopArtFile = "shop.png";
    public const string SmithArtFile = "smith.png";
    public const string PharmacyArtFile = "pharmacy.png";

    public const int MenuBackdropWidth = 640;
    public const int MenuBackdropHeight = 400;

    public IntPtr ChestClosed { get; private set; }
    public IntPtr ChestOpen { get; private set; }
    public IntPtr FullMapItem { get; private set; }
    public IntPtr GuildBackdrop { get; private set; }

    /// <summary>
    /// True when the guild room is a loaded illustration rather than the procedural fallback. The overlaid
    /// lettering (the carved sign, the date slate) is positioned against the procedural art's fixtures, so it
    /// is suppressed when a painting has replaced them — a hand-drawn room draws its own signage.
    /// </summary>
    public bool GuildBackdropIsArtwork { get; private set; }

    /// <summary>A plain wood-panelled room, used behind every flat menu screen (shop, quest board, potion
    /// crafting, dungeon select, inventory/kill-log overlays) so they read as a room instead of a black void.</summary>
    public IntPtr MenuBackdrop { get; private set; }

    /// <summary>The "SLIME DUNGEON" wordmark and the dungeon-entrance scene behind it, both title-screen only.</summary>
    public IntPtr TitleLogo { get; private set; }
    public IntPtr TitleBackdrop { get; private set; }

    /// <summary>The graveyard behind the death screen, or zero if no artwork was supplied.</summary>
    public IntPtr RipBackdrop { get; private set; }

    /// <summary>The shop interiors, or zero where the file is missing.</summary>
    public IntPtr ShopBackdrop { get; private set; }
    public IntPtr SmithBackdrop { get; private set; }
    public IntPtr PharmacyBackdrop { get; private set; }

    /// <summary>Small markers for the two kinds of guild work, so the quest board can be scanned by shape.</summary>
    public IntPtr QuestGatherIcon { get; private set; }
    public IntPtr QuestSlayIcon { get; private set; }

    /// <summary>Stamped wax seal that carries the rank letter on the guild card.</summary>
    public IntPtr RankSeal { get; private set; }

    private readonly Dictionary<HintIcon, IntPtr> _hintIcons = new();

    /// <summary>The glyph for one of the four controls, for <see cref="UI.ControlHints"/>.</summary>
    public IntPtr HintIcon(HintIcon icon) => _hintIcons[icon];

    private readonly Dictionary<Element, (IntPtr Wall, IntPtr Floor, IntPtr Stairs)> _tileSets = new();
    private readonly Dictionary<Element, IntPtr> _combatBackdrops = new();
    private readonly Dictionary<SlimeColor, (IntPtr Idle, IntPtr Hop)> _slimes = new();
    private readonly Dictionary<(Gender, Direction, WalkFrame), IntPtr> _player = new();
    private readonly Dictionary<ItemIconKey, IntPtr> _itemIcons = new();
    private readonly List<IntPtr> _allTextures = new();

    /// <summary>Wall/floor/stairs textures tinted to match the dungeon's element (null/None = neutral).</summary>
    public (IntPtr Wall, IntPtr Floor, IntPtr Stairs) TileSet(Element? element) => _tileSets[element ?? Element.None];

    /// <summary>The battle arena backdrop, tinted to match the dungeon's element (null/None = neutral).</summary>
    public IntPtr CombatBackdrop(Element? element) => _combatBackdrops[element ?? Element.None];

    public (IntPtr Idle, IntPtr Hop) Slime(SlimeColor color) => _slimes[color];

    private readonly Dictionary<Domain.Gem, (IntPtr Idle, IntPtr Hop)> _gemSlimes = new();

    /// <summary>
    /// The sprite for a slime that is actually on the floor. Gem slimes share one species but not one look —
    /// which stone a given one carries is the whole reason to walk over to it — so they are drawn from their
    /// own table. Everything else answers from its colour.
    /// </summary>
    public (IntPtr Idle, IntPtr Hop) SlimeSprite(Domain.Slime slime) =>
        slime.Gem is { } gem ? _gemSlimes[gem] : _slimes[slime.Color];
    public IntPtr PlayerSprite(Gender g, Direction d, WalkFrame f) => _player[(g, d, f)];

    public static SpriteFactory BuildAll(IntPtr renderer)
    {
        var f = new SpriteFactory();
        f.Build(renderer);
        return f;
    }

    private IntPtr Bake(IntPtr renderer, PixelCanvas canvas)
    {
        var tex = canvas.ToTexture(renderer);
        _allTextures.Add(tex);
        return tex;
    }

    private void Build(IntPtr renderer)
    {
        foreach (var element in Enum.GetValues<Element>())
        {
            var wall = Bake(renderer, BuildWall(element));
            var floor = Bake(renderer, BuildFloor(element));
            var stairs = Bake(renderer, BuildStairs(element));
            _tileSets[element] = (wall, floor, stairs);
            _combatBackdrops[element] = Bake(renderer, CombatArt.BuildBackdrop(element));
        }

        ChestClosed = Bake(renderer, BuildChest(open: false));
        ChestOpen = Bake(renderer, BuildChest(open: true));
        FullMapItem = Bake(renderer, BuildScrollItem());
        // A painted guild room if one has been supplied, otherwise the one built from rectangles.
        var guildArt = ArtLoader.TryLoad(renderer, GuildArtFile);
        if (guildArt != IntPtr.Zero)
        {
            _allTextures.Add(guildArt);
            GuildBackdrop = guildArt;
            GuildBackdropIsArtwork = true;
        }
        else
        {
            GuildBackdrop = Bake(renderer, BuildGuildBackdrop());
        }
        // Optional, and left at zero when absent — the death screen falls back to a plain dark field, which is
        // no worse than what it had before any artwork existed.
        RipBackdrop = ArtLoader.TryLoad(renderer, RipArtFile);
        if (RipBackdrop != IntPtr.Zero)
            _allTextures.Add(RipBackdrop);

        ShopBackdrop = LoadOptional(renderer, ShopArtFile);
        SmithBackdrop = LoadOptional(renderer, SmithArtFile);
        PharmacyBackdrop = LoadOptional(renderer, PharmacyArtFile);

        MenuBackdrop = Bake(renderer, BuildMenuBackdrop());
        TitleLogo = Bake(renderer, TitleArt.BuildLogo());
        TitleBackdrop = Bake(renderer, TitleArt.BuildBackdrop());
        QuestGatherIcon = Bake(renderer, BuildGatherIcon());
        QuestSlayIcon = Bake(renderer, BuildSlayIcon());
        RankSeal = Bake(renderer, BuildRankSeal());
        GoldIcon = Bake(renderer, BuildGoldIcon());

        foreach (var key in Enum.GetValues<ItemIconKey>())
            _itemIcons[key] = Bake(renderer, BuildItemIcon(key));

        foreach (var icon in Enum.GetValues<HintIcon>())
            _hintIcons[icon] = Bake(renderer, HintIcons.Build(icon));

        foreach (var gem in Enum.GetValues<Domain.Gem>())
            _gemSlimes[gem] = (Bake(renderer, BuildGemSlime(gem, hop: false)),
                               Bake(renderer, BuildGemSlime(gem, hop: true)));

        foreach (var color in Enum.GetValues<SlimeColor>())
        {
            var idle = Bake(renderer, BuildSlime(color, hop: false));
            var hop = Bake(renderer, BuildSlime(color, hop: true));
            _slimes[color] = (idle, hop);
        }

        foreach (var gender in Enum.GetValues<Gender>())
            foreach (var dir in Enum.GetValues<Direction>())
                foreach (var frame in Enum.GetValues<WalkFrame>())
                    _player[(gender, dir, frame)] = Bake(renderer, BuildPlayer(gender, dir, frame));
    }

    /// <summary>Loads a picture that the game can do without, registering it for disposal if it arrived.</summary>
    private IntPtr LoadOptional(IntPtr renderer, string fileName)
    {
        var texture = ArtLoader.TryLoad(renderer, fileName);
        if (texture != IntPtr.Zero)
            _allTextures.Add(texture);
        return texture;
    }

    /// <summary>Shifts a base color toward an element's theme (Fire=red, Water=blue, Wind=yellow-green, Earth=brown), so a
    /// dungeon's element is visible at a glance in its walls/floor/stairs, not just in which slimes show up.</summary>
    public static SDL.Color Tint(SDL.Color baseColor, Element element)
    {
        var (dr, dg, db) = element switch
        {
            Element.Fire => (40, -15, -20),
            Element.Water => (-15, -5, 40),
            Element.Wind => (0, 30, -20),
            Element.Earth => (25, 12, -20),
            _ => (0, 0, 0),
        };
        return Colors.Rgb(
            (byte)Math.Clamp(baseColor.R + dr, 0, 255),
            (byte)Math.Clamp(baseColor.G + dg, 0, 255),
            (byte)Math.Clamp(baseColor.B + db, 0, 255),
            baseColor.A);
    }

    // ---- Environment tiles ----------------------------------------------------

    private static PixelCanvas BuildWall(Element element)
    {
        var c = new PixelCanvas(TileSize, TileSize);
        var baseColor = Tint(Colors.Rgb(74, 66, 58), element);
        var mortar = Tint(Colors.Rgb(48, 42, 36), element);
        var highlight = Tint(Colors.Rgb(96, 87, 76), element);

        c.FillRect(0, 0, TileSize, TileSize, baseColor);

        const int brickH = 8;
        const int brickW = 16;
        for (var row = 0; row < TileSize / brickH; row++)
        {
            var y = row * brickH;
            c.FillRect(0, y, TileSize, 1, mortar);
            var offset = (row % 2 == 0) ? 0 : brickW / 2;
            for (var x = -brickW; x < TileSize + brickW; x += brickW)
                c.FillRect(x + offset, y, 1, brickH, mortar);
        }

        for (var x = 0; x < TileSize; x++)
            c.Set(x, 0, highlight);
        for (var y = 0; y < TileSize; y++)
            c.Set(0, y, highlight);

        return c;
    }

    private static PixelCanvas BuildFloor(Element element)
    {
        var c = new PixelCanvas(TileSize, TileSize);
        var baseColor = Tint(Colors.Rgb(26, 24, 30), element);
        var line = Tint(Colors.Rgb(18, 17, 21), element);
        var speck = Tint(Colors.Rgb(34, 32, 38), element);

        c.FillRect(0, 0, TileSize, TileSize, baseColor);
        c.FillRect(0, 0, TileSize, 1, line);
        c.FillRect(0, 0, 1, TileSize, line);

        var rnd = new Random(12345);
        for (var i = 0; i < 10; i++)
            c.Set(rnd.Next(2, TileSize - 2), rnd.Next(2, TileSize - 2), speck);

        return c;
    }

    private static PixelCanvas BuildStairs(Element element)
    {
        var c = BuildFloor(element);
        var dark = Tint(Colors.Rgb(60, 56, 70), element);
        var mid = Tint(Colors.Rgb(90, 86, 104), element);
        var light = Tint(Colors.Rgb(130, 126, 150), element);

        for (var i = 0; i < 5; i++)
        {
            var y = 26 - i * 6;
            var shade = i switch { 0 or 1 => dark, 2 or 3 => mid, _ => light };
            c.FillRect(2, y, TileSize - 4, 5, shade);
        }
        return c;
    }

    private static PixelCanvas BuildChest(bool open)
    {
        var c = new PixelCanvas(TileSize, TileSize);
        var wood = Colors.Rgb(120, 78, 40);
        var woodDark = Colors.Rgb(88, 56, 28);
        var metal = Colors.Rgb(180, 170, 90);
        var gold = Colors.Rgb(240, 210, 90);

        c.FillRect(4, 14, TileSize - 8, TileSize - 18, wood);
        c.FillRect(4, 14, TileSize - 8, 2, woodDark);
        c.FillRect(4, TileSize - 6, TileSize - 8, 2, woodDark);
        c.FillRect(9, 14, 2, TileSize - 18, woodDark);
        c.FillRect(TileSize - 11, 14, 2, TileSize - 18, woodDark);

        if (!open)
        {
            c.FillRect(3, 8, TileSize - 6, 7, woodDark);
            c.FillRect(3, 8, TileSize - 6, 2, wood);
            c.FillRect(14, 15, 4, 4, metal);
            c.FillRect(15, 16, 2, 2, Colors.Rgb(40, 36, 20));
        }
        else
        {
            c.FillRect(2, 4, TileSize - 4, 5, woodDark);
            c.FillRect(2, 4, TileSize - 4, 2, wood);
            c.FillRect(7, 15, TileSize - 14, TileSize - 20, gold);
            c.FillRect(8, 16, TileSize - 16, 2, Colors.Rgb(255, 240, 160));
        }

        return c;
    }

    private static PixelCanvas BuildScrollItem()
    {
        var c = new PixelCanvas(TileSize, TileSize);
        var paper = Colors.Rgb(230, 220, 190);
        var edge = Colors.Rgb(180, 165, 120);
        c.FillRect(6, 8, TileSize - 12, TileSize - 16, paper);
        c.FillRect(6, 8, TileSize - 12, 2, edge);
        c.FillRect(6, TileSize - 10, TileSize - 12, 2, edge);
        for (var y = 13; y < TileSize - 12; y += 4)
            c.FillRect(9, y, TileSize - 18, 1, edge);
        return c;
    }

    // ---- Quest-type icons ------------------------------------------------------

    private const int QuestIconSize = 16;

    /// <summary>A sprig of herb: gathering work.</summary>
    private static PixelCanvas BuildGatherIcon()
    {
        var c = new PixelCanvas(QuestIconSize, QuestIconSize);
        var stem = Colors.Rgb(84, 128, 62);
        var leaf = Colors.Rgb(116, 190, 88);
        var leafLight = Colors.Rgb(158, 224, 120);

        c.FillRect(7, 5, 2, 10, stem);
        c.FillEllipse(4, 7, 3.2, 2.2, leaf);
        c.FillEllipse(11, 6, 3.2, 2.2, leaf);
        c.FillEllipse(8, 3, 2.6, 2.6, leaf);
        c.FillEllipse(4, 6, 1.6, 1.0, leafLight);
        c.FillEllipse(11, 5, 1.6, 1.0, leafLight);
        c.AddOutline(Colors.Rgb(24, 40, 20));
        return c;
    }

    // ---- Item icons ------------------------------------------------------------

    /// <summary>
    /// A small icon per item kind, for anywhere loot is listed rather than merely named. They are deliberately
    /// silhouettes at this size — 16 pixels is not enough for detail, so each one is built around a shape that
    /// stays recognisable when it is the only thing distinguishing two rows of text.
    /// </summary>
    private const int ItemIconSize = 16;

    /// <summary>Key for the icon table: weapons split by kind, everything else keyed by category alone.</summary>
    public IntPtr ItemIcon(Item item) =>
        _itemIcons.TryGetValue(IconKeyFor(item), out var tex) ? tex : _itemIcons[ItemIconKey.Unknown];

    public IntPtr GoldIcon { get; private set; }

    private enum ItemIconKey
    {
        Unknown, Sword, Wand, Shield, Armor, Helmet, Gauntlet, Shoes,
        Bag, Herb, Antidote, HpPotion, MpPotion, Scroll, Map, Firecracker, Caltrops, Gemstone, Material,
    }

    private static ItemIconKey IconKeyFor(Item item) => item.Category switch
    {
        ItemCategory.Weapon => item.WeaponKind == WeaponKind.Sword ? ItemIconKey.Sword : ItemIconKey.Wand,
        ItemCategory.Shield => ItemIconKey.Shield,
        ItemCategory.Armor => ItemIconKey.Armor,
        ItemCategory.Helmet => ItemIconKey.Helmet,
        ItemCategory.Gauntlet => ItemIconKey.Gauntlet,
        ItemCategory.Shoes => ItemIconKey.Shoes,
        ItemCategory.Bag => ItemIconKey.Bag,
        ItemCategory.Herb => ItemIconKey.Herb,
        ItemCategory.Antidote => ItemIconKey.Antidote,
        ItemCategory.Potion => item.PotionKind == PotionKind.Hp ? ItemIconKey.HpPotion : ItemIconKey.MpPotion,
        ItemCategory.Scroll => ItemIconKey.Scroll,
        ItemCategory.FullMapReveal => ItemIconKey.Map,
        ItemCategory.Firecracker => ItemIconKey.Firecracker,
        ItemCategory.Caltrops => ItemIconKey.Caltrops,
        ItemCategory.Gemstone => ItemIconKey.Gemstone,
        ItemCategory.Material => ItemIconKey.Material,
        _ => ItemIconKey.Unknown,
    };

    private static PixelCanvas BuildItemIcon(ItemIconKey key)
    {
        var c = new PixelCanvas(ItemIconSize, ItemIconSize);
        var steel = Colors.Rgb(196, 202, 214);
        var steelDark = Colors.Rgb(128, 136, 152);
        var wood = Colors.Rgb(140, 96, 56);
        var leather = Colors.Rgb(150, 104, 62);
        var leatherDark = Colors.Rgb(110, 74, 42);
        var gold = Colors.Rgb(226, 186, 88);
        var glass = Colors.Rgb(206, 226, 240);

        switch (key)
        {
            case ItemIconKey.Sword:
                c.FillRect(7, 1, 2, 9, steel);          // blade
                c.FillRect(6, 2, 1, 7, steelDark);
                c.FillRect(4, 10, 8, 2, gold);          // crossguard
                c.FillRect(7, 12, 2, 3, wood);          // grip
                c.FillRect(6, 14, 4, 1, gold);          // pommel
                break;

            case ItemIconKey.Wand:
                c.FillRect(7, 5, 2, 10, wood);
                c.FillCircle(8, 3, 3.2, Colors.Rgb(120, 170, 232));
                c.FillCircle(7, 2, 1.2, Colors.Rgb(214, 236, 255));
                break;

            case ItemIconKey.Shield:
                c.FillRect(3, 2, 10, 7, steelDark);
                c.FillEllipse(8, 9, 5, 5, steelDark);
                c.FillRect(4, 3, 8, 6, steel);
                c.FillEllipse(8, 9, 4, 4, steel);
                c.FillCircle(8, 7, 1.8, gold);
                break;

            case ItemIconKey.Armor:
                // Shoulders sit a row above the torso and the collar is cut out of it, so the silhouette
                // reads as something worn rather than as a barrel.
                c.FillRect(2, 4, 12, 4, leatherDark);   // shoulders
                c.FillRect(4, 4, 8, 9, leather);        // torso
                c.FillRect(6, 3, 4, 3, Colors.Rgb(0, 0, 0, 0));  // collar opening
                c.FillRect(7, 7, 2, 6, leatherDark);    // lacing
                c.FillRect(4, 12, 8, 1, leatherDark);   // hem
                break;

            case ItemIconKey.Helmet:
                c.FillEllipse(8, 7, 5.2, 5.2, leather);
                c.FillRect(3, 7, 10, 4, leather);
                c.FillRect(3, 8, 10, 2, leatherDark);   // brow band
                break;

            case ItemIconKey.Gauntlet:
                // A mitt seen palm-on: four fingers, a thumb standing out to one side, and a banded cuff.
                c.FillRect(5, 2, 7, 9, leather);        // hand
                c.FillRect(6, 2, 1, 4, leatherDark);    // finger seams
                c.FillRect(8, 2, 1, 4, leatherDark);
                c.FillRect(10, 2, 1, 4, leatherDark);
                c.FillRect(2, 6, 3, 4, leather);        // thumb
                c.FillRect(4, 11, 9, 3, leatherDark);   // cuff
                c.FillRect(4, 12, 9, 1, leather);
                break;

            case ItemIconKey.Shoes:
                c.FillRect(4, 5, 4, 6, leather);        // ankle
                c.FillRect(4, 10, 9, 3, leather);       // foot
                c.FillRect(4, 12, 9, 1, leatherDark);   // sole
                break;

            case ItemIconKey.Bag:
                c.FillEllipse(8, 10, 5.4, 4.6, leather);
                c.FillRect(5, 4, 6, 4, leatherDark);    // neck
                c.FillRect(4, 6, 8, 1, gold);           // drawstring
                break;

            case ItemIconKey.Herb:
                c.FillRect(7, 6, 2, 9, Colors.Rgb(84, 128, 62));
                c.FillEllipse(4, 7, 3.2, 2.2, Colors.Rgb(116, 190, 88));
                c.FillEllipse(11, 6, 3.2, 2.2, Colors.Rgb(116, 190, 88));
                c.FillEllipse(8, 3, 2.6, 2.6, Colors.Rgb(140, 210, 104));
                break;

            case ItemIconKey.Antidote:
                // The same sprig, but blue-green and with a berry, so it never reads as a plain herb.
                c.FillRect(7, 6, 2, 9, Colors.Rgb(62, 112, 106));
                c.FillEllipse(4, 7, 3.2, 2.2, Colors.Rgb(84, 176, 160));
                c.FillEllipse(11, 6, 3.2, 2.2, Colors.Rgb(84, 176, 160));
                c.FillCircle(8, 3, 2.4, Colors.Rgb(150, 120, 210));
                break;

            case ItemIconKey.HpPotion:
            case ItemIconKey.MpPotion:
            {
                var liquid = key == ItemIconKey.HpPotion
                    ? Colors.Rgb(214, 72, 84)
                    : Colors.Rgb(78, 122, 220);
                c.FillRect(6, 1, 4, 3, Colors.Rgb(150, 110, 70));   // cork
                c.FillRect(6, 4, 4, 2, glass);                       // neck
                c.FillEllipse(8, 10, 4.6, 4.6, glass);               // body
                c.FillEllipse(8, 11, 3.4, 3.2, liquid);              // contents
                c.FillCircle(6, 8, 0.9, Colors.White);               // highlight
                break;
            }

            case ItemIconKey.Scroll:
                c.FillRect(3, 4, 10, 8, Colors.Rgb(230, 220, 190));
                c.FillRect(3, 3, 10, 2, Colors.Rgb(180, 165, 120));
                c.FillRect(3, 11, 10, 2, Colors.Rgb(180, 165, 120));
                c.FillRect(5, 7, 6, 1, Colors.Rgb(150, 120, 90));
                c.FillRect(5, 9, 4, 1, Colors.Rgb(150, 120, 90));
                break;

            case ItemIconKey.Map:
                c.FillRect(2, 3, 12, 10, Colors.Rgb(226, 212, 172));
                c.FillRect(2, 3, 12, 1, Colors.Rgb(180, 162, 120));
                c.FillRect(2, 12, 12, 1, Colors.Rgb(180, 162, 120));
                c.FillRect(5, 6, 6, 1, Colors.Rgb(140, 120, 88));
                c.FillRect(5, 9, 6, 1, Colors.Rgb(140, 120, 88));
                c.FillRect(10, 4, 1, 8, Colors.Rgb(196, 178, 138));
                c.FillCircle(11, 10, 1.2, Colors.Rgb(198, 74, 62));   // the X
                break;

            case ItemIconKey.Firecracker:
            {
                // A bundle of red tubes with a lit fuse — the spark is what says "about to go off".
                var paper = Colors.Rgb(206, 62, 54);
                var paperDark = Colors.Rgb(154, 40, 36);
                c.FillRect(4, 6, 3, 9, paper);
                c.FillRect(7, 5, 3, 10, paperDark);
                c.FillRect(10, 6, 3, 9, paper);
                c.FillRect(4, 9, 9, 1, gold);           // binding
                c.FillRect(8, 2, 1, 3, Colors.Rgb(120, 100, 70));  // fuse
                c.FillCircle(9, 1, 1.6, Colors.Rgb(252, 214, 96)); // spark
                break;
            }

            case ItemIconKey.Caltrops:
            {
                // Three spikes: whichever way it lands, one point is always up.
                void Spike(int cx2, int cy2)
                {
                    c.FillRect(cx2, cy2 - 3, 1, 4, steel);
                    c.FillRect(cx2 - 2, cy2, 5, 1, steel);
                    c.FillRect(cx2 - 1, cy2 + 1, 1, 2, steelDark);
                    c.FillRect(cx2 + 1, cy2 + 1, 1, 2, steelDark);
                }
                Spike(4, 7);
                Spike(11, 6);
                Spike(8, 12);
                break;
            }

            case ItemIconKey.Material:
            {
                // A rough lump of ore: an irregular mass with a couple of bright flecks of metal showing
                // through the rock, so it reads as raw material rather than as a finished ingot.
                var rock = Colors.Rgb(104, 92, 78);
                var rockDark = Colors.Rgb(66, 58, 48);
                var vein = Colors.Rgb(206, 168, 96);

                c.FillRect(4, 7, 8, 6, rock);
                c.FillRect(5, 5, 6, 2, rock);
                c.FillRect(3, 9, 1, 3, rock);
                c.FillRect(12, 8, 1, 4, rock);
                c.FillRect(4, 12, 8, 1, rockDark);
                c.FillRect(5, 11, 6, 1, rockDark);

                c.FillRect(6, 7, 2, 2, vein);
                c.FillRect(9, 9, 2, 1, vein);
                c.FillRect(5, 9, 1, 1, vein);
                break;
            }

            case ItemIconKey.Gemstone:
            {
                // A cut stone seen face on: table across the top, crown facets meeting at a point below.
                var facet = Colors.Rgb(150, 210, 240);
                var facetLight = Colors.Rgb(215, 245, 255);
                var facetDark = Colors.Rgb(80, 140, 180);

                c.FillRect(4, 4, 8, 2, facetLight);
                c.FillRect(3, 6, 10, 2, facet);
                for (var i = 0; i < 5; i++)
                    c.FillRect(3 + i, 8 + i, 10 - i * 2, 1, i < 2 ? facet : facetDark);

                // The spark that makes it read as a gem rather than a blue lozenge.
                c.FillRect(5, 4, 2, 1, Colors.White);
                c.FillRect(5, 7, 1, 2, facetLight);
                break;
            }

            default:
                c.FillRect(4, 4, 8, 8, steelDark);
                c.FillRect(5, 5, 6, 6, steel);
                break;
        }

        c.AddOutline(Colors.Rgb(26, 22, 20));
        return c;
    }

    /// <summary>A coin, for the gold line of a loot list.</summary>
    private static PixelCanvas BuildGoldIcon()
    {
        var c = new PixelCanvas(ItemIconSize, ItemIconSize);
        c.FillCircle(8, 8, 6.2, Colors.Rgb(168, 124, 40));
        c.FillCircle(8, 8, 5.2, Colors.Rgb(232, 194, 90));
        c.FillCircle(8, 8, 3.6, Colors.Rgb(206, 160, 60));
        c.FillCircle(6, 6, 1.4, Colors.Rgb(252, 234, 168));
        c.AddOutline(Colors.Rgb(26, 22, 20));
        return c;
    }

    /// <summary>
    /// The guild's rank seal: a gold ring around a dark face, with notches so it reads as something stamped
    /// rather than a plain dot. The rank letter is drawn over it with the font, so one texture serves all ranks.
    /// </summary>
    private static PixelCanvas BuildRankSeal()
    {
        const int size = 32;
        var c = new PixelCanvas(size, size);
        var ringLight = Colors.Rgb(214, 178, 96);
        var ringDark = Colors.Rgb(150, 116, 54);
        var face = Colors.Rgb(78, 44, 36);
        var faceShade = Colors.Rgb(58, 32, 26);

        c.FillCircle(16, 16, 15.5, ringDark);
        c.FillCircle(16, 16, 14, ringLight);
        c.FillCircle(16, 16, 12, ringDark);
        c.FillCircle(16, 16, 11, face);
        c.FillEllipse(16, 20, 9, 6, faceShade);

        // Eight notches around the rim, like the crimped edge of a wax seal.
        for (var i = 0; i < 8; i++)
        {
            var a = i * Math.PI / 4;
            var px = (int)Math.Round(16 + Math.Cos(a) * 14.2);
            var py = (int)Math.Round(16 + Math.Sin(a) * 14.2);
            c.FillRect(px - 1, py - 1, 2, 2, ringDark);
        }

        return c;
    }

    /// <summary>An upright sword: slaying work.</summary>
    private static PixelCanvas BuildSlayIcon()
    {
        var c = new PixelCanvas(QuestIconSize, QuestIconSize);
        var blade = Colors.Rgb(196, 204, 218);
        var bladeEdge = Colors.Rgb(240, 246, 255);
        var guard = Colors.Rgb(206, 170, 82);
        var grip = Colors.Rgb(122, 74, 44);

        c.FillRect(7, 1, 3, 9, blade);
        c.FillRect(7, 1, 1, 9, bladeEdge);
        c.FillRect(4, 10, 9, 2, guard);
        c.FillRect(7, 12, 3, 3, grip);
        c.FillRect(6, 14, 5, 1, guard);
        c.AddOutline(Colors.Rgb(26, 24, 28));
        return c;
    }

    // ---- Generic menu-screen backdrop -----------------------------------------

    /// <summary>A plain wood-panelled room (gradient wall + floorboards) kept dark enough that every
    /// existing menu screen's text stays legible drawn directly on top, unlike the guild backdrop which is
    /// bright enough to need its own boxed panels.</summary>
    private static PixelCanvas BuildMenuBackdrop()
    {
        const int w = MenuBackdropWidth, h = MenuBackdropHeight;
        var c = new PixelCanvas(w, h);

        var wallTop = Colors.Rgb(46, 38, 32);
        var wallBottom = Colors.Rgb(30, 25, 20);
        for (var y = 0; y < h; y++)
            c.FillRect(0, y, w, 1, LerpColor(wallTop, wallBottom, y / (double)h));

        var floorY = h * 2 / 3;
        var plankA = Colors.Rgb(70, 48, 32);
        var plankB = Colors.Rgb(62, 42, 28);
        var seam = Colors.Rgb(44, 30, 20);
        var baseboard = Colors.Rgb(50, 34, 24);

        c.FillRect(0, floorY, w, 4, baseboard);
        for (var y = floorY + 4; y < h; y += 18)
        {
            var plank = ((y - floorY) / 18) % 2 == 0 ? plankA : plankB;
            c.FillRect(0, y, w, 18, plank);
            c.FillRect(0, y, w, 1, seam);
        }

        return c;
    }

    private static SDL.Color LerpColor(SDL.Color a, SDL.Color b, double t) => Colors.Rgb(
        (byte)(a.R + (b.R - a.R) * t),
        (byte)(a.G + (b.G - a.G) * t),
        (byte)(a.B + (b.B - a.B) * t));

    // ---- Guild hub backdrop --------------------------------------------------

    /// <summary>
    /// A static "still" illustration for the guild hub: reception counter, a receptionist behind it,
    /// a window and a quest corkboard on the wall. Baked once at startup like everything else — there
    /// are no external art assets in this game, so this is built the same way as every tile/sprite.
    /// </summary>
    private static PixelCanvas BuildGuildBackdrop()
    {
        const int size = GuildBackdropSize;
        var c = new PixelCanvas(size, size);

        var wall = Colors.Rgb(222, 204, 173);
        var counterFront = Colors.Rgb(150, 100, 60);
        var counterTop = Colors.Rgb(180, 130, 80);
        var counterSeam = Colors.Rgb(120, 80, 45);

        c.FillRect(0, 0, size, size, wall);

        DrawGuildSign(c);
        DrawWindow(c, 40, 30);
        DrawQuestBoard(c, 268, 28);
        DrawDateBoard(c);
        DrawReceptionist(c);

        // Counter, spanning the full width across the lower third.
        const int counterY = 262;
        c.FillRect(0, counterY, size, size - counterY, counterFront);
        c.FillRect(0, counterY, size, 10, counterTop);
        for (var x = 20; x < size; x += 40)
            c.FillRect(x, counterY + 12, 2, size - counterY - 12, counterSeam);
        c.FillRect(0, size - 8, size, 8, Colors.Rgb(90, 60, 35));

        DrawCounterProps(c, counterY);

        return c;
    }

    private static void DrawWindow(PixelCanvas c, int x, int y)
    {
        var frame = Colors.Rgb(120, 85, 55);
        var glass = Colors.Rgb(150, 195, 235);
        var cloud = Colors.Rgb(235, 245, 255);

        c.FillRect(x, y, 90, 100, frame);
        c.FillRect(x + 8, y + 8, 74, 84, glass);
        c.FillRect(x + 8, y + 46, 74, 4, frame);
        c.FillRect(x + 43, y + 8, 4, 84, frame);
        c.FillEllipse(x + 28, y + 25, 13, 6, cloud);
        c.FillEllipse(x + 60, y + 65, 10, 5, cloud);
    }

    private static void DrawQuestBoard(PixelCanvas c, int x, int y)
    {
        var cork = Colors.Rgb(170, 130, 90);
        var frame = Colors.Rgb(110, 75, 45);
        var paper = Colors.Rgb(245, 240, 225);
        var line = Colors.Rgb(175, 168, 150);

        c.FillRect(x - 6, y - 6, 112, 122, frame);
        c.FillRect(x, y, 100, 110, cork);

        void Flyer(int fx, int fy, SDL.Color pin)
        {
            c.FillRect(fx, fy, 28, 36, paper);
            c.FillRect(fx + 4, fy + 8, 20, 2, line);
            c.FillRect(fx + 4, fy + 14, 20, 2, line);
            c.FillRect(fx + 4, fy + 20, 14, 2, line);
            c.FillCircle(fx + 14, fy + 3, 2, pin);
        }

        Flyer(x + 8, y + 10, Colors.Rgb(200, 60, 60));
        Flyer(x + 44, y + 18, Colors.Rgb(60, 120, 200));
        Flyer(x + 24, y + 60, Colors.Rgb(70, 160, 90));
    }

    /// <summary>
    /// The carved nameplate above the window, where the hall's name is painted. Like the date slate, the board
    /// is baked into the backdrop and only the lettering is drawn at runtime, so it sits on the wall instead of
    /// floating over it in a panel of its own.
    /// </summary>
    public static readonly (float X, float Y, float W, float H) GuildSign = (18f, 4f, 156f, 30f);

    private static void DrawGuildSign(PixelCanvas c)
    {
        var (sx, sy, sw, sh) = GuildSign;
        var x = (int)sx;
        var y = (int)sy;
        var w = (int)sw;
        var h = (int)sh;

        var timber = Colors.Rgb(122, 84, 50);
        var timberLight = Colors.Rgb(158, 114, 70);
        var timberDark = Colors.Rgb(88, 58, 34);
        var recess = Colors.Rgb(104, 70, 42);

        c.FillRect(x + 2, y + 3, w, h, Colors.Rgb(186, 168, 138));
        c.FillRect(x, y, w, h, timber);
        c.FillRect(x, y, w, 2, timberLight);
        c.FillRect(x, y + h - 2, w, 2, timberDark);

        // Recessed field the lettering sits in, plus a couple of iron nails.
        c.FillRect(x + 5, y + 5, w - 10, h - 10, recess);
        c.FillRect(x + 5, y + 5, w - 10, 1, timberDark);
        c.FillCircle(x + 6, y + h / 2, 2, timberDark);
        c.FillCircle(x + w - 7, y + h / 2, 2, timberDark);
    }

    /// <summary>
    /// The slate where today's date is chalked up, hung on the bare wall under the notice board. The board and
    /// its frame are baked into the backdrop so they are genuinely part of the room; only the date itself is
    /// drawn at runtime. Sitting it here rather than on the corkboard keeps it from burying the flyers.
    /// </summary>
    public static readonly (float X, float Y, float W, float H) GuildDateBoard = (268f, 154f, 108f, 46f);

    private static void DrawDateBoard(PixelCanvas c)
    {
        var (bx, by, bw, bh) = GuildDateBoard;
        var x = (int)bx;
        var y = (int)by;
        var w = (int)bw;
        var h = (int)bh;

        var frame = Colors.Rgb(110, 75, 45);
        var frameLight = Colors.Rgb(150, 108, 66);
        var slate = Colors.Rgb(56, 60, 56);
        var slateEdge = Colors.Rgb(42, 46, 42);

        // Same timber as the window and the notice board, so it belongs to the room.
        c.FillRect(x + 2, y + 3, w, h, Colors.Rgb(186, 168, 138));
        c.FillRect(x, y, w, h, frame);
        c.FillRect(x, y, w, 2, frameLight);
        c.FillRect(x + 4, y + 4, w - 8, h - 8, slateEdge);
        c.FillRect(x + 5, y + 5, w - 10, h - 10, slate);

        // Two pegs holding it up, and a chalk rule between the two lines of writing.
        c.FillCircle(x + 8, y + 5, 2, frameLight);
        c.FillCircle(x + w - 9, y + 5, 2, frameLight);
        c.FillRect(x + 16, y + 20, w - 32, 1, Colors.Rgb(120, 126, 118));
    }

    private static void DrawCounterProps(PixelCanvas c, int counterY)
    {
        var gold = Colors.Rgb(225, 190, 70);
        var goldLight = Colors.Rgb(240, 215, 120);
        var paper = Colors.Rgb(238, 230, 208);

        // Service bell.
        c.FillEllipse(64, counterY - 6, 14, 8, gold);
        c.FillEllipse(64, counterY - 13, 10, 10, goldLight);
        c.FillRect(62, counterY - 22, 4, 5, gold);

        // Stack of quest papers.
        c.FillRect(310, counterY - 16, 38, 6, paper);
        c.FillRect(312, counterY - 22, 34, 6, Colors.Rgb(242, 234, 214));
        c.FillRect(314, counterY - 28, 30, 6, Colors.Rgb(246, 240, 222));
    }

    private static void DrawReceptionist(PixelCanvas c)
    {
        var skin = Colors.Rgb(235, 195, 160);
        var hair = Colors.Rgb(165, 95, 55);
        var vest = Colors.Rgb(150, 40, 50);
        var vestShade = Colors.Rgb(120, 25, 35);
        var blouse = Colors.Rgb(250, 250, 245);
        var blush = Colors.Rgb(235, 150, 140);
        var ribbon = Colors.Rgb(200, 60, 90);

        const int cx = 200;

        // Torso / vest: a rounded shoulder cap blended into a rectangular body below, instead of a
        // flat-topped rectangle, so the shoulders slope naturally instead of reading as square corners.
        const int torsoHalfWidth = 55;
        const int shoulderY = 188;
        c.FillEllipse(cx, shoulderY, torsoHalfWidth, 24, vest);
        c.FillRect(cx - torsoHalfWidth, shoulderY, torsoHalfWidth * 2, 100, vest);
        c.FillRect(cx - torsoHalfWidth, shoulderY + 4, 16, 78, vestShade);
        c.FillRect(cx + torsoHalfWidth - 16, shoulderY + 4, 16, 78, vestShade);
        c.FillRect(cx - 20, 168, 40, 28, blouse);
        c.FillCircle(cx, 210, 5, goldButton());

        // Hair, back layer: narrow locks flanking the neck/shoulders (not a single wide blob spanning
        // the whole chest — that's what made it look like hair was smeared over the body) plus a small
        // fill directly behind the head so no wall color peeks through at the crown.
        c.FillEllipse(cx - 32, 158, 12, 42, hair);
        c.FillEllipse(cx + 32, 158, 12, 42, hair);
        c.FillEllipse(cx, 122, 36, 38, hair);

        // Neck + head.
        c.FillRect(cx - 10, 160, 20, 18, skin);
        c.FillCircle(cx, 132, 34, skin);
        c.FillCircle(cx - 33, 134, 6, skin);
        c.FillCircle(cx + 33, 134, 6, skin);

        // Hair, front bangs.
        c.FillEllipse(cx, 106, 36, 22, hair);

        // Ribbon accessory.
        c.FillCircle(cx + 24, 112, 6, ribbon);
        c.FillEllipse(cx + 18, 112, 5, 4, ribbon);
        c.FillEllipse(cx + 30, 112, 5, 4, ribbon);

        // Face.
        c.FillCircle(cx - 12, 133, 2.2, Colors.Black);
        c.FillCircle(cx + 12, 133, 2.2, Colors.Black);
        c.FillEllipse(cx - 16, 148, 5, 2.5, blush);
        c.FillEllipse(cx + 16, 148, 5, 2.5, blush);
        c.FillRect(cx - 6, 146, 12, 2, Colors.Rgb(150, 90, 80));

        SDL.Color goldButton() => Colors.Rgb(220, 190, 90);
    }

    // ---- Slimes -----------------------------------------------------------

    private static SDL.Color SlimeBodyColor(SlimeColor color) => color switch
    {
        SlimeColor.Green => Colors.Rgb(70, 190, 90),
        SlimeColor.Red => Colors.Rgb(210, 60, 55),
        SlimeColor.Blue => Colors.Rgb(60, 120, 220),
        SlimeColor.Yellow => Colors.Rgb(230, 210, 60),
        SlimeColor.Gray => Colors.Rgb(150, 150, 155),
        SlimeColor.Poison => Colors.Rgb(35, 30, 40),
        SlimeColor.Gold => Colors.Rgb(235, 190, 60),
        SlimeColor.White => Colors.Rgb(240, 240, 245),

        // The ores. Each is the metal's own colour rather than a tint of green, so which one you have walked
        // into is legible from across the room — that is the whole point of a rare drop being visible.
        SlimeColor.Bronze => Colors.Rgb(176, 118, 62),
        // Cooler and darker than the grey slime it would otherwise be mistaken for.
        SlimeColor.Iron => Colors.Rgb(104, 112, 126),
        SlimeColor.Copper => Colors.Rgb(206, 108, 56),
        SlimeColor.Silver => Colors.Rgb(206, 214, 228),
        SlimeColor.Mithril => Colors.Rgb(140, 200, 220),
        SlimeColor.Adamantite => Colors.Rgb(96, 84, 130),
        // Rose-gold, not gold. Drawn as a yellow metal it was indistinguishable from the gold slime on the
        // map — two entirely different encounters that looked like the same one.
        SlimeColor.Orichalcum => Colors.Rgb(242, 164, 142),

        SlimeColor.Dragon => Colors.Rgb(120, 40, 46),

        // Only used where a gem slime is drawn without knowing which stone it holds (the bestiary row). A
        // slime actually on the floor is drawn in its own gem's colour — see GemSlime.
        SlimeColor.Gem => Colors.Rgb(180, 205, 225),

        _ => Colors.Rgb(255, 0, 255),
    };

    /// <summary>The body colour of a slime grown around this stone.</summary>
    private static SDL.Color GemBodyColor(Domain.Gem gem) => gem switch
    {
        Domain.Gem.Diamond => Colors.Rgb(226, 240, 250),

        Domain.Gem.Ruby => Colors.Rgb(214, 44, 74),
        Domain.Gem.Sapphire => Colors.Rgb(44, 82, 198),
        Domain.Gem.Emerald => Colors.Rgb(30, 170, 106),
        Domain.Gem.Opal => Colors.Rgb(226, 190, 160),

        Domain.Gem.Agate => Colors.Rgb(196, 108, 74),
        Domain.Gem.Aquamarine => Colors.Rgb(122, 208, 214),
        Domain.Gem.Peridot => Colors.Rgb(160, 200, 60),
        Domain.Gem.Moonstone => Colors.Rgb(206, 206, 232),

        Domain.Gem.Flamestone => Colors.Rgb(224, 116, 40),
        Domain.Gem.Streamstone => Colors.Rgb(72, 150, 200),
        Domain.Gem.Galestone => Colors.Rgb(190, 196, 90),
        _ => Colors.Rgb(150, 118, 72),
    };

    /// <summary>
    /// A metal slime is drawn with a hard, narrow highlight instead of the soft wet one the others get — a
    /// polished surface reflects a line, a gel reflects a smear. It is what makes a silver slime read as metal
    /// rather than as an unusually pale grey one.
    /// </summary>
    private static bool IsMetallic(SlimeColor color) => Domain.Metals.IsMetalSlime(color);

    /// <summary>
    /// A gem slime: the body drawn up into a point instead of a dome, with the stone itself set in the middle
    /// of it. The silhouette is the identifying mark — a player scanning a floor for a commission target needs
    /// to spot one without reading anything — and the colour then says which stone it is.
    /// </summary>
    private static PixelCanvas BuildGemSlime(Domain.Gem gem, bool hop)
    {
        var c = new PixelCanvas(TileSize, TileSize);
        var body = GemBodyColor(gem);
        var dark = Colors.Rgb((byte)(body.R * 0.6), (byte)(body.G * 0.6), (byte)(body.B * 0.6));
        var shine = Colors.Rgb((byte)Math.Min(255, body.R + 60), (byte)Math.Min(255, body.G + 60), (byte)Math.Min(255, body.B + 60));

        // A rounded slime with a spire drawn up out of it, not a cone. The first attempt was a plain triangle
        // and read as a tent: the family resemblance to every other slime has to survive, with the point as
        // the thing that marks it out.
        var bodyCy = hop ? 20 : 23;
        var bodyRy = hop ? 9 : 8;
        var tipY = hop ? 1 : 5;

        c.FillEllipse(16, hop ? 30 : 31, 10, 2, Colors.Rgb(0, 0, 0, 80));

        // The spire, from the tip down into the shoulders of the body.
        var spireBase = bodyCy - bodyRy + 3;
        for (var y = tipY; y <= spireBase; y++)
        {
            var t = (y - tipY) / (double)Math.Max(1, spireBase - tipY);
            var w = (int)Math.Round(1 + 6 * Math.Pow(t, 1.35));
            c.FillRect(16 - w, y, w * 2 + 1, 1, body);
        }

        c.FillEllipse(16, bodyCy, 12, bodyRy, body);
        c.FillEllipse(16, bodyCy + 4, 12, bodyRy - 4, dark);

        // A soft highlight on the upper left of the body, and a hard one down the spire's near edge.
        c.FillEllipse(11, bodyCy - 4, 4, 3, shine);
        for (var y = tipY + 2; y < spireBase; y++)
        {
            var t = (y - tipY) / (double)Math.Max(1, spireBase - tipY);
            c.FillRect(16 - (int)Math.Round(1 + 6 * Math.Pow(t, 1.35)) + 1, y, 1, 1, shine);
        }

        // Eyes high on the body, well clear of the stone. Level with it they read as a face, and the gem
        // between them turned into a snout — which made every one of the thirteen look like a piglet.
        var eyeColor = Colors.Rgb(28, 28, 34);
        c.FillCircle(11, bodyCy - 4, 1.5, eyeColor);
        c.FillCircle(21, bodyCy - 4, 1.5, eyeColor);

        // The stone in the core, below the face. Drawn with its own dark rim and a near-white centre rather
        // than in the body's own colours: on a pale slime a merely lighter gem vanished entirely.
        var gemY = bodyCy + 3;
        var rim = Colors.Rgb(20, 20, 26);
        var core = Colors.Rgb((byte)Math.Min(255, body.R / 2 + 140),
                              (byte)Math.Min(255, body.G / 2 + 140),
                              (byte)Math.Min(255, body.B / 2 + 140));

        // A cut stone seen face on: rim first, then the bright table inside it.
        for (var i = 0; i < 4; i++)
        {
            c.FillRect(16 - i, gemY - 3 + i, 1 + i * 2, 1, rim);
            c.FillRect(16 - i, gemY + 3 - i, 1 + i * 2, 1, rim);
        }
        for (var i = 0; i < 3; i++)
        {
            c.FillRect(16 - i, gemY - 2 + i, 1 + i * 2, 1, core);
            c.FillRect(16 - i, gemY + 2 - i, 1 + i * 2, 1, core);
        }
        c.FillRect(15, gemY - 1, 1, 1, Colors.White);

        return c;
    }

    private static PixelCanvas BuildSlime(SlimeColor color, bool hop)
    {
        if (color == SlimeColor.Dragon)
            return BuildDragonSlime(hop);
        if (color == SlimeColor.Gem)
            return BuildGemSlime(Domain.Gem.Diamond, hop);

        var c = new PixelCanvas(TileSize, TileSize);
        var body = SlimeBodyColor(color);
        var dark = Colors.Rgb((byte)(body.R * 0.6), (byte)(body.G * 0.6), (byte)(body.B * 0.6));
        var shine = Colors.Rgb((byte)Math.Min(255, body.R + 60), (byte)Math.Min(255, body.G + 60), (byte)Math.Min(255, body.B + 60));
        var eyeColor = color == SlimeColor.White ? Colors.Rgb(30, 30, 30) : Colors.Black;
        var metallic = IsMetallic(color);
        var glint = Colors.Rgb(255, 255, 255, 210);

        if (!hop)
        {
            c.FillEllipse(16, 12, 3, 2, Colors.Rgb(20, 20, 20, 60));
            c.FillEllipse(16, 22, 13, 9, body);
            c.FillEllipse(16, 26, 13, 5, dark);
            c.FillEllipse(11, 18, 4, 3, shine);
            // One hard streak along the top of the highlight, and nothing else. A second glint lower down the
            // body read as a small white mouth rather than as a reflection.
            if (metallic)
                c.FillEllipse(11, 17, 3, 1, glint);
            c.FillCircle(12, 21, 1.6, eyeColor);
            c.FillCircle(20, 21, 1.6, eyeColor);
        }
        else
        {
            c.FillEllipse(16, 29, 9, 2, Colors.Rgb(0, 0, 0, 70));
            c.FillEllipse(16, 16, 10, 12, body);
            c.FillEllipse(16, 22, 10, 6, dark);
            c.FillEllipse(12, 10, 3, 3, shine);
            if (metallic)
                c.FillEllipse(12, 9, 3, 1, glint);
            c.FillCircle(12, 13, 1.6, eyeColor);
            c.FillCircle(20, 13, 1.6, eyeColor);
        }

        return c;
    }

    /// <summary>
    /// The dragon slime. The spec asks that a player be able to spot one on the map and decide whether to go
    /// near it, so this deliberately breaks the silhouette every other slime shares: horns above the body line,
    /// a spined ridge, a wider jaw, and slit eyes that burn. Nothing else on the floor looks like it.
    /// </summary>
    private static PixelCanvas BuildDragonSlime(bool hop)
    {
        var c = new PixelCanvas(TileSize, TileSize);
        var body = SlimeBodyColor(SlimeColor.Dragon);
        var dark = Colors.Rgb(72, 22, 28);
        var shine = Colors.Rgb(190, 78, 74);
        var horn = Colors.Rgb(228, 214, 190);
        var eye = Colors.Rgb(255, 190, 60);
        var pupil = Colors.Rgb(40, 10, 10);

        // The body sits a little lower and wider when settled, and rears up on the second frame.
        var cy = hop ? 20 : 23;
        var ry = hop ? 11 : 9;

        c.FillEllipse(16, 30, 11, 2, Colors.Rgb(0, 0, 0, 90));

        // Horns, drawn before the body so the body's edge tucks in front of their base.
        c.FillEllipse(9, cy - ry - 3, 2, 4, horn);
        c.FillEllipse(23, cy - ry - 3, 2, 4, horn);

        c.FillEllipse(16, cy, 14, ry, body);
        c.FillEllipse(16, cy + 4, 14, ry - 4, dark);
        c.FillEllipse(10, cy - 5, 4, 2, shine);

        // The spined ridge along the back.
        for (var i = -2; i <= 2; i++)
            c.FillEllipse(16 + i * 4, cy - ry + 1 + Math.Abs(i), 1, 2, dark);

        // Slit eyes: a bright almond with a vertical pupil, which no other slime has.
        c.FillEllipse(11, cy - 2, 3, 2, eye);
        c.FillEllipse(21, cy - 2, 3, 2, eye);
        c.FillEllipse(11, cy - 2, 1, 2, pupil);
        c.FillEllipse(21, cy - 2, 1, 2, pupil);

        // A jaw line, hinting at something with teeth inside.
        c.FillEllipse(16, cy + 5, 6, 1, Colors.Rgb(30, 8, 10));

        return c;
    }

    // ---- Player -------------------------------------------------------------

    /// <summary>
    /// The player: a hooded, cloaked adventurer in leather armour with a belt, pauldrons, boots and a sword.
    /// The earlier sprite was a figure in a t-shirt and trousers, which read as a villager rather than someone
    /// who delves dungeons for a living. Every direction shows a different piece of the kit — the face under
    /// the hood from the front, the pack and sword hilt from behind, the cloak trailing in profile.
    /// </summary>
    private static PixelCanvas BuildPlayer(Gender gender, Direction dir, WalkFrame frame)
    {
        var p = new AdventurerPalette(gender);
        var c = new PixelCanvas(TileSize, TileSize);
        var step = frame == WalkFrame.B;

        switch (dir)
        {
            case Direction.Down:
                DrawAdventurerFront(c, p, step);
                break;
            case Direction.Up:
                DrawAdventurerBack(c, p, step);
                break;
            case Direction.Left:
                DrawAdventurerSide(c, p, step);
                break;
            case Direction.Right:
                DrawAdventurerSide(c, p, step);
                c.FlipHorizontal();
                break;
        }

        c.AddOutline(p.Outline);
        return c;
    }

    private readonly struct AdventurerPalette
    {
        public readonly SDL.Color Outline, Skin, SkinShade, Hair, Cloak, CloakShade, CloakLight;
        public readonly SDL.Color Leather, LeatherShade, Belt, Buckle, Boot, BootSole, Trouser, Metal, MetalLight, Pack;

        public AdventurerPalette(Gender gender)
        {
            Outline = Colors.Rgb(26, 20, 16);
            Skin = Colors.Rgb(240, 202, 168);
            SkinShade = Colors.Rgb(198, 158, 126);
            // The two genders differ by cloak and hair colour only; both silhouettes are equally geared.
            Hair = gender == Gender.Male ? Colors.Rgb(88, 58, 34) : Colors.Rgb(158, 92, 56);
            Cloak = gender == Gender.Male ? Colors.Rgb(62, 92, 66) : Colors.Rgb(120, 58, 72);
            CloakShade = gender == Gender.Male ? Colors.Rgb(40, 62, 44) : Colors.Rgb(84, 38, 50);
            CloakLight = gender == Gender.Male ? Colors.Rgb(88, 122, 90) : Colors.Rgb(154, 84, 98);
            Leather = Colors.Rgb(134, 94, 56);
            LeatherShade = Colors.Rgb(96, 64, 36);
            Belt = Colors.Rgb(62, 42, 26);
            Buckle = Colors.Rgb(206, 174, 84);
            Boot = Colors.Rgb(74, 50, 32);
            BootSole = Colors.Rgb(46, 32, 22);
            Trouser = Colors.Rgb(92, 84, 68);
            Metal = Colors.Rgb(172, 180, 196);
            MetalLight = Colors.Rgb(222, 228, 240);
            Pack = Colors.Rgb(112, 80, 48);
        }
    }

    /// <summary>
    /// Legs and boots, shared by all four facings. The two frames differ by 2px so the stride is actually
    /// visible while walking — at a 1px offset the animation was imperceptible at this sprite size.
    /// </summary>
    private static void DrawLegs(PixelCanvas c, AdventurerPalette p, bool step, int leftX, int rightX)
    {
        var aOff = step ? 0 : 2;
        var bOff = step ? 2 : 0;
        c.FillRect(leftX, 21 + aOff, 3, 6, p.Trouser);
        c.FillRect(rightX, 21 + bOff, 3, 6, p.Trouser);
        c.FillRect(leftX, 26 + aOff, 3, 3, p.Boot);
        c.FillRect(rightX, 26 + bOff, 3, 3, p.Boot);
        c.FillRect(leftX, 28 + aOff, 3, 1, p.BootSole);
        c.FillRect(rightX, 28 + bOff, 3, 1, p.BootSole);
    }

    /// <summary>
    /// A cloak as a flared silhouette rather than a rectangle — it widens toward the hem, which is what makes
    /// it read as hanging cloth instead of a box behind the character.
    /// </summary>
    private static void DrawCloak(PixelCanvas c, int cx, int top, int bottom, double topHalf, double bottomHalf, SDL.Color color)
    {
        for (var y = top; y <= bottom; y++)
        {
            var t = (y - top) / (double)Math.Max(1, bottom - top);
            var half = (int)Math.Round(topHalf + (bottomHalf - topHalf) * t);
            c.FillRect(cx - half, y, half * 2, 1, color);
        }
    }

    private static void DrawAdventurerFront(PixelCanvas c, AdventurerPalette p, bool step)
    {
        var sway = step ? 1 : 0;

        // Cloak first, so everything else sits in front of it.
        DrawCloak(c, 16 + sway, 14, 26, 6, 9, p.Cloak);
        DrawCloak(c, 16 + sway, 22, 26, 8, 9, p.CloakShade);

        DrawLegs(c, p, step, 12, 17);

        // Leather cuirass with a centre seam, then the belt.
        c.FillRect(11, 15, 10, 7, p.Leather);
        c.FillRect(15, 16, 1, 6, p.LeatherShade);
        c.FillRect(11, 21, 10, 2, p.Belt);
        c.FillRect(15, 21, 2, 2, p.Buckle);

        // Cloak collar clasped at the throat — separates the head from the body.
        c.FillRect(11, 14, 10, 2, p.CloakLight);
        c.Set(16, 14, p.Buckle);

        // Pauldrons and arms, hands showing at the wrist. The arms counter-swing against the legs.
        var armL = step ? 1 : 0;
        var armR = step ? 0 : 1;
        c.FillEllipse(10, 16, 2.6, 2, p.Leather);
        c.FillEllipse(22, 16, 2.6, 2, p.Leather);
        c.FillRect(9, 17 + armL, 2, 4, p.CloakShade);
        c.FillRect(21, 17 + armR, 2, 4, p.CloakShade);
        c.FillRect(9, 20 + armL, 2, 2, p.Skin);
        c.FillRect(21, 20 + armR, 2, 2, p.Skin);

        // Hood with the face in its opening.
        c.FillEllipse(16, 9, 5.6, 6, p.Cloak);
        c.FillEllipse(16, 10, 4.4, 4.6, p.CloakShade);
        c.FillEllipse(16, 11, 3.4, 3.4, p.Skin);
        c.FillRect(13, 8, 6, 2, p.Hair);
        c.Set(14, 11, p.Outline);
        c.Set(18, 11, p.Outline);
        c.Set(16, 13, p.SkinShade);
    }

    private static void DrawAdventurerBack(PixelCanvas c, AdventurerPalette p, bool step)
    {
        var sway = step ? 1 : 0;

        DrawLegs(c, p, step, 12, 17);

        // From behind the cloak covers almost everything.
        DrawCloak(c, 16 + sway, 13, 27, 7, 10, p.Cloak);
        c.FillRect(15 + sway, 15, 2, 12, p.CloakShade);
        DrawCloak(c, 16 + sway, 24, 27, 9, 10, p.CloakShade);

        // Sword slung across the back: scabbard low-left to high-right, crossguard and pommel clear of the hood.
        for (var i = 0; i < 12; i++)
            c.FillRect(11 + i, 24 - i, 2, 1, p.LeatherShade);
        c.FillRect(22, 11, 2, 3, p.Metal);
        c.FillRect(20, 11, 6, 1, p.MetalLight);
        c.FillRect(22, 9, 2, 2, p.Buckle);

        // Travelling pack with two shoulder straps.
        c.FillRect(12, 16, 8, 6, p.Pack);
        c.FillRect(12, 16, 8, 1, p.Leather);
        c.FillRect(12, 20, 8, 1, p.Belt);
        c.FillRect(11, 15, 2, 6, p.LeatherShade);
        c.FillRect(19, 15, 2, 6, p.LeatherShade);

        // Hood from behind: no face, just the fold where it meets the shoulders.
        c.FillEllipse(16, 9, 5.6, 6, p.Cloak);
        c.FillEllipse(16, 8, 4.2, 4.6, p.CloakShade);
        c.FillRect(11, 14, 10, 2, p.CloakLight);
    }

    /// <summary>Left-facing profile. The right-facing sprite is this one flipped.</summary>
    private static void DrawAdventurerSide(PixelCanvas c, AdventurerPalette p, bool step)
    {
        var sway = step ? 1 : 0;

        // Cloak streams out behind (to the right, since the figure faces left).
        for (var y = 13; y <= 26; y++)
        {
            var t = (y - 13) / 13.0;
            var w = (int)Math.Round(5 + 5 * t);
            c.FillRect(16, y, w + sway, 1, y >= 23 ? p.CloakShade : p.Cloak);
        }

        DrawLegs(c, p, step, 12, 15);

        // Narrower torso in profile.
        c.FillRect(12, 15, 7, 7, p.Leather);
        c.FillRect(17, 16, 2, 6, p.LeatherShade);
        c.FillRect(12, 21, 7, 2, p.Belt);
        c.FillRect(13, 21, 2, 2, p.Buckle);
        c.FillRect(12, 14, 7, 2, p.CloakLight);

        // Sword hanging at the hip, angled back.
        for (var i = 0; i < 6; i++)
            c.FillRect(18 + i, 20 + i, 2, 1, p.LeatherShade);
        c.FillRect(17, 18, 3, 1, p.Metal);
        c.FillRect(17, 17, 1, 2, p.Buckle);

        // Leading arm swings forward, hand at the wrist.
        c.FillRect(10 + sway, 17, 2, 4, p.CloakShade);
        c.FillRect(10 + sway, 20, 2, 2, p.Skin);
        c.FillEllipse(14, 16, 2.6, 2, p.Leather);

        // Hood in profile: the brim juts forward, with the face beneath it.
        c.FillEllipse(17, 9, 5.4, 6, p.Cloak);
        c.FillEllipse(18, 9, 4.2, 4.8, p.CloakShade);
        c.FillEllipse(14, 11, 3.2, 3.2, p.Skin);
        c.FillRect(10, 8, 4, 3, p.Cloak);
        c.Set(12, 11, p.SkinShade);
        c.Set(13, 11, p.Outline);
        c.FillRect(14, 13, 3, 1, p.SkinShade);
    }

    public void Dispose()
    {
        foreach (var t in _allTextures)
            SDL.DestroyTexture(t);
        _allTextures.Clear();
    }
}
