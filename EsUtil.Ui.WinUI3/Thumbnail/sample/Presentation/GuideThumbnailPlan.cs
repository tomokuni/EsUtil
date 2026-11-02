namespace EsUtil.Ui.WinUI3.Thumbnail.Sample.Presentation;

/// <summary>強調（ホバー・選択）の見た目を差し替える見本の定義（描画計画の差し替え）。</summary>
/// <remarks>
/// <b>示すこと</b>: 提示層を派生しなくても、描画計画（<see cref="ThumbnailDrawPlan"/>）で見た目を差し替えられる<br/>
/// （<c>ThumbnailControl.DrawPlanResolver</c> へ設定する）。 差し替えは公開 API だけで行う。<br/>
/// <b>差分だけを返す</b>: 計画（矩形・層・文言・省略）は提示層が実寸から作る。 本見本は受け取った計画へ<br/>
/// 当てる差分だけを返す（計画の全体をサンプルが用意するのではない）。<br/>
/// <b>見た目</b>: ホバーも選択と同じ帯にする（既定はホバーと選択で色も濃さも違う）。<br/>
/// <b>帯の色は計画が持たない</b>: 帯の色は UI のテーマが決めるため、計画は <see cref="ThumbnailHighlightKind"/>（意味）だけを持つ。<br/>
/// <b>外観で表せることは外観で行う</b>: 名前の色は <c>ThumbnailAppearance.HighlightedLabelArgb</c> が持つため、<br/>
/// 本見本は外観では表せない「強調の種類の差し替え」だけを行う。
/// </remarks>
internal static class GuideThumbnailPlan
{
    /// <summary>強調中の項目だけ帯の種類を差し替えた計画を返す。</summary>
    /// <param name="plan">既定の解決結果（矩形・層・文言を持つ）。</param>
    /// <returns>強調中は帯を選択の種類にした計画。 強調外は受け取った計画をそのまま返す。</returns>
    /// <remarks>状態を持たない（同じ入力には同じ出力を返す）。</remarks>
    public static ThumbnailDrawPlan Resolve(in ThumbnailDrawPlan plan)
        => plan.IsHighlighted ? plan with { Highlight = ThumbnailHighlightKind.Selected } : plan;
}
