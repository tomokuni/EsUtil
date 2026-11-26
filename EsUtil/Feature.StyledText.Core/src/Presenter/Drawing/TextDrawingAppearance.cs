namespace EsUtil.Feature.StyledText.Presenter.Drawing;

/// <summary>文字の描画で使う外観の値（行の実寸を測る代表の文字・下線と取り消し線の太さ）を単一所有する。</summary>
/// <remarks>
/// <b>4 つの UI 版で同じ見た目にする</b>: 行の高さは代表の文字で測り、装飾の太さは文字の大きさに対する比で決める。
/// 値が UI ごとに違うと、同じ本文でも行の間隔と線の太さが変わる。 UI は自分の描画 API へ写すだけにする。
/// </remarks>
public static class TextDrawingAppearance
{
    /// <summary>行の高さを測るための代表の文字列（上端と下端が揃った字）。</summary>
    /// <remarks>行の高さは文字によらず書式で決まるため、代表の 2 文字で測る（全文字を測る必要はない）。</remarks>
    public const string LineMetricsText = "Ag";

    /// <summary>下線・取り消し線の太さの比率（文字の大きさに対する比）。</summary>
    public const double DecorationThicknessRatio = 0.06;
}
