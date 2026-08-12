using SlimeDungeon.Core;

namespace SlimeDungeon.Domain;

/// <summary>The seven pieces that make up a full set. A collector title wants all of them in one metal.</summary>
public enum ForgePiece { Sword, Wand, Shield, Armor, Helmet, Gauntlet, Shoes }

/// <summary>
/// One thing the smith can make: what it costs in ore and gold, and the gear that comes out. Recipes are
/// identified by <see cref="Id"/> rather than by name, because the id is what goes into the save as the record
/// of what this adventurer has forged — and that record is what the collector titles are counted from.
/// </summary>
public sealed record ForgeRecipe(
    string Id,
    string Name,
    ForgePiece Piece,
    Rank Rank,
    IReadOnlyDictionary<Metal, int> Cost,
    int GoldCost,
    Metal? Set)
{
    public Item Create() => Forge.Build(this);
}

public static class Forge
{
    /// <summary>
    /// What forged gear is worth compared to the same rank off the shelf.
    ///
    /// Shop gear averages 0.8 per rank step and rolls around it with a 15% spread, so its lucky tail reaches
    /// well past its mean — a flat 1.0 here looked like a 25% improvement but was actually beaten by a good
    /// shop roll at every single rank, which would make a whole set of ore worth less than shopping carefully.
    /// At 1.25 the smith's work is above anything the shop realistically produces (a shop roll would have to
    /// come in near four standard deviations high), and it never rolls at all — forged gear is exact.
    /// </summary>
    private const double ForgedBonusPerRank = 1.25;

    public static int ForgedBonus(Rank rank) =>
        Math.Max(1, (int)Math.Round(ForgedBonusPerRank * Math.Pow(CombatMath.RankStep, (int)rank - 1)));

    public static string PieceName(ForgePiece piece) => piece switch
    {
        ForgePiece.Sword => "剣",
        ForgePiece.Wand => "杖",
        ForgePiece.Shield => "盾",
        ForgePiece.Armor => "鎧",
        ForgePiece.Helmet => "兜",
        ForgePiece.Gauntlet => "籠手",
        ForgePiece.Shoes => "靴",
        _ => piece.ToString(),
    };

    /// <summary>
    /// The set forged from the three finest ores at once. Its id prefix is what combat looks for: only these
    /// seven pieces mean anything to a dragon slime.
    /// </summary>
    public const string SlimeSetPrefix = "Slime:";

    public static bool IsSlimeGear(Item? item) =>
        item?.ForgeId?.StartsWith(SlimeSetPrefix, StringComparison.Ordinal) == true;

    /// <summary>The ores the slime set is beaten out of — all three, for every single piece.</summary>
    public static readonly Metal[] SlimeSetOres = [Metal.Orichalcum, Metal.Adamantite, Metal.Mithril];

    /// <summary>
    /// The armour that has to be worn all at once to live through a dragon's blow. Five pieces, which leaves
    /// exactly one hand free — so an adventurer facing the dragon has to choose in advance between the sword
    /// and the wand, and can only hurt it the one way.
    /// </summary>
    public static readonly ForgePiece[] SlimeArmourPieces =
        [ForgePiece.Shield, ForgePiece.Armor, ForgePiece.Helmet, ForgePiece.Gauntlet, ForgePiece.Shoes];

    public static readonly ForgeRecipe[] All = BuildAll();

    private static readonly Dictionary<string, ForgeRecipe> ById = All.ToDictionary(r => r.Id);

    public static ForgeRecipe Get(string id) => ById[id];

    public static ForgeRecipe? Find(string id) => ById.GetValueOrDefault(id);

    /// <summary>Every recipe in one metal, in a fixed order so the menu never shuffles under the cursor.</summary>
    public static IEnumerable<ForgeRecipe> ForSet(Metal? set) => All.Where(r => r.Set == set);

    /// <summary>The metal sets, then the slime set — which is listed last because it is the end of the road.</summary>
    public static IEnumerable<Metal?> Sets => Metals.All.Select(m => (Metal?)m.Metal).Append(null);

    public static string SetName(Metal? set) => set is { } m ? Metals.Get(m).Name : "スライム";

    private static ForgeRecipe[] BuildAll()
    {
        var recipes = new List<ForgeRecipe>();

        foreach (var metal in Metals.All)
        {
            foreach (var piece in Enum.GetValues<ForgePiece>())
            {
                recipes.Add(new ForgeRecipe(
                    Id: $"{metal.Metal}:{piece}",
                    Name: $"{metal.Name}の{PieceName(piece)}",
                    Piece: piece,
                    Rank: metal.GearRank,
                    Cost: new Dictionary<Metal, int> { [metal.Metal] = 1 },
                    GoldCost: GoldFor(metal.GearRank),
                    Set: metal.Metal));
            }
        }

        var slimeCost = SlimeSetOres.ToDictionary(m => m, _ => 1);
        foreach (var piece in Enum.GetValues<ForgePiece>())
        {
            recipes.Add(new ForgeRecipe(
                Id: $"{SlimeSetPrefix}{piece}",
                // The sword has a name of its own; the rest are named for what they are.
                Name: piece == ForgePiece.Sword ? "スライムスレイヤー" : $"スライムの{PieceName(piece)}",
                Piece: piece,
                Rank: Rank.SS,
                Cost: slimeCost,
                GoldCost: SlimeSetGold,
                Set: null));
        }

        return recipes.ToArray();
    }

