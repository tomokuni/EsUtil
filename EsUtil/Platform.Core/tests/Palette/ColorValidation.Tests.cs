using EsUtil.Platform.Core.Palette;
using Xunit;

namespace EsUtil.Platform.Core.Tests.Palette;

/// <summary><see cref="ColorValidation"/> と <see cref="EsColor"/> の純粋計算テスト（UI 非依存・並列実行）。</summary>
/// <remarks>「空または有効な色はエラーなし」という規則と、窓口（<see cref="EsColor"/>）の委譲を固定する。</remarks>
public class ColorValidationTests
{
    [Theory]
    [InlineData("")]
    [InlineData("Red")]
    [InlineData("Red+50%")]
    internal void Determine_空または有効な色はエラーなしにする(string text)
    {
        var state = ColorValidation.Determine(text, ColorValidation.IsValid(text));
        Assert.False(state.HasError);
        Assert.Null(state.Message);
    }

    [Fact]
    internal void Determine_無効な色はエラーにする()
    {
        var state = ColorValidation.Determine("NotAColor", ColorValidation.IsValid("NotAColor"));
        Assert.True(state.HasError);
        Assert.Equal(ColorValidation.InvalidColorMessage, state.Message);
    }

    [Theory]
    [InlineData(false, true, true)]
    [InlineData(false, false, false)]
    [InlineData(true, true, false)]
    internal void ShouldRecover_無効かつ最後の有効値がある場合だけ戻す(bool isValid, bool hasLastValid, bool expected)
        => Assert.Equal(expected, ColorValidation.ShouldRecover(isValid, hasLastValid));

    [Fact]
    internal void 窓口_色名と一覧と整形を提供する()
    {
        Assert.True(EsColor.TryParseName("Red", out var red));
        Assert.Equal(red, System.Drawing.Color.FromArgb(255, 255, 0, 0));
        Assert.Equal("Red", EsColor.FindName(red));
        Assert.Equal("#FFFF0000", EsColor.ToArgbHex("Red"));
        Assert.Equal("Red+50%", EsColor.ToDisplayText("#80FF0000"));
        Assert.True(EsColor.IsValid("Red"));
        Assert.True(EsColor.IsDefinedName("red"));
        Assert.NotEmpty(EsColor.GetPalette());
        Assert.NotEmpty(EsColor.GetPalette(ColorSortMode.ByName));
    }
}
