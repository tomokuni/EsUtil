using EsUtil.Platform.Core.Geometry;

namespace EsUtil.Feature.Annotation;

/// <summary>本文の 1 行（その行の範囲と、書式ごとの区間）。</summary>
/// <remarks>座標は元画像ピクセルである。 行の高さは行内で最も大きい文字に合わせる。</remarks>
/// <param name="Bounds">行の矩形（元画像ピクセル）。</param>
/// <param name="Segments">行の中の書式ごとの区間（左から右の順）。</param>
public sealed record AnnotationTextLinePlan(EsRect Bounds, IReadOnlyList<AnnotationTextSegmentPlan> Segments);
