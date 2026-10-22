using Xunit;

namespace EsUtil.Feature.Thumbnail.Tests;

/// <summary>折り返し配置・行の高さ・下端揃え・可視範囲の単体テスト。</summary>
public class ThumbnailLayoutTests
{
    /// <summary>既定の設定（画像の幅 96 DIP）で 4 件を幅 300 DIP へ並べる。</summary>
    private static ThumbnailLayoutResult Arrange(double[] heights, double width = 300)
        => ThumbnailLayout.Resolve(width, 96, heights);

    /// <summary>利用可能な幅に収まる列数で折り返すことを検証する。</summary>
    [Fact]
    public void 幅に収まる列数で折り返す()
    {
        // 外枠の幅 = 96 + (2 + 12) * 2 = 124。 幅 300 では 2 列（余白 2 を両側に確保する）
        var layout = Arrange([96, 96, 96, 96, 96]);

        Assert.Equal(2, layout.Columns);
        Assert.Equal(3, layout.Rows);
        Assert.Equal(124, layout.SlotWidth, 6);
    }

    /// <summary>利用可能な幅が項目より狭い場合でも 1 列は並べることを検証する。</summary>
    [Fact]
    public void 幅が項目より狭い場合は1列にする()
    {
        var layout = Arrange([96, 96], width: 50);

        Assert.Equal(1, layout.Columns);
    }

    /// <summary>行の高さが行内の最大の画像の高さで決まることを検証する。</summary>
    [Fact]
    public void 行の高さは行内の最大の画像の高さで決まる()
    {
        // 1 行目は高さ 40 と 100、2 行目は 40 と 40（2 列）
        var layout = Arrange([40, 100, 40, 40], width: 300);

        var first = layout.Slot(0);
        var second = layout.Slot(1);

        // 外枠の高さ = 画像 + 影の余白（片側 14）+ 名前の行（30）。 項目ごとの高さは自分の画像で決まる
        Assert.Equal(40 + 28 + 30, first.Height, 6);
        Assert.Equal(100 + 28 + 30, second.Height, 6);

        // 下端揃えにより、行の下端は行内の最大の画像の高さで決まる
        Assert.Equal(first.Bottom, second.Bottom, 6);
    }

    /// <summary>項目と名前が行内で下端揃えになることを検証する。</summary>
    [Fact]
    public void 項目は行内で下端が揃う()
    {
        var layout = Arrange([40, 100], width: 300);

        Assert.Equal(layout.Slot(0).Bottom, layout.Slot(1).Bottom, 6);
        Assert.Equal(layout.Label(0).Bottom, layout.Label(1).Bottom, 6);
    }

    /// <summary>画像と名前の矩形が項目の外枠の内側に置かれることを検証する。</summary>
    [Fact]
    public void 画像と名前の矩形は外枠の内側に置かれる()
    {
        var layout = Arrange([96], width: 300);
        var slot = layout.Slot(0);

        Assert.True(slot.Contains(layout.Image(0)));
        Assert.True(slot.Contains(layout.Label(0)));
        Assert.Equal(layout.Image(0).Bottom, layout.Label(0).Y, 6);
    }

    /// <summary>位置から項目の添字を求めることを検証する。</summary>
    [Fact]
    public void 位置から項目の添字を求める()
    {
        var layout = Arrange([96, 96, 96], width: 300);

        Assert.Equal(0, layout.IndexAt(new EsPoint(10, 10)));
        Assert.Equal(1, layout.IndexAt(new EsPoint(layout.Slot(1).X + 1, layout.Slot(1).Y + 1)));
        Assert.Equal(-1, layout.IndexAt(new EsPoint(10_000, 10_000)));
    }

    /// <summary>ビューポートと交差する項目の範囲を返すことを検証する。</summary>
    [Fact]
    public void 見えている項目の範囲を返す()
    {
        var layout = Arrange([96, 96, 96, 96], width: 300);

        // 1 行目（0・1）の高さの途中まで見えている
        var range = layout.VisibleRange(0, layout.Slot(0).Height / 2);
        Assert.Equal(0, range.First);
        Assert.Equal(1, range.Last);

        // 2 行目だけが見えている
        var second = layout.Slot(2);
        var onlySecond = layout.VisibleRange(second.Y, second.Height);
        Assert.Equal(2, onlySecond.First);
        Assert.Equal(3, onlySecond.Last);
    }

    /// <summary>項目が無い場合に空の配置と範囲を返すことを検証する。</summary>
    [Fact]
    public void 項目が無い場合は空の配置を返す()
    {
        var layout = ThumbnailLayout.Resolve(300, 96, []);

        Assert.Equal(0, layout.Count);
        Assert.Equal(ThumbnailRange.Empty, layout.VisibleRange(0, 100));
        Assert.Equal(-1, layout.IndexAt(new EsPoint(0, 0)));
    }

    /// <summary>画像の表示高さが縦横比を保つことを検証する。</summary>
    [Fact]
    public void 画像の表示高さは縦横比を保つ()
    {
        Assert.Equal(48, ThumbnailLayout.ImageHeight(96, 200, 100), 6);
        Assert.Equal(192, ThumbnailLayout.ImageHeight(96, 100, 200), 6);
        Assert.Equal(96, ThumbnailLayout.ImageHeight(96, 0, 0), 6);
    }

    /// <summary>負の画像の高さを拒否することを検証する。</summary>
    [Fact]
    public void 負の高さは拒否する()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Arrange([-1]));
    }
}
