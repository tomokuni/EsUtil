using EsUtil.Platform.Core.Geometry;

namespace EsUtil.Feature.Annotation.Presenter;

/// <summary>本文の書式ごとの区間（同じ書式が続く範囲）。</summary>
/// <remarks>
/// <b>座標は元画像ピクセル</b>である。 下線・取り消し線の位置は <see cref="Style"/> のフラグから UI が求める
/// （線の太さは文字の大きさに対する比率であり、描画時に 1 か所で決める）。
/// </remarks>
/// <param name="Text">区間の文字列。</param>
/// <param name="Style">区間の書式。</param>
/// <param name="Bounds">区間の矩形（元画像ピクセル。 高さは行の高さ）。</param>
public sealed record AnnotationTextSegmentPlan(string Text, AnnotationTextStyle Style, EsRect Bounds);
