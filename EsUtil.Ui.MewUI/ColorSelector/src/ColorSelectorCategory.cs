namespace EsUtil.Ui.MewUI.ColorSelector;

/// <summary>一覧の項目の分類（ドロップダウン上部のタブ）。</summary>
/// <remarks>
/// <b>タブは分類だけを切り替える</b>: 並び順（<c>ColorSortMode</c>）は分類の中だけで効く<br/>
/// （テーマの色は意味の順、色名は色相順・色名順である）。
/// </remarks>
internal enum ColorSelectorCategory
{
    /// <summary>UI のテーマの色（明暗・アクセントの変更に追随する）。</summary>
    Theme,

    /// <summary>色名（コアの色の表）。</summary>
    Named,
}
