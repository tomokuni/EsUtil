using DrawingColor = System.Drawing.Color;
using EsUtil.Feature.Loupe.MewUI.Internal;
using EsUtil.Platform.Core.Geometry;
using MewUIColor = Aprillz.MewUI.Color;
using MewUIPoint = Aprillz.MewUI.Point;
using MewUIRect = Aprillz.MewUI.Rect;
using MewUISize = Aprillz.MewUI.Size;
using MewUIVector = Aprillz.MewUI.Vector;
using Xunit;

namespace EsUtil.Feature.Loupe.MewUI.Tests.Internal;

/// <summary><see cref="EsGeometryMewUIInterop"/> / <see cref="EsGeometryMewUIInterop"/> の幾何・色の変換テスト。</summary>
/// <remarks>
/// 値型の相互変換のみを検証するため、ウィンドウ・バックエンドの初期化に依存せず <c>[Fact]</c> で並列実行する。
/// </remarks>
public class EsGeometryMewUIInteropTests
{
    // ====================================================================
    // DIP 座標（EsPoint / EsSize / EsVector / EsRect）
    // ====================================================================

    [Fact]
    internal void EsPoint_往復変換で値が一致する()
    {
        var es = new EsPoint(1.5, -2.5);

        Assert.Equal(new MewUIPoint(1.5, -2.5), es.ToMewUIPoint());
        Assert.Equal(es, new MewUIPoint(1.5, -2.5).ToEsPoint());
    }

    [Fact]
    internal void EsPointInt_物理ピクセルの座標をそのまま写す()
    {
        // MewUI は DIP と物理ピクセルを同じ型（Point）で表すため、値の変換は行わない
        // （Avalonia 版の ToAvaloniaPixelPoint と対称）
        var es = new EsPointInt(-1920, 1080);

        Assert.Equal(new MewUIPoint(-1920, 1080), es.ToMewUIPoint());
    }

    [Fact]
    internal void EsSize_往復変換で値が一致する()
    {
        var es = new EsSize(100, 50);

        Assert.Equal(new MewUISize(100, 50), es.ToMewUISize());
        Assert.Equal(es, new MewUISize(100, 50).ToEsSize());
    }

    [Fact]
    internal void EsVector_往復変換で値が一致する()
    {
        var es = new EsVector(-3, 7.25);

        Assert.Equal(new MewUIVector(-3, 7.25), es.ToMewUIVector());
        Assert.Equal(es, new MewUIVector(-3, 7.25).ToEsVector());
    }

    [Fact]
    internal void EsRect_往復変換で値が一致する()
    {
        var es = new EsRect(10, 20, 300, 400);

        Assert.Equal(new MewUIRect(10, 20, 300, 400), es.ToMewUIRect());
        Assert.Equal(es, new MewUIRect(10, 20, 300, 400).ToEsRect());
    }

    [Fact]
    internal void EsRect_負のサイズはMewUI側で0にクランプされる()
    {
        // MewUI の Rect / Size は負のサイズを 0 にクランプする（Avalonia は保持する）ため、往復で値は変わらない。
        // 座標（X / Y）は保持される。
        var es = new EsRect(100, 100, -20, -30);

        var mewui = es.ToMewUIRect();

        Assert.Equal(100.0, mewui.X, 6);
        Assert.Equal(100.0, mewui.Y, 6);
        Assert.Equal(0.0, mewui.Width, 6);
        Assert.Equal(0.0, mewui.Height, 6);

        var back = mewui.ToEsRect();
        Assert.Equal(new EsRect(100, 100, 0, 0), back);
    }

    [Fact]
    internal void EsSize_負のサイズはMewUI側で0にクランプされる()
    {
        var clamped = new EsSize(-20, -30).ToMewUISize();

        Assert.Equal(0.0, clamped.Width, 6);
        Assert.Equal(0.0, clamped.Height, 6);
    }

    [Fact]
    internal void EsRect_正のサイズは値のまま往復する()
    {
        var es = new EsRect(10, 20, 300, 400);

        Assert.Equal(es, es.ToMewUIRect().ToEsRect());
    }

    // ====================================================================
    // 色
    // ====================================================================

    [Fact]
    internal void 色_往復変換でARGBが一致する()
    {
        var color = DrawingColor.FromArgb(128, 30, 144, 255);

        var mew = color.ToMewUIColor();
        var back = mew.ToDrawingColor();

        Assert.Equal(color.A, back.A);
        Assert.Equal(color.R, back.R);
        Assert.Equal(color.G, back.G);
        Assert.Equal(color.B, back.B);
    }

    [Fact]
    internal void 色_アルファ0と255も保持される()
    {
        var transparent = DrawingColor.FromArgb(0, 1, 2, 3).ToMewUIColor().ToDrawingColor();
        var opaque = DrawingColor.FromArgb(255, 4, 5, 6).ToMewUIColor().ToDrawingColor();

        Assert.Equal(0, transparent.A);
        Assert.Equal(255, opaque.A);
    }

    [Fact]
    internal void 色_MewUIの全成分が保持される()
    {
        var mew = MewUIColor.FromArgb(200, 10, 20, 30);

        var back = mew.ToDrawingColor();

        Assert.Equal(200, back.A);
        Assert.Equal(10, back.R);
        Assert.Equal(20, back.G);
        Assert.Equal(30, back.B);
    }
}
