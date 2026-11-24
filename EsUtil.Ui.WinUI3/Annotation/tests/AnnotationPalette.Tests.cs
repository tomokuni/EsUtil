using Xunit;
using EsUtil.Feature.Text.Core.Defaults;

namespace EsUtil.Ui.WinUI3.Annotation.Tests;

/// <summary>WinUI 3 版の注釈が公開する値のテスト（XAML のランタイムを必要としない範囲）。</summary>
/// <remarks>
/// コントロールの生成は XAML のランタイムを必要とするため、ここでは<b>色の並び・フォントの並び・
/// 実寸の近似</b>だけを確かめる（要素の描画はアプリで確認する）。
/// </remarks>
public class AnnotationPaletteTests
{
    /// <summary>代表色は 40 色あり、「代表色」のタブを 1 つ作る（ColorPicker は別のフラグで出す）。</summary>
    [Fact]
    internal void 代表色は40色でタブは代表色の1つ()
    {
        var tab = AnnotationPalette.CreatePaletteTab();

        Assert.Equal(40, AnnotationPalette.RepresentativeColors.Count);
        Assert.Equal(AnnotationPalette.PaletteTabTitle, tab.Title);
        Assert.Equal(40, tab.Colors.Count);
    }

    /// <summary>代表色のタブは指定した列数を持つ。</summary>
    [Fact]
    internal void 代表色は指定した列数を持つ()
    {
        var tab = AnnotationPalette.CreatePaletteTab(5);

        Assert.Equal(5, tab.Columns);
        Assert.Equal(40, tab.Colors.Count);
    }

    /// <summary>フォントの一覧は優先フォントを先頭に置く。</summary>
    [Fact]
    internal void フォントの一覧は優先フォントを先頭に置く()
    {
        Assert.Equal(TextDefaults.PreferredFontFamilies[0], AnnotationFonts.All[0]);
        Assert.True(AnnotationFonts.All.Count > TextDefaults.PreferredFontFamilies.Count);
    }

    /// <summary>XAML を使えない環境では文字数と文字サイズから実寸を近似する。</summary>
    [Fact]
    internal void 実寸は文字数に比例する()
    {
        var style = TextDefaults.DefaultStyle with { FontSize = 20 };

        var shortSize = AnnotationTextMetrics.Measure("あ", style);
        var longSize = AnnotationTextMetrics.Measure("ああああ", style);

        Assert.True(longSize.Width > shortSize.Width);
        Assert.True(shortSize.Height >= style.FontSize);
    }
}
