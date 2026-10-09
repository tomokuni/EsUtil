using System.Globalization;
using Avalonia.Media;
using EsUtil.Platform.Core.Geometry;

// 複製元: EsUtil.SaveEvidence.Platform.AvaloniaUI.Imaging（独立パッケージ用の意図的な複製。 .github/依存関係.md §4.1）
namespace EsUtil.Feature.Loupe.AvaloniaUI.Internal;

/// <summary>Avalonia の描画 API でテキストの実寸を測る（ラベル配置の唯一の入口）。</summary>
/// <remarks>
/// <b>位置付け</b>: 実寸は描画 API にしか問い合わせられないため、UI 層の各コントロールが同じ測定を<br/>
/// それぞれ書くと「片方だけ 0 を返す」といった食い違いが起きる（実際にラベルが実寸 0 で配置され、<br/>
/// 背景が内側余白の分だけの大きさになり文字が切り落とされる不具合が発生した）。<br/>
/// 測定は本クラスが単一所有し、配置の計算（クランプ）は提示層の配置計画が担う。<br/>
/// <br/>
/// <b>測定用のブラシ</b>: <see cref="FormattedText"/> はブラシを要求するが、測定結果に色は影響しないため<br/>
/// 固定のブラシを共有する（外観の値ではない）。
/// </remarks>
internal static class AvaloniaTextMeasure
{
    /// <summary>測定に使うブラシ（色は測定結果に影響しないため固定で共有する）。</summary>
    private static readonly SolidColorBrush s_brush = new(Colors.White);

    /// <summary>テキストの実寸を測る。</summary>
    /// <param name="text">測定するテキスト。 null 不可。</param>
    /// <param name="fontSize">フォントサイズ（DIP）。</param>
    /// <returns>テキストの実寸（DIP）。</returns>
    /// <remarks><see cref="FormattedText"/> は実寸を 2 つの double でしか公開しないため、境界でコアのサイズ型へ組み立てる。</remarks>
    public static EsSize Measure(string text, double fontSize)
    {
        var formatted = new FormattedText(
            text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Typeface.Default, fontSize, s_brush);

        return new EsSize(formatted.Width, formatted.Height);
    }
}
