using EsUtil.Platform.Core.Geometry;
using EsUtil.Feature.StyledText;

namespace EsUtil.Ui.WinUI3.Annotation;

/// <summary>本文の実寸の見積もり（XAML のテキスト エンジンを使えないときの既定値）。</summary>
/// <remarks>
/// <b>実アプリは <see cref="AnnotationTextRunMeasure"/> を使う</b>: ホストが
/// <see cref="AnnotationLayerControl.MeasureText"/> へ実寸を測る関数を渡す（描くエンジンで測る）。<br/>
/// 本クラスは文字数と文字サイズから近似する（ウィンドウを持たない検証で折り返しの規則だけを確かめられる）。
/// </remarks>
internal static class AnnotationTextMetrics
{
    /// <summary>近似の送り幅（文字サイズに対する比）。</summary>
    private const double ApproximateWidthRatio = 0.55;

    /// <summary>近似の行の高さ（文字サイズに対する比）。</summary>
    private const double ApproximateHeightRatio = 1.4;

    /// <summary>文字列と書式から実寸を近似する。</summary>
    /// <param name="text">文字列。</param>
    /// <param name="style">書式。</param>
    /// <returns>近似の実寸（元画像ピクセル）。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> または <paramref name="style"/> が null の場合。</exception>
    internal static EsSize Measure(string text, TextStyle style)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(style);

        return new EsSize(
            text.Length * style.FontSize * ApproximateWidthRatio,
            style.FontSize * ApproximateHeightRatio);
    }

    /// <summary>コアの色を XAML のブラシへ変換する。</summary>
    /// <param name="color">色（<c>#AARRGGBB</c> の成分を持つ）。</param>
    /// <returns>単色のブラシ。</returns>
    internal static Microsoft.UI.Xaml.Media.SolidColorBrush ToBrush(System.Drawing.Color color)
        => new(Windows.UI.Color.FromArgb(color.A, color.R, color.G, color.B));
}
