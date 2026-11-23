namespace EsUtil.Feature.Annotation;

/// <summary>注釈のフローティング パネルの種別。</summary>
/// <remarks>
/// <b>種別ごとに位置と寿命を分けない</b>: 4 つの UI 版が同じ規則で出し入れできるよう、開閉の状態は
/// <see cref="AnnotationPanelController"/> が単一所有する。 UI は種別に応じた中身を作るだけである。
/// </remarks>
public enum AnnotationPanelKind
{
    /// <summary>本文の書式バー（本文の編集中に出す）。</summary>
    FormatBar,

    /// <summary>右クリックの外観フライアウト（注釈の上で右クリックしたときに出す）。</summary>
    StyleEditor,
}
