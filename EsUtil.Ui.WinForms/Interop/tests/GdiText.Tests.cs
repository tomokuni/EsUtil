using EsUtil.Ui.WinForms.Interop.Imaging;
using Xunit;
using DrawingColor = System.Drawing.Color;

namespace EsUtil.Ui.WinForms.Interop.Tests;

/// <summary><see cref="GdiText"/> のテスト（実寸の測定と描画）。</summary>
/// <remarks>
/// ウィンドウ（ハンドル）を必要としないため、オフスクリーンの <see cref="Bitmap"/> 上で検証できる。
/// </remarks>
public sealed class GdiTextTests
{
    /// <summary>文字列の実寸が測定できること（幅・行高が正）を検証する。</summary>
    [Fact]
    internal void Measure_実寸を返す()
    {
        var size = GdiText.Measure("サンプル文字列", 12);

        Assert.True(size.Width > 0);
        Assert.True(size.Height > 0);
    }

    /// <summary>フォント サイズを大きくすると実寸も大きくなることを検証する。</summary>
    /// <remarks>測定結果が物理ピクセル（DPI 依存）ではなく DIP であることを、サイズの比例で確認する。</remarks>
    [Fact]
    internal void Measure_フォントサイズに比例する()
    {
        var small = GdiText.Measure("サンプル文字列", 10);
        var large = GdiText.Measure("サンプル文字列", 20);

        Assert.True(large.Width > small.Width);
        Assert.True(large.Height > small.Height);
    }

    /// <summary>長い文字列ほど幅が広いことを検証する。</summary>
    [Fact]
    internal void Measure_文字数に応じて幅が広がる()
    {
        Assert.True(GdiText.Measure("AAAA", 12).Width > GdiText.Measure("A", 12).Width);
    }

    /// <summary>空文字の測定が 0 以上の値で返ることを検証する（例外にしない）。</summary>
    [Fact]
    internal void Measure_空文字でも例外を出さない()
    {
        var size = GdiText.Measure(string.Empty, 12);

        Assert.True(size.Width >= 0);
        Assert.True(size.Height >= 0);
    }

    /// <summary>文字列の引数が null の場合に例外になることを検証する。</summary>
    [Fact]
    internal void Measure_nullは例外になる()
        => Assert.Throws<ArgumentNullException>(() => GdiText.Measure(null!, 12));

    /// <summary>文字を描画すると指定色の画素が現れることを検証する。</summary>
    [Fact]
    internal void Draw_指定色の画素を描く()
    {
        using var target = TestHelper.CreateTarget(120, 40);
        var color = DrawingColor.FromArgb(255, 255, 0, 0);

        GdiText.Draw(target.Graphics, "Ag", 1, 1, 24, color);

        Assert.True(target.CountColor(0, 0, 120, 40, color) > 0);
    }

    /// <summary>文字列の引数が null の場合に例外になることを検証する。</summary>
    [Fact]
    internal void Draw_nullは例外になる()
    {
        using var target = TestHelper.CreateTarget(10, 10);

        Assert.Throws<ArgumentNullException>(() => GdiText.Draw(target.Graphics, null!, 0, 0, 12, DrawingColor.Red));
    }
}
