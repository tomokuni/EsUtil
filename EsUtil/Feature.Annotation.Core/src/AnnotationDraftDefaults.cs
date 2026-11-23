using EsUtil.Feature.Annotation.Core.Defaults;

namespace EsUtil.Feature.Annotation;

/// <summary>新規作成時の既定値（ホストが設定ページで変える値）。</summary>
/// <remarks>
/// <b>既存の注釈には影響しない</b>: 各注釈は自分の外観・書式を持ち、既定値は「これから作る注釈」にだけ写す。<br/>
/// 値を 1 つの型にまとめるのは、設定ページ・サンプル・アプリが同じ組を渡せるようにするためである
/// （項目ごとに引数を増やすと、呼び出し側でどれを渡したか分からなくなる）。
/// </remarks>
/// <param name="FrameAppearance">枠線の注釈の外観。</param>
/// <param name="BubbleAppearance">吹き出しの外観。</param>
/// <param name="TextStyle">本文の書式。</param>
/// <param name="BodyPadding">本文の余白（元画像ピクセル）。</param>
/// <param name="TailWidth">しっぽの幅（元画像ピクセル）。</param>
/// <param name="TailEdge">しっぽを出す辺の既定値。</param>
public sealed record AnnotationDraftDefaults(
    AnnotationAppearance FrameAppearance,
    AnnotationAppearance BubbleAppearance,
    AnnotationTextStyle TextStyle,
    double BodyPadding,
    double TailWidth,
    BubbleEdge TailEdge)
{
    /// <summary>既定値（<see cref="AnnotationDefaults"/> と同じ値）を取得する。</summary>
    public static AnnotationDraftDefaults Default { get; } = new(
        AnnotationDefaults.FrameAppearance,
        AnnotationDefaults.BubbleAppearance,
        AnnotationDefaults.TextStyle,
        AnnotationDefaults.BodyPaddingDefault,
        AnnotationDefaults.TailWidthDefault,
        BubbleEdge.Bottom);
}
