using EsUtil.Feature.Marquee.WinForms.Internal;
using Xunit;

namespace EsUtil.Feature.Marquee.WinForms.Tests.Internal;

/// <summary><see cref="GraphicsText"/> のテキスト測定と描画のテスト。</summary>
/// <remarks>
/// 実寸の測定は描画に使う API で行う必要がある（小さいと背景から文字がはみ出して切れる）。<br/>
/// 測定は基準 DPI の専用描画面で行うため、描画先の DPI に影響されない。
/// </remarks>
public sealed class GraphicsTextTests
{
    // ====================================================================
    // 測定
    // ====================================================================

    /// <summary>空でないテキストの実寸が正の値になることを検証する。</summary>
    [Fact]
    internal void Measure_実寸は正の値になる()
    {
        var size = GraphicsText.Measure("800 x 600", 12.0);

        Assert.True(size.Width > 0);
        Assert.True(size.Height > 0);
    }

    /// <summary>文字数が多いほど幅が広くなることを検証する。</summary>
    [Fact]
    internal void Measure_文字数に比例して幅が広くなる()
        => Assert.True(GraphicsText.Measure("MMMM", 12.0).Width > GraphicsText.Measure("MM", 12.0).Width);

    /// <summary>フォント サイズが大きいほど実寸が大きくなることを検証する。</summary>
    [Fact]
    internal void Measure_フォントサイズに比例して大きくなる()
    {
        var small = GraphicsText.Measure("X: 100", 12.0);
        var large = GraphicsText.Measure("X: 100", 24.0);

        Assert.True(large.Width > small.Width);
        Assert.True(large.Height > small.Height);
    }

    /// <summary>同じ入力の測定が繰り返し同じ値になることを検証する。</summary>
    /// <remarks>フォントの使い回し（サイズごとの作り直し）が測定値を変えないことを固定する。</remarks>
    [Fact]
    internal void Measure_同じ入力では同じ値になる()
    {
        var first = GraphicsText.Measure("800 x 600", 12.0);

        // 別のサイズを挟む（大きさごとにフォントを作り直させる）
        _ = GraphicsText.Measure("1920 x 1080", 20.0);

        Assert.Equal(first, GraphicsText.Measure("800 x 600", 12.0));
    }

    /// <summary>null のテキストが引数例外になることを検証する。</summary>
    [Fact]
    internal void Measure_nullはArgumentNullExceptionになる()
        => Assert.Throws<ArgumentNullException>(() => GraphicsText.Measure(null!, 12.0));

    // ====================================================================
    // 描画
    // ====================================================================

    /// <summary>指定位置へ文字が描かれることを検証する。</summary>
    [Fact]
    internal void Draw_指定位置へ文字を描く()
    {
        using var bitmap = TestHelper.CreateCanvas(80, 40);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            GraphicsText.Draw(graphics, "MMMM", 5, 5, 16.0, Color.Black);
        }

        var inked = 0;
        for (var y = 0; y < 40; y++)
        {
            for (var x = 0; x < 80; x++)
            {
                if (TestHelper.Pixel(bitmap, x, y).ToArgb() != Color.White.ToArgb())
                {
                    inked++;
                }
            }
        }

        Assert.True(inked > 0);
    }

    /// <summary>文字色が反映されることを検証する。</summary>
    [Fact]
    internal void Draw_文字色を反映する()
    {
        using var bitmap = TestHelper.CreateCanvas(80, 40);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            GraphicsText.Draw(graphics, "MMMM", 5, 5, 20.0, Color.Red);
        }

        var hasRed = false;
        for (var y = 0; y < 40 && !hasRed; y++)
        {
            for (var x = 0; x < 80; x++)
            {
                var pixel = TestHelper.Pixel(bitmap, x, y);
                if (pixel.R > 150 && pixel.G < 120 && pixel.B < 120)
                {
                    hasRed = true;
                    break;
                }
            }
        }

        Assert.True(hasRed);
    }

    /// <summary>文字が描かれる範囲が測定した実寸の内側に収まることを検証する。</summary>
    /// <remarks>測定と描画が同じ書式を使うため、測定値で作った背景から文字がはみ出さない。</remarks>
    [Fact]
    internal void Draw_文字は測定した実寸の内側に収まる()
    {
        const string text = "1920 x 1080";
        const double fontSize = 14.0;

        var measured = GraphicsText.Measure(text, fontSize);
        using var bitmap = TestHelper.CreateCanvas(200, 60);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            GraphicsText.Draw(graphics, text, 10, 10, fontSize, Color.Black);
        }

        var maxX = 0;
        var maxY = 0;
        for (var y = 0; y < 60; y++)
        {
            for (var x = 0; x < 200; x++)
            {
                if (TestHelper.Pixel(bitmap, x, y).ToArgb() != Color.White.ToArgb())
                {
                    maxX = Math.Max(maxX, x);
                    maxY = Math.Max(maxY, y);
                }
            }
        }

        // 描き始め（10, 10）から実寸 + 1 画素の範囲に収まる（GDI+ の丸め分の余裕）
        Assert.InRange(maxX, 10, 10 + (int)Math.Ceiling(measured.Width) + 1);
        Assert.InRange(maxY, 10, 10 + (int)Math.Ceiling(measured.Height) + 1);
    }

    /// <summary>null の引数が引数例外になることを検証する。</summary>
    [Fact]
    internal void Draw_nullはArgumentNullExceptionになる()
    {
        using var bitmap = TestHelper.CreateCanvas(10, 10);
        using var graphics = Graphics.FromImage(bitmap);

        Assert.Throws<ArgumentNullException>(() => GraphicsText.Draw(null!, "A", 0, 0, 12.0, Color.Black));
        Assert.Throws<ArgumentNullException>(() => GraphicsText.Draw(graphics, null!, 0, 0, 12.0, Color.Black));
    }
}
