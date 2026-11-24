namespace EsUtil.Feature.Annotation.Presenter.Editor;

/// <summary>吹き出しのしっぽの辺の表示（項目名と選択肢）を単一所有する。</summary>
/// <remarks>
/// <b>並びは <see cref="BubbleEdge"/> の値の順</b>である: 選択肢の位置をそのまま<br/>
/// <see cref="BubbleEdge"/> へ写すため、UI が並べ替えると<b>選んだ辺と実際の辺が変わる</b>。<br/>
/// 文言も 4 つの UI 版で同じにする。
/// </remarks>
public static class AnnotationBubbleEdgeCatalog
{
    /// <summary>項目名を取得する。</summary>
    public static string Label => AnnotationEditorText.TailEdge;

    /// <summary>選択肢を取得する（<see cref="BubbleEdge"/> の値の順）。</summary>
    public static IReadOnlyList<string> Options { get; } = ["上", "右", "下", "左"];
}
