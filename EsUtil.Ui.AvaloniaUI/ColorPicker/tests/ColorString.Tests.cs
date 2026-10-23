using Avalonia.Media;
using EsUtil.Ui.AvaloniaUI.Interop;
using Xunit;

namespace EsUtil.Ui.AvaloniaUI.ColorPicker.Tests;

/// <summary><see cref="ColorString"/> の単体テスト。</summary>
/// <remarks>
/// 色と不透明度を統合した文字列形式（色名 / #RRGGBB / #AARRGGBB / +{n}%）の
/// 解析・整形・正規化を検証する。
/// </remarks>
public class ColorStringTests
{
    // ====================================================================
    // IsDefinedColorName
    // ====================================================================

    [Fact]
    internal void IsDefinedColorName_定義済み色名はtrueを返す()
    {
        Assert.True(ColorString.IsDefinedColorName("Red"));
        Assert.True(ColorString.IsDefinedColorName("White"));
        Assert.True(ColorString.IsDefinedColorName("Blue"));
    }

    [Fact]
    internal void IsDefinedColorName_大文字小文字を無視する()
    {
        Assert.True(ColorString.IsDefinedColorName("RED"));
        Assert.True(ColorString.IsDefinedColorName("red"));
    }

    [Fact]
    internal void IsDefinedColorName_未定義の文字列はfalseを返す()
    {
        Assert.False(ColorString.IsDefinedColorName("NotAColor"));
        Assert.False(ColorString.IsDefinedColorName(""));
        Assert.False(ColorString.IsDefinedColorName("#FF0000"));
    }

    // ====================================================================
    // TryParse
    // ====================================================================

    [Fact]
    internal void TryParse_色名のみは不透明度1で解析される()
    {
        Assert.True(ColorString.TryParse("Red", out var color, out var opacity));

        Assert.Equal(Colors.Red, color);
        Assert.Equal(1.0, opacity);
    }

    [Fact]
    internal void TryParse_16進6桁は不透明度1で解析される()
    {
        Assert.True(ColorString.TryParse("#336699", out var color, out var opacity));

        Assert.Equal(Color.FromArgb(255, 0x33, 0x66, 0x99), color);
        Assert.Equal(1.0, opacity);
    }

    [Fact]
    internal void TryParse_16進8桁はアルファを保持して解析される()
    {
        Assert.True(ColorString.TryParse("#80FF0000", out var color, out var opacity));

        Assert.Equal(Color.FromAColor(128, Colors.Red), color);
        // +{n}% が無い場合は不透明度は 1.0 として扱う（アルファ成分は色に保持される）
        Assert.Equal(1.0, opacity);
    }

    [Fact]
    internal void TryParse_色名と不透明度はアルファへ合成される()
    {
        Assert.True(ColorString.TryParse("Red+50%", out var color, out var opacity));

        Assert.Equal(Color.FromAColor(128, Colors.Red), color);
        Assert.Equal(0.5, opacity);
    }

    [Fact]
    internal void TryParse_16進と不透明度はアルファへ合成される()
    {
        Assert.True(ColorString.TryParse("#336699+75%", out var color, out var opacity));

        // 75% → 0.75 * 255 = 191.25 → 丸めで 191 (#BF)
        Assert.Equal(Color.FromArgb(191, 0x33, 0x66, 0x99), color);
        Assert.Equal(0.75, opacity);
    }

    [Fact]
    internal void TryParse_不透明度0はアルファ0になる()
    {
        Assert.True(ColorString.TryParse("Red+0%", out var color, out var opacity));

        Assert.Equal(Color.FromAColor(0, Colors.Red), color);
        Assert.Equal(0.0, opacity);
    }

    [Fact]
    internal void TryParse_不透明度100はアルファ255になる()
    {
        Assert.True(ColorString.TryParse("Red+100%", out var color, out var opacity));

        Assert.Equal(Colors.Red, color);
        Assert.Equal(1.0, opacity);
    }

    [Fact]
    internal void TryParse_不透明度の丸めは四捨五入される()
    {
        // 25% → 0.25 * 255 = 63.75 → 64 (#40)
        Assert.True(ColorString.TryParse("Red+25%", out var color, out _));
        Assert.Equal(64, color.A);

        // 33% → 0.33 * 255 = 84.15 → 84 (#54)
        Assert.True(ColorString.TryParse("Red+33%", out var color2, out _));
        Assert.Equal(84, color2.A);
    }

    [Fact]
    internal void TryParse_nullや空文字はfalseを返す()
    {
        Assert.False(ColorString.TryParse(null, out _, out _));
        Assert.False(ColorString.TryParse("", out _, out _));
        Assert.False(ColorString.TryParse("   ", out _, out _));
    }

