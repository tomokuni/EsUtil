using Xunit;

namespace EsUtil.Ui.AvaloniaUI.ColorComboBox.Tests;

/// <summary><see cref="ColorEntry"/> のテスト（UI 初期化を要しない・並列実行）。</summary>
/// <remarks>
/// 一覧と並び順の所有者はコア（<c>EsUtil.Platform.Core.Palette.ColorEntry</c>）であるため、本クラスは
/// <b>コントロールの <c>ItemsSource</c> へ渡せる形になっていること</b>だけを固定する（並び順の検証はコアが持つ）。
/// </remarks>
public class ColorEntryTests
{
    [Fact]
    internal void All_先頭ほど色名順に並ぶ()
    {
        var names = ColorEntry.All.Select(e => e.Name).ToArray();
        Assert.Equal(names.OrderBy(n => n, StringComparer.Ordinal), names);
    }

    [Fact]
    internal void All_色名が重複しない()
        => Assert.Equal(ColorEntry.All.Count, ColorEntry.All.Select(e => e.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count());

    [Fact]
    internal void AllByHue_件数はAllと同じ()
        => Assert.Equal(ColorEntry.All.Count, ColorEntry.AllByHue.Count);

    [Fact]
    internal void AllByHue_無彩色が先頭に来る()
        => Assert.Equal("Black", ColorEntry.AllByHue[0].Name);

    [Fact]
    internal void ToString_常に英語名を返す()
    {
        foreach (var entry in ColorEntry.All)
        {
            Assert.Equal(entry.Name, entry.ToString());
        }
    }

    [Fact]
    internal void All_全エントリのValueが色として解析できる()
    {
        foreach (var entry in ColorEntry.All)
        {
            Assert.True(ColorString.TryParse(entry.Value, out _, out _), entry.Value);
        }
    }
}