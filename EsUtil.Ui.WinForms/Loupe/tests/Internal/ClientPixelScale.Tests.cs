using EsUtil.Ui.WinForms.Loupe.Internal;
using Xunit;

namespace EsUtil.Ui.WinForms.Loupe.Tests.Internal;

/// <summary><see cref="ClientPixelScale"/>（描画面と表示領域の比から倍率を解決する）のテスト。</summary>
public class ClientPixelScaleTests
{
    /// <summary>表示領域が確定していれば実測の比を返すことを検証する。</summary>
    /// <param name="clientPixels">クライアント領域の一辺（物理ピクセル）。</param>
    /// <param name="clientDip">同じ辺の長さ（DIP）。</param>
    /// <param name="fallback">表示領域が未確定のときに使う係数。</param>
    /// <param name="expected">期待する係数。</param>
    [Theory]
    [InlineData(300, 240, 1.0, 1.25)]
    [InlineData(240, 240, 1.0, 1.0)]
    [InlineData(100, 200, 1.0, 0.5)]
    [InlineData(240, 240, 2.0, 1.0)]
    internal void Resolve_表示領域が確定していれば実測の比を返す(
        double clientPixels, double clientDip, double fallback, double expected)
    {
        Assert.Equal(expected, ClientPixelScale.Resolve(clientPixels, clientDip, fallback), 6);
    }

    /// <summary>表示領域が未確定（0 以下）の場合は基準値を使うことを検証する。</summary>
    /// <param name="clientPixels">クライアント領域の一辺（物理ピクセル）。</param>
    /// <param name="clientDip">同じ辺の長さ（DIP）。</param>
    /// <param name="fallback">表示領域が未確定のときに使う係数。</param>
    /// <param name="expected">期待する係数。</param>
    [Theory]
    [InlineData(0, 0, 1.25, 1.25)]
    [InlineData(100, 0, 2.0, 2.0)]
    [InlineData(100, -5, 3.0, 3.0)]
    [InlineData(0, 0, double.NaN, 1.0)]
    [InlineData(0, 0, 0.0, 1.0)]
    internal void Resolve_表示領域が未確定なら基準値を使う(
        double clientPixels, double clientDip, double fallback, double expected)
    {
        Assert.Equal(expected, ClientPixelScale.Resolve(clientPixels, clientDip, fallback), 6);
    }

    /// <summary>非数（NaN・無限大）の比を基準値へ寄せることを検証する。</summary>
    /// <param name="clientPixels">クライアント領域の一辺（物理ピクセル）。</param>
    /// <param name="clientDip">同じ辺の長さ（DIP）。</param>
    [Theory]
    [InlineData(double.NaN, 240)]
    [InlineData(double.PositiveInfinity, 240)]
    internal void Resolve_非数の比は基準値へ寄せる(double clientPixels, double clientDip)
    {
        Assert.Equal(1.0, ClientPixelScale.Resolve(clientPixels, clientDip, 1.0), 6);
    }
}
