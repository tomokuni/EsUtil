using System.Globalization;
using Xunit;

namespace EsUtil.Ui.AvaloniaUI.ColorComboBox.Tests;

/// <summary><see cref="ColorDisplayConverter"/> の単体テスト。</summary>
/// <remarks>
/// 保存値（#AARRGGBB）と画面表示テキスト（{色}+{不透明度%}）の相互変換を検証する。
/// </remarks>
public class ColorDisplayConverterTests
{
    private static readonly ColorDisplayConverter s_converter = new();

    // ====================================================================
    // Convert（保存値 → 表示テキスト）
    // ====================================================================

    [Fact]
    internal void Convert_アルファ付き16進は色名とパーセントに変換される()
    {
        var result = s_converter.Convert("#80FF0000", typeof(string), null, CultureInfo.InvariantCulture);

        Assert.Equal("Red+50%", result);
    }

    [Fact]
    internal void Convert_不透明な16進は色名のみに変換される()
    {
        var result = s_converter.Convert("#FFFF0000", typeof(string), null, CultureInfo.InvariantCulture);

        Assert.Equal("Red", result);
    }

    [Fact]
    internal void Convert_nullはnullを返す()
    {
        Assert.Null(s_converter.Convert(null, typeof(string), null, CultureInfo.InvariantCulture));
    }

    [Fact]
    internal void Convert_文字列以外の値はそのまま返す()
    {
        object value = 123;
        Assert.Same(value, s_converter.Convert(value, typeof(string), null, CultureInfo.InvariantCulture));
    }

    // ====================================================================
    // ConvertBack（表示テキスト → 保存値）
    // ====================================================================

    [Fact]
    internal void ConvertBack_色名とパーセントはアルファ付き16進に変換される()
    {
        var result = s_converter.ConvertBack("Red+50%", typeof(string), null, CultureInfo.InvariantCulture);

        Assert.Equal("#80FF0000", result);
    }

    [Fact]
    internal void ConvertBack_色名はアルファ255の8桁に変換される()
    {
        var result = s_converter.ConvertBack("Red", typeof(string), null, CultureInfo.InvariantCulture);

        Assert.Equal("#FFFF0000", result);
    }

    [Fact]
    internal void ConvertBack_16進6桁はアルファ255に変換される()
    {
        var result = s_converter.ConvertBack("#336699", typeof(string), null, CultureInfo.InvariantCulture);

        Assert.Equal("#FF336699", result);
    }

    [Fact]
    internal void ConvertBack_nullはnullを返す()
    {
        Assert.Null(s_converter.ConvertBack(null, typeof(string), null, CultureInfo.InvariantCulture));
    }

    [Fact]
    internal void ConvertBack_文字列以外の値はそのまま返す()
    {
        object value = 456;
        Assert.Same(value, s_converter.ConvertBack(value, typeof(string), null, CultureInfo.InvariantCulture));
    }
}
