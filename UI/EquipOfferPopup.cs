using SDL3;
using SlimeDungeon.Core;
using SlimeDungeon.Domain;

namespace SlimeDungeon.UI;

/// <summary>
/// "Put it on?", offered the moment something is made.
///
/// A piece off the anvil or a potion off the bench went straight into the bag, and finding out whether it was
/// actually better than what you had meant leaving the shop, opening the equipment screen, and comparing two
/// names. That is three screens away from the moment the player is actually deciding. This asks there and then,
/// with the slot's current occupant and the exact stat change on the same panel.
///
/// One row per slot the thing could go in, which is why a sword offers both hands: a sword in the right hand and
/// the same sword in the left displace different things and move different numbers, and the row is the only
/// place that difference is visible.
/// </summary>
public static class EquipOfferPopup
{
    /// <summary>
    /// Every slot this item could go in. Hands come as a pair for weapons and shields; the item slots come as a
    /// pair for anything pocketable; everything else has exactly one home. Empty for things that go nowhere,
    /// which is how a caller knows not to ask at all.
    /// </summary>
    public static EquipSlot[] Slots(Item item)
    {
        if (item.IsPocketable)
            return [EquipSlot.Item1, EquipSlot.Item2];

        return item.Category switch
        {
            ItemCategory.Weapon or ItemCategory.Shield => [EquipSlot.RightHand, EquipSlot.LeftHand],
            ItemCategory.Armor => [EquipSlot.Body],
            ItemCategory.Helmet => [EquipSlot.Head],
            ItemCategory.Gauntlet => [EquipSlot.Arm],
            ItemCategory.Shoes => [EquipSlot.Feet],
            _ => [],
        };
    }

    /// <summary>
    /// What putting <paramref name="item"/> in <paramref name="slot"/> would move. A swap can shift two stats
    /// at once — a sword where a wand was gains STR and loses INT — so both sides of the trade are reported.
    /// </summary>
    public static List<(string Label, int Before, int After)> Deltas(Player player, Item item, EquipSlot slot)
    {
        var after = new Dictionary<EquipSlot, Item>(player.Equipment) { [slot] = item };
        return StatDeltas(player, player.Equipment, after);
    }

    private static List<(string Label, int Before, int After)> StatDeltas(Player player,
        Dictionary<EquipSlot, Item> before, Dictionary<EquipSlot, Item> after)
    {
        int SwordStr(Dictionary<EquipSlot, Item> e) => player.Stats.Str +
            e.Values.Where(i => i is { Category: ItemCategory.Weapon, WeaponKind: WeaponKind.Sword }).Sum(i => i.StatBonus);
        int WandInt(Dictionary<EquipSlot, Item> e) => player.Stats.Int +
            e.Values.Where(i => i is { Category: ItemCategory.Weapon, WeaponKind: WeaponKind.Wand }).Sum(i => i.StatBonus);
        int GauntletDex(Dictionary<EquipSlot, Item> e) => player.Stats.Dex +
            e.Values.Where(i => i.Category == ItemCategory.Gauntlet).Sum(i => i.StatBonus);
        int ShoesAgl(Dictionary<EquipSlot, Item> e) => player.Stats.Agl +
            e.Values.Where(i => i.Category == ItemCategory.Shoes).Sum(i => i.StatBonus);
        int TotalDef(Dictionary<EquipSlot, Item> e) =>
            e.Values.Where(i => i.Category is ItemCategory.Armor or ItemCategory.Helmet or ItemCategory.Shield).Sum(i => i.Def);

        (string Label, Func<Dictionary<EquipSlot, Item>, int> Calc)[] all =
        [
            ("STR", SwordStr), ("INT", WandInt), ("DEX", GauntletDex), ("AGL", ShoesAgl), ("DEF", TotalDef),
        ];

        var changed = new List<(string, int, int)>();
        foreach (var (label, calc) in all)
        {
            var b = calc(before);
            var a = calc(after);
            if (b != a)
                changed.Add((label, b, a));
        }
        return changed;
    }

    public static string SlotLabel(EquipSlot slot) => slot switch
    {
        EquipSlot.RightHand => "右手",
        EquipSlot.LeftHand => "左手",
        EquipSlot.Arm => "腕",
        EquipSlot.Body => "胴",
        EquipSlot.Head => "頭",
        EquipSlot.Feet => "足",
        EquipSlot.Item1 => "アイテム1",
        EquipSlot.Item2 => "アイテム2",
        _ => slot.ToString(),
    };

    /// <summary>
    /// Whether this piece is worth stopping the player to ask about: it goes into a slot they have nothing in,
    /// or it beats what is already there.
    ///
    /// Used by the shop, where every purchase would otherwise interrupt with a question — buying a spare shield
    /// or a cheaper sword to sell on is a perfectly ordinary thing to do, and being asked "wear it?" every time
    /// trains the player to dismiss the prompt without reading it. The forge does not need this: nobody spends
    /// ore and a thousand gold on a piece they did not want.
    /// </summary>
    public static bool IsUpgrade(Player player, Item item)
    {
        var slots = Slots(item);

        // A slot standing empty is always worth offering: there is nothing to weigh it against and nothing to
        // lose by putting it on.
        if (slots.Any(slot => !player.Equipment.ContainsKey(slot)))
            return true;

        // Otherwise it is only ever compared against its own kind. A sword is not "better" than the shield in
        // that hand, it is a different decision about how to fight.
        var comparable = slots
            .Select(slot => player.Equipment[slot])
            .Where(current => SameKind(current, item))
            .ToArray();

        // Nothing of its own kind is worn at all — which used to fall through to "not an upgrade" and say
        // nothing. That is the case that went quiet: hold a wand and a shield, buy a sword, and both hands are
        // full of things a sword cannot be compared with, so the shop never asked. Having no sword at all is
        // precisely when being offered one matters most.
        if (comparable.Length == 0)
            return true;

        return comparable.Any(current => Merit(item) > Merit(current));
    }

