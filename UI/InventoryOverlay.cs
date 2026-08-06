using SDL3;
using SlimeDungeon.Core;
using SlimeDungeon.Domain;
using SlimeDungeon.Graphics;

namespace SlimeDungeon.UI;

/// <summary>
/// What the character is wearing, and what in the bag could replace it.
///
/// This used to be the whole inventory — slots, bag, and every verb that applied to anything. Using and
/// appraising have moved to <see cref="ItemOverlay"/>, which is the screen for the pack itself; what is left
/// here is only what this screen is for: putting things on and taking them off, with the stat change from doing
/// so shown before you commit.
/// </summary>
public sealed class InventoryOverlay
{
    private enum Phase { List, ItemActionMenu, SlotSelect }
    private enum ItemAction { Equip, Unequip, Discard }

    private Phase _phase = Phase.List;
    private int _cursor;
    private int _actionCursor;
    private int _slotChoiceCursor;
    private string? _message;
    private Item? _selectedItem;

    /// <summary>Which equipment slot <see cref="_selectedItem"/> came out of, or null if it came from the bag.</summary>
    private EquipSlot? _selectedItemSlot;

    private Item? _pendingSlotItem;

    private EquipSlot[] _slotChoices = Array.Empty<EquipSlot>();
    private List<ItemAction> _availableActions = new();

    /// <summary>The two hands, in the order the slot-choice menu lists them.</summary>
    private static readonly EquipSlot[] Hands = { EquipSlot.RightHand, EquipSlot.LeftHand };

    /// <summary>
    /// Every slot, laid out in two columns of four: gear down the left, the rest plus the two item slots down
    /// the right. The cursor runs column-major, so it reads in the same order the eye does.
    /// </summary>
    private static readonly EquipSlot[] Slots =
    {
        EquipSlot.RightHand, EquipSlot.LeftHand, EquipSlot.Arm, EquipSlot.Body,
        EquipSlot.Head, EquipSlot.Feet, EquipSlot.Item1, EquipSlot.Item2,
    };

    private const int SlotRows = 4;

    /// <summary>
    /// Wipes everything the last visit left behind, so the screen opens the same way every time.
    ///
    /// This overlay is a single long-lived instance — Program.cs builds one at startup and shows the same
    /// object for the whole run — so every field on it survives being closed. The visible symptom was the
    /// status line: throw away a herb, close the bag, open it again an hour later and it still said
    /// "薬草(H)を捨てた", reporting an action from a different part of the game entirely. The cursor and the
    /// half-finished selection state hung around just the same, they were simply less obvious about it.
    /// </summary>
    private void Reset()
    {
        _phase = Phase.List;
        _cursor = 0;
        _actionCursor = 0;
        _slotChoiceCursor = 0;
        _message = null;
        _selectedItem = null;
        _selectedItemSlot = null;
        _pendingSlotItem = null;
        _slotChoices = Array.Empty<EquipSlot>();
        _availableActions = new List<ItemAction>();
    }

    public void Update(GameContext ctx, float dt)
    {
        var input = ctx.Input;
        var player = ctx.Player!;

        switch (_phase)
        {
            case Phase.ItemActionMenu:
                UpdateItemActionMenu(input, player);
                return;
            case Phase.SlotSelect:
                UpdateSlotSelect(input, player);
                return;
        }

        // The I key used to open this overlay and also closed it. Opening moved onto the menu long ago; the
        // close half was left behind, an undocumented key that could shut the screen by surprise.
        if (MenuNav.Cancelled(input))
        {
            ctx.ShowEquipment = false;
            Reset();
            return;
        }

        var count = Slots.Length + player.Bag.Count;
        _cursor = MenuNav.Move(input, _cursor, count);

        if (!MenuNav.Confirmed(input))
            return;

        if (_cursor < Slots.Length)
        {
            var slot = Slots[_cursor];
            if (!player.Equipment.TryGetValue(slot, out var equipped))
                return;

            // A readied consumable is taken off through the action menu rather than by one keypress, because
            // "外す" needs bag room and the menu is where that failure can be reported.
            if (IsItemSlot(slot))
            {
                Select(player, equipped, slot);
                return;
            }

            if (!player.BagHasRoom)
            {
                _message = "鞄がいっぱいだ";
                return;
            }
            player.Equipment.Remove(slot);
            player.Bag.Add(equipped);
            _message = $"{equipped.Name}を外した";
            return;
        }

        Select(player, player.Bag[_cursor - Slots.Length], null);
    }

