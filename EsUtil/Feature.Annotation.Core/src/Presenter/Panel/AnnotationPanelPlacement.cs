using EsUtil.Feature.StyledText.Presenter.Formatting;
using EsUtil.Platform.Core.Geometry;

namespace EsUtil.Feature.Annotation.Presenter.Panel;

/// <summary>フローティング パネルの位置を決める（規則は書式バーと同じものを使う）。</summary>
/// <remarks>
/// <b>位置の規則は <see cref="FloatingPanelPlacement"/> が単一所有する</b>: 書式バーと注釈の外観フライアウトは
/// 同じフローティング パネルであり、規則を 2 つ持つと UI 間・パネル間で置かれ方が食い違う。 本クラスは
/// 注釈の語彙（レイヤー・対象）で呼べるようにするだけの入口である。<br/>
/// <b>座標はパネルを描く面の左上を原点とする DIP</b>。<br/>
/// <b>見えている範囲の外へは出さない</b>: 切れて見えなくなる（掴めなくなる）ため、置ける領域へ収める。<br/>
/// <b>編集対象に被せない</b>: 書式バーを本文の上へ重ねると、編集中の文字が隠れて読めない。 対象の外
/// （上・下・右・左の順）で置ける領域に収まる場所へ置く。
/// </remarks>
public static class AnnotationPanelPlacement
{
    /// <summary>パネルを置ける領域を求める。</summary>
    /// <param name="layer">注釈レイヤーが占める矩形（DIP）。</param>
    /// <param name="visible">パネルを描ける範囲（ウィンドウの内側。 DIP）。</param>
    /// <param name="allowance">レイヤーの外側へ広げる余白（DIP）。</param>
    /// <returns>パネルを置ける領域（DIP）。</returns>
    public static EsRect ResolveAvailableArea(EsRect layer, EsRect visible, double allowance)
        => FloatingPanelPlacement.ResolveAvailableArea(layer, visible, allowance);

    /// <summary>アンカー（右クリックした位置）をパネルの左上にする。</summary>
    /// <param name="anchor">アンカーの位置（DIP）。</param>
    /// <returns>パネルの左上（DIP）。</returns>
    public static EsPoint ResolveInitial(EsPoint anchor)
        => FloatingPanelPlacement.ResolveInitial(anchor);

    /// <summary>対象（編集中の注釈）に被さらない位置を求める。</summary>
    /// <param name="target">被せたくない矩形（DIP）。</param>
    /// <param name="panel">パネルの大きさ（DIP）。</param>
    /// <param name="available">パネルを置ける領域（DIP）。</param>
    /// <param name="gap">対象とパネルの間の余白（DIP）。</param>
    /// <returns>パネルの左上（DIP）。</returns>
    /// <remarks>上・下・右・左の順に探し、どれも入りきらないときは置ける領域へ収める（規則は共有する）。</remarks>
    public static EsPoint ResolveOutside(EsRect target, EsSize panel, EsRect available, double gap)
        => FloatingPanelPlacement.ResolveOutside(target, panel, available, gap);

    /// <summary>ドラッグ中のパネルの左上を求める。</summary>
    /// <param name="origin">ドラッグ開始時のパネルの左上（DIP）。</param>
    /// <param name="cursorOrigin">ドラッグ開始時のポインターの位置（DIP）。</param>
    /// <param name="cursorCurrent">現在のポインターの位置（DIP）。</param>
    /// <returns>移動先のパネルの左上（DIP）。</returns>
    public static EsPoint ResolveDrag(EsPoint origin, EsPoint cursorOrigin, EsPoint cursorCurrent)
        => FloatingPanelPlacement.ResolveDrag(origin, cursorOrigin, cursorCurrent);

    /// <summary>パネルが置ける領域からはみ出さない位置へ補正する。</summary>
    /// <param name="position">補正する位置（<paramref name="available"/> と同じ座標系）。</param>
    /// <param name="width">パネルの幅。</param>
    /// <param name="height">パネルの高さ。</param>
    /// <param name="available">パネルを置ける領域。</param>
    /// <returns>補正した位置。</returns>
    public static EsPoint Clamp(EsPoint position, double width, double height, EsRect available)
        => FloatingPanelPlacement.Clamp(position, width, height, available);
}
