using EsUtil.Platform.Core.Geometry;

namespace EsUtil.Feature.Loupe;

/// <summary>ルーペの描画計画のうち、装飾（十字線・枠線）の矩形の導出を担う部分。</summary>
/// <remarks>
/// コアが返す幾何（描画面・拡大率・切り出し領域の一辺）と外観（表示可否・枠線の太さ）から、<br/>
/// 描く矩形を導出する。 導出は本クラスが単一所有し、描画実装は返された矩形を自身の描画 API へ渡すだけにする（二重所有を防ぐ）。<br/>
/// <b>可視性</b>: 内部手順であるため private static とし、公開面（描画計画）だけを UI へ渡す。<br/>
/// UI フレームワークに依存しないため、UI 初期化なしの <c>[Fact]</c> で検証できる。
/// </remarks>
public partial class LoupePresenter
{
    // ====================================================================
    // 装飾（十字線・枠線）の矩形
    // ====================================================================

    /// <summary>十字線の 2 本の矩形を計算する。</summary>
    /// <param name="surface">描画面の全体（左上を原点とする正方形）。</param>
    /// <param name="zoomLevel">拡大率（1〜64）。</param>
    /// <param name="captureSize">切り出し領域の一辺（対象ピクセル。 コアが解決済みの値を渡す）。</param>
    /// <returns>十字線の縦・横 2 本の矩形。</returns>
    /// <remarks>
    /// 交点は「切り出した領域を等倍で並べたときの中央のブロック」の左上へ合わせる（<c>captureSize / 2</c> の切り捨て）。<br/>
    /// 切り出し領域の一辺は切り上げのため、<b>拡大率が偶数でない場合は交点が表面の中央から最大 1 ブロックずれる</b><br/>
    /// （切り捨てをやめると、奇数倍率で交点が画素境界から外れて十字線がぼやける）。
    /// </remarks>
    internal static IReadOnlyList<EsRect> ComputeCrosshairPixelRects(EsRect surface, int zoomLevel, int captureSize)
    {
        var pixelSize = (int)surface.Width;
        var start = captureSize / 2 * zoomLevel;
        return
        [
            new EsRect(surface.X + start, surface.Y, zoomLevel, pixelSize),
            new EsRect(surface.X, surface.Y + start, pixelSize, zoomLevel),
        ];
    }

    /// <summary>枠線の 4 辺の矩形を計算する。</summary>
    /// <param name="surface">描画面の全体（左上を原点とする正方形）。</param>
    /// <param name="thickness">枠線の太さ（物理ピクセル）。 補正済みの値を渡す（安全域は <c>LoupeCore.Limits</c> が単一所有する）。</param>
    /// <returns>枠線の上・下・左・右の 4 つの矩形。</returns>
    /// <remarks>
    /// 辺の矩形の計算は <see cref="EsRect.ComputeInnerEdges(double)"/> が単一所有する（選択矩形の枠線と同じ式）。<br/>
    /// 太さは面の短辺で上限を取る（面より太い枠線は面全体を覆い、矩形が面からはみ出さない）。
    /// </remarks>
    internal static IReadOnlyList<EsRect> ComputeBorderPixelRects(EsRect surface, double thickness)
        => surface.ComputeInnerEdges(Math.Min(thickness, Math.Min(surface.Width, surface.Height)));
}