    private void Select(Player player, Item item, EquipSlot? fromSlot)
    {
        _selectedItem = item;
        _selectedItemSlot = fromSlot;
        _availableActions = BuildActions(item, fromSlot);
        _actionCursor = 0;
        _phase = Phase.ItemActionMenu;
    }

    private static bool IsItemSlot(EquipSlot slot) => slot is EquipSlot.Item1 or EquipSlot.Item2;

    /// <summary>
    /// Only ever putting on, taking off, or throwing away. Drinking and appraising live on the item screen —
    /// this one is about what is worn, and a potion's effect has no bearing on that decision.
    /// </summary>
    private static List<ItemAction> BuildActions(Item item, EquipSlot? fromSlot)
    {
        var actions = new List<ItemAction>();
        if (fromSlot is null && item.HasEquipSlot)
            actions.Add(ItemAction.Equip);
        if (fromSlot is not null)
            actions.Add(ItemAction.Unequip);
        actions.Add(ItemAction.Discard);
        return actions;
    }

    private static string ActionLabel(ItemAction action) => action switch
    {
        ItemAction.Equip => "装備する",
        ItemAction.Unequip => "外す",
        _ => "捨てる",
    };

    private void UpdateItemActionMenu(InputManager input, Player player)
    {
        if (MenuNav.Cancelled(input))
        {
            _phase = Phase.List;
            _selectedItem = null;
            _selectedItemSlot = null;
            return;
        }

        _actionCursor = MenuNav.Move(input, _actionCursor, _availableActions.Count);

        if (!MenuNav.Confirmed(input) || _selectedItem is null)
            return;

        var item = _selectedItem;
        switch (_availableActions[_actionCursor])
        {
            case ItemAction.Equip:
                EquipItem(player, item);
                break;
            case ItemAction.Unequip:
                UnequipItem(player, item);
                break;
            case ItemAction.Discard:
                DiscardItem(player, item);
                break;
        }

        // EquipItem may switch to SlotSelect (nowhere free to put it); don't stomp it.
        if (_phase == Phase.ItemActionMenu)
        {
            _phase = Phase.List;
            _selectedItem = null;
            _selectedItemSlot = null;
        }

        var count = Slots.Length + player.Bag.Count;
        if (_cursor >= count)
            _cursor = Math.Max(0, count - 1);
    }

