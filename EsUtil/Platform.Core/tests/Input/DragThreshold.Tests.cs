using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Input;
using Xunit;

namespace EsUtil.Platform.Core.Tests.Input;

/// <summary>ドラッグの開始の判定（距離・軸・安全域）を検証する。</summary>
public sealed class DragThresholdTests
{
    /// <summary>既定の距離（4 DIP）以上動いたときだけドラッグとする。</summary>
    [Fact]
    internal void IsExceeded_既定の距離以上で動いたらドラッグになる()
    {
        var press = new EsPoint(10, 10);

        Assert.False(DragThreshold.IsExceeded(press, new EsPoint(13, 13)));
        Assert.True(DragThreshold.IsExceeded(press, new EsPoint(14, 10)));
        Assert.True(DragThreshold.IsExceeded(press, new EsPoint(10, 14)));
    }

    /// <summary>どちらか一方の軸で超えれば（斜めでも）ドラッグとする。</summary>
    [Fact]
    internal void IsExceeded_片方の軸で超えればドラッグになる()
    {
        var press = new EsPoint(100, 100);

        Assert.True(DragThreshold.IsExceeded(press, new EsPoint(100, 90)));
        Assert.True(DragThreshold.IsExceeded(press, new EsPoint(111, 101)));
    }

    /// <summary>距離は 0 以上を要求する（負の距離で常に真にしない）。</summary>
    [Fact]
    internal void IsExceeded_負の距離は例外にする()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => DragThreshold.IsExceeded(default, default, distance: -1));
    }
}
