using SDL3;
using SlimeDungeon.Core;
using SlimeDungeon.Domain;
using SlimeDungeon.Graphics;

namespace SlimeDungeon.UI;

/// <summary>The 'i' overlay: equipment + bag, usable from the guild or the dungeon alike.</summary>
public sealed class InventoryOverlay
{
    private enum Phase { List, ItemActionMenu, HandSelect, ForgetSpellSelect }
    private enum ItemAction { Use, Equip, Discard }

    private Phase _phase = Phase.List;
    private int _cursor;
    private int _actionCursor;
    private int _handCursor;
    private string? _message;
    private Item? _pendingScroll;
    private Item? _selectedBagItem;
    private Item? _pendingHandItem;
    private List<ItemAction> _availableActions = new();

    /// <summary>The two hands, in the order the hand-choice menu lists them.</summary>
    private static readonly EquipSlot[] Hands = { EquipSlot.RightHand, EquipSlot.LeftHand };

    private static readonly EquipSlot[] Slots = { EquipSlot.RightHand, EquipSlot.LeftHand, EquipSlot.Arm, EquipSlot.Body, EquipSlot.Head, EquipSlot.Feet };

    public void Update(GameContext ctx, float dt)
    {
        var input = ctx.Input;
        var player = ctx.Player!;

        switch (_phase)
        {
            case Phase.ForgetSpellSelect:
                UpdateForgetSpell(input, player);
                return;
            case Phase.ItemActionMenu:
                UpdateItemActionMenu(input, player);
                return;
            case Phase.HandSelect:
                UpdateHandSelect(input, player);
                return;
        }

        if (input.WasPressed(SDL.Keycode.Escape) || input.WasPressed(SDL.Keycode.I))
        {
            ctx.ShowInventory = false;
            return;
        }

        var count = Slots.Length + player.Bag.Count;
        _cursor = MenuNav.Move(input, _cursor, count);

        if (!MenuNav.Confirmed(input))
            return;

        if (_cursor < Slots.Length)
        {
            var slot = Slots[_cursor];
            if (player.Equipment.TryGetValue(slot, out var equipped))
            {
                if (!player.BagHasRoom)
                {
                    _message = "鞄がいっぱいだ";
                    return;
                }
                player.Equipment.Remove(slot);
                player.Bag.Add(equipped);
                _message = $"{equipped.Name}を外した";
            }
            return;
        }

        var item = player.Bag[_cursor - Slots.Length];
        _selectedBagItem = item;
        _availableActions = BuildActions(item);
        _actionCursor = 0;
        _phase = Phase.ItemActionMenu;
    }

    private static List<ItemAction> BuildActions(Item item)
    {
        var actions = new List<ItemAction>();
        if (IsUsableFromInventory(item))
            actions.Add(ItemAction.Use);
        if (item.IsEquippable)
            actions.Add(ItemAction.Equip);
        actions.Add(ItemAction.Discard);
        return actions;
    }

    private static bool IsUsableFromInventory(Item item) =>
        item.Category is ItemCategory.Herb or ItemCategory.Potion or ItemCategory.Antidote or ItemCategory.Scroll;

    private static string ActionLabel(ItemAction action) => action switch
    {
        ItemAction.Use => "使う",
        ItemAction.Equip => "装備する",
        ItemAction.Discard => "捨てる",
        _ => action.ToString(),
    };

    private void UpdateItemActionMenu(InputManager input, Player player)
    {
        if (input.WasPressed(SDL.Keycode.Escape))
        {
            _phase = Phase.List;
            _selectedBagItem = null;
            return;
        }

        _actionCursor = MenuNav.Move(input, _actionCursor, _availableActions.Count);

        if (!MenuNav.Confirmed(input) || _selectedBagItem is null)
            return;

        var item = _selectedBagItem;
        switch (_availableActions[_actionCursor])
        {
            case ItemAction.Use:
                UseConsumable(player, item);
                break;
            case ItemAction.Equip:
                EquipItem(player, item);
                break;
            case ItemAction.Discard:
                DiscardItem(player, item);
                break;
        }

        // UseConsumable may switch to ForgetSpellSelect (scroll while 4 spells known); don't stomp that.
        if (_phase == Phase.ItemActionMenu)
        {
            _phase = Phase.List;
            _selectedBagItem = null;
        }

        var count = Slots.Length + player.Bag.Count;
        if (_cursor >= count)
            _cursor = Math.Max(0, count - 1);
    }

