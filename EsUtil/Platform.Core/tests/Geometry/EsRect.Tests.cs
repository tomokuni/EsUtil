using EsUtil.Platform.Core.Geometry;
using Xunit;

namespace EsUtil.Platform.Core.Tests.Geometry;

/// <summary><see cref="EsRect"/> の純粋計算テスト（UI 非依存・並列実行）。</summary>
/// <remarks>
/// 値型の算術のみを検証するため、UI フレームワークの初期化に依存せず <c>[Fact]</c> で並列実行する。
/// </remarks>
public class EsRectTests
{
    // ====================================================================
    // 境界（Right / Bottom）
    // ====================================================================

    [Fact]
    internal void Right_左上Xと幅の和を返す()
    {
        var rect = new EsRect(10.5, 20.25, 100.5, 50.25);

        Assert.Equal(111.0, rect.Right, 6);
    }

    [Fact]
    internal void Bottom_左上Yと高さの和を返す()
    {
        var rect = new EsRect(10.5, 20.25, 100.5, 50.25);

        Assert.Equal(70.5, rect.Bottom, 6);
    }

    [Fact]
    internal void Right_幅が0なら左上Xを返す()
    {
        var rect = new EsRect(7.5, 3.5, 0, 0);

        Assert.Equal(7.5, rect.Right, 6);
        Assert.Equal(3.5, rect.Bottom, 6);
    }

    [Fact]
    internal void Right_負の幅は左上Xより小さい値を返す()
    {
        // 負の幅は空の矩形を表す（境界も左上より小さくなる）
        var rect = new EsRect(100, 100, -20, -30);

        Assert.Equal(80.0, rect.Right, 6);
        Assert.Equal(70.0, rect.Bottom, 6);
    }

    // ====================================================================
    // ComputeInnerEdges（枠線の内側 4 辺）
    // ====================================================================

    [Fact]
    internal void ComputeInnerEdges_四辺の矩形を上_下_左_右の順に返す()
    {
        var edges = new EsRect(10, 20, 100, 80).ComputeInnerEdges(thickness: 2);

        Assert.Equal(4, edges.Count);
        Assert.Equal(new EsRect(10, 20, 100, 2), edges[0]);
        Assert.Equal(new EsRect(10, 98, 100, 2), edges[1]);
        Assert.Equal(new EsRect(10, 22, 2, 76), edges[2]);
        Assert.Equal(new EsRect(108, 22, 2, 76), edges[3]);
    }

    [Fact]
    internal void ComputeInnerEdges_左右の辺は上下の辺の内側へ収まる()
    {
        var edges = new EsRect(0, 0, 100, 80).ComputeInnerEdges(thickness: 2);

        // 左右の辺は上端 +t から始まり、高さは h - 2t（角で二重に塗らない）
        Assert.Equal(2, edges[2].Y);
        Assert.Equal(80 - 4, edges[2].Height);
        Assert.Equal(2, edges[3].Y);
        Assert.Equal(80 - 4, edges[3].Height);
    }

    [Fact]
    internal void ComputeInnerEdges_太さが半分を超える場合は辺の高さを0にクランプする()
    {
        var edges = new EsRect(0, 0, 10, 6).ComputeInnerEdges(thickness: 4);

        Assert.Equal(0, edges[2].Height);
        Assert.Equal(0, edges[3].Height);
    }

    [Fact]
    internal void ComputeInnerEdges_太さ0は各辺を厚み0で返す()
    {
        var edges = new EsRect(5, 6, 10, 20).ComputeInnerEdges(thickness: 0);

        Assert.Equal(new EsRect(5, 6, 10, 0), edges[0]);
        Assert.Equal(new EsRect(5, 6, 0, 20), edges[2]);
    }

    // ====================================================================
    // ClampToArea（領域内へのクランプ）
    // ====================================================================

    [Fact]
    internal void ClampToArea_領域内の矩形は変更しない()
    {
        var rect = new EsRect(10, 20, 30, 40);

        Assert.Equal(rect, rect.ClampToArea(new EsSize(200, 100)));
    }