    private void EquipItem(Player player, Item item)
    {
        // Anything that goes in an item slot is matched on the property, not on a list of categories. This
        // used to be a switch case naming herbs, potions and antidotes; firecrackers and caltrops matched no
        // case at all, so choosing "装備する" on one fell straight through the switch and did nothing —
        // with no message to say why.
        if (item.IsPocketable)
        {
            // Same rule as the hands: take a free slot without asking, but when both are occupied the player
            // decides which readied item gets displaced back into the bag.
            var freeSlot = Player.ItemSlots.FirstOrDefault(s => !player.Equipment.ContainsKey(s), EquipSlot.Item1);
            if (Player.ItemSlots.Any(s => !player.Equipment.ContainsKey(s)))
                TryEquip(player, item, freeSlot);
            else
                BeginSlotSelect(item, Player.ItemSlots);
            return;
        }

        switch (item.Category)
        {
            case ItemCategory.Weapon or ItemCategory.Shield:
            {
                // A free hand is unambiguous, so use it. With both hands full something has to be displaced,
                // and which one is the player's call — it used to always take the left hand silently.
                var freeHand = Hands.FirstOrDefault(h => !player.Equipment.ContainsKey(h), EquipSlot.RightHand);
                if (Hands.Any(h => !player.Equipment.ContainsKey(h)))
                {
                    TryEquip(player, item, freeHand);
                    break;
                }

                BeginSlotSelect(item, Hands);
                return;
            }
            case ItemCategory.Armor:
                TryEquip(player, item, EquipSlot.Body);
                break;
            case ItemCategory.Helmet:
                TryEquip(player, item, EquipSlot.Head);
                break;
            case ItemCategory.Gauntlet:
                TryEquip(player, item, EquipSlot.Arm);
                break;
            case ItemCategory.Shoes:
                TryEquip(player, item, EquipSlot.Feet);
                break;
            case ItemCategory.Bag:
            {
                // Also slot-neutral, but swapping to a *smaller* bag could leave more items than the new bag
                // can hold, so check the contents fit first rather than orphaning them.
                var contentsAfterSwap = player.Bag.Count - 1 + (player.EquippedBag is null ? 0 : 1);
                if (contentsAfterSwap > item.BagCapacity)
                {
                    _message = $"今の荷物が{item.Name}に入りきらない";
                    break;
                }

                player.Bag.Remove(item);
                if (player.EquippedBag is { } oldBag)
                    player.Bag.Add(oldBag);
                player.EquippedBag = item;
                _message = $"{item.Name}を装備した";
                break;
            }
        }
    }

    private void BeginSlotSelect(Item item, EquipSlot[] choices)
    {
        _pendingSlotItem = item;
        _slotChoices = choices;
        _slotChoiceCursor = 0;
        _phase = Phase.SlotSelect;
    }

    /// <summary>Takes a readied consumable out of its slot and back into the bag, if the bag can take it.</summary>
    private void UnequipItem(Player player, Item item)
    {
        if (_selectedItemSlot is not { } slot)
            return;
        if (!player.BagHasRoom)
        {
            _message = "鞄がいっぱいだ";
            return;
        }
        player.Equipment.Remove(slot);
        player.Bag.Add(item);
        _message = $"{item.Name}を鞄にしまった";
    }

    private void DiscardItem(Player player, Item item)
    {
        var name = item.Name;
        player.ConsumeOne(item);
        _message = $"{name}を捨てた";
    }

    /// <summary>
    /// Equipping from the bag is slot-neutral — the item leaves the bag and whatever it replaces takes its
    /// place — so it is deliberately not gated on free bag space.
    /// </summary>
    private void TryEquip(Player player, Item item, EquipSlot slot)
    {
        player.Bag.Remove(item);
        if (!player.TryEquip(item, slot, out var displaced))
        {
            // The slot rejected it; put it back rather than letting the item vanish.
            player.Bag.Add(item);
            _message = $"{item.Name}はそこには装備できない";
            return;
        }
        if (displaced is not null)
            player.Bag.Add(displaced);
        _message = displaced is not null
            ? $"{displaced.Name}を外して{item.Name}を装備した"
            : $"{item.Name}を装備した";
    }

    /// <summary>Every candidate slot is occupied: the player picks which one gets displaced.</summary>
    private void UpdateSlotSelect(InputManager input, Player player)
    {
        if (MenuNav.Cancelled(input))
        {
            _phase = Phase.List;
            _pendingSlotItem = null;
            _selectedItem = null;
            _selectedItemSlot = null;
            return;
        }

        _slotChoiceCursor = MenuNav.Move(input, _slotChoiceCursor, _slotChoices.Length);

        if (!MenuNav.Confirmed(input) || _pendingSlotItem is null)
            return;

        TryEquip(player, _pendingSlotItem, _slotChoices[_slotChoiceCursor]);
        _pendingSlotItem = null;
        _selectedItem = null;
        _selectedItemSlot = null;
        _phase = Phase.List;

        var count = Slots.Length + player.Bag.Count;
        if (_cursor >= count)
            _cursor = Math.Max(0, count - 1);
    }