    private void UseConsumable(Player player, Item item)
    {
        switch (item.Category)
        {
            case ItemCategory.Herb:
            {
                var amount = ConsumableEffects.HerbHealAmount(item.Rank, player.Stats.MaxHp);
                player.Stats.Hp = Math.Min(player.Stats.MaxHp, player.Stats.Hp + amount);
                RemoveOne(player.Bag, item);
                _message = $"{item.Name}を使った。HPが{amount}回復した";
                break;
            }
            case ItemCategory.Potion:
            {
                var isHp = item.PotionKind == PotionKind.Hp;
                var amount = ConsumableEffects.PotionRestoreAmount(
                    item.Rank, isHp ? player.Stats.MaxHp : player.Stats.MaxMp);
                if (isHp) player.Stats.Hp = Math.Min(player.Stats.MaxHp, player.Stats.Hp + amount);
                else player.Stats.Mp = Math.Min(player.Stats.MaxMp, player.Stats.Mp + amount);
                RemoveOne(player.Bag, item);
                _message = $"{item.Name}を使った。{(isHp ? "HP" : "MP")}が{amount}回復した";
                break;
            }
            case ItemCategory.Antidote:
                _message = "戦闘中でないと使えない";
                break;
            case ItemCategory.Scroll:
                if (player.KnownSpells.Any(s => s.Id == item.SpellTaught))
                {
                    _message = "すでに覚えているまほうだ";
                }
                else if (player.KnownSpells.Count >= Player.MaxKnownSpells)
                {
                    _pendingScroll = item;
                    _phase = Phase.ForgetSpellSelect;
                    _cursor = 0;
                }
                else
                {
                    player.LearnSpell(item.SpellTaught, item.Rank);
                    RemoveOne(player.Bag, item);
                    _message = $"{SpellDefinitions.NameOf(item.SpellTaught)}を覚えた";
                }
                break;
        }
    }

