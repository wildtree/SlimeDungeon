using SlimeDungeon.Core;
using SlimeDungeon.Domain;

namespace SlimeDungeon.UI;

/// <summary>
/// Casting outside a fight. Only restorative magic appears here — a fireball has nothing to be pointed at in
/// a corridor — and it costs the same MP it would in battle, so healing up before a trip is a real decision
/// rather than free upkeep.
///
/// Shared by the guild and the dungeon so the same spells behave identically in both; it used to live inside
/// the dungeon screen and was unreachable anywhere else.
/// </summary>
public sealed class FieldMagicMenu
{
    private int _cursor;

    public bool IsOpen { get; private set; }

    /// <summary>Opens the menu, or reports why it cannot be opened.</summary>
    public bool TryOpen(Player player, out string reason)
    {
        if (UsableSpells(player).Count == 0)
        {
            reason = "使えるまほうがない";
            return false;
        }

        IsOpen = true;
        _cursor = 0;
        reason = "";
        return true;
    }

    public void Close() => IsOpen = false;

    /// <summary>Runs one frame. Returns a message to show the player when something happened, else null.</summary>
    public string? Update(GameContext ctx)
    {
        var player = ctx.Player!;
        var input = ctx.Input;
        var spells = UsableSpells(player);

        if (MenuNav.Cancelled(input) || spells.Count == 0)
        {
            IsOpen = false;
            return null;
        }

        _cursor = MenuNav.Move(input, _cursor, spells.Count);

        if (!MenuNav.Confirmed(input))
            return null;

        var spell = spells[_cursor];
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
        var player = ctx.Player!;
        var spells = UsableSpells(player);
        var labels = spells
            .Select(s => $"{SpellDefinitions.NameOf(s.Id)} (MP{SpellDefinitions.MpCost(s.Rank)})")
            .ToArray();
        var maxWidth = MenuNav.MaxLabelWidth(ctx, labels, 12);

        var h = 20 * labels.Length + 34;
        var w = maxWidth + 40;
        r.FillRect(x + 3, y + 3, w, h, Colors.Rgb(8, 7, 10));
        r.FillRect(x, y, w, h, Colors.PanelBg);
        r.DrawRect(x, y, w, h, Colors.Border);
        ctx.Fonts.DrawText(r.Handle, "まほう", x + 8, y + 4, 10, Colors.Highlight);
        ctx.Fonts.DrawText(r.Handle, $"MP {player.Stats.Mp}/{player.Stats.MaxMp}", x + w - 62, y + 5, 9, Colors.MpBar);

        for (var i = 0; i < labels.Length; i++)
            MenuNav.DrawRow(ctx, x + 12, y + 22 + i * 20, maxWidth, 18, labels[i], 12, i == _cursor);
    }

    /// <summary>Restorative spells only. Attack magic needs a target and Cure only matters mid-fight, where
    /// poison is inflicted and cleared.</summary>
    public static List<LearnedSpell> UsableSpells(Player player) =>
        player.KnownSpells.Where(s => SpellDefinitions.All[s.Id].Effect == SpellEffect.Heal).ToList();
}
