using SlimeDungeon.Core;
using SlimeDungeon.Domain;

namespace SlimeDungeon.UI;

/// <summary>
/// The pack, and only the pack.
///
/// This and <see cref="InventoryOverlay"/> were one screen that listed the eight equipment slots and the bag
/// together and offered every verb on everything. That made the common case — "what have I got, and can I drink
/// it" — a hunt through a list of eight mostly-empty slots first, and it meant a readied herb appeared in the
/// same list as the herbs in the bag with no way to tell which was which. They are separate screens now: this
/// one is what you are carrying, and anything worn or readied is deliberately absent from it until it is taken
/// off, so what is on this list is exactly what is in the bag.
/// </summary>
public sealed class ItemOverlay
{
    private enum Phase { List, ActionMenu, Appraise, ForgetSpellSelect }
    private enum ItemAction { Use, Appraise, Discard }

    private Phase _phase = Phase.List;
    private int _cursor;
    private int _actionCursor;
    private string? _message;
    private Item? _selected;
    private Item? _pendingScroll;
    private List<ItemAction> _actions = new();

    /// <summary>Non-null only while a dungeon is being explored; see <see cref="GameContext.RevealFullMap"/>.</summary>
    private Action? _revealFullMap;

    /// <summary>
    /// Wipes what the last visit left behind. One instance lives for the whole run, so without this the status
    /// line still reported an action taken an hour ago in a different part of the game.
    /// </summary>
    private void Reset()
    {
        _phase = Phase.List;
        _cursor = 0;
        _actionCursor = 0;
        _message = null;
        _selected = null;
        _pendingScroll = null;
        _actions = new List<ItemAction>();
    }

    public void Update(GameContext ctx, float dt)
    {
        var input = ctx.Input;
        var player = ctx.Player!;
        _revealFullMap = ctx.RevealFullMap;

        switch (_phase)
        {
            case Phase.ForgetSpellSelect:
                UpdateForgetSpell(input, player);
                return;
            case Phase.Appraise:
                if (MenuNav.Confirmed(input) || MenuNav.Cancelled(input))
                    _phase = Phase.ActionMenu;
                return;
            case Phase.ActionMenu:
                UpdateActionMenu(input, player);
                return;
        }

        if (MenuNav.Cancelled(input))
        {
            ctx.ShowItems = false;
            Reset();
            return;
        }

        _cursor = MenuNav.Move(input, _cursor, player.Bag.Count);

        if (!MenuNav.Confirmed(input) || player.Bag.Count == 0)
            return;

        _selected = player.Bag[_cursor];
        _actions = BuildActions(_selected);
        _actionCursor = 0;
        _phase = Phase.ActionMenu;
    }

    /// <summary>
    /// つかう is offered only where it would do something. Appraising and throwing away always apply, so the
    /// menu is never empty and never has an entry that answers "戦闘中でないと使えない".
    /// </summary>
    private static List<ItemAction> BuildActions(Item item)
    {
        var actions = new List<ItemAction>();
        if (ItemInfo.UsableOutsideCombat(item))
            actions.Add(ItemAction.Use);
        actions.Add(ItemAction.Appraise);
        actions.Add(ItemAction.Discard);
        return actions;
    }

    private static string ActionLabel(ItemAction action) => action switch
    {
        ItemAction.Use => "つかう",
        ItemAction.Appraise => "鑑定",
        _ => "捨てる",
    };

    private void UpdateActionMenu(InputManager input, Player player)
    {
        if (MenuNav.Cancelled(input))
        {
            _phase = Phase.List;
            _selected = null;
            return;
        }

        _actionCursor = MenuNav.Move(input, _actionCursor, _actions.Count);

        if (!MenuNav.Confirmed(input) || _selected is null)
            return;

        var item = _selected;
        switch (_actions[_actionCursor])
        {
            case ItemAction.Use:
                Use(player, item);
                break;
            case ItemAction.Appraise:
                _phase = Phase.Appraise;
                return;
            case ItemAction.Discard:
                var name = item.Name;
                player.ConsumeOne(item);
                _message = $"{name}を捨てた";
                break;
        }

        // Using a scroll can open the forget-a-spell list; do not stomp it.
        if (_phase == Phase.ActionMenu)
        {
            _phase = Phase.List;
            _selected = null;
        }

        if (_cursor >= player.Bag.Count)
            _cursor = Math.Max(0, player.Bag.Count - 1);
    }

