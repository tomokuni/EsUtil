using Avalonia.Media;
using Xunit;
using BindingFlags = System.Reflection.BindingFlags;

namespace EsUtil.Ui.AvaloniaUI.ColorPicker.Tests;

/// <summary><see cref="ColorEntry"/> の単体テスト（全色一覧・ソート・表示名・辞書）。</summary>
/// <remarks>
/// 純粋データ（静的構築された色一覧）と純粋演算（<see cref="ColorEntry.GetSortKey"/>）のみを検証するため、
/// Avalonia ヘッドレス実行を必要としない。 並列実行を妨げないよう <c>[Fact]</c> で検証する。
/// </remarks>
public class ColorEntryTests
{
    [Fact]
    internal void All_色名のアルファベット順に並ぶ()
    {
        var all = ColorEntry.All;

        Assert.NotEmpty(all);
        // アルファベット順
        for (var i = 1; i < all.Count; i++)
        {
            Assert.True(
                string.CompareOrdinal(all[i - 1].Name, all[i].Name) <= 0,
                $"{all[i - 1].Name} は {all[i].Name} より前でなければならない");
        }
    }

    [Fact]
    internal void All_Colorsの全色を含む()
    {
        var all = ColorEntry.All;
        var colorNames = typeof(Colors)
            .GetProperties(BindingFlags.Public | BindingFlags.Static)
            .Where(p => p.PropertyType == typeof(Color))
            .Select(p => p.Name)
            .ToArray();

        Assert.Equal(colorNames.Length, all.Count);
        foreach (var name in colorNames)
        {
            Assert.Contains(all, e => e.Name == name);
        }
    }

    [Fact]
    internal void AllByHue_無彩色が先頭で輝度昇順になる()
    {
        var all = ColorEntry.AllByHue;

        // 先頭は Black（無彩色の輝度最小）
        Assert.Equal("Black", all[0].Name);

        // 無彩色（Black/Gray/White）が有彩色より前に来る
        var blackIndex = IndexOf(all, "Black");
        var grayIndex = IndexOf(all, "Gray");
        var whiteIndex = IndexOf(all, "White");
        var redIndex = IndexOf(all, "Red");

        Assert.True(blackIndex < grayIndex);
        Assert.True(grayIndex < whiteIndex);
        Assert.True(whiteIndex < redIndex);
    }

    [Fact]
    internal void ToString_常に英語名を返す()
    {
        var entry = new ColorEntry("Red", "#FF0000");

        Assert.Equal("Red", entry.ToString());
    }

    [Fact]
    internal void All_全エントリのHexがColorとして解析可能()
    {
        foreach (var e in ColorEntry.All)
        {
            Assert.True(Color.TryParse(e.Value, out _), $"{e.Name} の Value '{e.Value}' が解析できない");
        }
    }

    // ====================================================================
    // GetSortKey（色相順ソートキー）
    // ====================================================================

    [Fact]
    internal void GetSortKey_無彩色は輝度昇順になる()
    {
        Assert.Equal((0, 0f), ColorEntry.GetSortKey("Black"));
        Assert.Equal((0, 1f), ColorEntry.GetSortKey("White"));
        // Black < White（priority は同じで key が異なる）
        var (priority, key) = ColorEntry.GetSortKey("Black");
        var white = ColorEntry.GetSortKey("White");
        Assert.Equal(priority, white.priority);
        Assert.True(key < white.key);
    }

    [Fact]
    internal void GetSortKey_有彩色は色相順になる()
    {
        var (priority, key) = ColorEntry.GetSortKey("Red");
        var lime = ColorEntry.GetSortKey("Lime");

        // どちらも有彩色（priority=1）
        Assert.Equal(1, priority);
        Assert.Equal(1, lime.priority);
        Assert.True(key < lime.key); // 赤(0°) < 緑(120°)
    }

    [Fact]
    internal void GetSortKey_解析できない色名は無彩色の先頭に来る()
    {
        // Color.TryParse が失敗する場合の防御分岐（priority=0, key=0）
        var result = ColorEntry.GetSortKey("NotAColor");

        Assert.Equal((0, 0f), result);
    }

    private static int IndexOf(IReadOnlyList<ColorEntry> list, string name)
    {
        for (var i = 0; i < list.Count; i++)
        {
            if (list[i].Name == name) return i;
        }
        return -1;
    }
}
