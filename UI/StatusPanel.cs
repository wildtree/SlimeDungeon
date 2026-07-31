using SlimeDungeon.Core;
using SlimeDungeon.Domain;

namespace SlimeDungeon.UI;

/// <summary>Always-visible right-side player status panel (name/rank/LV/HP+MP/EXP/stats/gold/equipment).</summary>
public static class StatusPanel
{
    public const float Width = 240f;

    public static void Draw(GameContext ctx, float x, float y, float h)
    {
        var player = ctx.Player;
        var r = ctx.Renderer;
        var fonts = ctx.Fonts;

        r.FillRect(x, y, Width, h, Colors.PanelBg);
        r.DrawRect(x, y, Width, h, Colors.Border);

        if (player is null)
            return;

        var pad = 8f;
        var cx = x + pad;

        var cardBottom = DrawGuildCard(ctx, player, x + pad, y + pad, Width - pad * 2);
        var cy = cardBottom + 10;

        fonts.DrawText(r.Handle, $"STR {player.EffectiveStr,3}   INT {player.EffectiveInt,3}", cx, cy, 10, Colors.White);
        cy += 14;
        fonts.DrawText(r.Handle, $"DEX {player.EffectiveDex,3}   AGL {player.EffectiveAgl,3}", cx, cy, 10, Colors.White);
        cy += 14;
        fonts.DrawText(r.Handle, $"DEF {player.TotalDef,3}", cx, cy, 10, Colors.White);
        cy += 18;
        fonts.DrawText(r.Handle, $"所持金 {player.Gold} G", cx, cy, 11, Colors.Gold);
        cy += 20;

        // Eight rows where there were six, so the rows and the spacing below them are tighter and the date is
        // one line instead of two — otherwise the block runs into the key hints along the bottom.
        fonts.DrawText(r.Handle, "装備", cx, cy, 10, Colors.Highlight);
        cy += 13;
        foreach (var slot in new[]
                 {
                     EquipSlot.RightHand, EquipSlot.LeftHand, EquipSlot.Arm, EquipSlot.Body,
                     EquipSlot.Head, EquipSlot.Feet, EquipSlot.Item1, EquipSlot.Item2,
                 })
        {
            var name = player.Equipment.TryGetValue(slot, out var item) ? item.Name : "-";
            // The item slots are what the player can actually reach in a fight, so they are picked out rather
            // than reading as two more lines of gear.
            var color = IsItemSlot(slot)
                ? player.Equipment.ContainsKey(slot) ? Colors.Highlight : Colors.Border
                : Colors.White;
            fonts.DrawText(r.Handle, $"{SlotLabel(slot)}: {name}", cx, cy, 9, color);
            cy += 11;
        }

        // The bag is worn like everything else, so it reads as the last equipment row rather than a separate
        // line — which also keeps its contents count without spending another row on it.
        var bagName = player.EquippedBag?.Name ?? "-";
        fonts.DrawText(r.Handle, $"鞄: {bagName} ({player.Bag.Count}/{player.BagCapacity})", cx, cy, 9, Colors.Highlight);
        cy += 15;
        var today = GameCalendar.FromDayNumber(player.DayCount);
        fonts.DrawText(r.Handle, $"新暦{GameCalendar.YearLabel(today.Year)} {today.MonthName}{today.Day}日",
            cx, cy, 9, Colors.White);

        fonts.DrawText(r.Handle, "[I]持ち物 [S]討伐記録", x + pad, y + h - 18, 9, Colors.Border);
    }

    // ---- Guild card -----------------------------------------------------------