    private void Use(Player player, Item item)
    {
        switch (item.Category)
        {
            case ItemCategory.Herb:
            {
                var amount = ConsumableEffects.HerbHealAmount(item.Rank, player.Stats.MaxHp);
                player.Stats.Hp = Math.Min(player.Stats.MaxHp, player.Stats.Hp + amount);
                player.ConsumeOne(item);
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
                player.ConsumeOne(item);
                _message = $"{item.Name}を使った。{(isHp ? "HP" : "MP")}が{amount}回復した";
                break;
            }
            case ItemCategory.FullMapReveal:
                if (_revealFullMap is null)
                {
                    _message = "ダンジョンの中でしか使えない";
                    break;
                }
                player.ConsumeOne(item);
                _revealFullMap();
                _message = $"{item.Name}を使った";
                break;
            case ItemCategory.Scroll:
                ReadScroll(player, item);
                break;
        }
    }

    /// <summary>
    /// A scroll for a spell already known is an upgrade, not a duplicate: the same spell at a higher rank
    /// replaces it in place and costs no extra slot.
    /// </summary>
    private void ReadScroll(Player player, Item item)
    {
        var known = player.KnownSpells.FirstOrDefault(s => s.Id == item.SpellTaught);
        var name = SpellDefinitions.NameOf(item.SpellTaught);

        if (known is not null && item.Rank <= known.Rank)
        {
            _message = item.Rank == known.Rank
                ? "すでに覚えているまほうだ"
                : $"覚えている{name}のほうが上位だ";
        }
        else if (known is null && player.KnownSpells.Count >= Player.MaxKnownSpells)
        {
            _pendingScroll = item;
            _phase = Phase.ForgetSpellSelect;
            _cursor = 0;
        }
        else
        {
            var upgraded = known is not null;
            player.LearnSpell(item.SpellTaught, item.Rank);
            player.ConsumeOne(item);
            _message = upgraded
                ? $"{name}が{known!.Rank.Label()}から{item.Rank.Label()}になった"
                : $"{name}を覚えた";
        }
    }

    private void UpdateForgetSpell(InputManager input, Player player)
    {
        if (MenuNav.Cancelled(input))
        {
            _phase = Phase.List;
            _pendingScroll = null;
            _selected = null;
            _cursor = 0;
            return;
        }

        _cursor = MenuNav.Move(input, _cursor, player.KnownSpells.Count);
        if (!MenuNav.Confirmed(input) || _pendingScroll is null)
            return;

        var forget = player.KnownSpells[_cursor];
        player.ForgetSpell(forget.Id);
        player.LearnSpell(_pendingScroll.SpellTaught, _pendingScroll.Rank);
        player.ConsumeOne(_pendingScroll);
        _message = $"{SpellDefinitions.NameOf(forget.Id)}を忘れて{SpellDefinitions.NameOf(_pendingScroll.SpellTaught)}を覚えた";
        _pendingScroll = null;
        _selected = null;
        _phase = Phase.List;
        _cursor = 0;
    }

    // The panel, and the columns inside it.
    private const float PanelX = 60f;
    private const float PanelY = 20f;
    private const float PanelW = 520f;
    private const float PanelH = 360f;
    private const float ListX = 76f;

    public void Draw(GameContext ctx)
    {
        var r = ctx.Renderer;
        var fonts = ctx.Fonts;
        var player = ctx.Player!;

        r.DrawTexture(ctx.Sprites.MenuBackdrop, PanelX, PanelY, PanelW, PanelH);
        r.DrawRect(PanelX, PanelY, PanelW, PanelH, Colors.Border);

        if (_phase == Phase.ForgetSpellSelect)
        {
            DrawForgetSpell(ctx, player);
            return;
        }

        fonts.DrawText(r.Handle, "アイテム", ListX, 32, 16, Colors.White);
        var wornBag = player.EquippedBag?.Name ?? "なし";
        fonts.DrawText(r.Handle, $"{wornBag} ({player.Bag.Count}/{player.BagCapacity})",
            ListX + 74, 36, 11, Colors.Highlight);

        var y = 60f;
        if (player.Bag.Count == 0)
        {
            fonts.DrawText(r.Handle, "何も持っていない", ListX, y, 12, Colors.Border);
            fonts.DrawText(r.Handle, "装備中のものは「装備」から確認できます", ListX, y + 20, 10, Colors.Border);
        }
        else
        {
            var labels = player.Bag.Select(i => i.Quantity > 1 ? $"{i.Name} x{i.Quantity}" : i.Name).ToArray();
            var maxWidth = Math.Min(220f, MenuNav.MaxLabelWidth(ctx, labels, 11));
            for (var i = 0; i < labels.Length; i++)
            {
                r.DrawTexture(ctx.Sprites.ItemIcon(player.Bag[i]), ListX - 1, y - 1, 14, 14);
                MenuNav.DrawRow(ctx, ListX + 20, y, maxWidth, 15, labels[i], 11, i == _cursor);
                y += 16;
            }
        }

        if (_phase == Phase.Appraise && _selected is not null)
            DrawAppraisal(ctx, _selected);
        else if (_phase == Phase.ActionMenu && _selected is not null)
            DrawActionMenu(ctx, _selected);

        if (_message is not null)
            fonts.DrawText(r.Handle, _message, ListX, 345, 11, Colors.Gold);
        ControlHints.Draw(ctx, ListX, 362, 10, Colors.Border,
            ControlHints.Direction("選ぶ"), ControlHints.Confirm("決定"), ControlHints.Cancel("閉じる"));
    }