    [Theory]
    [InlineData(-5, -5, 25, 35)]
    [InlineData(250, 120, 200, 100)]
    internal void ClampToArea_領域外の矩形は四隅の包絡矩形へ縮める(double x, double y, double expectedRight, double expectedBottom)
    {
        var clamped = new EsRect(x, y, 30, 40).ClampToArea(new EsSize(200, 100));

        Assert.Equal(expectedRight, clamped.Right);
        Assert.Equal(expectedBottom, clamped.Bottom);
        Assert.True(clamped.Width >= 0);
        Assert.True(clamped.Height >= 0);
    }

    [Fact]
    internal void ClampToArea_領域全体を覆う矩形は領域全体になる()
    {
        var clamped = new EsRect(-100, -100, 500, 500).ClampToArea(new EsSize(200, 100));

        Assert.Equal(new EsRect(0, 0, 200, 100), clamped);
    }

    [Fact]
    internal void ClampToArea_領域の外にある矩形は辺0になる()
    {
        var clamped = new EsRect(300, 200, 30, 40).ClampToArea(new EsSize(200, 100));

        Assert.Equal(200, clamped.X);
        Assert.Equal(100, clamped.Y);
        Assert.Equal(0, clamped.Width);
        Assert.Equal(0, clamped.Height);
    }

    // ====================================================================
    // Intersect（重なり部分）
    // ====================================================================

    [Fact]
    internal void Intersect_重なる部分を返す()
    {
        var intersection = new EsRect(0, 0, 100, 100).Intersect(new EsRect(50, 60, 100, 100));

        Assert.Equal(new EsRect(50, 60, 50, 40), intersection);
    }

    [Fact]
    internal void Intersect_包含関係では小さい方を返す()
    {
        var outer = new EsRect(0, 0, 100, 100);
        var inner = new EsRect(10, 20, 30, 40);

        Assert.Equal(inner, outer.Intersect(inner));
        Assert.Equal(inner, inner.Intersect(outer));
    }

    [Theory]
    [InlineData(200, 0)]
    [InlineData(0, 200)]
    [InlineData(-100, -100)]
    internal void Intersect_重なりが無ければ空になる(double x, double y)
    {
        var intersection = new EsRect(0, 0, 100, 100).Intersect(new EsRect(x, y, 50, 50));

        Assert.True(intersection.IsEmpty);
    }

    [Fact]
    internal void Intersect_接するだけの矩形は空になる()
    {
        // 辺が重なる（幅 0 の重なり）場合も空として扱う（描画できる面積が無いため）
        var intersection = new EsRect(0, 0, 100, 100).Intersect(new EsRect(100, 0, 50, 50));

        Assert.True(intersection.IsEmpty);
    }

    [Fact]
    internal void Intersect_自分自身とは同じ矩形になる()
    {
        var rect = new EsRect(10, 20, 30, 40);

        Assert.Equal(rect, rect.Intersect(rect));
    }

    // ====================================================================
    // Empty / Size / 生成
    // ====================================================================

    [Fact]
    internal void Empty_は全て0()
    {
        Assert.Equal(new EsRect(0, 0, 0, 0), EsRect.Empty);
    }

    [Fact]
    internal void Size_幅と高さを返す()
    {
        Assert.Equal(new EsSize(30, 40), new EsRect(10, 20, 30, 40).Size);
    }

    [Fact]
    internal void FromLTRB_四辺から幅と高さを求める()
    {
        Assert.Equal(new EsRect(10, 20, 30, 40), EsRect.FromLTRB(10, 20, 40, 60));
    }

    [Fact]
    internal void FromLTRB_右が左より小さければ負の幅になる()
    {
        var rect = EsRect.FromLTRB(50, 60, 10, 20);

        Assert.Equal(-40, rect.Width);
        Assert.Equal(-40, rect.Height);
    }

    [Fact]
    internal void FromPoints_二点を対角とする矩形を返す()
    {
        Assert.Equal(new EsRect(10, 20, 30, 40), EsRect.FromPoints(new EsPoint(10, 20), new EsPoint(40, 60)));
    }

