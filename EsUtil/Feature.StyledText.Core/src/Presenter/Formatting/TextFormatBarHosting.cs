namespace EsUtil.Feature.StyledText.Presenter.Formatting;

/// <summary>書式バーをどこへ出すか（ホストが置くか、コントロールがポップアップで出すか）。</summary>
/// <remarks>
/// <b>どちらもコントロールがバーを所有する</b>: 生成・項目の反映・キーとフォーカスの転送・内容の更新は
/// 本文コントロールが行う（<c>StyledTextControl.FormatBar</c>）。 本値が決めるのは<b>画面要素をどこへ置くか</b>
/// だけである。<br/>
/// <b>既定は <see cref="Custom"/></b>: ホストのレイアウト（設定の脇・本文の上など）へ置きたい利用者が多い。
/// 出す場所を用意したくない場合は <see cref="Popup"/> を選ぶ（選択範囲の近くへコントロールが出す）。
/// </remarks>
public enum TextFormatBarHosting
{
    /// <summary>ホストが <c>FormatBar.View</c> を自分のレイアウトへ置く（既定）。</summary>
    /// <remarks>出すかどうかは <c>ShouldShowFormatBar</c> が単一所有する（ホストは表示の切替だけを行う）。</remarks>
    Custom = 0,

    /// <summary>本文コントロールが選択範囲の近くへポップアップとして出す。</summary>
    /// <remarks>
    /// <b>置く場所を用意しなくてよい</b>: 本文の外へ出すため、編集中の文字に重なりにくい。
    /// 出す位置は <see cref="FloatingPanelPlacement"/> が単一所有する（4 つの UI 版で同じ規則）。
    /// </remarks>
    Popup = 1,
}
