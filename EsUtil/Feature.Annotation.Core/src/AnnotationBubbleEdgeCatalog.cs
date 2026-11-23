namespace EsUtil.Feature.Annotation;

/// <summary>吹き出しのしっぽの辺の表示（項目名と選択肢）を単一所有する。</summary>
/// <remarks>
/// <b>並びは <see cref="BubbleEdge"/> の値の順</b>である: 選択肢の位置をそのまま
/// <see cref="BubbleEdge"/> へ写すため、UI が並べ替えると**選んだ辺と実際の辺が変わる**
/// （実測 2026-11-22: WinUI 3 版と WinForms 版が「下・上・左・右」の順に並べており、
/// 「下」を選ぶと上辺が出ていました）。 文言も 4 つの UI 版で同じにします。
/// </remarks>
public static class AnnotationBubbleEdgeCatalog
{
    /// <summary>項目名を取得する。</summary>
    public static string Label => AnnotationEditorText.TailEdge;

    /// <summary>選択肢を取得する（<see cref="BubbleEdge"/> の値の順）。</summary>
    public static IReadOnlyList<string> Options { get; } = ["上", "右", "下", "左"];
}
