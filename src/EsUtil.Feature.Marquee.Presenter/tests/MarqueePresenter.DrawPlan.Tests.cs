using EsUtil.Platform.Core.Geometry;
using Xunit;


namespace EsUtil.Feature.Marquee.Tests;

/// <summary><see cref="MarqueePresenter.ComputeBorderDipRects"/> のテスト（UI 非依存・並列実行）。</summary>
/// <remarks>
/// 装飾（暗転領域・枠線矩形）の幾何計算のみを検証するため、UI 初期化に依存せず <c>[Fact]</c> で並列実行する。<br/>
/// 各 UI（Avalonia / MewUI）の描画は <see cref="EsRect"/> を自身の描画 API へ渡すだけであり、<br/>
/// 本テストが両 UI の見た目を保証する（幾何計算の単一所有）。
/// </remarks>
public class MarqueePresenterDrawPlanTests
{
    /// <summary>枠線が四辺（上下左右）の矩形で返ることを検証する。</summary>
    [Fact]
    public void ComputeBorderDipRects_四辺の矩形を返す()
    {
        var rect = new EsRect(10, 20, 100, 50);

        var result = MarqueePresenter.ComputeBorderDipRects(rect, 2.0);

        Assert.Equal(4, result.Count);
        Assert.Equal(new EsRect(10, 20, 100, 2), result[0]);     // 上
        Assert.Equal(new EsRect(10, 68, 100, 2), result[1]);     // 下
        Assert.Equal(new EsRect(10, 22, 2, 46), result[2]);      // 左
        Assert.Equal(new EsRect(108, 22, 2, 46), result[3]);     // 右
    }

    /// <summary>渡された太さがそのまま枠線の厚みになることを検証する。</summary>
    /// <remarks>太さの補正は装飾の外観の生成時に行われるため、本計算は補正を持たない。</remarks>
    [Fact]
    public void ComputeBorderDipRects_渡された太さをそのまま使う()
    {
        var rect = new EsRect(0, 0, 100, 50);

        var result = MarqueePresenter.ComputeBorderDipRects(rect, 0.5);

        Assert.Equal(0.5, result[0].Height);
    }

    /// <summary>高さより太い枠線でも負のサイズにならないことを検証する。</summary>
    [Fact]
    public void ComputeBorderDipRects_高さより太い枠線でも負にならない()
    {
        var rect = new EsRect(0, 0, 10, 2);

        var result = MarqueePresenter.ComputeBorderDipRects(rect, 2.0);

        Assert.Equal(0, result[2].Height);
        Assert.Equal(0, result[3].Height);
    }

    /// <summary>注目矩形がない場合は全面が暗転領域になることを検証する。</summary>
    [Fact]
    public void ComputeOverlayDipRects_注目矩形がなければ全面を返す()
    {
        var result = MarqueePresenter.ComputeOverlayDipRects(new EsRect(0, 0, 100, 100), null);

        Assert.Single(result);
        Assert.Equal(new EsRect(0, 0, 100, 100), result[0]);
    }

    /// <summary>注目矩形がある場合は四辺の帯（上・下・左・右）になることを検証する。</summary>
    [Fact]
    public void ComputeOverlayDipRects_注目矩形がある場合は四辺の帯になる()
    {
        var focus = new EsRect(20, 30, 40, 50);

        var result = MarqueePresenter.ComputeOverlayDipRects(new EsRect(0, 0, 100, 100), focus);

        // 上（全幅・高さ 30）/ 下（全幅・高さ 20）/ 左（高さ 50・幅 20）/ 右（高さ 50・幅 40）
        Assert.Equal(4, result.Count);
        Assert.Equal(new EsRect(0, 0, 100, 30), result[0]);
        Assert.Equal(new EsRect(0, 80, 100, 20), result[1]);
        Assert.Equal(new EsRect(0, 30, 20, 50), result[2]);
        Assert.Equal(new EsRect(60, 30, 40, 50), result[3]);
    }

    /// <summary>注目矩形がクライアントと交差しない場合は全面を暗転することを検証する。</summary>
    [Fact]
    public void ComputeOverlayDipRects_注目矩形が外側なら全面を返す()
    {
        var focus = new EsRect(200, 200, 50, 50);

        var result = MarqueePresenter.ComputeOverlayDipRects(new EsRect(0, 0, 100, 100), focus);

        Assert.Single(result);
        Assert.Equal(new EsRect(0, 0, 100, 100), result[0]);
    }

    /// <summary>注目矩形がクライアント全体の場合は暗転領域がなくなることを検証する。</summary>
    [Fact]
    public void ComputeOverlayDipRects_注目矩形が全面なら暗転なしになる()
    {
        var result = MarqueePresenter.ComputeOverlayDipRects(new EsRect(0, 0, 100, 100), new EsRect(0, 0, 100, 100));

        Assert.Empty(result);
    }

    /// <summary>注目矩形がクライアント端に接する場合はその辺の帯を生成しないことを検証する。</summary>
    [Fact]
    public void ComputeOverlayDipRects_注目矩形が上端左端に接する場合は下右のみになる()
    {
        var focus = new EsRect(0, 0, 40, 40);

        var result = MarqueePresenter.ComputeOverlayDipRects(new EsRect(0, 0, 100, 100), focus);

        // 上（Top == 0）と左（Left == 0）は幅・高さが 0 のため生成されない
        Assert.Equal(2, result.Count);
        Assert.Equal(new EsRect(0, 40, 100, 60), result[0]); // 下
        Assert.Equal(new EsRect(40, 0, 60, 40), result[1]); // 右
    }

    /// <summary>注目矩形がサイズ 0 の場合は全面が暗転領域になることを検証する。</summary>
    [Fact]
    public void ComputeOverlayDipRects_注目矩形がサイズ0なら全面を返す()
    {
        var focus = new EsRect(50, 50, 0, 0);

        var result = MarqueePresenter.ComputeOverlayDipRects(new EsRect(0, 0, 100, 100), focus);

        // 注目矩形が交差しない（幅・高さ 0）ため、暗転の抜けが生じないよう全面を返す
        Assert.Single(result);
        Assert.Equal(new EsRect(0, 0, 100, 100), result[0]);
    }
}
