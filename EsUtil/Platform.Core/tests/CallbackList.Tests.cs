using Xunit;

namespace EsUtil.Platform.Core.Tests;

/// <summary><see cref="CallbackList{T}"/> の単体テスト。</summary>
/// <remarks><c>event</c> を使わずに購読と通知を行う仕組み（値型の通知でヒープ確保を避けるための土台）を検証する。</remarks>
public class CallbackListTests
{
    /// <summary>登録した購読へ通知が届くことを検証する。</summary>
    [Fact]
    internal void Raise_登録した購読へ通知する()
    {
        var list = new CallbackList<int>();
        var received = new List<int>();

        list.Add(received.Add);
        list.Raise(42);

        Assert.Equal([42], received);
    }

    /// <summary>購読が 0 件でも例外にならないことを検証する（呼び出し側で件数を判定しない）。</summary>
    [Fact]
    internal void Raise_購読が無くても例外にならない()
    {
        var list = new CallbackList<int>();

        // 例外を投げないことが検証対象（0 件のときは通知の走査も行わない）
        list.Raise(1);
        list.Raise(2);
    }

    /// <summary>複数の購読へ登録順に通知することを検証する。</summary>
    [Fact]
    internal void Raise_複数の購読へ登録順に通知する()
    {
        var list = new CallbackList<int>();
        var order = new List<string>();

        list.Add(_ => order.Add("first"));
        list.Add(_ => order.Add("second"));
        list.Raise(1);

        Assert.Equal(["first", "second"], order);
    }

    /// <summary>返された <see cref="IDisposable"/> の破棄で解除されることを検証する。</summary>
    [Fact]
    internal void Subscription_破棄で解除される()
    {
        var list = new CallbackList<int>();
        var received = new List<int>();

        var subscription = list.Add(received.Add);
        list.Raise(1);
        subscription.Dispose();
        list.Raise(2);

        Assert.Equal([1], received);
    }

    /// <summary>二重の破棄が無害であることを検証する。</summary>
    [Fact]
    internal void Subscription_二重の破棄は無害()
    {
        var list = new CallbackList<int>();
        var subscription = list.Add(_ => { });

        subscription.Dispose();
        subscription.Dispose();
    }

    /// <summary>通知中に解除されても、その回の通知が同一の購読者集合へ届くことを検証する。</summary>
    /// <remarks>通知は登録のスナップショットへ行うため、途中の解除は次回の通知から効く。</remarks>
    [Fact]
    internal void Raise_通知中に解除されても同一の集合へ通知する()
    {
        var list = new CallbackList<int>();
        var received = new List<string>();
        IDisposable? second = null;

        list.Add(_ =>
        {
            received.Add("first");
            second?.Dispose();
        });
        second = list.Add(_ => received.Add("second"));

        list.Raise(1);

        Assert.Equal(["first", "second"], received);
    }

    /// <summary>通知中に追加された購読者へは今回の通知が届かない（次回から届く）ことを検証する。</summary>
    [Fact]
    internal void Raise_通知中に追加した購読者へは今回通知しない()
    {
        var list = new CallbackList<int>();
        var received = new List<string>();

        list.Add(_ =>
        {
            received.Add("first");
            list.Add(_ => received.Add("added"));
        });

        list.Raise(1);
        Assert.Equal(["first"], received);

        list.Raise(2);
        Assert.Equal(["first", "first", "added"], received);
    }

    /// <summary>null の購読が拒否されることを検証する。</summary>
    [Fact]
    internal void Add_nullは拒否する()
    {
        var list = new CallbackList<int>();

        Assert.Throws<ArgumentNullException>(() => list.Add(null!));
    }

    /// <summary><see cref="CallbackList{T}.Clear"/> で全購読が解除されることを検証する。</summary>
    [Fact]
    internal void Clear_全購読を解除する()
    {
        var list = new CallbackList<int>();
        var count = 0;

        list.Add(_ => count++);
        list.Add(_ => count++);
        list.Clear();
        list.Raise(1);

        Assert.Equal(0, count);
    }
}
