using EsUtil.Feature.Loupe.WinUI3.Internal;
using Xunit;

namespace EsUtil.Feature.Loupe.WinUI3.Tests.Internal;

/// <summary><see cref="ClientPixelScale"/>（描画面と表示領域から実効倍率を解決する）のテスト。</summary>
/// <remarks>
/// 実測比から求めた倍率は、DPI の変更通知が届かない状況（追加直後のコントロール）でも<br/>
/// 描画と拡大画像の倍率を一致させる唯一の手掛かりであるため、境界（0 以下・非数）を含めて検証する。
/// </remarks>
public class ClientPixelScaleTests
{
    /// <summary>表示領域が確定していれば実測比（描画面 ÷ DIP）を返すことを検証する。</summary>
    [Fact]
    internal void Resolve_表示領域が確定していれば実測比を返す()
    {
        // 125% の表示倍率（240 物理ピクセル / 192 DIP）
        Assert.Equal(1.25, ClientPixelScale.Resolve(240, 192, 1.0));
    }

    /// <summary>表示領域が未確定（0 以下）ならフォールバックを使うことを検証する。</summary>
    /// <param name="clientDip">表示領域の一辺（DIP）。 0 以下は未確定として扱う。</param>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    internal void Resolve_表示領域が未確定ならフォールバックを使う(double clientDip)
    {
        Assert.Equal(2.5, ClientPixelScale.Resolve(240, clientDip, 2.5));
    }

    /// <summary>フォールバックが無効な値なら基準値（1.0）になることを検証する。</summary>
    /// <param name="fallback">フォールバックの値。</param>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    internal void Resolve_フォールバックが無効なら基準値になる(double fallback)
    {
        Assert.Equal(1.0, ClientPixelScale.Resolve(0, 0, fallback));
    }

    /// <summary>実測比をそのまま返す（安全域への補正は呼び出し側が行う）ことを検証する。</summary>
    [Fact]
    internal void Resolve_実測比をそのまま返す()
    {
        // 200%（100 DIP が 200 物理ピクセル）
        Assert.Equal(2.0, ClientPixelScale.Resolve(200, 100, 1.0));
        // 50%（100 DIP が 50 物理ピクセル）
        Assert.Equal(0.5, ClientPixelScale.Resolve(50, 100, 1.0));
    }
}
