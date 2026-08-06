using SlimeDungeon.Core;
using SlimeDungeon.Domain;

namespace SlimeDungeon.UI;

/// <summary>
/// Casting outside a fight, and the only place the spellbook can be read.
///
/// This used to list restorative magic alone, which made casting straightforward and left the player with no
/// way to find out what they knew: a spell learned from a scroll was invisible until the next slime turned up.
/// Every spell is listed now, with the ones that need something to point at greyed and unselectable — the list
/// is the spellbook, and being unable to cast a fireball in a corridor is stated rather than hidden.
///
/// Shared by the guild and the dungeon so the same spells behave identically in both.
/// </summary>
public sealed class FieldMagicMenu
{
    private int _cursor;

    public bool IsOpen { get; private set; }

    /// <summary>Opens the menu, or reports why it cannot be opened.</summary>
    public bool TryOpen(Player player, out string reason)
    {
        if (player.KnownSpells.Count == 0)
        {
            reason = "まほうを覚えていない";
            return false;
        }

        IsOpen = true;
        // Opens on something castable if there is one, so the common case is one keypress.
        var firstUsable = player.KnownSpells.FindIndex(IsCastableInField);
        _cursor = firstUsable >= 0 ? firstUsable : 0;
        reason = "";
        return true;
    }

    public void Close() => IsOpen = false;

    /// <summary>
    /// Whether a spell does anything with no enemy in front of you. Healing does; attack magic has nothing to
    /// hit, and Cure only matters where poison is inflicted, which is mid-fight.
    /// </summary>
    public static bool IsCastableInField(LearnedSpell spell) =>
        SpellDefinitions.All[spell.Id].Effect == SpellEffect.Heal;

    /// <summary>Runs one frame. Returns a message to show the player when something happened, else null.</summary>
    public string? Update(GameContext ctx)
    {
        var player = ctx.Player!;
        var input = ctx.Input;
        var spells = player.KnownSpells;

        if (MenuNav.Cancelled(input) || spells.Count == 0)
        {
            IsOpen = false;
            return null;
        }

        _cursor = MenuNav.Move(input, _cursor, spells.Count);

        if (!MenuNav.Confirmed(input))
            return null;

        var spell = spells[_cursor];

        // Greyed rows stay selectable so the cursor never skips over anything — landing on one and being told
        // why is clearer than a cursor that jumps past rows for reasons the player has to infer.
        if (!IsCastableInField(spell))
        {
            IsOpen = false;
            return $"{SpellDefinitions.NameOf(spell.Id)}は戦闘中しか使えない";
        }

        var cost = SpellDefinitions.MpCost(spell.Rank);
        if (player.Stats.Mp < cost)
        {
            IsOpen = false;
            return "MPが足りない";
        }

        if (player.Stats.Hp >= player.Stats.MaxHp)
        {
            IsOpen = false;
            return "HPは満タンだ";
        }

        player.Stats.Mp -= cost;
        player.Counters.SpellsCast++;
        var amount = SpellDefinitions.HealAmount(spell.Rank, player.Stats.MaxHp);
        var before = player.Stats.Hp;
        player.Stats.Hp = Math.Min(player.Stats.MaxHp, player.Stats.Hp + amount);
        IsOpen = false;
        return $"{SpellDefinitions.NameOf(spell.Id)}！ HPが{player.Stats.Hp - before}回復した";
    }

    public void Draw(GameContext ctx, float x, float y)
    {
        var r = ctx.Renderer;
        var fonts = ctx.Fonts;
        var player = ctx.Player!;
        var spells = player.KnownSpells;

        var labels = spells
            .Select(s => $"{SpellDefinitions.NameOf(s.Id)} ({s.Rank.Label()}) MP{SpellDefinitions.MpCost(s.Rank)}")
            .ToArray();
        var maxWidth = MenuNav.MaxLabelWidth(ctx, labels, 12);

        var h = 20 * labels.Length + 48;
        var w = Math.Max(maxWidth + 40, 176f);
        r.FillRect(x + 3, y + 3, w, h, Colors.Rgb(8, 7, 10));
        r.FillRect(x, y, w, h, Colors.PanelBg);
        r.DrawRect(x, y, w, h, Colors.Border);
        fonts.DrawText(r.Handle, "まほう", x + 8, y + 4, 10, Colors.Highlight);
        fonts.DrawText(r.Handle, $"MP {player.Stats.Mp}/{player.Stats.MaxMp}", x + w - 62, y + 5, 9, Colors.MpBar);

        for (var i = 0; i < labels.Length; i++)
        {
            var rowY = y + 22 + i * 20;
            var castable = IsCastableInField(spells[i]);
            var selected = i == _cursor;

            // A greyed row still highlights when the cursor is on it — otherwise the cursor vanishes whenever
            // it lands on one and the player cannot tell where they are in the list.
            if (selected)
                r.FillRect(x + 8, rowY - 2, w - 16, 18, castable ? Colors.Highlight : Colors.Rgb(84, 78, 70));

            var ink = selected
                ? (castable ? Colors.Black : Colors.Rgb(206, 200, 190))
                : (castable ? Colors.White : Colors.Rgb(118, 112, 104));
            fonts.DrawText(r.Handle, labels[i], x + 12, rowY, 12, ink);
        }

        fonts.DrawText(r.Handle, "灰色のまほうは戦闘中のみ", x + 10, y + h - 15, 9, Colors.Rgb(140, 134, 126));
    }
}
