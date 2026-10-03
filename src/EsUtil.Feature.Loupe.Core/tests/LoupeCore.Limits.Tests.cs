using Xunit;

namespace EsUtil.Feature.Loupe.Tests;

/// <summary><see cref="LoupeCore.Limits"/> の純粋計算テスト（UI 非依存・並列実行）。</summary>
/// <remarks>
/// 数値パラメータの安全域への補正（clamp）のみを検証するため、UI 初期化に依存せず <c>[Fact]</c> で並列実行する。<br/>
/// UI の <c>StyledProperty</c> coerce 経由の反映は UI 側のテスト（ヘッドレス直列）が担う。
/// </remarks>
public class LoupeCoreLimitsTests
{
    // ====================================================================
    // RequestedSize（1〜4096、既定 160）
    // ====================================================================

    [Fact]
    public void CoerceRequestedSize_範囲内はそのまま返す()
    {
        Assert.Equal(160, LoupeCore.Limits.CoerceRequestedSize(160));
    }

    [Fact]
    public void CoerceRequestedSize_下限未満は1に補正する()
    {
        Assert.Equal(1, LoupeCore.Limits.CoerceRequestedSize(0));
        Assert.Equal(1, LoupeCore.Limits.CoerceRequestedSize(-100));
    }

    [Fact]
    public void CoerceRequestedSize_上限超過は4096に補正する()
    {
        Assert.Equal(4096, LoupeCore.Limits.CoerceRequestedSize(5000));
    }

    // ====================================================================
    // ZoomLevel（1〜64、既定 4）
    // ====================================================================

    [Fact]
    public void CoerceZoomLevel_範囲内はそのまま返す()
    {
        Assert.Equal(4, LoupeCore.Limits.CoerceZoomLevel(4));
    }

    [Fact]
    public void CoerceZoomLevel_下限未満は1に補正する()
    {
        Assert.Equal(1, LoupeCore.Limits.CoerceZoomLevel(0));
    }

    [Fact]
    public void CoerceZoomLevel_上限超過は64に補正する()
    {
        Assert.Equal(64, LoupeCore.Limits.CoerceZoomLevel(100));
    }

    // ====================================================================
    // ClientToPixelScaling（0.1〜8.0、既定 1.0）
    // ====================================================================

    [Fact]
    public void CoerceClientToPixelScaling_範囲内はそのまま返す()
    {
        Assert.Equal(2.0, LoupeCore.Limits.CoerceClientToPixelScaling(2.0));
    }

    [Fact]
    public void CoerceClientToPixelScaling_範囲外は最小値と最大値に補正する()
    {
        Assert.Equal(0.1, LoupeCore.Limits.CoerceClientToPixelScaling(0.0));
        Assert.Equal(8.0, LoupeCore.Limits.CoerceClientToPixelScaling(10.0));
    }

    [Fact]
    public void CoerceClientToPixelScaling_非有限値は既定値に補正する()
    {
        Assert.Equal(1.0, LoupeCore.Limits.CoerceClientToPixelScaling(double.NaN));
        Assert.Equal(1.0, LoupeCore.Limits.CoerceClientToPixelScaling(double.PositiveInfinity));
    }

    // ====================================================================
    // BorderThickness（1〜64、既定 2）
    // ====================================================================

    [Fact]
    public void CoerceBorderThickness_範囲内はそのまま返す()
    {
        Assert.Equal(2, LoupeCore.Limits.CoerceBorderThickness(2));
    }

    [Fact]
    public void CoerceBorderThickness_範囲外は最小値と最大値に補正する()
    {
        Assert.Equal(1, LoupeCore.Limits.CoerceBorderThickness(0));
        Assert.Equal(64, LoupeCore.Limits.CoerceBorderThickness(100));
    }
}