    /// <summary>
    /// The slot-choice menu, used whenever every slot an item could go in is already occupied — both hands for
    /// weapons and shields, both item slots for consumables. Each row names the slot, what it is holding, and
    /// how the swap would move the player's stats, so the decision can be made without leaving the menu.
    /// </summary>
    private void DrawSlotSelect(GameContext ctx, Player player, Item item)
    {
        var r = ctx.Renderer;
        var fonts = ctx.Fonts;

        var isHands = _slotChoices.Length > 0 && _slotChoices[0] == EquipSlot.RightHand;

        const float menuX = 290f;
        const float menuY = 200f;
        const float menuW = 274f;
        const float rowH = 32f;
        var menuH = 46f + _slotChoices.Length * rowH + 10f;

        r.FillRect(menuX, menuY, menuW, menuH, Colors.PanelBg);
        r.DrawRect(menuX, menuY, menuW, menuH, Colors.Border);

        var prompt = isHands ? $"{item.Name} をどちらの手に？" : $"{item.Name} をどちらの欄に？";
        var note = isHands ? "両手がふさがっています" : "アイテム欄が両方ふさがっています";
        fonts.DrawText(r.Handle, prompt, menuX + 10, menuY + 6, 11, Colors.Highlight);
        fonts.DrawText(r.Handle, note, menuX + 10, menuY + 20, 9, Colors.Border);

        var y = menuY + 38f;
        for (var i = 0; i < _slotChoices.Length; i++)
        {
            var slot = _slotChoices[i];
            var selected = i == _slotChoiceCursor;
            if (selected)
                r.FillRect(menuX + 6, y - 2, menuW - 12, rowH - 3, Colors.Highlight);

            var text = selected ? Colors.Black : Colors.White;
            var sub = selected ? Colors.Rgb(60, 50, 20) : Colors.Rgb(170, 165, 158);

            fonts.DrawText(r.Handle, SlotLabel(slot), menuX + 12, y, 12, text);

            var held = player.Equipment.TryGetValue(slot, out var current) ? current.Name : "-";
            var labelW = fonts.Measure(SlotLabel(slot), 12).Item1;
            fonts.DrawText(r.Handle, $"今: {held}", menuX + 16 + Math.Max(36f, labelW), y + 1, 10, text);

            // Every stat this particular swap moves — the gain from the new item and the loss from whatever it
            // displaces, which is what actually distinguishes the two slots. Consumables move none, so the row
            // says what the swap really costs instead: the item that gets put away.
            var deltas = SlotSwapDeltas(player, item, slot);
            var dx = menuX + 12;
            foreach (var (label, before, after) in deltas)
            {
                var sign = after - before;
                var text2 = $"{label} {(sign > 0 ? "+" : "")}{sign}";
                var color = selected
                    ? sub
                    : sign > 0 ? Colors.Rgb(120, 230, 120) : Colors.HpBar;
                fonts.DrawText(r.Handle, text2, dx, y + 14, 9, color);
                var (dw, _) = fonts.Measure(text2 + "  ", 9);
                dx += dw;
            }
            if (deltas.Count == 0)
            {
                var swapNote = current is null ? "変化なし" : $"{current.Name}は鞄に戻る";
                fonts.DrawText(r.Handle, swapNote, dx, y + 14, 9, sub);
            }

            y += rowH;
        }

        ControlHints.Draw(ctx, menuX + 10, menuY + menuH - 14, 9, Colors.Border, ControlHints.Confirm("決定"), ControlHints.Cancel("やめる"));
    }

    /// <summary>Stat changes from putting <paramref name="item"/> in a specific slot, replacing what is there.</summary>
    private static List<(string Label, int Before, int After)> SlotSwapDeltas(Player player, Item item, EquipSlot slot)
    {
        var simulated = new Dictionary<EquipSlot, Item>(player.Equipment) { [slot] = item };
        return AllStatDeltas(player, player.Equipment, simulated);
    }

