using DrawingColor = System.Drawing.Color;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Feature.StyledText;

namespace EsUtil.Feature.Annotation;

/// <summary>注釈 1 件の描画計画（描く値はすべて解決済み）。</summary>
/// <remarks>
/// <b>UI は計算を持たない</b>: 角丸の半径・塗り・枠線・しっぽの多角形・行の位置はすべて本値に入っている。
/// UI 版が行うのは自分の描画 API へ写すことだけである（4 つの UI 版で見た目がずれない）。<br/>
/// 座標は<b>元画像ピクセル</b>である（UI は表示倍率を掛けて DIP へ写す）。
/// </remarks>
/// <param name="Id">注釈の識別子。</param>
/// <param name="Kind">種別。</param>
/// <param name="Bounds">本体の矩形（元画像ピクセル）。</param>
/// <param name="CornerRadius">角丸の半径（元画像ピクセル）。</param>
/// <param name="Fill">内側の色（透明なら塗らない）。</param>
/// <param name="Border">枠線の色。</param>
/// <param name="BorderThickness">枠線の太さ（元画像ピクセル）。</param>
/// <param name="Tail">しっぽの多角形（3 点。 枠線の注釈では空）。</param>
/// <param name="Lines">本文の行（枠線の注釈では空）。</param>
public sealed record AnnotationShapePlan(
    string Id,
    AnnotationKind Kind,
    EsRect Bounds,
    double CornerRadius,
    DrawingColor Fill,
    DrawingColor Border,
    double BorderThickness,
    IReadOnlyList<EsPoint> Tail,
    IReadOnlyList<TextLinePlan> Lines)
{
    /// <summary>内側を塗るかどうかを取得する。</summary>
    /// <value>塗りの色に不透明度がある場合は <see langword="true"/>。</value>
    public bool HasFill => Fill.A > 0;

    /// <summary>しっぽを描くかどうかを取得する。</summary>
    /// <value>しっぽの多角形が 3 点以上ある場合は <see langword="true"/>。</value>
    public bool HasTail => Tail.Count >= 3;

    /// <summary>本文を描くかどうかを取得する。</summary>
    /// <value>行がある場合は <see langword="true"/>。</value>
    public bool HasText => Lines.Count > 0;
}
