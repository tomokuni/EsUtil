namespace EsUtil.Ui.WinUI3.ColorSelector;

/// <summary>利用者が追加するタブ（「カスタム」タブ）。</summary>
/// <param name="Title">タブの題名（1 つだけ表示するときに見出しとしても使う）。</param>
/// <param name="Colors">一覧に並べる色（並び順がそのまま一覧の順序になる）。</param>
/// <remarks>
/// <b>代表色をすぐ選べるようにする</b>: テーマの色と色名だけでは、よく使う色へ素早く届かない。<br/>
/// 利用側は用途ごとのタブ（例: 注釈の枠線の色）を足せる。<br/>
/// 空の <see cref="Colors"/> のタブは表示しない（設定の途中で空になっても壊れない）。
/// </remarks>
public sealed record ColorSelectorCustomTab(string Title, IReadOnlyList<ColorSelectorCustomColor> Colors);
