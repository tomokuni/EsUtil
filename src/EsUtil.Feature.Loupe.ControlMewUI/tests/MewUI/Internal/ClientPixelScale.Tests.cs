using EsUtil.Feature.Loupe.MewUI.Internal;
using EsUtil.Platform.Core.Coord;
using Xunit;

namespace EsUtil.Feature.Loupe.MewUI.Tests.Internal;

/// <summary><see cref="ClientPixelScale"/> のテスト（描画面と表示領域から実効倍率を求める）。</summary>
/// <remarks>
/// DPI の通知は追加直後のコントロールへ届かないことがあるため、描画面（物理ピクセル）と<br/>
/// 表示領域（DIP）の比から倍率を求める。 ここが誤ると図形が 100% で描かれ、画像と重ねたときにずれる。
/// </remarks>
public sealed class ClientPixelScaleTests
{
    // ====================================================================
    // 描画面の実測からの解決
    // ====================================================================

    /// <summary>描画面と表示領域の比が倍率になることを検証する。</summary>
    /// <param name="clientPixels">クライアント領域の長さ（物理ピクセル）。</param>
    /// <param name="clientDip">同じ辺の長さ（DIP）。</param>
    /// <param name="expected">期待する倍率。</param>
    [Theory]
    [InlineData(968, 774.4, 1.25)]
    [InlineData(774.4, 774.4, 1.0)]
    [InlineData(2, 1, 2.0)]
    [InlineData(48, 96, 0.5)]
    internal void Resolve_描画面と表示領域の比を返す(double clientPixels, double clientDip, double expected)
        => Assert.Equal(expected, ClientPixelScale.Resolve(clientPixels, clientDip, fallback: 1.0), 3);

    /// <summary>表示領域が未確定（0 以下）の場合は保持値（DPI から与えられる係数）を使うことを検証する。</summary>
    /// <param name="clientDip">同じ辺の長さ（DIP）。</param>
    [Theory]
    [InlineData(0.0)]
    [InlineData(-1.0)]
    [InlineData(double.NaN)]
    internal void Resolve_表示領域が未確定なら保持値を使う(double clientDip)
        => Assert.Equal(1.25, ClientPixelScale.Resolve(968, clientDip, fallback: 1.25), 3);

    /// <summary>非数・範囲外の値が基準値へ補正されることを検証する。</summary>
    /// <remarks>
    /// 補正の規則は <see cref="DpiScaler.NormalizeScaling"/> が単一所有する（描画面の値も同じ規則で扱う）。<br/>
    /// 表示領域が確定している場合はその比を補正して使う（保持値へは戻らない）。
    /// </remarks>
    [Fact]
    internal void Resolve_異常値は基準値へ補正される()
    {
        // 描画面が非数（表面が未生成）の場合は補正の基準値（1.0）になる
        Assert.Equal(1.0, ClientPixelScale.Resolve(double.NaN, 100, fallback: 1.25), 3);
        // 倍率が範囲外（∞）の場合も基準値になる
        Assert.Equal(1.0, ClientPixelScale.Resolve(double.PositiveInfinity, 100, fallback: 1.25), 3);
        // 表示領域が確定していれば、保持値が異常でも実測の比（1.0）を使う
        Assert.Equal(1.0, ClientPixelScale.Resolve(100, 100, fallback: 0.0), 3);
    }
}
