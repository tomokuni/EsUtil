using EsUtil.Platform.Core.Geometry;
using EsUtil.Ui.WinUI3.Interop.Drawing;
using Xunit;
using DrawingColor = System.Drawing.Color;

namespace EsUtil.Ui.WinUI3.Interop.Tests;

/// <summary><see cref="DrawList"/> の単体テスト（追加順・空の矩形の除外）。</summary>
/// <remarks>
/// 本型は UI 型を持たないため、XAML を初期化せずに検証できる（要素の生成を伴わずに描画内容を検証できる）。
/// </remarks>
public class DrawListTests
{
    [Fact]
    internal void AddRect_追加した順に並ぶ()
    {
        var list = new DrawList();

        list.AddRect(new EsRect(0, 0, 10, 10), DrawingColor.Red);
        list.AddRect(new EsRect(5, 5, 10, 10), DrawingColor.Blue);

        Assert.Equal(2, list.RectCount);
        Assert.Equal(DrawingColor.Red, list.Rects[0].Color);
        Assert.Equal(DrawingColor.Blue, list.Rects[1].Color);
        Assert.Equal(5, list.Rects[1].Bounds.X);
    }

    [Fact]
    internal void AddRect_大きさが0以下の矩形は追加しない()
    {
        var list = new DrawList();

        list.AddRect(new EsRect(0, 0, 0, 10), DrawingColor.Red);
        list.AddRect(new EsRect(0, 0, 10, 0), DrawingColor.Red);
        list.AddRect(new EsRect(0, 0, -1, 10), DrawingColor.Red);

        Assert.Equal(0, list.RectCount);
    }

    [Fact]
    internal void AddRoundRect_角丸を保持する()
    {
        var list = new DrawList();

        list.AddRoundRect(new EsRect(1, 2, 30, 20), 6, DrawingColor.Green);

        var item = Assert.Single(list.RoundRects);
        Assert.Equal(6, item.CornerRadius);
        Assert.Equal(30, item.Bounds.Width);
        Assert.Equal(DrawingColor.Green, item.Color);
    }

    [Fact]
    internal void Clear_すべての矩形を消す()
    {
        var list = new DrawList();
        list.AddRect(new EsRect(0, 0, 10, 10), DrawingColor.Red);
        list.AddRoundRect(new EsRect(0, 0, 10, 10), 2, DrawingColor.Red);

        list.Clear();

        Assert.Equal(0, list.RectCount);
        Assert.Equal(0, list.RoundRectCount);
    }
}
