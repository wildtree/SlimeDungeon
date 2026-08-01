using SlimeDungeon.Core;
using SlimeDungeon.Domain;

namespace SlimeDungeon.Dungeon;

public static class LootTables
{
    public static void FillChest(Chest chest, Rank dungeonRank)
    {
        var rnd = RandomUtil.Shared;

        if (rnd.NextDouble() < 0.5)
            chest.Gold = ItemFactory.RollGold(dungeonRank);

        if (chest.Gold == 0 || rnd.NextDouble() < 0.7)
            chest.Items.Add(RollItem(dungeonRank));
    }

    private static Item RollItem(Rank dungeonRank)
    {
        var rnd = RandomUtil.Shared;
        var itemRank = RandomUtil.SampleRank(dungeonRank, 0.7, 2);
        var roll = rnd.NextDouble();

        // Weighted so a dungeon mostly stocks you with consumables and only occasionally turns up a piece of
        // gear. Finding equipment should feel like a small event, not the expected outcome of every chest.
        return roll switch
        {
            < 0.34 => ItemFactory.CreateHerb(itemRank),
            < 0.58 => ItemFactory.CreateAntidoteHerb(itemRank),
            < 0.66 => rnd.Next(2) == 0
                ? ItemFactory.CreateFirecracker(itemRank)
                : ItemFactory.CreateCaltrops(itemRank),
            < 0.72 => ItemFactory.CreateWeapon(itemRank, rnd.Next(2) == 0 ? WeaponKind.Sword : WeaponKind.Wand),
            < 0.78 => rnd.Next(3) switch
            {
                0 => ItemFactory.CreateArmor(itemRank),
                1 => ItemFactory.CreateHelmet(itemRank),
                _ => ItemFactory.CreateShield(itemRank),
            },
            < 0.82 => ItemFactory.CreateGauntlet(itemRank),
            < 0.85 => ItemFactory.CreateShoes(itemRank),
            < 0.87 => ItemFactory.CreateBag(itemRank),
            < 0.96 => ItemFactory.CreateScroll(itemRank, SpellDefinitions.RandomSpell()),
            _ => ItemFactory.CreateFullMapReveal(),
        };
    }
}
