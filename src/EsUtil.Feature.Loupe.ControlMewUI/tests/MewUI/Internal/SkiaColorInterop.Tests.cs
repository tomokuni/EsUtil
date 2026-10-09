using DrawingColor = System.Drawing.Color;
using EsUtil.Feature.Loupe.MewUI.Internal;
using SkiaSharp;
using Xunit;

namespace EsUtil.Feature.Loupe.MewUI.Tests.Internal;

/// <summary><see cref="SkiaColorInterop"/> の色変換のテスト（UI 非依存・並列実行）。</summary>
/// <remarks>
/// 変換は MewUI 版の描画層（6 箇所）が共用するため、ARGB の各成分が欠落・入れ替わりなく<br/>
/// 保持されることを画素の値で検証する。
/// </remarks>
public class SkiaColorInteropTests
{
    [Fact]
    internal void ToSKColor_ARGBを保持して変換する()
    {
        var source = DrawingColor.FromArgb(0x80, 0x12, 0x34, 0x56);

        var result = source.ToSKColor();

        Assert.Equal(0x80, result.Alpha);
        Assert.Equal(0x12, result.Red);
        Assert.Equal(0x34, result.Green);
        Assert.Equal(0x56, result.Blue);
    }

    [Fact]
    internal void ToSKColor_透明も保持する()
    {
        // アルファ 0 は「描画しない」の判定（コア）に使うため、0 のまま渡す
        // （System.Drawing.Color.Transparent は透明な白。 成分をそのまま保持することを確かめる）
        var source = DrawingColor.Transparent;

        var result = source.ToSKColor();

        Assert.Equal(0, result.Alpha);
        Assert.Equal(source.R, result.Red);
        Assert.Equal(source.G, result.Green);
        Assert.Equal(source.B, result.Blue);
    }

    [Fact]
    internal void ToSKColor_不透明は既知の色と一致する()
    {
        Assert.Equal(SKColors.Red, DrawingColor.Red.ToSKColor());
        Assert.Equal(SKColors.White, DrawingColor.White.ToSKColor());
        Assert.Equal(SKColors.Black, DrawingColor.Black.ToSKColor());
    }

    [Fact]
    internal void ToSKColor_成分ごとの最大値でも桁溢れしない()
    {
        var result = DrawingColor.FromArgb(255, 255, 255, 255).ToSKColor();

        Assert.Equal(SKColors.White, result);
    }

    [Fact]
    internal void ToSKColor_既定値は透明な黒である()
    {
        // default(System.Drawing.Color) は RGB も 0 の透明な黒（Color.Transparent の透明な白とは異なる）
        Assert.Equal(new SKColor(0, 0, 0, 0), default(DrawingColor).ToSKColor());
    }

    [Fact]
    internal void ToDrawingColor_ARGBを保持して変換する()
    {
        var source = new SKColor(0x12, 0x34, 0x56, 0x80);

        var result = source.ToDrawingColor();

        Assert.Equal(0x80, result.A);
        Assert.Equal(0x12, result.R);
        Assert.Equal(0x34, result.G);
        Assert.Equal(0x56, result.B);
    }

    [Fact]
    internal void ToDrawingColor_往復しても同じ色になる()
    {
        // 境界変換を 2 回通しても成分が欠落しないことを確かめる（Skia ⇔ コアの双方向）。
        // System.Drawing.Color の等価は名前付き色と ARGB 生成で異なるため、成分で比較する。
        foreach (var source in new[] { DrawingColor.FromArgb(0, 255, 255, 255), DrawingColor.Red, DrawingColor.FromArgb(0x40, 1, 2, 3) })
        {
            var result = source.ToSKColor().ToDrawingColor();

            Assert.Equal(source.A, result.A);
            Assert.Equal(source.R, result.R);
            Assert.Equal(source.G, result.G);
            Assert.Equal(source.B, result.B);
        }
    }

    [Fact]
    internal void ToDrawingColor_既定値は透明な黒である()
    {
        var result = default(SKColor).ToDrawingColor();

        Assert.Equal(0, result.A);
        Assert.Equal(0, result.R);
        Assert.Equal(0, result.G);
        Assert.Equal(0, result.B);
    }
}