    [Fact]
    internal void FromRectInt_成分をそのまま引き継ぐ()
    {
        Assert.Equal(new EsRect(10, 20, 30, 40), EsRect.FromRectInt(new EsRectInt(10, 20, 30, 40)));
    }

    // ====================================================================
    // With系
    // ====================================================================

    [Fact]
    internal void With系_指定した成分だけを差し替える()
    {
        var rect = new EsRect(1, 2, 3, 4);

        Assert.Equal(new EsRect(9, 2, 3, 4), rect.WithX(9));
        Assert.Equal(new EsRect(1, 9, 3, 4), rect.WithY(9));
        Assert.Equal(new EsRect(1, 2, 9, 4), rect.WithWidth(9));
        Assert.Equal(new EsRect(1, 2, 3, 9), rect.WithHeight(9));
        Assert.Equal(new EsRect(8, 9, 3, 4), rect.WithPosition(new EsPoint(8, 9)));
        Assert.Equal(new EsRect(1, 2, 8, 9), rect.WithSize(new EsSize(8, 9)));
    }

    // ====================================================================
    // Offset / Inflate / Deflate
    // ====================================================================

    [Fact]
    internal void Offset_位置だけを平行移動する()
    {
        Assert.Equal(new EsRect(11, 22, 3, 4), new EsRect(1, 2, 3, 4).Offset(10, 20));
    }

    [Fact]
    internal void Inflate_四辺を広げる()
    {
        Assert.Equal(new EsRect(-2, -3, 9, 14), new EsRect(1, 2, 3, 4).Inflate(3, 5));
    }

    [Fact]
    internal void Inflate_厚さで四辺を広げる()
    {
        Assert.Equal(new EsRect(-3, -5, 16, 30), new EsRect(0, 0, 10, 20).Inflate(new EsThickness(3, 5, 3, 5)));
    }

    [Fact]
    internal void Deflate_厚さで四辺を狭める()
    {
        Assert.Equal(new EsRect(3, 5, 7, 14), new EsRect(0, 0, 10, 20).Deflate(new EsThickness(3, 5, 0, 1)));
    }

    [Fact]
    internal void Deflate_負のサイズはそのまま返す()
    {
        // クランプしない（必要な箇所は Normalize を使う）
        Assert.Equal(-5, new EsRect(0, 0, 10, 20).Deflate(new EsThickness(10, 0, 5, 0)).Width);
    }

    // ====================================================================
    // Normalize
    // ====================================================================

    [Fact]
    internal void Normalize_負の幅と高さを正にする()
    {
        Assert.Equal(new EsRect(10, 20, 30, 40), new EsRect(40, 60, -30, -40).Normalize());
    }

    [Fact]
    internal void Normalize_既に正なら同じ値()
    {
        var rect = new EsRect(1, 2, 3, 4);

        Assert.Equal(rect, rect.Normalize());
    }

    [Theory]
    [InlineData(double.NaN, 0, 10, 10)]
    [InlineData(0, double.NaN, 10, 10)]
    [InlineData(0, 0, double.NaN, 10)]
    internal void Normalize_非数を含むなら空になる(double x, double y, double width, double height)
    {
        Assert.Equal(EsRect.Empty, new EsRect(x, y, width, height).Normalize());
    }

    // ====================================================================
    // ContainsExclusive / Contains(Rect) / Intersects
    // ====================================================================

    [Fact]
    internal void ContainsExclusive_右端と下端を含まない()
    {
        var rect = new EsRect(0, 0, 100, 100);

        Assert.True(rect.ContainsExclusive(new EsPoint(99, 99)));
        Assert.False(rect.ContainsExclusive(new EsPoint(100, 50)));
        Assert.False(rect.ContainsExclusive(new EsPoint(50, 100)));
    }

    [Fact]
    internal void Contains_矩形を完全に含むかを判定する()
    {
        var rect = new EsRect(0, 0, 100, 100);

        Assert.True(rect.Contains(new EsRect(10, 10, 20, 20)));
        Assert.False(rect.Contains(new EsRect(90, 90, 20, 20)));
    }