    private void DrawActionMenu(GameContext ctx, Item item)
    {
        var r = ctx.Renderer;
        var labels = _actions.Select(ActionLabel).ToArray();
        var maxWidth = MenuNav.MaxLabelWidth(ctx, labels, 12);
        var w = Math.Max(140f, maxWidth + 40f);
        var h = 26f + labels.Length * 20f;
        const float x = 350f;
        const float y = 150f;

        r.FillRect(x + 3, y + 4, w, h, Colors.Rgb(6, 6, 10, 170));
        r.FillRect(x, y, w, h, Colors.PanelBg);
        r.DrawRect(x, y, w, h, Colors.Border);

        ctx.Renderer.DrawTexture(ctx.Sprites.ItemIcon(item), x + 10, y + 5, 14, 14);
        ctx.Fonts.DrawText(r.Handle, item.Name, x + 28, y + 5, 11, Colors.Highlight);

        for (var i = 0; i < labels.Length; i++)
            MenuNav.DrawRow(ctx, x + 14, y + 24 + i * 20, w - 28, 18, labels[i], 12, i == _actionCursor);
    }

    /// <summary>
    /// The appraisal. Everything the flat item record knows, written out: what it is, its rank, what a shop
    /// would give for it, and what it actually does.
    /// </summary>
    private static void DrawAppraisal(GameContext ctx, Item item)
    {
        var r = ctx.Renderer;
        var fonts = ctx.Fonts;

        var effects = ItemInfo.EffectLines(item);
        const float w = 300f;
        // Header, the three fixed rows, the effect lines, and a clear band at the foot for the hint — which
        // was overlapping the last effect line when the height only allowed for the lines themselves.
        var h = 120f + effects.Count * 15f;
        var x = 320f - w / 2f + 130f;
        var y = 200f - h / 2f + 40f;

        r.FillRect(x + 4, y + 5, w, h, Colors.Rgb(4, 4, 8, 180));
        r.FillRect(x, y, w, h, Colors.Rgb(28, 24, 20));
        r.DrawRect(x, y, w, h, Colors.Gold);

        r.DrawTexture(ctx.Sprites.ItemIcon(item), x + 12, y + 10, 24, 24);
        fonts.DrawText(r.Handle, item.Name, x + 44, y + 12, 13, Colors.Highlight);
        fonts.DrawText(r.Handle, "鑑定", x + w - 34, y + 12, 10, Colors.Rgb(150, 142, 128));

        var ly = y + 42f;
        r.FillRect(x + 12, ly - 6, w - 24, 1, Colors.Rgb(74, 64, 50));

        void Row(string label, string value)
        {
            fonts.DrawText(r.Handle, label, x + 12, ly + 1, 10, Colors.Rgb(158, 150, 138));
            fonts.DrawText(r.Handle, value, x + 84, ly, 11, Colors.White);
            ly += 16f;
        }

        Row("種別", ItemInfo.CategoryLabel(item));
        Row("ランク", item.Rank.Label());
        // Quantity matters to the figure, because the shop buys a bag entry whole.
        Row("売却価格", item.Quantity > 1
            ? $"{item.SellValue * item.Quantity}G （1個 {item.SellValue}G）"
            : $"{item.SellValue}G");

        ly += 4f;
        foreach (var line in effects)
        {
            fonts.DrawText(r.Handle, line, x + 12, ly, 10, Colors.Rgb(196, 214, 190));
            ly += 15f;
        }

        ControlHints.DrawCentered(ctx, x + w / 2f, y + h - 18f, 10, Colors.Rgb(168, 160, 148),
            ControlHints.Confirm("閉じる"));
    }

    private void DrawForgetSpell(GameContext ctx, Player player)
    {
        var r = ctx.Renderer;
        var fonts = ctx.Fonts;

        fonts.DrawText(r.Handle, "まほうを4つ覚えている。どれを忘れる？", ListX, 36, 13, Colors.Highlight);
        var labels = player.KnownSpells
            .Select(s => $"{SpellDefinitions.NameOf(s.Id)} ({s.Rank.Label()})")
            .ToArray();
        var maxWidth = MenuNav.MaxLabelWidth(ctx, labels, 12);
        var y = 66f;
        for (var i = 0; i < labels.Length; i++)
        {
            MenuNav.DrawRow(ctx, ListX, y, maxWidth, 16, labels[i], 12, i == _cursor);
            y += 18;
        }
        ControlHints.Draw(ctx, ListX, 360, 10, Colors.Border, ControlHints.Cancel("やめる"));
    }
}