    [Fact]
    internal void TryParse_未知の文字列はfalseを返す()
    {
        Assert.False(ColorString.TryParse("NotAColor", out _, out _));
    }

    [Fact]
    internal void TryParse_不透明度が数値でない場合はfalseを返す()
    {
        Assert.False(ColorString.TryParse("Red+abc%", out _, out _));
    }

    [Fact]
    internal void TryParse_不透明度が範囲外の場合はfalseを返す()
    {
        Assert.False(ColorString.TryParse("Red+101%", out _, out _));
        Assert.False(ColorString.TryParse("Red+-1%", out _, out _));
    }

    [Fact]
    internal void TryParse_先頭にプラスのみの場合はfalseを返す()
    {
        // "+50%" は色部分が空になるため解析できない
        Assert.False(ColorString.TryParse("+50%", out _, out _));
    }

    [Fact]
    internal void TryParse_末尾がパーセントでないプラスは色名として扱われる()
    {
        // 色名に + を含む文字列（"+" は色名に無いため解析失敗）
        Assert.False(ColorString.TryParse("Red+50", out _, out _));
    }

    [Fact]
    internal void TryParse_不透明度が有効でも色が無効ならfalseを返す()
    {
        // "+{n}%" 部分は有効だが色部分が解釈不能な場合（&& の最終条件で失敗）
        Assert.False(ColorString.TryParse("NotAColor+50%", out _, out _));
    }

    // ====================================================================
    // TryParse キャッシュ（同一文字列の再解析排除）
    // ====================================================================

    [Fact]
    internal void TryParse_同一文字列はキャッシュから同じ結果を返す()
    {
        Assert.True(ColorString.TryParse("Red+50%", out var first, out var firstOpacity));
        Assert.True(ColorString.TryParse("Red+50%", out var second, out var secondOpacity));

        // キャッシュヒットにより 2 回目も同じ解析結果が返る（再解析なし）
        Assert.Equal(first, second);
        Assert.Equal(firstOpacity, secondOpacity);
    }

    [Fact]
    internal void TryParse_失敗結果もキャッシュされる()
    {
        Assert.False(ColorString.TryParse("NotAColor", out _, out _));
        Assert.False(ColorString.TryParse("NotAColor", out _, out _));
    }

    [Fact]
    internal void TryParse_キャッシュ容量超過でクリアされても正しく解析される()
    {
        // 容量（ParseCacheCapacity）を超える個別の入力でキャッシュを溢れさせ、
        // 全クリア後も同じ結果が返ることを検証する（無制限増加の防止と正しさの両立）
        for (var i = 0; i < ColorString.ParseCacheCapacity + 1; i++)
        {
            Assert.True(ColorString.TryParse($"#{i:X6}", out _, out _));
        }

        // クリア後に再解析しても同じ結果になる
        Assert.True(ColorString.TryParse("#123456", out var color, out var opacity));
        Assert.Equal(Color.FromArgb(255, 0x12, 0x34, 0x56), color);
        Assert.Equal(1.0, opacity);
    }

    // ====================================================================
    // Format
    // ====================================================================

    [Fact]
    internal void Format_色名あり不透明度1は色名のみを返す()
    {
        Assert.Equal("Red", ColorString.Format(Colors.Red, 1.0));
    }

    [Fact]
    internal void Format_色名あり不透明度はパーセント付きで返す()
    {
        Assert.Equal("Red+50%", ColorString.Format(Colors.Red, 0.5));
    }

    [Fact]
    internal void Format_色名なしは16進で返す()
    {
        Assert.Equal("#336699", ColorString.Format(Color.FromRgb(0x33, 0x66, 0x99), 1.0));
        Assert.Equal("#336699+75%", ColorString.Format(Color.FromRgb(0x33, 0x66, 0x99), 0.75));
    }

    [Fact]
    internal void Format_アルファは無視されRGBで色名を判定する()
    {
        // アルファ付きでも RGB が一致すれば色名で表す
        Assert.Equal("Red+50%", ColorString.Format(Color.FromAColor(128, Colors.Red), 0.5));
    }

    [Fact]
    internal void Format_不透明度はパーセント整数へ丸める()
    {
        Assert.Equal("Red+33%", ColorString.Format(Colors.Red, 0.333));
        Assert.Equal("Red+67%", ColorString.Format(Colors.Red, 0.666));
    }

    [Fact]
    internal void Format_不透明度0は0パーセントを返す()
    {
        Assert.Equal("Red+0%", ColorString.Format(Colors.Red, 0.0));
    }

    [Fact]
    internal void Format_不透明度は0から1にクランプされる()
    {
        Assert.Equal("Red+0%", ColorString.Format(Colors.Red, -0.5));
        Assert.Equal("Red", ColorString.Format(Colors.Red, 1.5));
    }