    /// <summary>
    /// The identity half of the panel, presented as the adventurer's guild licence: a parchment card with a
    /// portrait, a stamped rank seal, and the vitals along the bottom. Returns the card's bottom edge so the
    /// rest of the panel can flow underneath it.
    /// </summary>
    private static float DrawGuildCard(GameContext ctx, Player player, float x, float y, float w)
    {
        var r = ctx.Renderer;
        var fonts = ctx.Fonts;

        var parchment = Colors.Rgb(228, 212, 178);
        var parchmentShade = Colors.Rgb(206, 188, 150);
        var ink = Colors.Rgb(58, 44, 30);
        var inkSoft = Colors.Rgb(104, 86, 62);
        var band = Colors.Rgb(96, 64, 38);
        var edge = Colors.Rgb(176, 142, 74);

        const float headerH = 19f;
        // Tall enough that the portrait and rank seal finish before the vitals begin — at a shorter height the
        // seal and the HP row ran into each other.
        const float bodyH = 140f;
        var h = headerH + bodyH;

        // Card stock: a drop shadow, the gold edge, then the parchment face with a shaded lower half.
        r.FillRect(x + 3, y + 3, w, h, Colors.Rgb(14, 12, 16));
        r.FillRect(x, y, w, h, edge);
        r.FillRect(x + 2, y + 2, w - 4, h - 4, parchment);
        r.FillRect(x + 2, y + headerH + 72, w - 4, h - headerH - 74, parchmentShade);

        // Header band: guild emblem, title, and a card number.
        r.FillRect(x + 2, y + 2, w - 4, headerH - 2, band);
        var (emblem, _) = ctx.Sprites.Slime(SlimeColor.Green);
        r.DrawTexture(emblem, x + 5, y + 3, 15, 15);
        fonts.DrawText(r.Handle, "冒険者証", x + 24, y + 4, 11, parchment);
        var cardNo = $"No.{CardNumber(player)}";
        var (noW, _) = fonts.Measure(cardNo, 9);
        fonts.DrawText(r.Handle, cardNo, x + w - noW - 6, y + 6, 9, Colors.Rgb(210, 186, 140));

        // Portrait, framed like a photograph pasted on.
        const float portrait = 42f;
        var px = x + 8;
        var py = y + headerH + 6;
        r.FillRect(px - 1, py - 1, portrait + 2, portrait + 2, ink);
        r.FillRect(px, py, portrait, portrait, Colors.Rgb(46, 52, 64));
        var sprite = ctx.Sprites.PlayerSprite(player.Gender, Direction.Down, WalkFrame.A);
        r.DrawTexture(sprite, px + 1, py + 1, portrait - 2, portrait - 2);

        // Name with the gender alongside it, freeing the line below for the title the holder is bearing —
        // which is where a licence would carry its holder's standing.
        var textX = px + portrait + 8;
        fonts.DrawText(r.Handle, player.Name, textX, py, 14, ink);
        var (nameW, _) = fonts.Measure(player.Name, 14);
        fonts.DrawText(r.Handle, $"（{GenderLabel(player.Gender)}）", textX + nameW + 3, py + 5, 9, inkSoft);

        var titleText = player.DisplayedTitle is { } shown ? Titles.NameOf(shown) : "称号なし";
        var titleColor = player.DisplayedTitle is null ? inkSoft : Colors.Rgb(132, 58, 40);
        fonts.DrawText(r.Handle, titleText, textX, py + 17, 10, titleColor);

        // Rank seal with the rank letter stamped on it, and the level beside it.
        const float seal = 26f;
        var sx = textX;
        var sy = py + 30;
        r.DrawTexture(ctx.Sprites.RankSeal, sx, sy, seal, seal);
        var rankText = player.Rank.Label();
        var (rankW, rankH) = fonts.Measure(rankText, 13);
        fonts.DrawText(r.Handle, rankText, sx + (seal - rankW) / 2f, sy + (seal - rankH) / 2f - 1, 13, Colors.Rgb(240, 216, 150));

        fonts.DrawText(r.Handle, "RANK", sx + seal + 6, sy + 1, 8, inkSoft);
        fonts.DrawText(r.Handle, $"LV {player.Level}", sx + seal + 6, sy + 11, 12, ink);

        // Registration date, the way a licence carries an issue date.
        var reg = GameCalendar.FromDayNumber(player.StartDay);
        fonts.DrawText(r.Handle, $"登録 {reg.MonthName}{reg.Day}日", x + w - 84, y + headerH + 6, 8, inkSoft);

        // Vitals across the foot of the card.
        var barX = x + 8;
        var barW = w - 16;
        var by = y + headerH + 68;
        DrawCardBar(ctx, barX, by, barW, "HP", player.Stats.Hp, player.Stats.MaxHp, Colors.HpBar, ink);
        by += 22;
        DrawCardBar(ctx, barX, by, barW, "MP", player.Stats.Mp, player.Stats.MaxMp, Colors.MpBar, ink);
        by += 22;
        DrawCardBar(ctx, barX, by, barW, "EXP", player.Exp, player.ExpToNext, Colors.ExpBar, ink);

        return y + h;
    }

    /// <summary>A card number that is stable for a given adventurer without needing to be stored anywhere.</summary>
    private static int CardNumber(Player player)
    {
        var hash = 17;
        foreach (var ch in player.Name)
            hash = hash * 31 + ch;
        hash = hash * 31 + (int)player.Gender;
        return Math.Abs(hash) % 9000 + 1000;
    }

    private static void DrawCardBar(GameContext ctx, float x, float y, float w, string label,
        int current, int max, SDL3.SDL.Color fill, SDL3.SDL.Color ink)
    {
        var r = ctx.Renderer;
        var fonts = ctx.Fonts;

        fonts.DrawText(r.Handle, label, x, y, 9, ink);
        var value = $"{current}/{max}";
        var (valueW, _) = fonts.Measure(value, 9);
        fonts.DrawText(r.Handle, value, x + w - valueW, y, 9, ink);

        var barY = y + 11;
        const float barH = 6f;
        r.FillRect(x - 1, barY - 1, w + 2, barH + 2, Colors.Rgb(74, 60, 44));
        r.FillRect(x, barY, w, barH, Colors.Rgb(120, 104, 84));
        var frac = max > 0 ? Math.Clamp((float)current / max, 0f, 1f) : 0f;
        if (frac > 0)
            r.FillRect(x, barY, Math.Max(1f, w * frac), barH, fill);
    }

    private static string GenderLabel(Gender gender) => gender == Gender.Male ? "男" : "女";

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

    private static bool IsItemSlot(EquipSlot slot) => slot is EquipSlot.Item1 or EquipSlot.Item2;

    private static void DrawBar(GameContext ctx, float x, float y, string label, int current, int max, SDL3.SDL.Color color)
    {
        var r = ctx.Renderer;
        ctx.Fonts.DrawText(r.Handle, $"{label} {current}/{max}", x, y, 10, Colors.White);
        var barY = y + 12;
        var barW = Width - 16;
        r.FillRect(x, barY, barW, 6, Colors.BarBg);
        var frac = max > 0 ? Math.Clamp((float)current / max, 0f, 1f) : 0f;
        if (frac > 0)
            r.FillRect(x, barY, barW * frac, 6, color);
    }
}
