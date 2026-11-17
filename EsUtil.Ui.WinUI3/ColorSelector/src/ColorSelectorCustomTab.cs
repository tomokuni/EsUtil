namespace EsUtil.Ui.WinUI3.ColorSelector;

/// <summary>利用者が追加するタブ（「カスタム」タブ）。</summary>
/// <param name="Title">タブの題名（1 つだけ表示するときに見出しとしても使う）。</param>
/// <param name="Colors">一覧に並べる色（並び順がそのまま一覧の順序になる）。</param>
/// <param name="Columns">
/// 色の四角（■）だけを並べるときの列数。 <b>0 は色名付きの一覧</b>、<b>1 以上は ■ だけの格子</b>
/// （その列数。 名前はツールチップで示す）。
/// </param>
/// <remarks>
/// <b>代表色をすぐ選べるようにする</b>: テーマの色と色名だけでは、よく使う色へ素早く届かない。<br/>
/// 利用側は用途ごとのタブ（例: 注釈の枠線の色）を足せる。<br/>
/// 空の <see cref="Colors"/> のタブは表示しない（設定の途中で空になっても壊れない）。<br/>
/// <b>格子はスクロールしない</b>: 数が少なく色で選ぶときは <see cref="Columns"/> を 1 以上にする
/// （16 色なら 6 列 × 3 行、40 色なら 8 列 × 5 行に収まる）。
/// </remarks>
public sealed record ColorSelectorCustomTab(
    string Title,
    IReadOnlyList<ColorSelectorCustomColor> Colors,
    int Columns = 0)
{
    /// <summary>色名付きの一覧（スクロールする）で並べるかどうかを取得する。</summary>
    /// <value>列数が 0 以下の場合は <see langword="true"/>。</value>
    public bool IsList => Columns <= 0;
}
