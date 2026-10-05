using EsUtil.Platform.Core.Geometry;
using Xunit;

namespace EsUtil.Feature.Loupe.Tests;

/// <summary><see cref="LoupePresenter.ComputeCrosshairPixelRects"/> のテスト（UI 非依存・並列実行）。</summary>
/// <remarks>
/// 十字線・枠線の描画矩形の計算のみを検証するため、UI 初期化に依存せず <c>[Fact]</c> で並列実行する。<br/>
/// 各 UI（Avalonia / MewUI）の描画は <see cref="EsRect"/> を自身の描画 API へ渡すだけであり、<br/>
/// 本テストが両 UI の見た目を保証する（矩形計算の単一所有）。
/// </remarks>
public class LoupePresenterDrawPlanTests
{
    /// <summary>十字線が縦横 2 本の矩形で返ることを検証する。</summary>
    [Fact]
    internal void ComputeCrosshairPixelRects_十字線の縦横2本の矩形を返す()
    {
        // 一辺 100, zoomLevel=4, 切り出しの中心が中央（切り出し一辺 25 の 12.5） → start = 12.5×4 = 50
        var rects = LoupePresenter.ComputeCrosshairPixelRects(new EsRect(0, 0, 100, 100), 4, new EsPoint(12.5, 12.5));

        Assert.Equal(2, rects.Count);
        Assert.Equal(new EsRect(50, 0, 4, 100), rects[0]); // 縦
        Assert.Equal(new EsRect(0, 50, 100, 4), rects[1]); // 横
    }

    /// <summary>拡大率 1 の場合は中心を 1 ピクセルで描画することを検証する。</summary>
    [Fact]
    internal void ComputeCrosshairPixelRects_zoomLevel1では中心を1ピクセルで描画する()
    {
        var rects = LoupePresenter.ComputeCrosshairPixelRects(new EsRect(0, 0, 100, 100), 1, new EsPoint(50, 50));

        Assert.Equal(new EsRect(50, 0, 1, 100), rects[0]);
        Assert.Equal(new EsRect(0, 50, 100, 1), rects[1]);
    }

    /// <summary>描画面の原点が 0 でない場合もその座標系で矩形が返ることを検証する。</summary>
    [Fact]
    internal void ComputeCrosshairPixelRects_描画面の原点を基準に計算する()
    {
        var rects = LoupePresenter.ComputeCrosshairPixelRects(new EsRect(10, 20, 100, 100), 1, new EsPoint(50, 50));

        Assert.Equal(new EsRect(60, 20, 1, 100), rects[0]);
        Assert.Equal(new EsRect(10, 70, 100, 1), rects[1]);
    }

    /// <summary>切り出し内の中心位置の小数部が十字線の位置へ反映されることを検証する。</summary>
    /// <remarks>
    /// 切り出しの原点だけが整数へ丸められるため、中心の小数部は切り出し内のオフセットに残る。<br/>
    /// 切り出し領域の中央のブロックへ合わせると、小数部 × 拡大率だけ他の装飾とずれる（本テストはその退行を防ぐ）。
    /// </remarks>
    [Theory]
    [InlineData(12.0, 48)]
    [InlineData(12.5, 50)]
    [InlineData(11.5, 46)]
    internal void ComputeCrosshairPixelRects_中心の小数部を位置へ反映する(double offset, int expected)
        => Assert.Equal(expected, (int)LoupePresenter.ComputeCrosshairPixelRects(
            new EsRect(0, 0, 100, 100), 4, new EsPoint(offset, offset))[0].X);

    /// <summary>X と Y の中心位置が異なる場合は軸ごとに位置が決まることを検証する。</summary>
    [Fact]
    internal void ComputeCrosshairPixelRects_軸ごとに中心位置を解決する()
    {
        var rects = LoupePresenter.ComputeCrosshairPixelRects(new EsRect(0, 0, 100, 100), 4, new EsPoint(12.0, 13.0));

        Assert.Equal(48, (int)rects[0].X);  // 縦線（X）
        Assert.Equal(52, (int)rects[1].Y);  // 横線（Y）
    }

    /// <summary>枠線が 4 辺（上下左右）の矩形で返ることを検証する。</summary>
    [Fact]
    internal void ComputeBorderPixelRects_枠線の4辺の矩形を返す()
    {
        // 一辺 100・width=2 → 上 = 100×2、内側 = 100-4 = 96
        var rects = LoupePresenter.ComputeBorderPixelRects(new EsRect(0, 0, 100, 100), 2.0);

        Assert.Equal(4, rects.Count);
        Assert.Equal(new EsRect(0, 0, 100, 2), rects[0]);   // 上
        Assert.Equal(new EsRect(0, 98, 100, 2), rects[1]);  // 下
        Assert.Equal(new EsRect(0, 2, 2, 96), rects[2]);    // 左
        Assert.Equal(new EsRect(98, 2, 2, 96), rects[3]);   // 右
    }

    /// <summary>枠線の太さが描画面より大きい場合は面の短辺で上限を取り、内側が 0 になることを検証する。</summary>
    /// <remarks>太さの安全域への補正は外観（<c>LoupeAppearance.BorderThickness</c> の生成時）が担う。</remarks>
    [Fact]
    internal void ComputeBorderPixelRects_幅が描画面を超えるなら内側が0になる()
    {
        var rects = LoupePresenter.ComputeBorderPixelRects(new EsRect(0, 0, 100, 100), 200.0);

        Assert.Equal(new EsRect(0, 0, 100, 100), rects[0]);  // 上（全面）
        Assert.Equal(new EsRect(0, 0, 100, 100), rects[1]);  // 下（全面）
        Assert.Equal(new EsRect(0, 100, 100, 0), rects[2]);  // 左（高さ 0）
        Assert.Equal(new EsRect(0, 100, 100, 0), rects[3]);  // 右（高さ 0）
    }
}