    [Theory]
    [InlineData(50, 50, 100, 100, true)]
    [InlineData(100, 0, 50, 50, false)]
    [InlineData(200, 0, 50, 50, false)]
    internal void Intersects_接触のみは偽(double x, double y, double w, double h, bool expected)
    {
        Assert.Equal(expected, new EsRect(0, 0, 100, 100).Intersects(new EsRect(x, y, w, h)));
    }

    // ====================================================================
    // Union
    // ====================================================================

    [Fact]
    internal void Union_両者を包含する最小の矩形を返す()
    {
        Assert.Equal(new EsRect(0, 0, 300, 150), new EsRect(0, 0, 100, 100).Union(new EsRect(200, 50, 100, 100)));
    }

    [Fact]
    internal void Union_空の矩形なら相手を返す()
    {
        var empty = new EsRect(10, 10, 0, 0);
        var other = new EsRect(0, 0, 100, 100);

        Assert.Equal(other, empty.Union(other));
        Assert.Equal(other, other.Union(empty));
    }

    [Fact]
    internal void Union_点を含む矩形を返す()
    {
        Assert.Equal(new EsRect(0, 0, 150, 150), new EsRect(0, 0, 100, 100).Union(new EsPoint(150, 150)));
    }

    [Fact]
    internal void Union_空の矩形と点は面積0の矩形を返す()
    {
        Assert.Equal(new EsRect(5, 6, 0, 0), EsRect.Empty.Union(new EsPoint(5, 6)));
    }

    // ====================================================================
    // CenterRect
    // ====================================================================

    [Fact]
    internal void CenterRect_指定した矩形を中央へ配置する()
    {
        Assert.Equal(new EsRect(45, 40, 10, 20), new EsRect(0, 0, 100, 100).CenterRect(new EsRect(0, 0, 10, 20)));
    }

    // ====================================================================
    // ResolveInnerEdges / ComputeInnerEdges
    // ====================================================================

    [Fact]
    internal void ResolveInnerEdges_四辺を上_下_左_右の順に返す()
    {
        var edges = new EsRect(10, 20, 100, 80).ResolveInnerEdges(thickness: 2);

        Assert.Equal(new EsRect(10, 20, 100, 2), edges[0]);
        Assert.Equal(new EsRect(10, 98, 100, 2), edges[1]);
        Assert.Equal(new EsRect(10, 22, 2, 76), edges[2]);
        Assert.Equal(new EsRect(108, 22, 2, 76), edges[3]);
    }

    [Fact]
    internal void ResolveInnerEdges_ComputeInnerEdgesと一致する()
    {
        var rect = new EsRect(10, 20, 100, 80);

        Assert.Equal(rect.ComputeInnerEdges(2), rect.ResolveInnerEdges(2).ToArray());
    }

