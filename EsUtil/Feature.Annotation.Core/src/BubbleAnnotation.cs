using EsUtil.Platform.Core.Geometry;
using EsUtil.Feature.Annotation.Core.Defaults;
using EsUtil.Feature.StyledText;

namespace EsUtil.Feature.Annotation;

/// <summary>吹き出しの注釈（本文 + しっぽ）。</summary>
/// <remarks>
/// <b>本文は吹き出し内で折り返す</b>（折り返しの行の配置は提示層が解決する）。<br/>
/// <b>しっぽは本体と独立して動かせる</b>（<see cref="BubbleTail.Tip"/> は絶対座標）。<br/>
/// <b>不変である</b>: 本文・しっぽ・余白の変更は <see cref="AnnotationDocument"/> の <c>With...</c> が返す。
/// </remarks>
public sealed record BubbleAnnotation : AnnotationItem
{
    /// <summary>本文（書式付きの文字列）を取得する。</summary>
    public RichText Text { get; init; } = RichText.Empty;

    /// <summary>しっぽ（吹き出し口）を取得する。</summary>
    /// <remarks>先端が画像の外へ出ると掴めないため、変更は <see cref="AnnotationDocument.WithTail"/> を通す。</remarks>
    public BubbleTail Tail { get; init; } = BubbleTail.Default;

    /// <summary>本文と枠線の間の余白（元画像ピクセル）を取得する。</summary>
    public double BodyPadding { get; init; } = AnnotationDefaults.BodyPaddingDefault;

    /// <summary>本文を置く矩形（枠線の内側・余白を除いた領域）を求める。</summary>
    /// <returns>本文の矩形（元画像ピクセル）。</returns>
    /// <remarks>枠線の半分だけ内側へ寄せる（枠線の中心が矩形の辺に乗るため、内側の端は半分だけ内側になる）。</remarks>
    public EsRect ResolveBodyRect()
    {
        var padding = AnnotationLimits.CoerceBodyPadding(BodyPadding);
        var inset = padding + (AnnotationLimits.CoerceBorderThickness(Appearance.BorderThickness) / 2);

        return new EsRect(
            Bounds.X + inset,
            Bounds.Y + inset,
            Math.Max(0, Bounds.Width - (inset * 2)),
            Math.Max(0, Bounds.Height - (inset * 2)));
    }
}
