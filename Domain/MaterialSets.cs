namespace SlimeDungeon.Domain;

/// <summary>Every piece the shop makes out of one substance, and the title for having worn the lot.</summary>
public sealed record MaterialSet(string Material, TitleId Title, string[] Pieces);

/// <summary>
/// The shop's stock, regrouped by what it is made of rather than by rank.
///
/// A rank is not quite a material: 木 turns up as rank-H weapons and as the rank-H shield and boots, 石 as
/// rank-G weapons and the rank-E shield, and there is no cloth shield or cloth boot at all. So the groups are
/// derived from <see cref="EquipmentNames"/> at startup rather than typed out — a material's set is however
/// many pieces genuinely bear its name, which is four for 木, three for 布 and seven for the ranks that are
/// worked in one substance throughout. Retyping that list by hand would be one rename away from a title that
/// can never be earned.
/// </summary>
public static class MaterialSets
{
    /// <summary>
    /// Every rank, not only the seven the shop stocks. 水晶, 神木 and 星石 gear exists — it simply has to be
    /// found rather than bought, which makes those three the hardest of these to complete and worth having.
    /// </summary>
    private static readonly Rank[] Ranks =
        [Rank.H, Rank.G, Rank.F, Rank.E, Rank.D, Rank.C, Rank.B, Rank.A, Rank.S, Rank.SS];

    /// <summary>
    /// One title per material. Held as an explicit table rather than generated, because <see cref="TitleId"/>
    /// values go into the save by name and must stay pinned to the material they were awarded for.
    /// </summary>
    private static readonly Dictionary<string, TitleId> TitleFor = new()
    {
        ["木"] = TitleId.WoodMania,
        ["布"] = TitleId.ClothMania,
        ["石"] = TitleId.StoneMania,
        ["革"] = TitleId.LeatherMania,
        ["樫"] = TitleId.OakMania,
        ["翡翠"] = TitleId.JadeMania,
        ["厚革"] = TitleId.ThickLeatherMania,
        ["黒檀"] = TitleId.EbonyMania,
        ["黒曜石"] = TitleId.ObsidianMania,
        ["古木"] = TitleId.AncientWoodMania,
        ["水晶"] = TitleId.CrystalMania,
        ["神木"] = TitleId.DivineWoodMania,
        ["星石"] = TitleId.StarstoneMania,
    };

    public static readonly MaterialSet[] All = Build();

    private static MaterialSet[] Build()
    {
        var pieces = new Dictionary<string, List<string>>();

        void Add(string material, string name)
        {
            if (!TitleFor.ContainsKey(material))
                return;
            var list = pieces.TryGetValue(material, out var existing) ? existing : pieces[material] = [];
            if (!list.Contains(name))
                list.Add(name);
        }

        // A shelf's label may name two substances (布・木), so each piece is filed under whichever component
        // its own name actually carries.
        void Classify(string label, string name)
        {
            foreach (var material in label.Split('・'))
                if (name.Contains(material))
                {
                    Add(material, name);
                    return;
                }
        }

        foreach (var rank in Ranks)
        {
            var weapon = EquipmentNames.WeaponMaterial(rank);
            Classify(weapon, EquipmentNames.Sword(rank));
            Classify(weapon, EquipmentNames.Wand(rank));

            var armour = EquipmentNames.ArmourMaterial(rank);
            Classify(armour, EquipmentNames.Shield(rank));
            Classify(armour, EquipmentNames.Armor(rank));
            Classify(armour, EquipmentNames.Helmet(rank));
            Classify(armour, EquipmentNames.Gauntlet(rank));
            Classify(armour, EquipmentNames.Shoes(rank));
        }

        // Ordered by the rank the material first appears at, so the title list reads bottom-of-the-ladder first.
        return TitleFor.Keys
            .Where(pieces.ContainsKey)
            .Select(m => new MaterialSet(m, TitleFor[m], pieces[m].ToArray()))
            .ToArray();
    }

    /// <summary>Whether every piece in one material has been on the body at least once.</summary>
    public static bool HasWornAll(Player player, MaterialSet set) =>
        set.Pieces.All(player.WornGear.Contains);
}