    // ====================================================================
    // ToDisplayText
    // ====================================================================

    [Fact]
    internal void ToDisplayText_アルファ付き16進は色名とパーセントで表示される()
    {
        Assert.Equal("Red+50%", ColorString.ToDisplayText("#80FF0000"));
    }

    [Fact]
    internal void ToDisplayText_不透明な色名はそのまま表示される()
    {
        Assert.Equal("Red", ColorString.ToDisplayText("Red"));
    }

    [Fact]
    internal void ToDisplayText_パーセント指定済みはそのまま表示される()
    {
        Assert.Equal("Red+50%", ColorString.ToDisplayText("Red+50%"));
        Assert.Equal("#336699+75%", ColorString.ToDisplayText("#336699+75%"));
    }

    [Fact]
    internal void ToDisplayText_色名のないアルファ付き16進は16進とパーセントで表示される()
    {
        Assert.Equal("#336699+75%", ColorString.ToDisplayText("#BF336699"));
    }

    [Fact]
    internal void ToDisplayText_16進6桁は色名があれば色名で表示される()
    {
        Assert.Equal("Blue", ColorString.ToDisplayText("#0000FF"));
    }

    [Fact]
    internal void ToDisplayText_nullや空文字はそのまま返す()
    {
        Assert.Equal("", ColorString.ToDisplayText(null));
        Assert.Equal("", ColorString.ToDisplayText(""));
    }

    [Theory]
    [InlineData("NotAColor")]
    [InlineData("#GGGGGG")]
    internal void 変換_解析できない値はそのまま返す(string input)
    {
        // 解析できない値は、表示変換（ToDisplayText）と正規化（ToArgbHex）の両方でそのまま返す
        Assert.Equal(input, ColorString.ToDisplayText(input));
        Assert.Equal(input, ColorString.ToArgbHex(input));
    }

    [Fact]
    internal void ToDisplayText_100パーセントは省略される()
    {
        Assert.Equal("White", ColorString.ToDisplayText("White+100%"));
    }

    [Fact]
    internal void ToDisplayText_半透明の白は色名Whiteで表示される()
    {
        // White と Transparent は同 RGB のため、RGB 一致では不透明（A=255）の色名を優先する
        Assert.Equal("White+50%", ColorString.ToDisplayText("#80FFFFFF"));
    }

    // ====================================================================
    // ToDisplayText キャッシュ（同一文字列の再整形排除）
    // ====================================================================

    [Fact]
    internal void ToDisplayText_同一文字列はキャッシュから同じ結果を返す()
    {
        var first = ColorString.ToDisplayText("#80FF0000");
        var second = ColorString.ToDisplayText("#80FF0000");

        // 表示テキストキャッシュにより 2 回目も同じ整形結果が返る（再整形なし）
        Assert.Equal(first, second);
        Assert.Equal("Red+50%", second);
    }

    [Fact]
    internal void ToDisplayText_解析できない値はキャッシュされずそのまま返す()
    {
        Assert.Equal("NotAColor", ColorString.ToDisplayText("NotAColor"));
        Assert.Equal("NotAColor", ColorString.ToDisplayText("NotAColor"));
    }

    [Fact]
    internal void ToDisplayText_キャッシュ容量超過でクリアされても正しく整形される()
    {
        // 容量（DisplayTextCacheCapacity）を超える個別の入力でキャッシュを溢れさせ、
        // 全クリア後も同じ結果が返ることを検証する（無制限増加の防止と正しさの両立）
        for (var i = 0; i < ColorString.DisplayTextCacheCapacity + 1; i++)
        {
            ColorString.ToDisplayText($"#{i:X6}");
        }

        Assert.Equal("Blue", ColorString.ToDisplayText("#0000FF"));
    }

    // ====================================================================
    // ToArgbHex
    // ====================================================================

    [Fact]
    internal void ToArgbHex_色名はアルファ255の8桁に正規化される()
    {
        Assert.Equal("#FFFF0000", ColorString.ToArgbHex("Red"));
        Assert.Equal("#FFFFFFFF", ColorString.ToArgbHex("White"));
    }

    [Fact]
    internal void ToArgbHex_パーセント付きはアルファへ合成される()
    {
        Assert.Equal("#80FF0000", ColorString.ToArgbHex("Red+50%"));
    }

    [Fact]
    internal void ToArgbHex_16進6桁はアルファ255に正規化される()
    {
        Assert.Equal("#FF336699", ColorString.ToArgbHex("#336699"));
    }

