using EsUtil.Ui.WinForms.Marquee.Internal;
using Xunit;

namespace EsUtil.Ui.WinForms.Marquee.Tests.Internal;

/// <summary><see cref="EsGeometryWinFormsInterop"/> の幾何の変換テスト。</summary>
/// <remarks>
/// 値型の相互変換のみを検証するため、ウィンドウの初期化に依存せず <c>[Fact]</c> で並列実行する。
/// </remarks>
public sealed class EsGeometryWinFormsInteropTests
{
    // ====================================================================
    // 座標（EsPoint / EsPointInt）
    // ====================================================================

    [Fact]
    internal void EsPoint_往復変換で値が一致する()
    {
        var es = new EsPoint(1.5, -2.5);

        Assert.Equal(new PointF(1.5f, -2.5f), es.ToPointF());
        Assert.Equal(es, new PointF(1.5f, -2.5f).ToEsPoint());
    }

    [Fact]
    internal void EsPoint_整数座標へは四捨五入して変換する()
    {
        Assert.Equal(new Point(2, 3), new EsPoint(1.5, 2.5).ToPoint());
        Assert.Equal(new Point(-2, -2), new EsPoint(-1.5, -2.4).ToPoint());
    }

    [Fact]
    internal void EsPointInt_整数座標としてそのまま写る()
    {
        var es = new EsPointInt(-1920, 1080);

        Assert.Equal(new Point(-1920, 1080), es.ToPoint());
        Assert.Equal(es, new Point(-1920, 1080).ToEsPointInt());
    }

    [Fact]
    internal void PointF_画面物理座標へは四捨五入して変換する()
    {
        Assert.Equal(new EsPointInt(2, 3), new PointF(1.5f, 2.5f).ToEsPointInt());
        Assert.Equal(new EsPointInt(-2, -2), new PointF(-1.5f, -2.4f).ToEsPointInt());
    }

    [Fact]
    internal void Point_コア座標へ変換する()
    {
        Assert.Equal(new EsPoint(10, 20), new Point(10, 20).ToEsPoint());
    }

    // ====================================================================
    // サイズ（EsSize）
    // ====================================================================

    [Fact]
    internal void EsSize_往復変換で値が一致する()
    {
        var es = new EsSize(100, 50);

        Assert.Equal(new SizeF(100f, 50f), es.ToSizeF());
        Assert.Equal(es, new SizeF(100f, 50f).ToEsSize());
    }

    // ====================================================================
    // ベクター（EsVector）
    // ====================================================================

    [Fact]
    internal void EsVector_往復変換で値が一致する()
    {
        var es = new EsVector(-3, 7.25);

        Assert.Equal(new PointF(-3f, 7.25f), es.ToPointF());
        Assert.Equal(es, new PointF(-3f, 7.25f).ToEsVector());
    }

    // ====================================================================
    // 矩形（EsRect）
    // ====================================================================

    [Fact]
    internal void EsRect_往復変換で値が一致する()
    {
        var es = new EsRect(10, 20, 300, 400);

        Assert.Equal(new RectangleF(10f, 20f, 300f, 400f), es.ToRectangleF());
        Assert.Equal(es, new RectangleF(10f, 20f, 300f, 400f).ToEsRect());
    }

    /// <summary>GDI+ の矩形は負のサイズを保持する（値はそのまま往復する）ことを検証する。</summary>
    /// <remarks>MewUI 版は負のサイズを 0 にクランプするが、GDI+ の <see cref="RectangleF"/> は保持する。</remarks>
    [Fact]
    internal void EsRect_負のサイズも保持される()
    {
        var es = new EsRect(100, 100, -20, -30);

        var gdi = es.ToRectangleF();

        Assert.Equal(-20f, gdi.Width);
        Assert.Equal(-30f, gdi.Height);
        Assert.Equal(es, gdi.ToEsRect());
    }

    /// <summary>単精度へ丸められる値でも往復で一致する（整数と 0.5 刻みは損失しない）ことを検証する。</summary>
    [Fact]
    internal void EsPoint_単精度で表現できる値は往復で一致する()
    {
        var es = new EsPoint(0.5, -0.25);

        Assert.Equal(es, es.ToPointF().ToEsPoint());
    }
}
