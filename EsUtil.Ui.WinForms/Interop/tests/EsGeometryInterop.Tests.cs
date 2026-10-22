using Xunit;

namespace EsUtil.Ui.WinForms.Interop.Tests;

/// <summary><see cref="EsGeometryInterop"/> の変換テスト（UI 初期化不要・並列実行）。</summary>
/// <remarks>
/// GDI+ の幾何型は単精度であるため、往復で値が変わりうる。 ここでは「整数値」と「0.5 刻み」に限定し、<br/>
/// 往復で値が保たれること（単精度へ丸めても損失しない値）を検証する。
/// </remarks>
public sealed class EsGeometryInteropTests
{
    // ====================================================================
    // 座標
    // ====================================================================

    /// <summary>コアの座標が GDI+ の座標へ変換されることを検証する。</summary>
    [Fact]
    internal void ToPointF_コアの座標を写す()
    {
        var point = new EsPoint(12.5, -3.25).ToPointF();

        Assert.Equal(12.5f, point.X);
        Assert.Equal(-3.25f, point.Y);
    }

    /// <summary>整数の座標が丸めで 0.5 を 0 から遠い側へ丸めることを検証する。</summary>
    /// <param name="value">丸める値。</param>
    /// <param name="expected">期待する整数。</param>
    [Theory]
    [InlineData(1.4, 1)]
    [InlineData(1.6, 2)]
    [InlineData(-1.5, -2)]
    [InlineData(2.5, 3)]
    internal void ToPoint_0_5は0から遠い側へ丸める(double value, int expected)
        => Assert.Equal(expected, new EsPoint(value, value).ToPoint().X);

    /// <summary>画面物理ピクセルの座標が GDI+ の整数座標へ変換されることを検証する。</summary>
    [Fact]
    internal void ToPoint_画面物理の座標を写す()
    {
        var point = new EsPointInt(-1920, 1080).ToPoint();

        Assert.Equal(-1920, point.X);
        Assert.Equal(1080, point.Y);
    }

    /// <summary>GDI+ の座標がコアの座標へ戻ることを検証する。</summary>
    [Fact]
    internal void ToEsPoint_往復で値が保たれる()
    {
        var original = new EsPoint(10, -20);
        var roundTripped = original.ToPointF().ToEsPoint();

        Assert.Equal(original.X, roundTripped.X);
        Assert.Equal(original.Y, roundTripped.Y);
    }

    /// <summary>GDI+ の整数座標が画面物理ピクセルの座標へ変換されることを検証する。</summary>
    [Fact]
    internal void ToEsPointInt_整数の座標を写す()
    {
        var point = new Point(3, 4).ToEsPointInt();

        Assert.Equal(3, point.X);
        Assert.Equal(4, point.Y);
    }

    // ====================================================================
    // サイズ・ベクター
    // ====================================================================

    /// <summary>コアのサイズが GDI+ のサイズへ変換されることを検証する。</summary>
    [Fact]
    internal void ToSizeF_往復で値が保たれる()
    {
        var original = new EsSize(128.5, 64);
        var roundTripped = original.ToSizeF().ToEsSize();

        Assert.Equal(original.Width, roundTripped.Width);
        Assert.Equal(original.Height, roundTripped.Height);
    }

    /// <summary>コアのサイズが GDI+ の整数サイズへ丸められることを検証する。</summary>
    [Fact]
    internal void ToSize_四捨五入する()
        => Assert.Equal(new Size(129, 64), new EsSize(128.5, 64).ToSize());

    /// <summary>コアのベクターが GDI+ の座標へ変換して戻ることを検証する。</summary>
    [Fact]
    internal void ToPointF_ベクターを往復できる()
    {
        var original = new EsVector(-4, 8);
        var roundTripped = original.ToPointF().ToEsVector();

        Assert.Equal(original.X, roundTripped.X);
        Assert.Equal(original.Y, roundTripped.Y);
    }

    // ====================================================================
    // 矩形
    // ====================================================================

    /// <summary>コアの矩形が GDI+ の矩形へ変換して戻ることを検証する。</summary>
    [Fact]
    internal void ToRectangleF_往復で値が保たれる()
    {
        var original = new EsRect(1, 2, 30, 40);
        var roundTripped = original.ToRectangleF().ToEsRect();

        Assert.Equal(original.X, roundTripped.X);
        Assert.Equal(original.Y, roundTripped.Y);
        Assert.Equal(original.Width, roundTripped.Width);
        Assert.Equal(original.Height, roundTripped.Height);
    }

    /// <summary>画面物理ピクセルの矩形が GDI+ の矩形へそのまま写ることを検証する。</summary>
    [Fact]
    internal void ToRectangle_整数の矩形を写す()
    {
        var rect = new EsRectInt(-1920, -100, 3840, 1180).ToRectangle();

        Assert.Equal(-1920, rect.X);
        Assert.Equal(-100, rect.Y);
        Assert.Equal(3840, rect.Width);
        Assert.Equal(1180, rect.Height);
    }
}
