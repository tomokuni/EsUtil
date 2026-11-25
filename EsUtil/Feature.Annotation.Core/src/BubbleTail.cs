using EsUtil.Platform.Core.Geometry;
using EsUtil.Feature.Annotation.Core.Defaults;

namespace EsUtil.Feature.Annotation;

/// <summary>吹き出しのしっぽ（吹き出し口）。</summary>
/// <remarks>
/// <b>先端は本体と独立して動かせる</b>（<see cref="Tip"/> は絶対座標）。 本体を移動しても先端は動かない。<br/>
/// <b>付け根は本体に追従する</b>（<see cref="BaseEdge"/> と <see cref="BaseOffset"/> は本体の辺に対する相対指定）。
/// 本体の大きさを変えても、しっぽは同じ辺の同じ割合の位置から出る。
/// </remarks>
/// <param name="Tip">先端（指す位置。 元画像ピクセル）。</param>
/// <param name="BaseEdge">本体から出る辺。</param>
/// <param name="BaseOffset">辺上の位置（0 = 辺の始点側、1 = 終点側）。</param>
/// <param name="Width">付け根の幅（元画像ピクセル）。</param>
public sealed record BubbleTail(
    EsPoint Tip,
    BubbleEdge BaseEdge = BubbleEdge.Bottom,
    double BaseOffset = AnnotationDefaults.TailBaseOffsetDefault,
    double Width = AnnotationDefaults.TailWidthDefault)
{
    /// <summary>既定のしっぽ（先端は原点、下辺の中央、既定の幅）を取得する。</summary>
    /// <remarks>本体の位置は本体の矩形が決めるため、作成時は先端だけを決めればよい。</remarks>
    public static BubbleTail Default { get; } = new(new EsPoint(0, 0));

    /// <summary>安全域へ補正したしっぽを取得する。</summary>
    /// <returns>幅と付け根の位置を安全域へ補正したしっぽ。</returns>
    public BubbleTail Normalize() => this with
    {
        BaseOffset = AnnotationLimits.CoerceTailBaseOffset(BaseOffset),
        Width = AnnotationLimits.CoerceTailWidth(Width),
    };

    /// <summary>本体の矩形から、しっぽの付け根（辺上の 2 点）を求める。</summary>
    /// <param name="body">本体の矩形（元画像ピクセル）。</param>
    /// <returns>付け根の 2 点（始点・終点）。</returns>
    /// <remarks>付け根の線分は本体の辺に沿う（先端と結んで三角形にする）。</remarks>
    public (EsPoint From, EsPoint To) ResolveBase(EsRect body)
    {
        var width = AnnotationLimits.CoerceTailWidth(Width);
        var offset = AnnotationLimits.CoerceTailBaseOffset(BaseOffset);

        return BaseEdge switch
        {
            BubbleEdge.Top => (
                new EsPoint(body.X + (body.Width * offset) - (width / 2), body.Y),
                new EsPoint(body.X + (body.Width * offset) + (width / 2), body.Y)),
            BubbleEdge.Bottom => (
                new EsPoint(body.X + (body.Width * offset) - (width / 2), body.Bottom),
                new EsPoint(body.X + (body.Width * offset) + (width / 2), body.Bottom)),
            BubbleEdge.Left => (
                new EsPoint(body.X, body.Y + (body.Height * offset) - (width / 2)),
                new EsPoint(body.X, body.Y + (body.Height * offset) + (width / 2))),
            _ => (
                new EsPoint(body.Right, body.Y + (body.Height * offset) - (width / 2)),
                new EsPoint(body.Right, body.Y + (body.Height * offset) + (width / 2))),
        };
    }
}
