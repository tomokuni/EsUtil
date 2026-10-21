using Avalonia.Media;
using Xunit;

namespace EsUtil.Ui.AvaloniaUI.Interop.Tests;

/// <summary><see cref="ColorExtensions"/>（C# 14 静的拡張メンバー）の検証テスト。</summary>
public class ColorExtensionsTests
{
    [Fact]
    internal void FromAColor_アルファと名前付き色から生成できる()
    {
        var color = Color.FromAColor(128, Colors.Black);

        Assert.Equal(128, color.A);
        Assert.Equal(Colors.Black.R, color.R);
        Assert.Equal(Colors.Black.G, color.G);
        Assert.Equal(Colors.Black.B, color.B);
    }

    [Fact]
    internal void FromAColor_アルファ255で不透明になる()
    {
        var color = Color.FromAColor(255, Colors.Red);

        Assert.Equal(255, color.A);
        Assert.Equal(255, color.R);
        Assert.Equal(0, color.G);
        Assert.Equal(0, color.B);
    }
}
