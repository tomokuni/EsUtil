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
    /// <param name="captureCenterOffset">可視域内での拡大中心の位置（対象ピクセル。 コアが解決済みの値を渡す）。</param>
    /// <returns>十字線の縦・横 2 本の矩形。</returns>
    /// <remarks>
    /// 交点は「可視域内の中心位置 × 拡大率」であり、可視域は中心が描画面の中心へ写るように決まるため、<br/>
    /// <b>十字線は常に描画面の中心（<c>ClientPixelSize / 2</c>）になる</b>（拡大中心が小数でも位置が動かない）。<br/>
    /// ホストが描くオーバーレイ（選択矩形の破線等）も同じ実効原点（<c>LoupeGeometry.CaptureTopLeft</c>）を使うため、<br/>
    /// 拡大中心と一致する対象座標（ドラッグ中のハンドル等）は十字線と同じ位置へ写る。<br/>
    /// 線の太さは拡大率と同じ（1 対象ピクセル = 拡大率 px を強調する）。<br/>
    /// 丸めは <see cref="EsRounding.Round"/>（0.5 は 0 から遠い方）を使う（負の座標でも中心から同じ距離になる）。
    /// </remarks>
    internal static IReadOnlyList<EsRect> ComputeCrosshairPixelRects(EsRect surface, int zoomLevel, EsPoint captureCenterOffset)
    {
        var pixelSize = (int)surface.Width;
        var start = EsPointInt.FromEsPoint(
            new EsPoint(captureCenterOffset.X * zoomLevel, captureCenterOffset.Y * zoomLevel), EsRounding.Round);
        return
        [
            new EsRect(surface.X + start.X, surface.Y, zoomLevel, pixelSize),
            new EsRect(surface.X, surface.Y + start.Y, pixelSize, zoomLevel),
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