    /// <summary>Two pieces that answer the same question — and for weapons, in the same way.</summary>
    private static bool SameKind(Item a, Item b) =>
        a.Category == b.Category
        && (a.Category != ItemCategory.Weapon || a.WeaponKind == b.WeaponKind);

    /// <summary>
    /// The single number that decides better-or-worse within a kind: armour, helmets and shields are their DEF,
    /// everything else is its stat bonus.
    /// </summary>
    private static int Merit(Item item) =>
        item.Category is ItemCategory.Shield or ItemCategory.Armor or ItemCategory.Helmet
            ? item.Def
            : item.StatBonus;

    /// <summary>Rows are the slots, plus one final "leave it in the bag".</summary>
    public static int RowCount(IReadOnlyList<EquipSlot> slots) => slots.Count + 1;

    /// <summary>The slot at this cursor position, or null on the "leave it" row.</summary>
    public static EquipSlot? Chosen(IReadOnlyList<EquipSlot> slots, int cursor) =>
        cursor >= 0 && cursor < slots.Count ? slots[cursor] : null;

    private const float Width = 330f;

    /// <summary>Two lines per row — the slot and its occupant, then the stat change — so the highlight has to
    /// be tall enough to cover both. At 30 it cut the bottom off the numbers.</summary>
    private const float RowHeight = 33f;

    private const float Pad = 12f;

    public static void Draw(GameContext ctx, Player player, Item item, IReadOnlyList<EquipSlot> slots, int cursor)
    {
        var r = ctx.Renderer;
        var fonts = ctx.Fonts;

        // Header, the slot rows, the "leave it" row, and a band at the foot for the hints.
        var h = Pad + 34f + slots.Count * RowHeight + 26f + 20f + Pad;
        var x = 200f - Width / 2f;
        var y = (400f - h) / 2f;

        r.FillRect(0, 0, 400, 400, Colors.Rgb(0, 0, 0, 130));
        r.FillRect(x + 5, y + 6, Width, h, Colors.Rgb(6, 6, 10, 190));
        r.FillRect(x, y, Width, h, Colors.Rgb(28, 24, 20));
        r.DrawRect(x, y, Width, h, Colors.Gold);

        r.DrawTexture(ctx.Sprites.ItemIcon(item), x + Pad, y + Pad, 18, 18);
        fonts.DrawText(r.Handle, item.Name, x + Pad + 24, y + Pad + 2, 13, Colors.Highlight);
        fonts.DrawText(r.Handle, "装備しますか？", x + Width - Pad - 76, y + Pad + 4, 11, Colors.White);

        var ly = y + Pad + 34f;
        r.FillRect(x + Pad, ly - 8, Width - Pad * 2, 1, Colors.Rgb(74, 64, 50));

        for (var i = 0; i < slots.Count; i++)
        {
            var slot = slots[i];
            var selected = i == cursor;
            if (selected)
                r.FillRect(x + 8, ly - 4, Width - 16, RowHeight - 3, Colors.Highlight);

            var ink = selected ? Colors.Black : Colors.White;
            var sub = selected ? Colors.Rgb(60, 50, 20) : Colors.Rgb(170, 165, 158);

            fonts.DrawText(r.Handle, SlotLabel(slot), x + Pad, ly, 12, ink);
            var current = player.Equipment.GetValueOrDefault(slot);
            fonts.DrawText(r.Handle, $"今: {current?.Name ?? "なし"}", x + Pad + 62, ly + 1, 10, ink);

            var deltas = Deltas(player, item, slot);
            var dx = x + Pad;
            foreach (var (label, before, after) in deltas)
            {
                var diff = after - before;
                var text = $"{label} {before}→{after} ({(diff > 0 ? "+" : "")}{diff})";
                var colour = selected ? sub : diff > 0 ? Colors.Rgb(120, 230, 120) : Colors.HpBar;
                fonts.DrawText(r.Handle, text, dx, ly + 15, 9, colour);
                dx += fonts.Measure(text + "  ", 9).Item1;
            }

            if (deltas.Count == 0)
            {
                // Consumables move no stats; what the swap costs is the thing put away, so that is the note.
                var note = current is null ? "能力値は変わらない" : $"{current.Name}は鞄に戻る";
                fonts.DrawText(r.Handle, note, dx, ly + 15, 9, sub);
            }

            ly += RowHeight;
        }

        var leaveSelected = cursor >= slots.Count;
        if (leaveSelected)
            r.FillRect(x + 8, ly - 4, Width - 16, 22f, Colors.Highlight);
        fonts.DrawText(r.Handle, "装備しない（鞄に入れる）", x + Pad, ly, 12,
            leaveSelected ? Colors.Black : Colors.White);

        ControlHints.DrawCentered(ctx, 200f, y + h - Pad - 6f, 10, Colors.Rgb(168, 160, 148),
            ControlHints.Confirm("決定"), ControlHints.Cancel("装備しない"));
    }
}
