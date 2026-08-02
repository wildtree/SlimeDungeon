using SlimeDungeon.Core;
using SlimeDungeon.Domain;
using SlimeDungeon.Graphics;
using SlimeDungeon.UI;

namespace SlimeDungeon.Guild;

/// <summary>
/// The guild's smith. Ore prised out of metal slimes goes in; weapons and armour that no shop stocks come out.
///
/// Two levels: pick a material, then pick a piece. The material list doubles as the player's ore ledger — it
/// shows every ore in the world with how much of each is in the pouch, so someone who has never seen a mithril
/// slime can still find out that mithril is a thing and that it comes from rank A.
/// </summary>
public sealed class ForgeScreen : IScreen
{
    private enum Phase { SelectSet, SelectPiece }

    private Phase _phase = Phase.SelectSet;
    private int _setCursor;
    private int _pieceCursor;
    private string? _message;

    private static readonly Metal?[] Sets = Forge.Sets.ToArray();

    private Metal? SelectedSet => Sets[_setCursor];

    private static ForgeRecipe[] Recipes(Metal? set) => Forge.ForSet(set).ToArray();

    public void Update(GameContext ctx, float dt)
    {
        var input = ctx.Input;
        var player = ctx.Player!;

        if (MenuNav.Cancelled(input))
        {
            if (_phase == Phase.SelectPiece)
            {
                _phase = Phase.SelectSet;
                _message = null;
            }
            else
            {
                ctx.Screens.ChangeTo(new GuildScreen());
            }
            return;
        }

        if (_phase == Phase.SelectSet)
        {
            _setCursor = MenuNav.Move(input, _setCursor, Sets.Length);
            if (MenuNav.Confirmed(input))
            {
                _phase = Phase.SelectPiece;
                _pieceCursor = 0;
                _message = null;
            }
            return;
        }

        var recipes = Recipes(SelectedSet);
        _pieceCursor = MenuNav.Move(input, _pieceCursor, recipes.Length);
        if (!MenuNav.Confirmed(input))
            return;

        TryForge(player, recipes[_pieceCursor]);
    }

    private void TryForge(Player player, ForgeRecipe recipe)
    {
        // Forging the same piece twice is allowed — gear is lost, sold and replaced — but it is almost never
        // what someone means to do, and the ore is far too scarce to spend by a slip of the cursor.
        if (player.HasCrafted(recipe.Id) && player.Bag.Any(i => i.ForgeId == recipe.Id))
        {
            _message = "それはもう持っている";
            return;
        }

        var missing = recipe.Cost.FirstOrDefault(c => player.MaterialCount(c.Key) < c.Value);
        if (missing.Value > 0)
        {
            _message = $"{Metals.Get(missing.Key).Name}の素材が足りない";
            return;
        }

        if (player.Gold < recipe.GoldCost)
        {
            _message = "所持金が足りない";
            return;
        }

        if (!player.BagHasRoom)
        {
            _message = "鞄がいっぱいだ";
            return;
        }

        player.Forge(recipe);
        _message = $"{recipe.Name}を打ち上げた！";

        // A completed set may have just earned a collector title, but nothing is awarded here: the guild
        // settles titles when you come back to the counter, which is where the fanfare and the announcement
        // already live. Walking out of the forge is what triggers it.
    }

    public void Draw(GameContext c)
    {
        var r = c.Renderer;
        r.Clear(Colors.Rgb(24, 20, 16));
        r.DrawTexture(c.Sprites.MenuBackdrop, 0, 0, SpriteFactory.MenuBackdropWidth, SpriteFactory.MenuBackdropHeight);
        var fonts = c.Fonts;
        var player = c.Player!;

        fonts.DrawText(r.Handle, "鍛冶", 20, 16, 18, Colors.White);

        if (_phase == Phase.SelectSet)
            DrawSetList(c, player);
        else
            DrawPieceList(c, player);

        if (_message is not null)
            fonts.DrawText(r.Handle, _message, 20, 345, 11, Colors.Gold);
        fonts.DrawText(r.Handle,
            $"[{MenuNav.Hint.Direction}]選ぶ  [{MenuNav.Hint.Confirm}]決定  [{MenuNav.Hint.Cancel}]戻る", 20, 370, 11, Colors.Border);

        StatusPanel.Draw(c, 400, 0, 400);
    }

    // Columns are drawn at fixed pixel positions rather than by padding the strings. Japanese glyphs are
    // double-width and the names run from four characters to seven, so string padding left the ore counts and
    // the tallies stepping raggedly in and out across the list.
    private const float SetNameX = 24f;
    private const float SetStockX = 128f;
    private const float SetTallyX = 300f;
    private const float RowWidth = 356f;

