using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Input;
using Xunit;

namespace EsUtil.Platform.Core.Tests.Input;

/// <summary>ダブルクリックの判定（時間・距離・リセット）を検証する。</summary>
public sealed class DoubleClickTrackerTests
{
    /// <summary>間隔内かつ距離内の 2 回目の押下だけをダブルクリックとする。</summary>
    [Fact]
    internal void Press_間隔内で近い位置ならダブルクリックになる()
    {
        var tracker = new DoubleClickTracker();
        var start = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        Assert.False(tracker.Press(new EsPoint(10, 10), start));
        Assert.True(tracker.Press(new EsPoint(12, 11), start.AddMilliseconds(200)));
        Assert.False(tracker.Press(new EsPoint(12, 11), start.AddMilliseconds(800)));
    }

    /// <summary>距離が離れた押下はダブルクリックとしない。</summary>
    [Fact]
    internal void Press_距離が離れていればダブルクリックにならない()
    {
        var tracker = new DoubleClickTracker();
        var start = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        Assert.False(tracker.Press(new EsPoint(10, 10), start));
        Assert.False(tracker.Press(new EsPoint(14, 10), start.AddMilliseconds(100)));
    }

    /// <summary>リセットすると次の押下をダブルクリックとしない。</summary>
    [Fact]
    internal void Reset_次の押下をダブルクリックとしない()
    {
        var tracker = new DoubleClickTracker();
        var start = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        Assert.False(tracker.Press(new EsPoint(10, 10), start));
        tracker.Reset();
        Assert.False(tracker.Press(new EsPoint(10, 10), start.AddMilliseconds(100)));
    }

    /// <summary>間隔と距離は安全域（正の値・0 以上）を要求する。</summary>
    [Fact]
    internal void コンストラクター_不正な値は例外にする()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new DoubleClickTracker(TimeSpan.Zero));
        Assert.Throws<ArgumentOutOfRangeException>(() => new DoubleClickTracker(distance: -1));
    }
}
