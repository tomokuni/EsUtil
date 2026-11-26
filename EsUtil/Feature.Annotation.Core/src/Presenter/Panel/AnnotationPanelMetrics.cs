using EsUtil.Feature.StyledText.Presenter.Formatting;

namespace EsUtil.Feature.Annotation.Presenter.Panel;

/// <summary>書式バーと注釈（編集中の本文を囲む矩形）の間の余白（DIP）を所有する。</summary>
/// <remarks>
/// <b>余白を広めに取る</b>: パネルの実寸は UI ごとに希望寸法と描画サイズがずれることがあり、
/// 小さな余白だと書式バーが注釈に数ピクセル重なる。 余白を 1 か所で所有し、
/// 4 つの UI 版が同じ値を使う。
/// </remarks>
public static class AnnotationPanelMetrics
{
    /// <summary>書式バーと注釈の間の余白（DIP）。</summary>
    public const double FormatBarGap = FloatingPanelPlacement.FormatBarGap;

    /// <summary>レイヤーの範囲より外側へパネルを置ける余白（DIP）。</summary>
    /// <remarks>
    /// <b>レイヤーの端で内側へ押し戻さないための余白</b>: レイヤーの範囲だけで挟むと、端にある注釈の
    /// 書式バーが内側へ押し戻され、本文に重なる。 広げた範囲は見えている範囲
    /// （<see cref="AnnotationPanelPlacement.ResolveAvailableArea"/>）との交わりで切られる。
    /// </remarks>
    public const double OutsideAllowance = FloatingPanelPlacement.OutsideAllowance;
}