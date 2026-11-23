namespace EsUtil.Feature.Annotation;

/// <summary>書式バーに並べる項目。</summary>
/// <remarks>
/// <b>並び順は単一所有する</b>: 4 つの UI 版がそれぞれ独自の順で並べると、版ごとに操作の位置が変わる
/// （実測 2026-11-20: AvaloniaUI 版と MewUI 版で装飾のボタンが末尾にあった）。 並びは
/// <see cref="AnnotationFormatBarLayout.Order"/> が決め、UI はその順に要素を置くだけでよい。
/// </remarks>
public enum AnnotationFormatBarItem
{
    /// <summary>太字。</summary>
    Bold,

    /// <summary>斜体。</summary>
    Italic,

    /// <summary>下線。</summary>
    Underline,

    /// <summary>取り消し線。</summary>
    Strike,

    /// <summary>フォント名。</summary>
    Font,

    /// <summary>フォントの大きさ。</summary>
    Size,

    /// <summary>色。</summary>
    Color,
}
