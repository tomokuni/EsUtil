using EsUtil.Feature.Annotation.Presenter.Panel;
using EsUtil.Feature.StyledText.Presenter.Formatting;

namespace EsUtil.Feature.Annotation.Presenter.Editor;

/// <summary>注釈の編集 UI（右クリックのフライアウト・書式バー）の寸法と余白（単一所有）。</summary>
/// <remarks>
/// <b>UI ごとに値を決めない</b>: 同じ役割の項目（項目名の幅・色の入力の幅・装飾ボタンの幅）が<br/>
/// 4 つの UI 版で違うと、同じ内容でも押す位置と見た目が変わる。 値はここが単一所有し、<br/>
/// UI は自分の単位へ写すだけにする（WinForms のフォントだけはポイント指定のため換算する）。<br/>
/// <b>処理は持たない</b>: 行の組み立て（要素の生成・配置）は UI 固有なので UI に残す。
/// </remarks>
public static class AnnotationEditorLayout
{
    /// <summary>項目名の幅（DIP）。</summary>
    public const double LabelWidth = 104;

    /// <summary>入力欄の幅（DIP。 右クリックのフライアウトの行で使う）。</summary>
    /// <remarks>
    /// <b>行の入力欄はすべて同じ幅にする</b>: 数値・選択肢・色で幅が違うと、行の右端が揃わず
    /// 「入力部がばらばら」に見えます。 フライアウトの幅もこの値から決めます
    /// （<see cref="AnnotationPanelAppearance.StyleEditor"/>）。
    /// </remarks>
    public const double FieldWidth = 160;

    /// <summary>行の中の項目の間隔（DIP）。</summary>
    /// <remarks>値の所有者は書式付き文字列の編集層（<see cref="TextEditorLayout.ControlSpacing"/>）である。</remarks>
    public const double RowSpacing = TextEditorLayout.ControlSpacing;

    /// <summary>行と行の間隔（DIP）。</summary>
    public const double GroupSpacing = 6;

    /// <summary>装飾の切り替えボタンの幅（DIP）。</summary>
    /// <remarks>値の所有者は書式付き文字列の編集層（<see cref="TextEditorLayout.ToggleWidth"/>）である。</remarks>
    public const double ToggleWidth = TextEditorLayout.ToggleWidth;

    /// <summary>装飾の切り替えボタンの最小の幅（DIP。 幅を指定しない UI 用）。</summary>
    public const double ToggleMinWidth = TextEditorLayout.ToggleMinWidth;

    /// <summary>本文の文字サイズ（DIP。 編集 UI の文字）。</summary>
    public const double FontSizeDip = TextEditorLayout.FontSizeDip;

    /// <summary>装飾の切り替え（B / I / U / S）の文字サイズ（DIP）。</summary>
    /// <remarks>
    /// <b>編集 UI の文字より一回り大きくする</b>: 記号 1 文字だけのボタンは、行の他の文字と同じ大きさだと
    /// 線（下線・取り消し線）と字面が小さく、押せる場所としても見えにくい。
    /// UI の既定の文字サイズ（12 DIP）より 1 段大きい 14 DIP にします。
    /// </remarks>
    public const double ToggleFontSize = TextEditorLayout.ToggleFontSize;

    /// <summary>装飾の切り替え（B / I / U / S）のフォント名。</summary>
    /// <remarks>
    /// <b>「I」を見分けられるフォントにする</b>: UI の既定フォント（<c>Yu Gothic UI</c> / <c>Segoe UI</c>）の
    /// 斜体の「I」は単なる斜線に見え、太字の「B」とも形が近く、押す意味が伝わりません。
    /// <c>Consolas</c> は Windows に標準で入っており（環境が変わっても解決できる）、
    /// <b>「I」の上下にセリフ</b>が付くため、記号として読めます。
    /// 斜体の「I」が読めるかを優先し、欧文の記号用フォントとして選びます。
    /// </remarks>
    public const string ToggleFontFamily = TextEditorLayout.ToggleFontFamily;
}
