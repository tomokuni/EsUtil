using EsUtil.Platform.Core.Geometry;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using EsUtil.Feature.StyledText;

namespace EsUtil.Ui.WinUI3.Annotation;

/// <summary>WinUI 3 のテキスト エンジンで本文の実寸を測る（ホストがレイヤーへ渡す）。</summary>
/// <remarks>
/// <b>描くエンジンで測る</b>: 本文は <see cref="TextBlock"/> として置くため、実寸も <see cref="TextBlock"/> で測る
/// （文字数からの見積もりでは折り返しの位置が実際の描画とずれる）。<br/>
/// <b>UI スレッドから呼ぶ</b>: 測定はツリー外の <see cref="TextBlock"/> を作るため、XAML の UI スレッドで行う
/// （<see cref="AnnotationLayerControl"/> の描画は UI スレッドで走る）。<br/>
/// <b>行の高さは斜体で変えない</b>: 行の高さは斜体を外した書式で測る（4 つの UI 版で同じ規則）。
/// </remarks>
public static class AnnotationTextRunMeasure
{
    /// <summary>行の高さを測るための文字列。</summary>
    private const string LineMetricsText = "Ag";

    /// <summary>測定に使う要素（初回に生成して使い回す）。</summary>
    private static TextBlock? s_probe;

    /// <summary>文字列と書式から実寸を測る。</summary>
    /// <param name="text">文字列。</param>
    /// <param name="style">書式。</param>
    /// <returns>実寸（元画像ピクセル）。 高さは斜体を外した書式で測る。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> または <paramref name="style"/> が null の場合。</exception>
    public static EsSize Measure(string text, TextStyle style)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(style);

        var height = MeasureSize(LineMetricsText, style with { IsItalic = false }).Height;
        var width = text.Length == 0 ? 0 : MeasureSize(text, style).Width;
        return new EsSize(width, height);
    }

    /// <summary>文字列の実寸を測る。</summary>
    /// <param name="text">文字列。</param>
    /// <param name="style">書式。</param>
    /// <returns>実寸（元画像ピクセル）。</returns>
    private static EsSize MeasureSize(string text, TextStyle style)
    {
        var probe = s_probe ??= new TextBlock
        {
            TextWrapping = TextWrapping.NoWrap,
            IsTextScaleFactorEnabled = false,
            UseLayoutRounding = false,
        };

        probe.FontFamily = new FontFamily(style.FontFamily);
        probe.FontSize = style.FontSize;
        probe.FontWeight = style.IsBold ? Microsoft.UI.Text.FontWeights.Bold : Microsoft.UI.Text.FontWeights.Normal;
        probe.FontStyle = style.IsItalic ? Windows.UI.Text.FontStyle.Italic : Windows.UI.Text.FontStyle.Normal;
        probe.Text = text;
        probe.Measure(new Windows.Foundation.Size(double.PositiveInfinity, double.PositiveInfinity));

        var desired = probe.DesiredSize;
        probe.Text = string.Empty;
        return new EsSize(desired.Width, desired.Height);
    }
}