    /// <summary>
    /// The smith's fee on top of the ore, per gear rank.
    ///
    /// This was rank squared times twenty, which was the wrong shape. A trip's takings — bounty and chests —
    /// grow with the same 1.6-per-rank curve as everything else in combat, so by iron the fee had fallen to
    /// about half of what the trips needed to find one ore already paid out, and by mithril it was a rounding
    /// error against a single gem commission. Gold stopped being a decision, which made every piece of gear
    /// automatic the moment its ore turned up.
    ///
    /// The figures below are each set against measured income: a piece costs roughly what the trips spent
    /// finding its ore actually earn, rising above that at the top ranks where commissions pay several times
    /// what the dungeon does. Ore and gold now run out at about the same time, which is the point — the
    /// interesting question is which piece to spend on, not whether you can afford all seven.
    ///
    /// Bronze is deliberately left where it was. It is the metal a new adventurer meets while also paying for
    /// their first armour and their heals, and there is nothing to fix at a rank where the whole set costs less
    /// than one gem.
    /// </summary>
    private static int GoldFor(Rank rank) => rank switch
    {
        // Keyed to the rank each ore now sits at. The figures themselves are the ones already tuned against
        // measured income per metal — they simply moved with the metals when the ladder was packed to one ore
        // per rank. Rank B has no ore at all, so it never reaches this table.
        Rank.F => 180,      // bronze — deliberately cheap, see below
        Rank.E => 1100,     // iron
        Rank.D => 1600,     // copper
        Rank.C => 2400,     // silver
        Rank.A => 3600,     // adamantite
        Rank.S => 5400,     // mithril
        Rank.SS => 8000,    // orichalcum
        // No ore has its gear at the other ranks, but a fee is defined for all of them so adding one later
        // cannot silently produce a free recipe.
        _ => (int)rank * (int)rank * 20,
    };

    /// <summary>
    /// The slime set is beyond the price list. Three of the rarest ores in the world go into every piece, and
    /// the fee is meant to be felt even by someone who has been claiming SS bounties for a while — so it stays
    /// half again above orichalcum now that orichalcum itself costs what it does.
    /// </summary>
    public const int SlimeSetGold = 12000;

    internal static Item Build(ForgeRecipe recipe)
    {
        var bonus = ForgedBonus(recipe.Rank);
        var value = recipe.GoldCost;

        return recipe.Piece switch
        {
            ForgePiece.Sword => new Item
            {
                Name = recipe.Name, Category = ItemCategory.Weapon, WeaponKind = WeaponKind.Sword,
                Rank = recipe.Rank, StatBonus = bonus, Value = value, ForgeId = recipe.Id,
            },
            ForgePiece.Wand => new Item
            {
                Name = recipe.Name, Category = ItemCategory.Weapon, WeaponKind = WeaponKind.Wand,
                Rank = recipe.Rank, StatBonus = bonus, Value = value, ForgeId = recipe.Id,
            },
            ForgePiece.Shield => new Item
            {
                Name = recipe.Name, Category = ItemCategory.Shield,
                Rank = recipe.Rank, Def = bonus, Value = value, ForgeId = recipe.Id,
            },
            ForgePiece.Armor => new Item
            {
                Name = recipe.Name, Category = ItemCategory.Armor,
                Rank = recipe.Rank, Def = bonus, Value = value, ForgeId = recipe.Id,
            },
            ForgePiece.Helmet => new Item
            {
                Name = recipe.Name, Category = ItemCategory.Helmet,
                Rank = recipe.Rank, Def = bonus, Value = value, ForgeId = recipe.Id,
            },
            ForgePiece.Gauntlet => new Item
            {
                Name = recipe.Name, Category = ItemCategory.Gauntlet,
                Rank = recipe.Rank, StatBonus = bonus, Value = value, ForgeId = recipe.Id,
            },
            ForgePiece.Shoes => new Item
            {
                Name = recipe.Name, Category = ItemCategory.Shoes,
                Rank = recipe.Rank, StatBonus = bonus, Value = value, ForgeId = recipe.Id,
            },
            _ => throw new ArgumentOutOfRangeException(nameof(recipe)),
        };
    }
}
