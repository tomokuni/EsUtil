namespace EsUtil.Feature.Thumbnail;

/// <summary>項目の強調（背景の帯）の種類。</summary>
/// <remarks>
/// <b>色ではなく意味を持つ</b>: 帯の色は UI フレームワークのテーマが決めるため、描画計画は「どの強調か」だけを運び、<br/>
/// 色は UI 層が自分のテーマから解決して <see cref="ThumbnailAppearance.ResolveHighlightArgb"/> へ渡す<br/>
/// （明暗 2 色を提示層が持つと、テーマの値と一致しないうえ利用者のアクセント変更にも追随できない）。
/// </remarks>
public enum ThumbnailHighlightKind
{
    /// <summary>強調しない（帯を描かない）。</summary>
    None,

    /// <summary>マウスが重なっている。</summary>
    Hover,

    /// <summary>選択中である（ホバーより優先する）。</summary>
    Selected,
}
