using System.Numerics;
using Xunit;
using DrawingColor = System.Drawing.Color;
using WinUIColor = Windows.UI.Color;
using WinUIPoint = Windows.Foundation.Point;
using WinUIRect = Windows.Foundation.Rect;
using WinUISize = Windows.Foundation.Size;

namespace EsUtil.Ui.WinUI3.Interop.Tests;

/// <summary><see cref="EsGeometryInterop"/> の幾何・色の変換テスト。</summary>
/// <remarks>
/// 値型の相互変換のみを検証するため、XAML の初期化に依存せず <c>[Fact]</c> で並列実行する。
/// </remarks>
public class EsGeometryInteropTests
{
    // ====================================================================
    // DIP 座標（EsPoint / EsSize / EsVector / EsRect）
    // ====================================================================

    [Fact]
    internal void EsPoint_往復変換で値が一致する()
    {
        var es = new EsPoint(1.5, -2.5);

        Assert.Equal(new WinUIPoint(1.5, -2.5), es.ToWinUIPoint());
        Assert.Equal(es, new WinUIPoint(1.5, -2.5).ToEsPoint());
    }

    [Fact]
    internal void EsPoint_内部の記憶域はfloatのため小数は丸められる()
    {
        // WinUI 3 の Point は公開プロパティが double だが、記憶域は float である（実測）。
        // 往復では float の有効桁までしか戻らない（利用側はこの前提で座標を扱う）。
        var back = new EsPoint(0.1, 0.2).ToWinUIPoint().ToEsPoint();

        Assert.Equal(0.1, back.X, 6);
        Assert.Equal(0.2, back.Y, 6);
    }

    [Fact]
    internal void EsPointInt_物理ピクセルの座標をそのまま写す()
    {
        // WinUI 3 は DIP と物理ピクセルを同じ型（Point）で表すため、値の変換は行わない
        // （MewUI 版の ToMewUIPoint(EsPointInt) と対称）
        var es = new EsPointInt(-1920, 1080);

        Assert.Equal(new WinUIPoint(-1920, 1080), es.ToWinUIPoint());
    }

    [Fact]
    internal void EsPoint_物理ピクセルの座標は四捨五入する()
    {
        var result = new WinUIPoint(10.4, 10.5).ToEsPointInt();

        Assert.Equal(10, result.X);
        Assert.Equal(10, result.Y);
    }

    [Fact]
    internal void EsSize_往復変換で値が一致する()
    {
        var es = new EsSize(100, 50);

        Assert.Equal(new WinUISize(100, 50), es.ToWinUISize());
        Assert.Equal(es, new WinUISize(100, 50).ToEsSize());
    }

    [Fact]
    internal void EsVector_往復変換で値が一致する()
    {
        var es = new EsVector(-3, 7.25);

        Assert.Equal(new Vector2(-3f, 7.25f), es.ToVector2());
        Assert.Equal(es, new Vector2(-3f, 7.25f).ToEsVector());
    }

    [Fact]
    internal void EsRect_往復変換で値が一致する()
    {
        var es = new EsRect(10, 20, 300, 400);

        Assert.Equal(new WinUIRect(10, 20, 300, 400), es.ToWinUIRect());
        Assert.Equal(es, new WinUIRect(10, 20, 300, 400).ToEsRect());
    }

    [Fact]
    internal void EsRect_負のサイズはWinUI3側で0にクランプされる()
    {
        // WinUI 3 の Rect は負の幅・高さを受け付けない（コンストラクターが例外を投げる。 実測）ため、
        // 本 Interop が 0 へクランプする。 座標（X / Y）は保持される。
        var es = new EsRect(100, 100, -20, -30);

        var winui = es.ToWinUIRect();

        Assert.Equal(100.0, winui.X, 6);
        Assert.Equal(100.0, winui.Y, 6);
        Assert.Equal(0.0, winui.Width, 6);
        Assert.Equal(0.0, winui.Height, 6);

        var back = winui.ToEsRect();
        Assert.Equal(new EsRect(100, 100, 0, 0), back);
    }

    [Fact]
    internal void EsSize_負のサイズはWinUI3側で0にクランプされる()
    {
        var clamped = new EsSize(-20, -30).ToWinUISize();

        Assert.Equal(0.0, clamped.Width, 6);
        Assert.Equal(0.0, clamped.Height, 6);
    }

    [Fact]
    internal void EsRect_正のサイズは値のまま往復する()
    {
        var es = new EsRect(10, 20, 300, 400);

        Assert.Equal(es, es.ToWinUIRect().ToEsRect());
    }

    // ====================================================================
    // 色
    // ====================================================================

    [Fact]
    internal void 色_往復変換でARGBが一致する()
    {
        var color = DrawingColor.FromArgb(128, 30, 144, 255);

        var winui = color.ToWinUIColor();
        var back = winui.ToDrawingColor();

        Assert.Equal(color.A, back.A);
        Assert.Equal(color.R, back.R);
        Assert.Equal(color.G, back.G);
        Assert.Equal(color.B, back.B);
    }

    [Fact]
    internal void 色_アルファ0と255も保持される()
    {
        var transparent = DrawingColor.FromArgb(0, 1, 2, 3).ToWinUIColor().ToDrawingColor();
        var opaque = DrawingColor.FromArgb(255, 4, 5, 6).ToWinUIColor().ToDrawingColor();

        Assert.Equal(0, transparent.A);
        Assert.Equal(255, opaque.A);
    }

    [Fact]
    internal void 色_WinUI3の全成分が保持される()
    {
        var winui = WinUIColor.FromArgb(200, 10, 20, 30);

        var back = winui.ToDrawingColor();

        Assert.Equal(200, back.A);
        Assert.Equal(10, back.R);
        Assert.Equal(20, back.G);
        Assert.Equal(30, back.B);
    }
}
