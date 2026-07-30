namespace SlimeDungeon.Domain;

public enum Element { None, Fire, Water, Wind, Earth }

public enum Matchup { Neutral, Advantage, Disadvantage }

public static class ElementExtensions
{
    // Cycle: Water > Fire > Wind > Earth > Water (attacker beats defender).
    private static readonly Dictionary<Element, Element> Beats = new()
    {
        [Element.Water] = Element.Fire,
        [Element.Fire] = Element.Wind,
        [Element.Wind] = Element.Earth,
        [Element.Earth] = Element.Water,
    };

    public static Matchup GetMatchup(Element attacker, Element defender)
    {
        if (attacker == Element.None || defender == Element.None)
            return Matchup.Neutral;
        if (Beats.TryGetValue(attacker, out var beaten) && beaten == defender)
            return Matchup.Advantage;
        if (Beats.TryGetValue(defender, out var beatsAttacker) && beatsAttacker == attacker)
            return Matchup.Disadvantage;
        return Matchup.Neutral;
    }

    /// <summary>In a dungeon of this element, the element that is at a disadvantage (the one this element beats).</summary>
    public static Element? WeakElementIn(Element dungeonElement) =>
        Beats.TryGetValue(dungeonElement, out var weak) ? weak : null;

    /// <summary>In a dungeon of this element, the spell element that is amplified (the one that beats it).</summary>
    public static Element? StrongSpellElementIn(Element dungeonElement)
    {
        foreach (var (attacker, beaten) in Beats)
            if (beaten == dungeonElement)
                return attacker;
        return null;
    }
}
