using SlimeDungeon.Core;

namespace SlimeDungeon.Domain;

/// <summary>
/// Display names for slime species and elements. The UI previously interpolated the raw enum name, producing
/// mixed-language strings like "Greenスライム討伐" in a Japanese interface; going through here keeps every
/// screen naming the same slime the same way.
/// </summary>
public static class SlimeNames
{
    public static string Of(SlimeColor color) => color switch
    {
        SlimeColor.Green => "グリーン",
        SlimeColor.Red => "レッド",
        SlimeColor.Blue => "ブルー",
        SlimeColor.Yellow => "イエロー",
        SlimeColor.Gray => "グレー",
        SlimeColor.Poison => "ポイズン",
        SlimeColor.Gold => "ゴールド",
        SlimeColor.White => "ホワイト",
        SlimeColor.Dragon => "ドラゴン",
        SlimeColor.Gem => "ジェム",
        // The seven metal species read their name from the ore table, so a metal is spelled one way everywhere.
        _ => Metals.ForSlime(color)?.Name ?? color.ToString(),
    };

    public static string FullName(SlimeColor color) => $"{Of(color)}スライム";

    public static string ElementLabel(Element element) => element switch
    {
        Element.Fire => "火",
        Element.Water => "水",
        Element.Wind => "風",
        Element.Earth => "地",
        _ => "無",
    };
}