    /// <summary>Shows the stat this item would change if equipped right now — the item no longer prints
    /// its rank, so this comparison is how the player judges whether it's actually an upgrade.</summary>
    private List<(string Label, int Before, int After)> BuildEquipPreview(Player player, Item item)
    {
        if (item.Category == ItemCategory.Bag)
            return new() { ("鞄容量", player.BagCapacity, item.BagCapacity) };

        // A readied consumable moves no stats at all; what it buys is a free bag slot, so that is what the
        // preview reports — and it correctly shows no gain when the slot it would take is already occupied.
        if (item.IsPocketable)
        {
            if (_selectedItemSlot is not null)
                return new();
            var free = player.BagCapacity - player.Bag.Count;
            var displaces = Player.ItemSlots.All(player.Equipment.ContainsKey);
            return new() { ("鞄の空き", free, free + (displaces ? 0 : 1)) };
        }

        if (!item.IsEquippable)
            return new();

        var slot = TargetSlotFor(player, item);
        var simulated = new Dictionary<EquipSlot, Item>(player.Equipment) { [slot] = item };
        return AllStatDeltas(player, player.Equipment, simulated);
    }

    /// <summary>
    /// Every stat that differs between two equipment layouts. A swap can move two stats at once — putting a
    /// sword where a wand was gains STR and loses INT — so reporting only the new item's own stat would hide
    /// exactly the trade-off the player is deciding on.
    /// </summary>
    private static List<(string Label, int Before, int After)> AllStatDeltas(Player player,
        Dictionary<EquipSlot, Item> before, Dictionary<EquipSlot, Item> after)
    {
        int SwordStr(Dictionary<EquipSlot, Item> e) => player.Stats.Str + e.Values.Where(i => i.Category == ItemCategory.Weapon && i.WeaponKind == WeaponKind.Sword).Sum(i => i.StatBonus);
        int WandInt(Dictionary<EquipSlot, Item> e) => player.Stats.Int + e.Values.Where(i => i.Category == ItemCategory.Weapon && i.WeaponKind == WeaponKind.Wand).Sum(i => i.StatBonus);
        int GauntletDex(Dictionary<EquipSlot, Item> e) => player.Stats.Dex + e.Values.Where(i => i.Category == ItemCategory.Gauntlet).Sum(i => i.StatBonus);
        int ShoesAgl(Dictionary<EquipSlot, Item> e) => player.Stats.Agl + e.Values.Where(i => i.Category == ItemCategory.Shoes).Sum(i => i.StatBonus);
        int TotalDef(Dictionary<EquipSlot, Item> e) => e.Values.Where(i => i.Category is ItemCategory.Armor or ItemCategory.Helmet or ItemCategory.Shield).Sum(i => i.Def);

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

    /// <summary>Where <see cref="EquipItem"/> would actually put this, so the preview matches the outcome.</summary>
    private static EquipSlot TargetSlotFor(Player player, Item item)
    {
        if (item.IsPocketable)
            return Player.ItemSlots.FirstOrDefault(s => !player.Equipment.ContainsKey(s), EquipSlot.Item1);

        return item.Category switch
        {
            // A free hand if there is one, otherwise the right hand — which is where the slot-choice menu's
            // cursor starts, so the preview matches what confirming straight through would do.
            ItemCategory.Weapon or ItemCategory.Shield =>
                Hands.FirstOrDefault(h => !player.Equipment.ContainsKey(h), EquipSlot.RightHand),
            ItemCategory.Armor => EquipSlot.Body,
            ItemCategory.Helmet => EquipSlot.Head,
            ItemCategory.Gauntlet => EquipSlot.Arm,
            ItemCategory.Shoes => EquipSlot.Feet,
            _ => EquipSlot.RightHand,
        };
    }

    public void Draw(GameContext ctx)
    {
        var r = ctx.Renderer;
        var player = ctx.Player!;
        r.DrawTexture(ctx.Sprites.MenuBackdrop, 60, 20, 520, 360);
        r.DrawRect(60, 20, 520, 360, Colors.Border);
        var fonts = ctx.Fonts;

        fonts.DrawText(r.Handle, "装備", 76, 32, 16, Colors.White);
        fonts.DrawText(r.Handle, "枠を選んで外す／鞄のものを選んで装備する", 130, 37, 10, Colors.Border);

        var yy = 60f;
        fonts.DrawText(r.Handle, "装備中:", 76, yy, 12, Colors.Highlight);
        yy += 18;
        var slotLabels = Slots.Select(slot =>
        {
            var has = player.Equipment.TryGetValue(slot, out var eq);
            return $"{SlotLabel(slot)}: {(has ? eq!.Name : "-")}";
        }).ToArray();
        // Eight slots stacked in one column would push the bag off the bottom of the panel, so they run in two
        // columns of four — column-major, matching the order the cursor moves in.
        var slotMaxWidth = Math.Min(230f, MenuNav.MaxLabelWidth(ctx, slotLabels, 11));
        const float slotColumnX = 240f;
        for (var i = 0; i < slotLabels.Length; i++)
        {
            var col = i / SlotRows;
            var row = i % SlotRows;
            MenuNav.DrawRow(ctx, 76 + col * slotColumnX, yy + row * 15, slotMaxWidth, 15, slotLabels[i], 11, i == _cursor);
        }
        yy += SlotRows * 15;

        yy += 8;
        // Naming the worn bag here is what makes swapping to a found one a comparison rather than a guess —
        // it is the only place the current bag is written down.
        var wornBag = player.EquippedBag?.Name ?? "なし";
        fonts.DrawText(r.Handle, $"鞄: {wornBag} ({player.Bag.Count}/{player.BagCapacity})", 76, yy, 12, Colors.Highlight);
        yy += 18;
        var bagLabels = player.Bag.Select(item => $"{item.Name} x{item.Quantity}").ToArray();
        var bagMaxWidth = MenuNav.MaxLabelWidth(ctx, bagLabels, 11);
        for (var i = 0; i < bagLabels.Length; i++)
        {
            MenuNav.DrawRow(ctx, 76, yy, bagMaxWidth, 15, bagLabels[i], 11, Slots.Length + i == _cursor);
            yy += 15;
        }

        if (_phase == Phase.SlotSelect && _pendingSlotItem is { } slotItem)
        {
            DrawSlotSelect(ctx, player, slotItem);
        }
        else if (_phase == Phase.ItemActionMenu && _selectedItem is not null)
        {
            var actionLabels = _availableActions.Select(ActionLabel).ToArray();
            var actionMaxWidth = MenuNav.MaxLabelWidth(ctx, actionLabels, 12);
            var preview = BuildEquipPreview(player, _selectedItem);
            var previewH = preview.Count * 14;
            var menuH = 24 + previewH + actionLabels.Length * 18;
            var menuY = 200f;
            r.FillRect(300, menuY, 260, menuH, Colors.PanelBg);
            r.DrawRect(300, menuY, 260, menuH, Colors.Border);
            fonts.DrawText(r.Handle, _selectedItem.Name, 310, menuY + 4, 11, Colors.Highlight);

            var py = menuY + 20;
            foreach (var (label, before, after) in preview)
            {
                var diff = after - before;
                var diffText = diff switch { > 0 => $"+{diff}", < 0 => diff.ToString(), _ => "±0" };
                var diffColor = diff switch { > 0 => Colors.Rgb(120, 220, 120), < 0 => Colors.HpBar, _ => Colors.White };
                fonts.DrawText(r.Handle, $"{label} {before}→{after} ({diffText})", 310, py, 10, diffColor);
                py += 14;
            }

            for (var i = 0; i < actionLabels.Length; i++)
                MenuNav.DrawRow(ctx, 310, py + i * 18, actionMaxWidth, 16, actionLabels[i], 12, i == _actionCursor);
        }

        if (_message is not null)
            fonts.DrawText(r.Handle, _message, 76, 345, 11, Colors.Gold);
        ControlHints.Draw(ctx, 76, 362, 10, Colors.Border,
            ControlHints.Direction("選ぶ"), ControlHints.Confirm("選択/装備解除"), ControlHints.Cancel("閉じる"));
    }

    private static string SlotLabel(EquipSlot slot) => slot switch
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
}
