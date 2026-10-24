using EsUtil.Platform.Core.Palette;
using Xunit;
using DrawingColor = System.Drawing.Color;

namespace EsUtil.Platform.Core.Tests.Palette;

/// <summary><see cref="ColorNames"/> の純粋計算テスト（UI 非依存・並列実行）。</summary>
/// <remarks>
/// 色名の出所が Windows 標準（<c>System.Drawing.KnownColor</c>）であること、システム色を除くこと、
/// 色名 ⇔ 色の相互参照が往復することを固定する。
/// </remarks>
public class ColorNamesTests
{
    [Fact]
    internal void 色名は100件以上ある()
        => Assert.True(ColorNames.ByName.Count >= 100, $"色名が少なすぎます: {ColorNames.ByName.Count}");

    [Fact]
    internal void 色名は大文字小文字を無視して解決できる()
    {
        Assert.True(ColorNames.TryGet("Red", out var upper));
        Assert.True(ColorNames.TryGet("red", out var lower));
        Assert.True(ColorNames.TryGet("RED", out var allUpper));
        Assert.Equal(upper, lower);
        Assert.Equal(upper, allUpper);
    }

    [Fact]
    internal void 未定義の色名は解決できない()
    {
        Assert.False(ColorNames.TryGet("NotAColor", out var color));
        Assert.Equal(DrawingColor.Empty, color);
        Assert.False(ColorNames.TryGet(null, out _));
    }

    [Fact]
    internal void システム色は含めない()
    {
        // システム色（ActiveBorder 等）は画面の配色に追随するため、選ばせる色としては扱わない
        Assert.False(ColorNames.IsDefined("ActiveBorder"));
        Assert.False(ColorNames.IsDefined("Window"));
    }

    [Fact]
    internal void 色名から色を引ける()
    {
        Assert.True(ColorNames.TryGet("Red", out var red));
        Assert.Equal(DrawingColor.FromArgb(255, 255, 0, 0), red);
    }

    [Fact]
    internal void 色から色名を引ける()
        => Assert.Equal("Red", ColorNames.FindName(DrawingColor.FromArgb(255, 255, 0, 0)));

    [Fact]
    internal void 半透明の色は不透明の色名を返す()
    {
        // White と Transparent は同じ RGB であるため、アルファが不透明でなければ不透明の名前を優先する
        var halfWhite = DrawingColor.FromArgb(128, 255, 255, 255);
        Assert.Equal("White", ColorNames.FindName(halfWhite));
    }

    [Fact]
    internal void 色名の無い色はnullを返す()
        => Assert.Null(ColorNames.FindName(DrawingColor.FromArgb(255, 1, 2, 3)));

    [Fact]
    internal void 全色名が自分の色へ往復する()
    {
        foreach (var (name, color) in ColorNames.ByName)
        {
            Assert.True(ColorNames.TryGet(name, out var resolved), name);
            Assert.Equal(color, resolved);
        }
    }
}
