using EsUtil.Platform.Core.Palette;
using Xunit;
using DrawingColor = System.Drawing.Color;

namespace EsUtil.Platform.Core.Tests.Palette;

/// <summary><see cref="ColorString"/> の純粋計算テスト（UI 非依存・並列実行）。</summary>
/// <remarks>
/// 保存値（<c>#AARRGGBB</c>）と画面表示（<c>{色}+{不透明度%}</c>）の 2 形式と、受理する入力を固定する。
/// </remarks>
public class ColorStringTests
{
    [Theory]
    [InlineData("Red", 255, 255, 0, 0)]
    [InlineData("red", 255, 255, 0, 0)]
    [InlineData("#FF0000", 255, 255, 0, 0)]
    [InlineData("#80FF0000", 128, 255, 0, 0)]
    [InlineData("80FF0000", 128, 255, 0, 0)]
    internal void TryParse_色名と16進を受け付ける(string text, byte a, byte r, byte g, byte b)
    {
        Assert.True(ColorString.TryParse(text, out var color, out var opacity));
        Assert.Equal(DrawingColor.FromArgb(a, r, g, b), color);
        Assert.Equal(1.0, opacity);
    }

    [Theory]
    [InlineData("Red+50%", 128, 0.5)]
    [InlineData("#336699+75%", 191, 0.75)]
    [InlineData("Red+0%", 0, 0.0)]
    [InlineData("Red+100%", 255, 1.0)]
    internal void TryParse_不透明度をアルファへ合成する(string text, byte expectedAlpha, double expectedOpacity)
    {
        Assert.True(ColorString.TryParse(text, out var color, out var opacity));
        Assert.Equal(expectedAlpha, color.A);
        Assert.Equal(expectedOpacity, opacity, 6);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("NotAColor")]
    [InlineData("#GGGGGG")]
    [InlineData("Red+101%")]
    [InlineData("Red+-1%")]
    [InlineData("Red+50")]
    [InlineData("Red+50%+50%")]
    [InlineData("#FFF")]
    [InlineData("#FFFF")]
    internal void TryParse_受理しない入力を拒否する(string text)
        => Assert.False(ColorString.TryParse(text, out _, out _));

    [Fact]
    internal void TryParse_nullは拒否する()
        => Assert.False(ColorString.TryParse(null, out _, out _));

    [Fact]
    internal void Format_色名と不透明度を整形する()
    {
        Assert.Equal("Red", ColorString.Format(DrawingColor.FromArgb(255, 255, 0, 0), 1.0));
        Assert.Equal("Red+50%", ColorString.Format(DrawingColor.FromArgb(255, 255, 0, 0), 0.5));
    }

    [Fact]
    internal void Format_色名の無い色は16進で表す()
        => Assert.Equal("#010203+75%", ColorString.Format(DrawingColor.FromArgb(255, 1, 2, 3), 0.75));

    [Theory]
    [InlineData("#80FF0000", "Red+50%")]
    [InlineData("Red", "Red")]
    [InlineData("Red+50%", "Red+50%")]
    [InlineData("#336699+75%", "#336699+75%")]
    internal void ToDisplayText_保存値を画面表示へ変換する(string text, string expected)
        => Assert.Equal(expected, ColorString.ToDisplayText(text));

    [Fact]
    internal void ToDisplayText_解析できない場合は入力をそのまま返す()
        => Assert.Equal("NotAColor", ColorString.ToDisplayText("NotAColor"));

    [Fact]
    internal void ToDisplayText_nullは空文字を返す()
        => Assert.Equal(string.Empty, ColorString.ToDisplayText(null));

    [Theory]
    [InlineData("Red", ColorTextFormat.NameWithAlpha, "Red")]
    [InlineData("#80FF0000", ColorTextFormat.NameWithAlpha, "Red+50%")]
    [InlineData("#80FF0000", ColorTextFormat.HexCode, "#80FF0000")]
    [InlineData("#336699+75%", ColorTextFormat.HexCode, "#BF336699")]
    internal void ToText_指定の表記へ変換する(string text, ColorTextFormat format, string expected)
        => Assert.Equal(expected, ColorString.ToText(text, format));

    [Fact]
    internal void ToText_解析できない場合は入力をそのまま返す()
        => Assert.Equal("NotAColor", ColorString.ToText("NotAColor", ColorTextFormat.HexCode));

    [Fact]
    internal void ToText_nullは空文字を返す()
        => Assert.Equal(string.Empty, ColorString.ToText(null, ColorTextFormat.HexCode));

    [Theory]
    [InlineData("Red", "#FFFF0000")]
    [InlineData("Red+50%", "#80FF0000")]
    [InlineData("#FF0000", "#FFFF0000")]
    [InlineData("#80FF0000", "#80FF0000")]
    internal void ToArgbHex_保存値の形へ正規化する(string text, string expected)
        => Assert.Equal(expected, ColorString.ToArgbHex(text));

    [Fact]
    internal void ToArgbHex_解析できない場合は入力をそのまま返す()
        => Assert.Equal("NotAColor", ColorString.ToArgbHex("NotAColor"));

    [Fact]
    internal void ToArgbHex_色をアルファ先頭の16進へ変換する()
        => Assert.Equal("#80FF0000", ColorString.ToArgbHex(DrawingColor.FromArgb(128, 255, 0, 0)));

    [Fact]
    internal void IsDefinedColorName_色名の定義を調べる()
    {
        Assert.True(ColorString.IsDefinedColorName("Red"));
        Assert.True(ColorString.IsDefinedColorName("red"));
        Assert.False(ColorString.IsDefinedColorName("NotAColor"));
        Assert.False(ColorString.IsDefinedColorName(null));
    }

    [Fact]
    internal void 同じ文字列を繰り返し解析しても結果が変わらない()
    {
        // 容量制限付きキャッシュを使うため、キャッシュの入れ替えをまたいでも同じ結果になることを固定する
        for (var i = 0; i < 2000; i++)
        {
            Assert.True(ColorString.TryParse("Red+50%", out var color, out var opacity));
            Assert.Equal(128, color.A);
            Assert.Equal(0.5, opacity, 3);
        }
    }
}
