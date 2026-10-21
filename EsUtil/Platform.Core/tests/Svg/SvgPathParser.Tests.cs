using EsUtil.Platform.Core.Svg;
using Xunit;

namespace EsUtil.Platform.Core.Tests.Svg;

/// <summary><see cref="SvgPathParser"/> の単体テスト（SVG パス データの解析）。</summary>
/// <remarks>
/// アイコンは <c>M</c> / <c>L</c> / <c>H</c> / <c>V</c> / <c>C</c> / <c>Z</c> だけで構成されるため、<br/>
/// その範囲の解釈（絶対・相対・暗黙の繰り返し・未対応コマンドの扱い）を固定する。
/// </remarks>
public class SvgPathParserTests
{
    /// <summary>絶対コマンドの図形が読めることを検証する。</summary>
    [Fact]
    internal void Parse_絶対コマンドの図形を読む()
    {
        var figures = SvgPathParser.Parse("M1 2 L3 4 H5 V6 C7 8 9 10 11 12 Z");

        var figure = Assert.Single(figures);
        Assert.Equal(1, figure.StartX);
        Assert.Equal(2, figure.StartY);
        Assert.True(figure.IsClosed);

        // L / H / V / C / Z の順に 4 つのセグメントが並ぶ
        Assert.Equal(4, figure.Segments.Count);
        Assert.False(figure.Segments[0].IsCubic);
        Assert.Equal(3, figure.Segments[0].X);
        Assert.Equal(4, figure.Segments[0].Y);

        Assert.False(figure.Segments[1].IsCubic);
        Assert.Equal(5, figure.Segments[1].X);
        Assert.Equal(4, figure.Segments[1].Y);

        Assert.False(figure.Segments[2].IsCubic);
        Assert.Equal(5, figure.Segments[2].X);
        Assert.Equal(6, figure.Segments[2].Y);

        Assert.True(figure.Segments[3].IsCubic);
        Assert.Equal(11, figure.Segments[3].X);
        Assert.Equal(12, figure.Segments[3].Y);
    }

    /// <summary>相対コマンドが直前の位置からの移動として読めることを検証する。</summary>
    [Fact]
    internal void Parse_相対コマンドを直前の位置からの移動として読む()
    {
        var figures = SvgPathParser.Parse("M10 10 l5 0 v5 h-5 z");

        var figure = Assert.Single(figures);
        Assert.Equal(10, figure.StartX);
        Assert.Equal(10, figure.StartY);
        Assert.Equal(15, figure.Segments[0].X);
        Assert.Equal(10, figure.Segments[0].Y);
        Assert.Equal(15, figure.Segments[1].X);
        Assert.Equal(15, figure.Segments[1].Y);
        Assert.Equal(10, figure.Segments[2].X);
        Assert.Equal(15, figure.Segments[2].Y);
        Assert.True(figure.IsClosed);
    }

    /// <summary>M の後に続く座標が線分として読めることを検証する。</summary>
    [Fact]
    internal void Parse_移動の後の座標を線分として読む()
    {
        var figures = SvgPathParser.Parse("M0 0 10 0 10 10");

        var figure = Assert.Single(figures);
        Assert.Equal(2, figure.Segments.Count);
        Assert.Equal(10, figure.Segments[1].X);
        Assert.Equal(10, figure.Segments[1].Y);
        Assert.False(figure.IsClosed);
    }

    /// <summary>複数の図形が別々に読めることを検証する。</summary>
    [Fact]
    internal void Parse_複数の図形を分けて読む()
    {
        var figures = SvgPathParser.Parse("M0 0 L1 1 Z M5 5 L6 6 Z");

        Assert.Equal(2, figures.Count);
        Assert.Equal(5, figures[1].StartX);
        Assert.Equal(6, figures[1].Segments[0].X);
    }

    /// <summary>未対応のコマンドが空の結果になることを検証する。</summary>
    [Fact]
    internal void Parse_未対応のコマンドは空を返す()
    {
        // 円弧（A）はアイコンに現れないため未対応である（誤った形を描くより描かない）
        Assert.Empty(SvgPathParser.Parse("M0 0 A5 5 0 0 1 10 10 Z"));
    }

    /// <summary>空の入力が空の結果になることを検証する。</summary>
    /// <param name="data">入力。</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    internal void Parse_空の入力は空を返す(string? data) => Assert.Empty(SvgPathParser.Parse(data));

    /// <summary>先頭の M より前の描画コマンドを拒否することを検証する。</summary>
    [Fact]
    internal void Parse_移動より前の描画コマンドは空を返す() => Assert.Empty(SvgPathParser.Parse("L1 1"));
}
