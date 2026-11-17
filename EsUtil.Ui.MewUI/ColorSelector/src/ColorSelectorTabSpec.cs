namespace EsUtil.Ui.MewUI.ColorSelector;

/// <summary>一覧の 1 タブ（題名・識別子・並べ方・並べる項目）。</summary>
/// <param name="Title">タブの題名（1 つだけ表示するときの見出しにも使う）。</param>
/// <param name="Key">タブの識別子（次に開いたときに同じタブから始めるための目印）。</param>
/// <param name="Columns">■ だけを並べる列数（0 は色名付きの一覧）。</param>
/// <param name="Items">タブに並べる項目。</param>
/// <param name="IsPicker">カラーピッカーのタブかどうか（項目は持たない）。</param>
/// <remarks>表示の設定と中身から作る（空のタブは作らない）。</remarks>
internal sealed record ColorSelectorTabSpec(
    string Title,
    string Key,
    int Columns,
    IReadOnlyList<ColorSelectorItem> Items,
    bool IsPicker = false)
{
    /// <summary>色名付きの一覧（スクロールする）で並べるかどうかを取得する。</summary>
    /// <value>列数が 0 以下の場合は <see langword="true"/>。</value>
    public bool IsList => Columns <= 0;
}
