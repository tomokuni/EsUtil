using EsUtil.Platform.Core.Palette;
using DrawingColor = System.Drawing.Color;
using EsUtil.Feature.Text;
using EsUtil.Feature.Text.Core.Defaults;

namespace EsUtil.Feature.Annotation.Core.Defaults;

/// <summary>設定の値（色の文字列と数値）から新規作成時の既定値を組み立てる（単一所有）。</summary>
/// <remarks>
/// <b>対応付けを UI 版ごとに書かない</b>: 設定の項目名はホストの語彙であるが、「枠線の色を枠線の外観へ、
/// 角丸を枠線と吹き出しの両方へ」という対応は注釈の規則である。 4 つの UI 版がそれぞれ書くと、
/// 片方だけ角丸を吹き出しへ写さないといった差が出る。<br/>
/// <b>色の解釈は <see cref="ColorString"/> が単一所有する</b>（色名・16 進・不透明度を同じ規則で読む）。
/// 解釈できない色は透明として扱い、例外にしない（入力途中の値を弾かない）。
/// </remarks>
public static class AnnotationDraftDefaultsFactory
{
    /// <summary>設定の値から新規作成時の既定値を組み立てる。</summary>
    /// <param name="frameBorderColor">枠線の色（色名または 16 進）。</param>
    /// <param name="borderThickness">枠線の太さ（元画像ピクセル。 枠線と吹き出しの両方へ使う）。</param>
    /// <param name="frameFillColor">枠線の内側の色（色名または 16 進）。</param>
    /// <param name="cornerRadius">角丸の半径（元画像ピクセル。 枠線と吹き出しの両方へ使う）。</param>
    /// <param name="bubbleBorderColor">吹き出しの枠線の色（色名または 16 進）。</param>
    /// <param name="bubbleFillColor">吹き出しの内側の色（色名または 16 進）。</param>
    /// <param name="textColor">本文の色（色名または 16 進）。</param>
    /// <param name="fontSize">本文の文字の大きさ（元画像ピクセル）。</param>
    /// <param name="bodyPadding">本文の余白（元画像ピクセル）。</param>
    /// <param name="tailWidth">しっぽの幅（元画像ピクセル）。</param>
    /// <param name="tailEdge">しっぽを出す辺。 省略すると下辺。</param>
    /// <returns>新規作成時の既定値（安全域へ補正済み）。</returns>
    public static AnnotationDraftDefaults FromText(
        string? frameBorderColor,
        double borderThickness,
        string? frameFillColor,
        double cornerRadius,
        string? bubbleBorderColor,
        string? bubbleFillColor,
        string? textColor,
        double fontSize,
        double bodyPadding,
        double tailWidth,
        BubbleEdge tailEdge = BubbleEdge.Bottom)
        => new(
            new AnnotationAppearance(
                Parse(frameBorderColor),
                borderThickness,
                Parse(frameFillColor),
                cornerRadius).Normalized(),
            new AnnotationAppearance(
                Parse(bubbleBorderColor),
                borderThickness,
                Parse(bubbleFillColor),
                cornerRadius).Normalized(),
            new TextStyle(
                TextDefaults.PreferredFontFamilies[0],
                fontSize,
                Parse(textColor)).Normalized(),
            AnnotationLimits.CoerceBodyPadding(bodyPadding),
            AnnotationLimits.CoerceTailWidth(tailWidth),
            tailEdge);

    /// <summary>色の文字列を色へ変換する（解釈できない場合は透明）。</summary>
    /// <param name="text">色の文字列。</param>
    /// <returns>解釈した色。 解釈できない場合は透明。</returns>
    private static DrawingColor Parse(string? text)
        => ColorString.TryParse(text, out var color, out _) ? color : DrawingColor.Transparent;
}
