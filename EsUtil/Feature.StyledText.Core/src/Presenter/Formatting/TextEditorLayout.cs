namespace EsUtil.Feature.StyledText.Presenter.Formatting;

/// <summary>本文の編集 UI（書式バー・装飾の切り替え）の寸法と余白（単一所有）。</summary>
/// <remarks>
/// <b>UI ごとに値を決めない</b>: 同じ役割の項目（装飾ボタンの幅・記号の文字サイズ）が 4 つの UI 版で
/// 違うと、同じ内容でも押す位置と見た目が変わる。 値はここが単一所有し、UI は自分の単位へ写すだけにする。
/// </remarks>
public static class TextEditorLayout
{
    /// <summary>行の中の項目の間隔（DIP）。</summary>
    public const double ControlSpacing = 4;

    /// <summary>装飾の切り替えボタンの幅（DIP）。</summary>
    public const double ToggleWidth = 34;

    /// <summary>装飾の切り替えボタンの最小の幅（DIP。 幅を指定しない UI 用）。</summary>
    public const double ToggleMinWidth = 28;

    /// <summary>本文の文字サイズ（DIP。 編集 UI の文字）。</summary>
    public const double FontSizeDip = 12;

    /// <summary>装飾の切り替え（B / I / U / S）の文字サイズ（DIP）。</summary>
    /// <remarks>
    /// <b>編集 UI の文字より一回り大きくする</b>: 記号 1 文字だけのボタンは、行の他の文字と同じ大きさだと
    /// 線（下線・取り消し線）と字面が小さく、押せる場所としても見えにくい。
    /// </remarks>
    public const double ToggleFontSize = 14;

    /// <summary>装飾の切り替え（B / I / U / S）のフォント名。</summary>
    /// <remarks>
    /// <b>「I」を見分けられるフォントにする</b>: 既定フォントの斜体の「I」は単なる斜線に見え、太字の「B」とも
    /// 形が近く、押す意味が伝わらない。 <c>Consolas</c> は Windows に標準で入っており、
    /// 「I」の上下にセリフが付くため記号として読める。
    /// </remarks>
    public const string ToggleFontFamily = "Consolas";
}