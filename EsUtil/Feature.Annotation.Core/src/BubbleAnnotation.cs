using System.Text.Json.Serialization;
using EsUtil.Platform.Core.Geometry;

namespace EsUtil.Feature.Annotation;

/// <summary>吹き出しの注釈（本文 + しっぽ）。</summary>
/// <remarks>
/// <b>本文は吹き出し内で折り返す</b>（折り返しの計算は <see cref="AnnotationPresenter"/> が単一所有する）。<br/>
/// <b>しっぽは本体と独立して動かせる</b>（<see cref="BubbleTail.Tip"/> は絶対座標）。
/// </remarks>
public sealed class BubbleAnnotation : AnnotationItem
{
    /// <summary>本文（書式付きの文字列）を取得または設定する。</summary>
    public RichText Text { get; set; } = RichText.Empty;

    /// <summary>しっぽ（吹き出し口）を取得する。</summary>
    /// <remarks>
    /// <b>設定子は Core の内部に閉じる</b>: 先端が画像の外へ出ると掴めなくなるため、変更は
    /// <see cref="AnnotationDocument.SetTailTip"/>（画像の内側へ収める）を通す。
    /// </remarks>
    [JsonInclude]
    public BubbleTail Tail { get; internal set; } = BubbleTail.Default;

    /// <summary>本文と枠線の間の余白（元画像ピクセル）を取得または設定する。</summary>
    public double BodyPadding { get; set; } = AnnotationDefaults.BodyPaddingDefault;

    /// <summary>同じ内容の複製を作る（本文も複製する）。</summary>
    /// <returns>複製。</returns>
    public override AnnotationItem Clone() => new BubbleAnnotation
    {
        Id = Id,
        Bounds = Bounds,
        Appearance = Appearance,
        Text = new RichText(Text.Runs),
        Tail = Tail,
        BodyPadding = BodyPadding,
    };

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