    private void EquipItem(Player player, Item item)
    {
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

                _pendingHandItem = item;
                _handCursor = 0;
                _phase = Phase.HandSelect;
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

    private void DiscardItem(Player player, Item item)
    {
        var name = item.Name;
        RemoveOne(player.Bag, item);
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

    private void UpdateForgetSpell(InputManager input, Player player)
    {
        if (input.WasPressed(SDL.Keycode.Escape))
        {
            _phase = Phase.List;
            _pendingScroll = null;
            return;
        }

        _cursor = MenuNav.Move(input, _cursor, player.KnownSpells.Count);
        if (!MenuNav.Confirmed(input) || _pendingScroll is null)
            return;

        var forget = player.KnownSpells[_cursor];
        player.ForgetSpell(forget.Id);
        player.LearnSpell(_pendingScroll.SpellTaught, _pendingScroll.Rank);
        RemoveOne(player.Bag, _pendingScroll);
        _message = $"{SpellDefinitions.NameOf(forget.Id)}を忘れて{SpellDefinitions.NameOf(_pendingScroll.SpellTaught)}を覚えた";
        _pendingScroll = null;
        _phase = Phase.List;
        _cursor = 0;
    }

    /// <summary>Both hands are full: the player picks which one gets replaced.</summary>
    private void UpdateHandSelect(InputManager input, Player player)
    {
        if (input.WasPressed(SDL.Keycode.Escape))
        {
            _phase = Phase.List;
            _pendingHandItem = null;
            _selectedBagItem = null;
            return;
        }

        _handCursor = MenuNav.Move(input, _handCursor, Hands.Length);

        if (!MenuNav.Confirmed(input) || _pendingHandItem is null)
            return;

        TryEquip(player, _pendingHandItem, Hands[_handCursor]);
        _pendingHandItem = null;
        _selectedBagItem = null;
        _phase = Phase.List;

        var count = Slots.Length + player.Bag.Count;
        if (_cursor >= count)
            _cursor = Math.Max(0, count - 1);
    }

    /// <summary>
    /// The hand-choice menu. Each row names the hand, what it is holding, and how the swap would move the
    /// relevant stat, so the decision can be made without leaving the menu.
    /// </summary>
    private void DrawHandSelect(GameContext ctx, Player player, Item item)
    {
        var r = ctx.Renderer;
        var fonts = ctx.Fonts;

        const float menuX = 290f;
        const float menuY = 200f;
        const float menuW = 274f;
        const float rowH = 32f;
        var menuH = 46f + Hands.Length * rowH + 10f;

        r.FillRect(menuX, menuY, menuW, menuH, Colors.PanelBg);
        r.DrawRect(menuX, menuY, menuW, menuH, Colors.Border);

        fonts.DrawText(r.Handle, $"{item.Name} をどちらの手に？", menuX + 10, menuY + 6, 11, Colors.Highlight);
        fonts.DrawText(r.Handle, "両手がふさがっています", menuX + 10, menuY + 20, 9, Colors.Border);

        var y = menuY + 38f;
        for (var i = 0; i < Hands.Length; i++)
        {
            var hand = Hands[i];
            var selected = i == _handCursor;
            if (selected)
                r.FillRect(menuX + 6, y - 2, menuW - 12, rowH - 3, Colors.Highlight);

            var text = selected ? Colors.Black : Colors.White;
            var sub = selected ? Colors.Rgb(60, 50, 20) : Colors.Rgb(170, 165, 158);

            fonts.DrawText(r.Handle, SlotLabel(hand), menuX + 12, y, 12, text);

            var held = player.Equipment.TryGetValue(hand, out var current) ? current.Name : "-";
            fonts.DrawText(r.Handle, $"今: {held}", menuX + 52, y + 1, 10, text);

            // Every stat this particular swap moves — the gain from the new item and the loss from whatever it
            // displaces, which is what actually distinguishes the two hands.
            var deltas = HandSwapDeltas(player, item, hand);
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
                fonts.DrawText(r.Handle, "変化なし", dx, y + 14, 9, sub);

            y += rowH;
        }

        fonts.DrawText(r.Handle, "[Enter]決定 [Esc]やめる", menuX + 10, menuY + menuH - 14, 9, Colors.Border);
    }

    /// <summary>Stat changes from putting <paramref name="item"/> in a specific hand, replacing what is there.</summary>
    private static List<(string Label, int Before, int After)> HandSwapDeltas(Player player, Item item, EquipSlot hand)
    {
        var simulated = new Dictionary<EquipSlot, Item>(player.Equipment) { [hand] = item };
        return AllStatDeltas(player, player.Equipment, simulated);
    }

    /// <summary>Shows the stat this item would change if equipped right now — the item no longer prints
    /// its rank, so this comparison is how the player judges whether it's actually an upgrade.</summary>
    private static List<(string Label, int Before, int After)> BuildEquipPreview(Player player, Item item)
    {
        if (item.Category == ItemCategory.Bag)
            return new() { ("鞄容量", player.BagCapacity, item.BagCapacity) };

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

    private static EquipSlot TargetSlotFor(Player player, Item item) => item.Category switch
    {
        // A free hand if there is one, otherwise the right hand — which is where the hand-choice menu's cursor
        // starts, so the preview in the action menu matches what confirming straight through would do.
        ItemCategory.Weapon or ItemCategory.Shield =>
            Hands.FirstOrDefault(h => !player.Equipment.ContainsKey(h), EquipSlot.RightHand),
        ItemCategory.Armor => EquipSlot.Body,
        ItemCategory.Helmet => EquipSlot.Head,
        ItemCategory.Gauntlet => EquipSlot.Arm,
        ItemCategory.Shoes => EquipSlot.Feet,
        _ => EquipSlot.RightHand,
    };

    private static void RemoveOne(List<Item> bag, Item item)
    {
        item.Quantity--;
        if (item.Quantity <= 0)
            bag.Remove(item);
    }

    public void Draw(GameContext ctx)
    {
        var r = ctx.Renderer;
        var player = ctx.Player!;
        r.DrawTexture(ctx.Sprites.MenuBackdrop, 60, 20, 520, 360);
        r.DrawRect(60, 20, 520, 360, Colors.Border);
        var fonts = ctx.Fonts;

        if (_phase == Phase.ForgetSpellSelect)
        {
            fonts.DrawText(r.Handle, "まほうを4つ覚えている。どれを忘れる？", 76, 36, 13, Colors.Highlight);
            var spellLabels = player.KnownSpells.Select(s => $"{SpellDefinitions.NameOf(s.Id)} ({s.Rank.Label()})").ToArray();
            var spellMaxWidth = MenuNav.MaxLabelWidth(ctx, spellLabels, 12);
            var y = 66f;
            for (var i = 0; i < spellLabels.Length; i++)
            {
                MenuNav.DrawRow(ctx, 76, y, spellMaxWidth, 16, spellLabels[i], 12, i == _cursor);
                y += 18;
            }
            fonts.DrawText(r.Handle, "[Esc]キャンセル", 76, 360, 10, Colors.Border);
            return;
        }

        fonts.DrawText(r.Handle, "持ち物", 76, 32, 16, Colors.White);

        var yy = 60f;
        fonts.DrawText(r.Handle, "装備:", 76, yy, 12, Colors.Highlight);
        yy += 18;
        var slotLabels = Slots.Select(slot =>
        {
            var has = player.Equipment.TryGetValue(slot, out var eq);
            return $"{SlotLabel(slot)}: {(has ? eq!.Name : "-")}";
        }).ToArray();
        var slotMaxWidth = MenuNav.MaxLabelWidth(ctx, slotLabels, 11);
        for (var i = 0; i < slotLabels.Length; i++)
        {
            MenuNav.DrawRow(ctx, 76, yy, slotMaxWidth, 15, slotLabels[i], 11, i == _cursor);
            yy += 15;
        }

        yy += 8;
        fonts.DrawText(r.Handle, $"鞄 ({player.Bag.Count}/{player.BagCapacity}):", 76, yy, 12, Colors.Highlight);
        yy += 18;
        var bagLabels = player.Bag.Select(item => $"{item.Name} x{item.Quantity}").ToArray();
        var bagMaxWidth = MenuNav.MaxLabelWidth(ctx, bagLabels, 11);
        for (var i = 0; i < bagLabels.Length; i++)
        {
            MenuNav.DrawRow(ctx, 76, yy, bagMaxWidth, 15, bagLabels[i], 11, Slots.Length + i == _cursor);
            yy += 15;
        }

        if (_phase == Phase.HandSelect && _pendingHandItem is { } handItem)
        {
            DrawHandSelect(ctx, player, handItem);
        }
        else if (_phase == Phase.ItemActionMenu && _selectedBagItem is not null)
        {
            var actionLabels = _availableActions.Select(ActionLabel).ToArray();
            var actionMaxWidth = MenuNav.MaxLabelWidth(ctx, actionLabels, 12);
            var preview = BuildEquipPreview(player, _selectedBagItem);
            var previewH = preview.Count * 14;
            var menuH = 24 + previewH + actionLabels.Length * 18;
            var menuY = 200f;
            r.FillRect(300, menuY, 260, menuH, Colors.PanelBg);
            r.DrawRect(300, menuY, 260, menuH, Colors.Border);
            fonts.DrawText(r.Handle, _selectedBagItem.Name, 310, menuY + 4, 11, Colors.Highlight);

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
        fonts.DrawText(r.Handle, "[Enter]選択/装備解除  [I/Esc]閉じる", 76, 362, 10, Colors.Border);
    }

    private static string SlotLabel(EquipSlot slot) => slot switch
    {
        EquipSlot.RightHand => "右手",
        EquipSlot.LeftHand => "左手",
        EquipSlot.Arm => "腕",
        EquipSlot.Body => "胴",
        EquipSlot.Head => "頭",
        EquipSlot.Feet => "足",
        _ => slot.ToString(),
    };
}