    private void DrawSetList(GameContext c, Player player)
    {
        var r = c.Renderer;
        var fonts = c.Fonts;

        fonts.DrawText(r.Handle, "素材を選ぶ", 20, 44, 12, Colors.Highlight);

        var y = 64f;
        for (var i = 0; i < Sets.Length; i++)
        {
            var set = Sets[i];
            MenuNav.DrawRow(c, 20, y, RowWidth, 17, "", 11, i == _setCursor);

            var selected = i == _setCursor;
            var text = selected ? Colors.Black : Colors.White;
            var dim = selected ? Colors.Black : Colors.Border;

            fonts.DrawText(r.Handle, Forge.SetName(set), SetNameX, y + 3, 11, text);
            fonts.DrawText(r.Handle, StockLabel(set, player), SetStockX, y + 3, 11, dim);

            var forged = Forge.ForSet(set).Count(rc => player.HasCrafted(rc.Id));
            var total = Forge.ForSet(set).Count();
            fonts.DrawText(r.Handle, $"作成 {forged}/{total}", SetTallyX, y + 3, 11,
                forged == total && !selected ? Colors.Gold : dim);

            y += 17;
        }

        // A one-line note about where the ore in front of the cursor comes from, so the whole system is
        // discoverable from this screen alone rather than by dying in the right dungeon.
        fonts.DrawText(r.Handle, SetHint(SelectedSet, player), 20, y + 10, 11, Colors.Border);
    }

    private static string StockLabel(Metal? set, Player player)
    {
        if (set is { } metal)
            return $"素材 {player.MaterialCount(metal)}個";

        // Every slime piece needs one of all three ores, so the only number that means anything is the
        // smallest of them. Naming the three in full ran straight through the tally column beside it, and
        // said less: what the player wants to know is how many pieces they could forge right now.
        var sets = Forge.SlimeSetOres.Min(player.MaterialCount);
        return $"素材 {sets}組分";
    }

    private static string SetHint(Metal? set, Player player)
    {
        if (set is not { } metal)
        {
            var stock = string.Join(" ", Forge.SlimeSetOres.Select(m =>
                $"{Metals.Get(m).Name}{player.MaterialCount(m)}"));
            // Just the holdings. What it costs is stated on the next screen, and naming both here ran the
            // line off the right edge of the panel.
            return $"所持: {stock}";
        }

        var def = Metals.Get(metal);
        var ranks = string.Join("/", def.Ranks.Select(rk => rk.Label()));
        var gloss = def.OreName == def.Name ? "" : $"素材は{def.OreName}。";
        return $"{gloss}{def.Name}スライム ({ranks}ランクのダンジョン) がまれに落とす";
    }

    private void DrawPieceList(GameContext c, Player player)
    {
        var r = c.Renderer;
        var fonts = c.Fonts;
        var set = SelectedSet;

        fonts.DrawText(r.Handle, $"{Forge.SetName(set)}の武器・防具", 20, 44, 12, Colors.Highlight);
        fonts.DrawText(r.Handle, CostSummary(set), 20, 60, 11, Colors.Border);

        var recipes = Recipes(set);
        var y = 80f;
        for (var i = 0; i < recipes.Length; i++)
        {
            var recipe = recipes[i];
            var selected = i == _pieceCursor;
            MenuNav.DrawRow(c, 40, y, PieceRowWidth, 18, "", 11, selected);

            var text = selected ? Colors.Black : Colors.White;
            var dim = selected ? Colors.Black : Colors.Border;

            // The icon sits left of the row, and a tick marks what has already been made — the collector
            // titles are counted on exactly this, so it has to be readable at a glance.
            r.DrawTexture(c.Sprites.ItemIcon(recipe.Create()), 20, y + 1, 15, 15);
            fonts.DrawText(r.Handle, recipe.Name, PieceNameX, y + 3, 11, text);
            fonts.DrawText(r.Handle, recipe.Rank.Label(), PieceRankX, y + 4, 10, dim);
            fonts.DrawText(r.Handle, StatLabel(recipe), PieceStatX, y + 3, 11, dim);
            if (player.HasCrafted(recipe.Id))
                fonts.DrawText(r.Handle, "作成済", PieceDoneX, y + 4, 10, selected ? Colors.Black : Colors.Gold);

            y += 18;
        }
    }

    private const float PieceNameX = 44f;
    private const float PieceRankX = 190f;
    private const float PieceStatX = 216f;
    private const float PieceDoneX = 300f;
    private const float PieceRowWidth = 336f;

    /// <summary>
    /// Deliberately drops the "(所持 …G)" it used to carry. With three ores named in full, the slime set's
    /// line ran off the edge of the panel — and the purse is already on the status panel to the right.
    /// </summary>
    private static string CostSummary(Metal? set)
    {
        var sample = Forge.ForSet(set).First();
        var ore = sample.Cost.Count == 1
            ? $"{Metals.Get(sample.Cost.Keys.First()).Name}1個"
            : string.Join("・", sample.Cost.Keys.Select(m => Metals.Get(m).Name)) + " 各1個";
        return $"1つにつき {ore} と {sample.GoldCost}G";
    }

    private static string StatLabel(ForgeRecipe recipe)
    {
        var bonus = Forge.ForgedBonus(recipe.Rank);
        return recipe.Piece switch
        {
            ForgePiece.Sword => $"STR+{bonus}",
            ForgePiece.Wand => $"INT+{bonus}",
            ForgePiece.Gauntlet => $"DEX+{bonus}",
            ForgePiece.Shoes => $"AGL+{bonus}",
            _ => $"DEF+{bonus}",
        };
    }
}
