using DrawingColor = System.Drawing.Color;
using EsUtil.Feature.Annotation.Core.Defaults;

namespace EsUtil.Feature.Annotation;

/// <summary>注釈の外観（枠線・内側の塗り・角丸）。</summary>
/// <remarks>
/// <b>注釈ごとに持つ</b>（設定の既定値は新規作成時に写す）。 編集中に変えた外観はその注釈だけに効き、
/// 保存した JSON にも残る（見た目はデータの一部である）。<br/>
/// <b>不透明度は色のアルファに統合する</b>（別の不透明度の項目を持たない）。
/// </remarks>
/// <param name="Border">枠線の色（不透明度を含む）。</param>
/// <param name="BorderThickness">枠線の太さ（元画像ピクセル）。</param>
/// <param name="Fill">内側の色（不透明度を含む）。 <see cref="DrawingColor.Transparent"/> は塗らない。</param>
/// <param name="CornerRadius">角丸の半径（元画像ピクセル）。 0 は角を丸めない。</param>
public sealed record AnnotationAppearance(
    DrawingColor Border,
    double BorderThickness,
    DrawingColor Fill,
    double CornerRadius)
{
    /// <summary>枠線の既定の外観を取得する。</summary>
    public static AnnotationAppearance Default => AnnotationDefaults.FrameAppearance;

    /// <summary>安全域へ補正した外観を取得する。</summary>
    /// <returns>枠線の太さと角丸の半径を安全域へ補正した外観。</returns>
    public AnnotationAppearance Normalize() => this with
    {
        BorderThickness = AnnotationLimits.CoerceBorderThickness(BorderThickness),
        CornerRadius = AnnotationLimits.CoerceCornerRadius(CornerRadius),
    };
}
