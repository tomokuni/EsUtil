namespace EsUtil.Ui.WinUI3.ColorSelector;

/// <summary>一覧の 1 タブ（題名・識別子・並べる項目）。</summary>
/// <param name="Title">タブの題名（1 つだけ表示するときの見出しにも使う）。</param>
/// <param name="Key">タブの識別子（次に開いたときに同じタブから始めるための目印）。</param>
/// <param name="Items">タブに並べる項目。</param>
/// <remarks>表示の設定と中身から作る（空のタブは作らない）。</remarks>
internal sealed record ColorSelectorTabSpec(string Title, string Key, IReadOnlyList<ColorSelectorItem> Items);
