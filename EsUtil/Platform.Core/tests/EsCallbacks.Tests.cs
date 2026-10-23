using EsUtil.Platform.Core.Common;
using Xunit;

namespace EsUtil.Platform.Core.Tests;

/// <summary>購読の窓口（<see cref="EsCallbacks"/>）の単体テスト。</summary>
/// <remarks>
/// 通知の手順は <see cref="CallbackList{T}"/>（`Common`）が単一所有するため、ここでは<b>窓口が一覧を生成し、
/// その一覧が機能する</b>ことだけを検証する。
/// </remarks>
public class EsCallbacksTests
{
    [Fact]
    internal void Create_購読の一覧を生成する()
    {
        var list = EsCallbacks.Create<int>();
        var received = 0;
        using var subscription = list.Add(value => received = value);

        list.Raise(42);

        Assert.Equal(42, received);
    }

    [Fact]
    internal void Create_解除した購読へ通知しない()
    {
        var list = EsCallbacks.Create<int>();
        var count = 0;
        var subscription = list.Add(_ => count++);
        subscription.Dispose();

        list.Raise(1);

        Assert.Equal(0, count);
    }
}