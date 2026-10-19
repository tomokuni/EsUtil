using EsUtil.Ui.WinUI3.Loupe.Internal;
using SkiaSharp;
using Xunit;
using DrawingColor = System.Drawing.Color;

namespace EsUtil.Ui.WinUI3.Loupe.Tests.Internal;

/// <summary><see cref="SkiaColorInterop"/>（コアの色型と Skia の色型の変換）のテスト。</summary>
/// <remarks>
/// 外観（<see cref="LoupeAppearance"/>）が持つ色は UI 非依存の <see cref="System.Drawing.Color"/> であり、<br/>
/// 描画の境界で本変換を通る。 不透明度（アルファ）を含めて成分が落ちないことを検証する。
/// </remarks>
public class SkiaColorInteropTests
{
    /// <summary>コアの色の成分（ARGB）が Skia の色へそのまま写ることを検証する。</summary>
    [Fact]
    internal void ToSKColor_成分を保持する()
    {
        var source = DrawingColor.FromArgb(0x12, 0x34, 0x56, 0x78);

        var result = source.ToSKColor();

        Assert.Equal(0x12, result.Alpha);
        Assert.Equal(0x34, result.Red);
        Assert.Equal(0x56, result.Green);
        Assert.Equal(0x78, result.Blue);
    }

    /// <summary>透明（アルファ 0）が透明のまま変換されることを検証する。</summary>
    /// <remarks>透明色は「描かない」の代用ではないが、外観の既定値として現れるため成分を保つ必要がある。</remarks>
    [Fact]
    internal void ToSKColor_透明を保つ()
    {
        var result = DrawingColor.Transparent.ToSKColor();

        Assert.Equal(0, result.Alpha);
    }

    /// <summary>Skia の色の成分（ARGB）がコアの色へそのまま写ることを検証する。</summary>
    [Fact]
    internal void ToDrawingColor_成分を保持する()
    {
        var result = new SKColor(red: 0x34, green: 0x56, blue: 0x78, alpha: 0x12).ToDrawingColor();

        Assert.Equal(0x12, result.A);
        Assert.Equal(0x34, result.R);
        Assert.Equal(0x56, result.G);
        Assert.Equal(0x78, result.B);
    }

    /// <summary>コア → Skia → コアの往復で値が変わらないことを検証する。</summary>
    [Fact]
    internal void 往復で値が変わらない()
    {
        var source = DrawingColor.FromArgb(102, 255, 0, 0);

        var result = source.ToSKColor().ToDrawingColor();

        Assert.Equal(source, result);
    }
}
