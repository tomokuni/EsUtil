namespace EsUtil.Feature.Annotation.Presenter.Selection;

/// <summary>注釈の編集セッションで起きた変更の種類。</summary>
/// <remarks>
/// <b>通知は 1 つの値にまとめる</b>: 選択・本文の編集・文書は同じ操作で同時に変わることがある（削除は文書と選択が変わる）。
/// 種類ごとに購読を作ると、購読側が同じタイミングの複数の通知を受けて順序を気にする必要がある。
/// </remarks>
[Flags]
public enum AnnotationEditChange
{
    /// <summary>変更なし。</summary>
    None = 0,

    /// <summary>文書が変わった。</summary>
    Document = 1,

    /// <summary>選択が変わった。</summary>
    Selection = 2,

    /// <summary>本文の編集の開始・終了が変わった。</summary>
    Editing = 4,

    /// <summary>本文のキャレット・選択範囲が変わった。</summary>
    TextSelection = 8,
}
