namespace EsUtil.Feature.Annotation.Presenter;

/// <summary>書式バーと注釈（編集中の本文を囲む矩形）の間の余白（DIP）を所有する。</summary>
/// <remarks>
/// <b>余白を広めに取る</b>: パネルの実寸は UI ごとに希望寸法と描画サイズがずれることがあり、
/// 小さな余白だと書式バーが注釈に数ピクセル重なる（実測 2026-11-21）。 余白を 1 か所で所有し、
/// 4 つの UI 版が同じ値を使う。
/// </remarks>
public static class AnnotationPanelMetrics
{
    /// <summary>書式バーと注釈の間の余白（DIP）。</summary>
    public const double FormatBarGap = 24;

    /// <summary>レイヤーの範囲より外側へパネルを置ける余白（DIP）。</summary>
    /// <remarks>
    /// <b>ウィンドウの外へ出すための余白</b>: レイヤーの範囲だけで挟むと、ウィンドウの端にある注釈の
    /// 書式バーが内側へ押し戻され、ウィンドウの外へ出せない（実測 2026-11-23）。 画面の外へ出ない
    /// ことは各 UI が作業領域で止める。
    /// </remarks>
    public const double OutsideAllowance = 240;
}