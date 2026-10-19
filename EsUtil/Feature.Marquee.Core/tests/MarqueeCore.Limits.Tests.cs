using Xunit;

namespace EsUtil.Feature.Marquee.Tests;

/// <summary><see cref="MarqueeCore.Limits"/> の純粋計算テスト（UI 非依存・並列実行）。</summary>
/// <remarks>
/// 数値パラメータの既定値と、未設定（0 以下）の解決規則のみを検証するため、UI 初期化に依存せず <c>[Fact]</c> で並列実行する。<br/>
/// UI の依存関係プロパティ経由の反映は UI 側のテスト（ヘッドレス直列）が担う。
/// </remarks>
public class MarqueeCoreLimitsTests
{
    // ====================================================================
    // ClientToSourceScaling（既定 1.0）
    // ====================================================================

    /// <summary>倍率の既定値が等倍であることを検証する。</summary>
    [Fact]
    internal void ClientToSourceScalingDefault_等倍である()
    {
        Assert.Equal(1.0, MarqueeCore.Limits.ClientToSourceScalingDefault);
    }

    // ====================================================================
    // BorderThickness（既定 2.0・最小 0.5）
    // ====================================================================

    /// <summary>枠線の太さの既定値が 2.0 であることを検証する。</summary>
    [Fact]
    internal void BorderThicknessDefault_2である()
    {
        Assert.Equal(2.0, MarqueeCore.Limits.BorderThicknessDefault);
    }

    /// <summary>枠線の太さの最小値が 0.5 であることを検証する。</summary>
    [Fact]
    internal void BorderThicknessMin_05である()
    {
        Assert.Equal(0.5, MarqueeCore.Limits.BorderThicknessMin);
    }

    /// <summary>枠線の太さが安全域へ補正されることを検証する。</summary>
    /// <param name="value">補正する太さ（DIP）。</param>
    /// <param name="expected">補正後の太さ（DIP）。</param>
    [Theory]
    [InlineData(0.1, 0.5)]
    [InlineData(0.0, 0.5)]
    [InlineData(-1.0, 0.5)]
    [InlineData(0.5, 0.5)]
    [InlineData(2.0, 2.0)]
    [InlineData(10.0, 10.0)]
    [InlineData(100.0, 64.0)]
    [InlineData(double.NaN, 2.0)]
    internal void CoerceBorderThickness_安全域へ補正する(double value, double expected)
    {
        Assert.Equal(expected, MarqueeCore.Limits.CoerceBorderThickness(value));
    }

    // ====================================================================
    // 各設定値の Coerce（非有限は既定値）
    // ====================================================================

    /// <summary>ハンドルの一辺サイズが安全域へ補正されることを検証する。</summary>
    /// <param name="value">補正する値（DIP）。</param>
    /// <param name="expected">補正後の値（DIP）。</param>
    [Theory]
    [InlineData(0.5, 1.0)]
    [InlineData(10.0, 10.0)]
    [InlineData(300.0, 256.0)]
    [InlineData(double.NaN, 10.0)]
    internal void CoerceHandleSize_安全域へ補正する(double value, double expected)
    {
        Assert.Equal(expected, MarqueeCore.Limits.CoerceHandleSize(value));
    }

    /// <summary>十字マーカーの全長が安全域へ補正されることを検証する。</summary>
    /// <param name="value">補正する値（DIP）。</param>
    /// <param name="expected">補正後の値（DIP）。</param>
    [Theory]
    [InlineData(1.0, 2.0)]
    [InlineData(16.0, 16.0)]
    [InlineData(5000.0, 4096.0)]
    [InlineData(double.PositiveInfinity, 16.0)]
    internal void CoerceMarkerLength_安全域へ補正する(double value, double expected)
    {
        Assert.Equal(expected, MarqueeCore.Limits.CoerceMarkerLength(value));
    }

    /// <summary>ハンドルのヒットテスト半径が安全域へ補正されることを検証する。</summary>
    /// <param name="value">補正する値（DIP）。</param>
    /// <param name="expected">補正後の値（DIP）。</param>
    [Theory]
    [InlineData(-1.0, 0.0)]
    [InlineData(0.0, 0.0)]
    [InlineData(12.0, 12.0)]
    [InlineData(300.0, 256.0)]
    [InlineData(double.NaN, 12.0)]
    internal void CoerceHandleHitRadius_安全域へ補正する(double value, double expected)
    {
        Assert.Equal(expected, MarqueeCore.Limits.CoerceHandleHitRadius(value));
    }

    /// <summary>選択矩形の最小サイズが安全域へ補正されることを検証する。</summary>
    /// <param name="value">補正する値（対象座標）。</param>
    /// <param name="expected">補正後の値（対象座標）。</param>
    /// <remarks>0 以下の指定は最小値（1.0）へ補正される（幅・高さが 0 の選択を残さない）。</remarks>
    [Theory]
    [InlineData(-1.0, 1.0)]
    [InlineData(0.0, 1.0)]
    [InlineData(0.5, 1.0)]
    [InlineData(6.0, 6.0)]
    [InlineData(5000.0, 4096.0)]
    [InlineData(double.NaN, 6.0)]
    internal void CoerceMinSelectionSize_安全域へ補正する(double value, double expected)
    {
        Assert.Equal(expected, MarqueeCore.Limits.CoerceMinSelectionSize(value));
    }

    /// <summary>ポート未指定時の倍率が安全域へ補正されることを検証する。</summary>
    /// <param name="value">補正する値。</param>
    /// <param name="expected">補正後の値。</param>
    [Theory]
    [InlineData(0.0, 0.1)]
    [InlineData(1.0, 1.0)]
    [InlineData(1.25, 1.25)]
    [InlineData(10.0, 8.0)]
    [InlineData(double.NaN, 1.0)]
    internal void CoerceClientToSourceScaling_安全域へ補正する(double value, double expected)
    {
        Assert.Equal(expected, MarqueeCore.Limits.CoerceClientToSourceScaling(value));
    }

    // ====================================================================
    // ResolveDisplayScale（0 以下は倍率を使う）
    // ====================================================================

    /// <summary>正の指定はそのまま使うことを検証する。</summary>
    /// <param name="displayScaling">指定する表示スケール。</param>
    [Theory]
    [InlineData(1.25)]
    [InlineData(2.0)]
    [InlineData(0.5)]
    internal void ResolveDisplayScale_正の指定はそのまま使う(double displayScaling)
    {
        Assert.Equal(displayScaling, MarqueeCore.Limits.ResolveDisplayScale(displayScaling, 1.0));
    }

    /// <summary>0 以下は未指定として倍率を使うことを検証する。</summary>
    /// <param name="displayScaling">指定する表示スケール。</param>
    [Theory]
    [InlineData(0.0)]
    [InlineData(-1.0)]
    internal void ResolveDisplayScale_0以下は倍率を使う(double displayScaling)
    {
        Assert.Equal(1.25, MarqueeCore.Limits.ResolveDisplayScale(displayScaling, 1.25));
    }

    /// <summary>0 以下で倍率も無効な場合は基準値（等倍）になることを検証する。</summary>
    /// <param name="dipToSourceScaling">指定する倍率。</param>
    [Theory]
    [InlineData(0.0)]
    [InlineData(-1.0)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    internal void ResolveDisplayScale_倍率も無効なら等倍になる(double dipToSourceScaling)
    {
        Assert.Equal(1.0, MarqueeCore.Limits.ResolveDisplayScale(displayScaling: 0.0, dipToSourceScaling));
    }
}
