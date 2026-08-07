namespace SlimeDungeon.Domain;

/// <summary>
/// What an item is and what it does, in words.
///
/// Item names carry a rank in brackets and nothing else, which is enough to shop with and not enough to decide
/// with: two herbs of different ranks heal different amounts, and nothing on screen ever said by how much. This
/// is the one place that turns the flat <see cref="Item"/> record into a description, so the appraisal screen,
/// the forge's "equip this?" prompt and the alchemist's all say the same thing about the same object.
/// </summary>
public static class ItemInfo
{
    /// <summary>The kind of thing it is, at the granularity a player would use to sort a pack.</summary>
    public static string CategoryLabel(Item item) => item.Category switch
    {
        ItemCategory.Weapon => item.WeaponKind == WeaponKind.Sword ? "武器（剣）" : "武器（杖）",
        ItemCategory.Shield => "盾",
        ItemCategory.Armor => "鎧",
        ItemCategory.Helmet => "兜",
        ItemCategory.Gauntlet => "籠手",
        ItemCategory.Shoes => "靴",
        ItemCategory.Bag => "鞄",
        ItemCategory.Herb => "薬草",
        ItemCategory.Antidote => "毒消し",
        ItemCategory.Potion => item.PotionKind == PotionKind.Hp ? "ポーション（HP）" : "ポーション（MP）",
        ItemCategory.Scroll => "巻物",
        ItemCategory.FullMapReveal => "巻物（地図）",
        ItemCategory.Firecracker => "投擲",
        ItemCategory.Caltrops => "設置",
        ItemCategory.Gemstone => "宝石",
        ItemCategory.Material => "素材",
        _ => "その他",
    };

    /// <summary>
    /// What it does, as one or two lines. Percentages rather than absolute numbers where the effect scales off
    /// the character's own maximum — a rank-D potion restores a fixed share, not a fixed amount, and quoting
    /// today's number would go stale the moment the player levelled.
    /// </summary>
    public static List<string> EffectLines(Item item)
    {
        var lines = new List<string>();

        switch (item.Category)
        {
            case ItemCategory.Weapon:
                lines.Add(item.WeaponKind == WeaponKind.Sword
                    ? $"装備すると STR +{item.StatBonus}"
                    : $"装備すると INT +{item.StatBonus}");
                lines.Add(item.WeaponKind == WeaponKind.Sword ? "剣は物理攻撃に使う" : "杖はまほうの威力に効く");
                break;
            case ItemCategory.Shield:
            case ItemCategory.Armor:
            case ItemCategory.Helmet:
                lines.Add($"装備すると DEF +{item.Def}");
                lines.Add("受けるダメージを減らす");
                break;
            case ItemCategory.Gauntlet:
                lines.Add($"装備すると DEX +{item.StatBonus}");
                lines.Add("命中率に効く");
                break;
            case ItemCategory.Shoes:
                lines.Add($"装備すると AGL +{item.StatBonus}");
                lines.Add("先に動けるかどうかに効く");
                break;
            case ItemCategory.Bag:
                lines.Add($"荷物を {item.BagCapacity} 個まで持てる");
                break;
            case ItemCategory.Herb:
                lines.Add($"HPを最大値の {ConsumableEffects.HerbHealFraction(item.Rank) * 100:F0}% 回復");
                lines.Add("いつでも使える");
                lines.Add("薬局でHPポーションに加工できる");
                break;
            case ItemCategory.Potion:
            {
                var what = item.PotionKind == PotionKind.Hp ? "HP" : "MP";
                lines.Add($"{what}を最大値の {ConsumableEffects.PotionRestoreFraction(item.Rank) * 100:F0}% 回復");
                lines.Add("いつでも使える");
                break;
            }
            case ItemCategory.Antidote:
                lines.Add("毒を治す");
                lines.Add("戦闘中しか使えない");
                // The second use is the less obvious one and the reason to keep more than a couple on hand.
                lines.Add("薬局でMPポーションに加工できる");
                break;
            case ItemCategory.Scroll:
                lines.Add($"{SpellDefinitions.NameOf(item.SpellTaught)}（{item.Rank.Label()}）を覚える");
                lines.Add("同じまほうの下位なら上書きされる");
                break;
            case ItemCategory.FullMapReveal:
                lines.Add("ダンジョン全体が数秒見える");
                lines.Add("ダンジョンの中でしか使えない");
                break;
            case ItemCategory.Firecracker:
                lines.Add("敵全体にダメージ");
                lines.Add("戦闘中しか使えない");
                break;
            case ItemCategory.Caltrops:
                lines.Add("敵全体にダメージと足止め");
                lines.Add("戦闘中しか使えない");
                break;
            case ItemCategory.Gemstone:
                lines.Add("売るか、依頼に納品する");
                break;
            case ItemCategory.Material:
                lines.Add("鍛冶屋で武具に加工できる");
                lines.Add("売却・納品にも使える");
                break;
        }

        if (item.ForgeId is not null)
            lines.Add("鍛冶屋で打たれた一品");

        return lines;
    }

    /// <summary>
    /// Whether "つかう" belongs on this item's menu outside a fight. An antidote, a firecracker and caltrops
    /// all need a slime in front of them, so offering the option would only produce a refusal.
    /// </summary>
    public static bool UsableOutsideCombat(Item item) =>
        item.Category is ItemCategory.Herb or ItemCategory.Potion or ItemCategory.Scroll
            or ItemCategory.FullMapReveal;
}
