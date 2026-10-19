using EsUtil.Ui.WinUI3.Marquee.Internal;
using Xunit;

namespace EsUtil.Ui.WinUI3.Marquee.Tests.Internal;

/// <summary><see cref="ClientPixelScale"/> のテスト（UI 非依存・並列実行）。</summary>
/// <remarks>
/// 描画の倍率は「描画面の実寸 ÷ 割り当て領域」と「XAML のルートが報告する表示倍率」の 2 系統から決まる。<br/>
/// 前者が確定していれば常に実測を優先し、未確定のときだけ後者へフォールバックする境界を検証する。
/// </remarks>
public class ClientPixelScaleTests
{
    /// <summary>割り当て領域が確定している場合は実測値（描画面 ÷ 割り当て領域）を返すことを検証する。</summary>
    /// <param name="surfacePixels">描画面の一辺（物理ピクセル）。</param>
    /// <param name="clientDip">割り当て領域の同じ辺（DIP）。</param>
    /// <param name="fallback">未確定のときに使う係数。</param>
    /// <param name="expected">期待する倍率。</param>
    [Theory]
    [InlineData(300, 200, 1.0, 1.5)]
    [InlineData(200, 200, 3.0, 1.0)]
    [InlineData(100, 200, 1.0, 0.5)]
    [InlineData(250, 200, 1.0, 1.25)]
    internal void Resolve_割り当て領域が確定していれば実測値を返す(
        double surfacePixels, double clientDip, double fallback, double expected)
        => Assert.Equal(expected, ClientPixelScale.Resolve(surfacePixels, clientDip, fallback));

    /// <summary>割り当て領域が未確定の場合は与えられた係数を使うことを検証する。</summary>
    /// <param name="surfacePixels">描画面の一辺（物理ピクセル）。</param>
    /// <param name="clientDip">割り当て領域の同じ辺（DIP。 0 以下は未確定）。</param>
    /// <param name="fallback">未確定のときに使う係数。</param>
    /// <param name="expected">期待する倍率。</param>
    [Theory]
    [InlineData(0, 0, 1.25, 1.25)]
    [InlineData(0, 0, 2.0, 2.0)]
    [InlineData(200, 0, 1.5, 1.5)]
    [InlineData(200, -1, 1.25, 1.25)]
    internal void Resolve_割り当て領域が未確定なら与えられた係数を使う(
        double surfacePixels, double clientDip, double fallback, double expected)
        => Assert.Equal(expected, ClientPixelScale.Resolve(surfacePixels, clientDip, fallback));

    /// <summary>両方が無効な場合は基準値（1.0）へ寄せることを検証する。</summary>
    /// <param name="fallback">未確定のときに使う係数（無効値）。</param>
    [Theory]
    [InlineData(0.0)]
    [InlineData(-1.0)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    internal void Resolve_両方が無効なら基準値を使う(double fallback)
        => Assert.Equal(1.0, ClientPixelScale.Resolve(0, 0, fallback));

    /// <summary>描画面が負（未確定）の場合は基準値へ寄せることを検証する。</summary>
    [Fact]
    internal void Resolve_描画面が負なら基準値を使う() => Assert.Equal(1.0, ClientPixelScale.Resolve(-200, 200, 1.0));
}
