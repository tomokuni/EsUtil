namespace EsUtil.Feature.Annotation;

/// <summary>枠線の注釈（矩形 + 角丸）。</summary>
/// <remarks>
/// <b>入れ子の二重枠は持たない</b>（枠線を重ねたい場合は注釈を複数置く）。
/// 破線・楕円・矢印は持たない（角丸の半径だけを <see cref="AnnotationAppearance.CornerRadius"/> で変えられる）。
/// </remarks>
public sealed record FrameAnnotation : AnnotationItem;