    [Fact]
    internal void ToArgbHex_16進8桁はそのまま返す()
    {
        Assert.Equal("#80FF0000", ColorString.ToArgbHex("#80FF0000"));
    }

    [Fact]
    internal void ToArgbHex_nullや空文字はそのまま返す()
    {
        Assert.Equal("", ColorString.ToArgbHex(null));
        Assert.Equal("", ColorString.ToArgbHex(""));
    }

    // ====================================================================
    // ToArgbHex(string) 正規化キャッシュ（同一文字列の再補間排除）
    // ====================================================================

    [Fact]
    internal void ToArgbHex_同一文字列はキャッシュから同じ結果を返す()
    {
        var first = ColorString.ToArgbHex("Red+50%");
        var second = ColorString.ToArgbHex("Red+50%");

        // 正規化文字列キャッシュにより 2 回目も同じ結果が返る（再補間なし）
        Assert.Equal(first, second);
        Assert.Equal("#80FF0000", second);
    }

    [Fact]
    internal void ToArgbHex_解析できない値はキャッシュされずそのまま返す()
    {
        Assert.Equal("NotAColor", ColorString.ToArgbHex("NotAColor"));
        Assert.Equal("NotAColor", ColorString.ToArgbHex("NotAColor"));
    }

    [Fact]
    internal void ToArgbHex_キャッシュ容量超過でクリアされても正しく正規化される()
    {
        // 容量（NormalizedCacheCapacity）を超える個別の入力でキャッシュを溢れさせ、
        // 全クリア後も同じ結果が返ることを検証する（無制限増加の防止と正しさの両立）
        for (var i = 0; i < ColorString.NormalizedCacheCapacity + 1; i++)
        {
            ColorString.ToArgbHex($"#{i:X6}");
        }

        Assert.Equal("#FF0000FF", ColorString.ToArgbHex("Blue"));
    }

    // ====================================================================
    // ToArgbHex(Color)
    // ====================================================================

    [Fact]
    internal void ToArgbHex_色オブジェクトはアルファ先頭の8桁に変換される()
    {
        Assert.Equal("#80FF0000", ColorString.ToArgbHex(Color.FromAColor(0x80, Colors.Red)));
    }

    [Fact]
    internal void ToArgbHex_色オブジェクトのアルファ255でも省略せず8桁で返す()
    {
        Assert.Equal("#FF123456", ColorString.ToArgbHex(Color.FromRgb(0x12, 0x34, 0x56)));
    }

    // ====================================================================
    // BuildReverseLookups（逆引き辞書の構築）
    // ====================================================================

    [Fact]
    internal void BuildReverseLookups_ARGB完全一致の辞書が構築される()
    {
        var names = new Dictionary<string, Color>
        {
            ["Red"] = Colors.Red,
            ["Blue"] = Colors.Blue,
        };

        var (exact, rgb) = ColorString.BuildReverseLookups(names);

        Assert.Equal("Red", exact[Colors.Red]);
        Assert.Equal("Blue", rgb[(0, 0, 255)]);
    }

    [Fact]
    internal void BuildReverseLookups_同一ARGBは最初の色名が保持される()
    {
        var names = new Dictionary<string, Color>
        {
            ["First"] = Colors.Red,
            ["Second"] = Colors.Red,
        };

        var (exact, _) = ColorString.BuildReverseLookups(names);

        Assert.Equal("First", exact[Colors.Red]);
    }

    [Fact]
    internal void BuildReverseLookups_同RGBは不透明が優先される()
    {
        // 半透明（A=0）→ 不透明（A=255）の順に処理され、不透明の色名が優先登録される
        var names = new Dictionary<string, Color>
        {
            ["Transparent"] = Color.FromAColor(0, Colors.White),
            ["White"] = Colors.White,
        };

        var (_, rgb) = ColorString.BuildReverseLookups(names);

        Assert.Equal("White", rgb[(255, 255, 255)]);
    }

    [Fact]
    internal void BuildReverseLookups_同RGBが両方不透明なら先勝ち()
    {
        var names = new Dictionary<string, Color>
        {
            ["First"] = Colors.Magenta,
            ["Second"] = Colors.Magenta,
        };

        var (_, rgb) = ColorString.BuildReverseLookups(names);

        Assert.Equal("First", rgb[(255, 0, 255)]);
    }

    [Fact]
    internal void BuildReverseLookups_同RGBが両方半透明なら先勝ち()
    {
        var names = new Dictionary<string, Color>
        {
            ["First"] = Color.FromAColor(100, Colors.Red),
            ["Second"] = Color.FromAColor(50, Colors.Red),
        };

        var (_, rgb) = ColorString.BuildReverseLookups(names);

        Assert.Equal("First", rgb[(255, 0, 0)]);
    }
}
