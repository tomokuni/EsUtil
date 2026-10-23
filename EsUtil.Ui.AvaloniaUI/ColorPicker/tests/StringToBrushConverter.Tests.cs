using System.Globalization;
using Avalonia.Media;
using Xunit;

namespace EsUtil.Ui.AvaloniaUI.ColorPicker.Tests;

/// <summary><see cref="StringToBrushConverter"/> の単体テスト。</summary>
/// <remarks>
/// 変換は <see cref="ColorString.TryParse"/>（純粋演算）と <see cref="SolidColorBrush"/> 生成のみのため、
/// Avalonia ヘッドレス実行（Dispatcher / Application）を必要としない。<br/>
/// 並列実行を妨げないよう <c>[Fact]</c> で検証する。
/// </remarks>
public class StringToBrushConverterTests
{
    private static readonly StringToBrushConverter s_converter = new();

    [Fact]
    internal void Convert_色名はSolidColorBrushを返す()
    {
        var result = s_converter.Convert("Red", typeof(IBrush), null, CultureInfo.InvariantCulture);

        var brush = Assert.IsType<SolidColorBrush>(result);
        Assert.Equal(Colors.Red, brush.Color);
    }

    [Fact]
    internal void Convert_同一入力はキャッシュされた同一インスタンスを返す()
    {
        // Flyweight キャッシュにより、同一入力は生成済みブラシを共有する（無駄な生成の排除）
        var first = s_converter.Convert("Red", typeof(IBrush), null, CultureInfo.InvariantCulture);
        var second = s_converter.Convert("Red", typeof(IBrush), null, CultureInfo.InvariantCulture);

        Assert.Same(first, second);
    }

    [Fact]
    internal void Convert_キャッシュ容量超過後も正しく変換される()
    {
        // 容量（BrushCacheCapacity）を超える個別の入力でキャッシュを溢れさせ、
        // 全クリア後も正しく変換できることを検証する（無制限増加の防止と正しさの両立）
        for (var i = 0; i < StringToBrushConverter.BrushCacheCapacity + 1; i++)
        {
            var r = s_converter.Convert($"#{i:X6}", typeof(IBrush), null, CultureInfo.InvariantCulture);
            Assert.IsType<SolidColorBrush>(r);
        }

        var result = s_converter.Convert("#123456", typeof(IBrush), null, CultureInfo.InvariantCulture);
        var brush = Assert.IsType<SolidColorBrush>(result);
        Assert.Equal(Color.FromArgb(255, 0x12, 0x34, 0x56), brush.Color);
    }

    [Fact]
    internal void Convert_16進コードはSolidColorBrushを返す()
    {
        var result = s_converter.Convert("#00FF00", typeof(IBrush), null, CultureInfo.InvariantCulture);

        var brush = Assert.IsType<SolidColorBrush>(result);
        Assert.Equal(Colors.Lime, brush.Color);
    }

    [Fact]
    internal void Convert_無効な値はTransparentを返す()
    {
        var result = s_converter.Convert("NotAColor", typeof(IBrush), null, CultureInfo.InvariantCulture);

        Assert.Equal(Brushes.Transparent, result);
    }

    [Fact]
    internal void Convert_nullはTransparentを返す()
    {
        var result = s_converter.Convert(null, typeof(IBrush), null, CultureInfo.InvariantCulture);

        Assert.Equal(Brushes.Transparent, result);
    }

    [Fact]
    internal void Convert_空文字はTransparentを返す()
    {
        var result = s_converter.Convert("", typeof(IBrush), null, CultureInfo.InvariantCulture);

        Assert.Equal(Brushes.Transparent, result);
    }

    [Fact]
    internal void ConvertBack_NotSupportedExceptionを投げる()
    {
        Assert.Throws<NotSupportedException>(() =>
            s_converter.ConvertBack("Red", typeof(IBrush), null, CultureInfo.InvariantCulture));
    }
}