    [Fact]
    internal void ResolveInnerEdges_範囲外のインデクサは例外()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new EsRect(0, 0, 10, 10).ResolveInnerEdges(1)[4]);
    }

    // ====================================================================
    // NearlyEquals / ToEsRectInt / タプル
    // ====================================================================

    [Fact]
    internal void NearlyEquals_許容誤差内なら真()
    {
        Assert.True(new EsRect(1, 2, 3, 4).NearlyEquals(new EsRect(1 + 1e-12, 2, 3, 4)));
        Assert.False(new EsRect(1, 2, 3, 4).NearlyEquals(new EsRect(1.001, 2, 3, 4)));
    }

    [Fact]
    internal void ToEsRectInt_既定は四捨五入で成分ごとに丸める()
    {
        Assert.Equal(new EsRectInt(2, 3, 4, 5), new EsRect(1.5, 2.5, 3.5, 4.5).ToEsRectInt());
    }

    [Fact]
    internal void ToEsRectInt_丸め方法を指定できる()
    {
        Assert.Equal(new EsRectInt(1, 1, 1, 1), new EsRect(1.9, 1.9, 1.9, 1.9).ToEsRectInt(EsRounding.Truncate));
    }

    [Fact]
    internal void FromTuple_ToTuple_タプルと往復する()
    {
        Assert.Equal(new EsRect(1, 2, 3, 4), EsRect.FromTuple((1, 2, 3, 4)));
        Assert.Equal((1, 2, 3, 4), new EsRect(1, 2, 3, 4).ToTuple());
    }

    [Fact]
    internal void Deconstruct_タプルへ分解できる()
    {
        var (x, y, width, height) = new EsRect(1, 2, 3, 4);

        Assert.Equal(1, x);
        Assert.Equal(2, y);
        Assert.Equal(3, width);
        Assert.Equal(4, height);
    }

    // ====================================================================
    // 区間の流儀（Interval）
    // ====================================================================

    [Fact]
    internal void Interval_既定は半開区間である()
    {
        Assert.Equal(EsRectInterval.HalfOpen, new EsRect(1, 2, 3, 4).Interval);
        Assert.Equal(EsRectInterval.HalfOpen, default(EsRect).Interval);
        Assert.Equal(EsRectInterval.HalfOpen, EsRect.Empty.Interval);
    }

    [Fact]
    internal void IsClosedInterval_閉区間の指定を判定する()
    {
        Assert.False(new EsRect(1, 2, 3, 4).IsClosedInterval);
        Assert.True(new EsRect(1, 2, 3, 4, EsRectInterval.Closed).IsClosedInterval);
    }

    [Fact]
    internal void WithInterval_幾何を保ったまま流儀だけを差し替える()
    {
        var rect = new EsRect(1, 2, 3, 4).WithInterval(EsRectInterval.Closed);

        Assert.Equal(new EsRect(1, 2, 3, 4, EsRectInterval.Closed), rect);
        Assert.Equal(EsRectInterval.HalfOpen, rect.WithInterval(EsRectInterval.HalfOpen).Interval);
    }

    [Theory]
    [InlineData(EsRectInterval.Closed)]
    [InlineData(EsRectInterval.HalfOpen)]
    internal void 幾何を差し替えるメソッド_流儀を引き継ぐ(EsRectInterval interval)
    {
        var rect = new EsRect(10, 20, 100, 80, interval);
        var other = rect.WithInterval(EsRectInterval.HalfOpen);

        Assert.Equal(interval, rect.WithX(11).Interval);
        Assert.Equal(interval, rect.WithY(21).Interval);
        Assert.Equal(interval, rect.WithWidth(101).Interval);
        Assert.Equal(interval, rect.WithHeight(81).Interval);
        Assert.Equal(interval, rect.WithPosition(new EsPoint(12, 22)).Interval);
        Assert.Equal(interval, rect.WithSize(new EsSize(102, 82)).Interval);
        Assert.Equal(interval, rect.Offset(1, 2).Interval);
        Assert.Equal(interval, rect.Inflate(1, 2).Interval);
        Assert.Equal(interval, rect.Inflate(new EsThickness(1, 2, 3, 4)).Interval);
        Assert.Equal(interval, rect.Deflate(new EsThickness(1, 2, 3, 4)).Interval);
        Assert.Equal(interval, new EsRect(-10, -20, -100, -80, interval).Normalize().Interval);
        Assert.Equal(interval, rect.ClampToArea(new EsSize(1000, 1000)).Interval);
        Assert.Equal(interval, rect.Intersect(other).Interval);
        Assert.Equal(interval, rect.Union(other).Interval);
        Assert.Equal(interval, rect.Union(new EsPoint(0, 0)).Interval);
        Assert.Equal(interval, new EsRect(0, 0, 0, 0, interval).Union(new EsPoint(5, 5)).Interval);
    }

    [Fact]
    internal void CenterRect_流儀は配置する矩形から引き継ぐ()
    {
        var area = new EsRect(0, 0, 100, 100, EsRectInterval.HalfOpen);
        var content = new EsRect(0, 0, 40, 20, EsRectInterval.Closed);

        Assert.Equal(EsRectInterval.Closed, area.CenterRect(content).Interval);
    }

    [Fact]
    internal void NearlyEquals_流儀は比較しない()
    {
        Assert.True(new EsRect(1, 2, 3, 4).NearlyEquals(new EsRect(1, 2, 3, 4, EsRectInterval.Closed)));
    }
}
