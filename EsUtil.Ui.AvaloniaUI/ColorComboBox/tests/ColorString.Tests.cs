using Avalonia.Media;
using Xunit;

namespace EsUtil.Ui.AvaloniaUI.ColorComboBox.Tests;

/// <summary><see cref="ColorString"/>（Avalonia の色型による入口）のテスト（UI 初期化を要しない・並列実行）。</summary>
/// <remarks>
/// 解析・整形・正規化の規則はコア（<c>EsUtil.Platform.Core.Palette.ColorString</c>）が単一所有するため、
/// 本クラスは <b><see cref="Color"/> との変換が正しく行われること</b>を固定する
/// （受理する入力の網羅と色名の表はコアのテストが持つ）。
/// </remarks>
public class ColorStringTests
{
    [Theory]
    [InlineData("Red")]
    [InlineData("red")]
    [InlineData("Transparent")]
    internal void IsDefinedColorName_定義済み色名を受理する(string name)
        => Assert.True(ColorString.IsDefinedColorName(name));

    [Theory]
    [InlineData("NotAColor")]
    [InlineData("")]
    internal void IsDefinedColorName_未定義は拒否する(string name)
        => Assert.False(ColorString.IsDefinedColorName(name));

    [Theory]
    [InlineData("Red", 255, 255, 0, 0, 1.0)]
    [InlineData("#336699", 255, 51, 102, 153, 1.0)]
    [InlineData("#80336699", 128, 51, 102, 153, 1.0)]
    [InlineData("Red+50%", 128, 255, 0, 0, 0.5)]
    internal void TryParse_色と不透明度をAvaloniaの色型で返す(string text, byte a, byte r, byte g, byte b, double opacity)
    {
        Assert.True(ColorString.TryParse(text, out var color, out var parsedOpacity));
        Assert.Equal(Color.FromArgb(a, r, g, b), color);
        Assert.Equal(opacity, parsedOpacity, 6);
    }

    [Theory]
    [InlineData("")]
    [InlineData("NotAColor")]
    [InlineData("#GGGGGG")]
    [InlineData("Red+101%")]
    internal void TryParse_無効な入力はfalseを返す(string text)
        => Assert.False(ColorString.TryParse(text, out _, out _));

    [Fact]
    internal void TryParse_nullはfalseを返す()
        => Assert.False(ColorString.TryParse(null, out _, out _));

    [Fact]
    internal void Format_色名と不透明度を整形する()
    {
        Assert.Equal("Red", ColorString.Format(Color.FromArgb(255, 255, 0, 0), 1.0));
        Assert.Equal("Red+50%", ColorString.Format(Color.FromArgb(255, 255, 0, 0), 0.5));
    }

    [Fact]
    internal void Format_アルファは無視してRGBで色名を判定する()
        => Assert.Equal("Red", ColorString.Format(Color.FromArgb(128, 255, 0, 0), 1.0));

    [Fact]
    internal void Format_色名の無い色は16進で返す()
        => Assert.Equal("#010203", ColorString.Format(Color.FromArgb(255, 1, 2, 3), 1.0));

    [Theory]
    [InlineData("#80FF0000", "Red+50%")]
    [InlineData("Red", "Red")]
    [InlineData("Red+50%", "Red+50%")]
    internal void ToDisplayText_保存値を画面表示へ変換する(string text, string expected)
        => Assert.Equal(expected, ColorString.ToDisplayText(text));

    [Fact]
    internal void ToDisplayText_nullや空文字はそのまま返す()
    {
        Assert.Equal(string.Empty, ColorString.ToDisplayText(null));
        Assert.Equal(string.Empty, ColorString.ToDisplayText(""));
    }

    [Theory]
    [InlineData("Red", "#FFFF0000")]
    [InlineData("Red+50%", "#80FF0000")]
    [InlineData("#336699", "#FF336699")]
    internal void ToArgbHex_保存値の形へ正規化する(string text, string expected)
        => Assert.Equal(expected, ColorString.ToArgbHex(text));

    [Fact]
    internal void ToArgbHex_色オブジェクトはアルファ先頭の8桁に変換する()
        => Assert.Equal("#80FF0000", ColorString.ToArgbHex(Color.FromArgb(128, 255, 0, 0)));

    [Fact]
    internal void ToArgbHex_解析できない値はそのまま返す()
        => Assert.Equal("NotAColor", ColorString.ToArgbHex("NotAColor"));
}