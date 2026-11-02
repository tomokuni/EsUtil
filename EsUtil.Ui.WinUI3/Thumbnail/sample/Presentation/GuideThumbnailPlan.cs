namespace EsUtil.Ui.WinUI3.Thumbnail.Sample.Presentation;

/// <summary>強調（ホバー・選択）の見た目を差し替える見本の定義（描画計画の差し替え）。</summary>
/// <remarks>
/// <b>示すこと</b>: 提示層を派生しなくても、描画計画（<see cref="ThumbnailDrawPlan"/>）で見た目を差し替えられる<br/>
/// （<c>ThumbnailControl.DrawPlanResolver</c> へ設定する）。 差し替えは公開 API だけで行う。<br/>
/// <b>見た目</b>: 強調中の項目の名前を白にし、ホバーも選択と同じ帯にする（既定はホバーと選択で濃さが違う）。<br/>
/// <b>帯の色は計画が持たない</b>: 帯の色は UI のテーマが決めるため、計画は <see cref="ThumbnailHighlightKind"/>（意味）だけを持つ。<br/>
/// <b>文言は変えない</b>: 名前を置ける大きさは提示層が実寸から決めているため、本見本は文字の体裁だけを変える<br/>
/// （文言を伸ばすと行からはみ出す）。
/// </remarks>
internal static class GuideThumbnailPlan
{
    /// <summary>強調中の名前の色（白）。</summary>
    private const uint HighlightedLabelArgb = 0xFFFFFFFF;

    /// <summary>強調中の項目だけ帯と名前の色を差し替えた計画を返す。</summary>
    /// <param name="plan">既定の解決結果（矩形・層・文言を持つ）。</param>
    /// <returns>強調中は色を差し替えた計画。 強調外は受け取った計画をそのまま返す。</returns>
    /// <remarks>状態を持たない（同じ入力には同じ出力を返す）。</remarks>
    public static ThumbnailDrawPlan Resolve(in ThumbnailDrawPlan plan)
        => plan.IsHighlighted
            ? plan with
            {
                Highlight = ThumbnailHighlightKind.Selected,
                Label = plan.Label with { ForegroundArgb = HighlightedLabelArgb },
            }
            : plan;
}
