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
        var cy = cardBottom + 8;

        // Stats in two labelled columns rather than one long line, which lets them go up a size and still fit.
        var col2 = cx + 108;
        fonts.DrawText(r.Handle, $"STR {player.EffectiveStr,3}", cx, cy, 12, Colors.White);
        fonts.DrawText(r.Handle, $"INT {player.EffectiveInt,3}", col2, cy, 12, Colors.White);
        cy += 17;
        fonts.DrawText(r.Handle, $"DEX {player.EffectiveDex,3}", cx, cy, 12, Colors.White);
        fonts.DrawText(r.Handle, $"AGL {player.EffectiveAgl,3}", col2, cy, 12, Colors.White);
        cy += 17;
        fonts.DrawText(r.Handle, $"DEF {player.TotalDef,3}", cx, cy, 12, Colors.White);
        var gold = $"{player.Gold} G";
        var (goldW, _) = fonts.Measure(gold, 12);
        fonts.DrawText(r.Handle, gold, x + Width - pad - goldW, cy, 12, Colors.Gold);
        cy += 18;

        // A rule under the heading, so the equipment reads as its own block instead of running on from the
        // numbers above it.
        fonts.DrawText(r.Handle, "装備", cx, cy, 11, Colors.Highlight);
        r.FillRect(cx + 30, cy + 7, Width - pad * 2 - 30, 1, Colors.Rgb(70, 66, 60));
        cy += 16;

        foreach (var slot in new[]
                 {
                     EquipSlot.RightHand, EquipSlot.LeftHand, EquipSlot.Arm, EquipSlot.Body,
                     EquipSlot.Head, EquipSlot.Feet, EquipSlot.Item1, EquipSlot.Item2,
                 })
        {
            var name = player.Equipment.TryGetValue(slot, out var item) ? item.Name : "-";
            // The item slots are what the player can actually reach in a fight, so they are picked out rather
            // than reading as two more lines of gear.
            var filled = player.Equipment.ContainsKey(slot);
            var color = IsItemSlot(slot)
                ? filled ? Colors.Highlight : Colors.Border
                : filled ? Colors.White : Colors.Border;

            // The slot name sits in a fixed column so the item names line up down the panel.
            fonts.DrawText(r.Handle, SlotLabel(slot), cx, cy, 10, Colors.Rgb(150, 145, 136));
            fonts.DrawText(r.Handle, name, cx + 52, cy, 10, color);
            cy += 13;
        }

        // The bag is worn like everything else, so it reads as the last equipment row rather than a separate
        // line — which also keeps its contents count without spending another row on it.
        fonts.DrawText(r.Handle, "鞄", cx, cy, 10, Colors.Rgb(150, 145, 136));
        var bagName = player.EquippedBag?.Name ?? "-";
        fonts.DrawText(r.Handle, bagName, cx + 52, cy, 10, Colors.Highlight);
        var capacity = $"{player.Bag.Count}/{player.BagCapacity}";
        var (capW, _) = fonts.Measure(capacity, 10);
        fonts.DrawText(r.Handle, capacity, x + Width - pad - capW, cy, 10, Colors.Highlight);

        // The bag and the kill log moved onto the menu, so the old I/S shortcuts no longer exist — this line
        // was still advertising them.
        ControlHints.Draw(ctx, x + pad, y + h - 18, 9, Colors.Border, ControlHints.Menu("メニュー"));
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
        // seal and the HP row ran into each other. Raised from 140 to make room for the contract line under the
        // vitals; the sixteen pixels that cost are taken back further down the panel, where the equipment rows
        // and the gaps around them were each carrying a pixel or two of slack.
        const float bodyH = 156f;
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

        // Issue date, the way a licence carries one — and today's date under it, which is what quest deadlines
        // are counted against. Today's used to be a line of its own at the foot of the panel; moving it onto
        // the card is what freed the room for the equipment list below to be legible.
        var reg = GameCalendar.FromDayNumber(player.StartDay);
        var today = GameCalendar.FromDayNumber(player.DayCount);
        DrawRightAligned(ctx, $"登録 {reg.MonthName}{reg.Day}日", x + w - 8, y + headerH + 5, 8, inkSoft);
        DrawRightAligned(ctx, $"本日 {today.MonthName}{today.Day}日", x + w - 8, y + headerH + 16, 9, ink);

        // Vitals across the foot of the card.
        var barX = x + 8;
        var barW = w - 16;
        var by = y + headerH + 68;
        DrawCardBar(ctx, barX, by, barW, "HP", player.Stats.Hp, player.Stats.MaxHp, Colors.HpBar, ink);
        by += 22;
        DrawCardBar(ctx, barX, by, barW, "MP", player.Stats.Mp, player.Stats.MaxMp, Colors.MpBar, ink);
        by += 22;
        DrawCardBar(ctx, barX, by, barW, "EXP", player.Exp, player.ExpToNext, Colors.ExpBar, ink);
        by += 25;

        // The contract in hand, as a licence would carry its holder's current commission. Below the vitals
        // rather than beside them: this is the one field on the card that changes shape from job to job, and a
        // line of its own is what lets it be as long as it needs to be.
        fonts.DrawText(r.Handle, "依頼", barX, by, 9, inkSoft);
        const float questX = 26f;
        if (player.ActiveQuest is { } quest)
        {
            // The deadline is pinned to the right edge and the summary is given what is left, so a long ore
            // commission shrinks rather than running into the days — the days are the half of this line that
            // must never be the part that gets clipped.
            const float daysW = 54f;
            var (days, daysColor) = DeadlineNote(quest, player.DayCount, ink, inkSoft);
            DrawRightAligned(ctx, days, barX + barW, by, 9, daysColor);
            DrawFitted(ctx, quest.CardSummary(player), barX + questX, by - 1,
                barW - questX - daysW, 11f, ink);
        }
        else
        {
            fonts.DrawText(r.Handle, "なし", barX + questX, by, 9, inkSoft);
        }

        return y + h;
    }

    /// <summary>
    /// How long is left on the contract, and how alarmed to look about it.
    ///
    /// The deadline is an absolute calendar day, so the useful figure is the difference from today rather than
    /// the date itself — "あと3日" is something a player acts on where "秋12日まで" has to be worked out. Due
    /// today is called out separately from having a day in hand, because those are different decisions: one
    /// means report before you go back down, the other does not.
    /// </summary>
    private static (string Text, SDL3.SDL.Color Color) DeadlineNote(Quest quest, int today,
        SDL3.SDL.Color ink, SDL3.SDL.Color inkSoft)
    {
        var left = quest.DeadlineDay - today;
        var urgent = Colors.Rgb(158, 44, 32);

        return left switch
        {
            < 0 => ("期限切れ", urgent),
            0 => ("本日まで", urgent),
            1 => ("あと1日", urgent),
            <= 3 => ($"あと{left}日", ink),
            _ => ($"あと{left}日", inkSoft),
        };
    }

    /// <summary>
    /// Draws text at the largest size that still fits the space, down to a floor where it stops being readable.
    ///
    /// The quest line is the only thing on the card whose width is not known in advance: "苦草1/3本" is half the
    /// panel, an ore commission from a named house is most of it, and shrinking a little beats either clipping
    /// the count off the end or leaving the common case needlessly small.
    /// </summary>
    private static void DrawFitted(GameContext ctx, string text, float x, float y, float maxWidth,
        float size, SDL3.SDL.Color color)
    {
        var chosen = size;
        while (chosen > 8f && ctx.Fonts.Measure(text, chosen).Item1 > maxWidth)
            chosen -= 1f;

        // Baselines differ by size, so a shrunk line is nudged down to sit level with the label beside it.
        ctx.Fonts.DrawText(ctx.Renderer.Handle, text, x, y + (size - chosen) * 0.5f, chosen, color);
    }

    private static void DrawRightAligned(GameContext ctx, string text, float right, float y, float size, SDL3.SDL.Color color)
    {
        var (w, _) = ctx.Fonts.Measure(text, size);
        ctx.Fonts.DrawText(ctx.Renderer.Handle, text, right - w, y, size, color);
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
