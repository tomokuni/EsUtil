using EsUtil.Platform.Core.Palette;
using Xunit;

namespace EsUtil.Platform.Core.Tests.Palette;

/// <summary><see cref="ColorEntry"/> の純粋計算テスト（UI 非依存・並列実行）。</summary>
/// <remarks>並び順（色名順・色相順）と、並び順の指定の検証を固定する。</remarks>
public class ColorEntryTests
{
    [Fact]
    internal void All_は色名のアルファベット順である()
    {
        var names = ColorEntry.All.Select(e => e.Name).ToArray();
        Assert.Equal(names.OrderBy(n => n, StringComparer.Ordinal), names);
    }

    [Fact]
    internal void AllByHue_は無彩色を明度順で先に置く()
    {
        var first = ColorEntry.AllByHue.Take(3).Select(e => e.Name).ToArray();
        Assert.Equal(["Black", "DimGray", "Gray"], first);
    }

    [Fact]
    internal void AllByHue_は有彩色を赤から順に並べる()
    {
        // 無彩色の直後は赤系（色相 0 付近）になる
        var vivid = ColorEntry.AllByHue.Where(e => Math.Abs(e.Color.R - e.Color.G) + Math.Abs(e.Color.G - e.Color.B) > 0).First();
        Assert.NotNull(vivid);
        Assert.True(vivid.Color.R >= vivid.Color.B, $"{vivid.Name} が赤系ではありません");
    }

    [Fact]
    internal void AllとAllByHueは同じ件数である()
        => Assert.Equal(ColorEntry.All.Count, ColorEntry.AllByHue.Count);

    [Fact]
    internal void GetSorted_並び順に応じた一覧を返す()
    {
        Assert.Same(ColorEntry.All, ColorEntry.GetSorted(ColorSortMode.ByName));
        Assert.Same(ColorEntry.AllByHue, ColorEntry.GetSorted(ColorSortMode.ByHue));
    }

    [Fact]
    internal void GetSorted_未定義の並び順は例外にする()
        => Assert.Throws<ArgumentOutOfRangeException>(() => ColorEntry.GetSorted((ColorSortMode)99));

    [Fact]
    internal void ToString_は色名を返す()
        => Assert.Equal("Red", ColorEntry.All.First(e => e.Name == "Red").ToString());
}
