using EsUtil.Platform.Core.Geometry;
using EsUtil.Ui.WinForms.Interop.Imaging;
using Xunit;
using DrawingColor = System.Drawing.Color;

namespace EsUtil.Ui.WinForms.Interop.Tests;

/// <summary><see cref="GdiLabel"/> のテスト（背景 → 文字の順に描く）。</summary>
/// <remarks>
/// ウィンドウを必要としないため、オフスクリーンのビットマップ上で画素を実測できる。<br/>
/// 背景と文字色を変えて、背景が先（下層）・文字が後（上層）に描かれることを検証する。
/// </remarks>
public sealed class GdiLabelTests
{
    /// <summary>背景の矩形が指定色で塗られることを検証する。</summary>
    [Fact]
    internal void Draw_背景を塗る()
    {
        using var target = TestHelper.CreateTarget(120, 40);
        var background = DrawingColor.FromArgb(255, 0, 0, 255);

        GdiLabel.Draw(
            target.Graphics,
            new EsRect(10, 10, 60, 20),
            background,
            "Ag",
            new EsPoint(12, 12),
            12,
            DrawingColor.FromArgb(255, 255, 255, 0));

        // 文字の届かない四隅は背景色のまま
        Assert.Equal(background, target.GetPixel(11, 11));
        Assert.Equal(background, target.GetPixel(68, 28));
    }

    /// <summary>文字が背景の上へ描かれることを検証する。</summary>
    /// <remarks>
    /// 背景 → 文字の順を逆にすると文字が背景へ隠れるため、文字色の画素が残ることで順序を確認する。
    /// </remarks>
    [Fact]
    internal void Draw_文字を背景の上へ描く()
    {
        using var target = TestHelper.CreateTarget(120, 40);
        var text = DrawingColor.FromArgb(255, 255, 255, 0);

        GdiLabel.Draw(
            target.Graphics,
            new EsRect(0, 0, 120, 40),
            DrawingColor.FromArgb(255, 0, 0, 255),
            "Ag",
            new EsPoint(2, 2),
            24,
            text);

        Assert.True(target.CountColor(0, 0, 120, 40, text) > 0);
    }

    /// <summary>角丸の半径を与えても矩形が描かれることを検証する（左上の角が丸くなる）。</summary>
    [Fact]
    internal void Draw_角丸でも描く()
    {
        using var target = TestHelper.CreateTarget(60, 60);
        var background = DrawingColor.FromArgb(255, 0, 128, 0);

        GdiLabel.Draw(
            target.Graphics,
            new EsRect(10, 10, 40, 40),
            background,
            string.Empty,
            new EsPoint(12, 12),
            12,
            DrawingColor.White,
            cornerRadius: 12);

        // 角（左上の外側）は塗られず、辺の中央は塗られる
        Assert.Equal(TestHelper.EmptyPixel, target.GetPixel(10, 10));
        Assert.Equal(background, target.GetPixel(30, 12));
    }

    /// <summary>背景の矩形が空の場合は背景を塗らないことを検証する。</summary>
    [Fact]
    internal void Draw_空の背景は塗らない()
    {
        using var target = TestHelper.CreateTarget(20, 20);

        GdiLabel.Draw(
            target.Graphics,
            new EsRect(0, 0, 0, 0),
            DrawingColor.FromArgb(255, 0, 0, 255),
            "Ag",
            new EsPoint(2, 2),
            12,
            DrawingColor.White);

        Assert.Equal(TestHelper.EmptyPixel, target.GetPixel(18, 18));
    }

    /// <summary>文字列の引数が null の場合に例外になることを検証する。</summary>
    [Fact]
    internal void Draw_nullは例外になる()
    {
        using var target = TestHelper.CreateTarget(20, 20);

        Assert.Throws<ArgumentNullException>(() => GdiLabel.Draw(
            target.Graphics,
            new EsRect(0, 0, 10, 10),
            DrawingColor.Black,
            null!,
            new EsPoint(0, 0),
            12,
            DrawingColor.White));
    }
}
