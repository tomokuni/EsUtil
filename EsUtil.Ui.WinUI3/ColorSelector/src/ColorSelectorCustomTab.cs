namespace EsUtil.Ui.WinUI3.ColorSelector;

/// <summary>利用者が追加するタブ（「カスタム」タブ）。</summary>
/// <param name="Title">タブの題名（1 つだけ表示するときに見出しとしても使う）。</param>
/// <param name="Colors">一覧に並べる色（並び順がそのまま一覧の順序になる）。</param>
/// <param name="Layout">並べ方（色名付きの一覧 / 色の四角を 6 列に並べる格子。 既定は一覧）。</param>
/// <remarks>
/// <b>代表色をすぐ選べるようにする</b>: テーマの色と色名だけでは、よく使う色へ素早く届かない。<br/>
/// 利用側は用途ごとのタブ（例: 注釈の枠線の色）を足せる。<br/>
/// 空の <see cref="Colors"/> のタブは表示しない（設定の途中で空になっても壊れない）。<br/>
/// <b>格子はスクロールしない</b>: 数が少なく色で選ぶときは <see cref="ColorSelectorTabLayout.Grid"/> を使う
/// （16 色なら 6 列 × 3 行に収まる）。
/// </remarks>
public sealed record ColorSelectorCustomTab(
    string Title,
    IReadOnlyList<ColorSelectorCustomColor> Colors,
    ColorSelectorTabLayout Layout = ColorSelectorTabLayout.List);
