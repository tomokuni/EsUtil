using EsUtil.Platform.Core.Caching;
using Xunit;

namespace EsUtil.Platform.Core.Tests.Caching;

/// <summary><see cref="CapacityLimitedCache{TKey, TValue}"/> の容量管理を検証するテスト（UI 非依存・並列実行）。</summary>
/// <remarks>
/// 上限に達したら全体をクリアすること、破棄できる値は破棄してから消すこと、
/// 生成の競合で捨てる値も破棄することを固定する。
/// </remarks>
public class CapacityLimitedCacheTests
{
    [Fact]
    internal void GetOrAdd_同じキーは同じインスタンスを返す()
    {
        var calls = 0;
        var cache = new CapacityLimitedCache<string, object>(_ =>
        {
            calls++;
            return new object();
        });

        var first = cache.GetOrAdd("a");
        var second = cache.GetOrAdd("a");

        Assert.Same(first, second);
        Assert.Equal(1, calls);
        Assert.Equal(1, cache.Count);
    }

    [Fact]
    internal void GetOrAdd_上限に達したら全体をクリアする()
    {
        var cache = new CapacityLimitedCache<int, int>(static key => key, 2);

        _ = cache.GetOrAdd(1);
        _ = cache.GetOrAdd(2);
        Assert.Equal(2, cache.Count);

        _ = cache.GetOrAdd(3);

        Assert.Equal(1, cache.Count);
        Assert.True(cache.TryGetValue(3, out _));
        Assert.False(cache.TryGetValue(1, out _));
    }

    [Fact]
    internal void GetOrAdd_破棄できる値はクリア時に破棄する()
    {
        var disposed = new List<int>();
        var cache = new CapacityLimitedCache<int, DisposableValue>(
            key => new DisposableValue(key, disposed), 2);

        _ = cache.GetOrAdd(1);
        _ = cache.GetOrAdd(2);
        _ = cache.GetOrAdd(3);

        Assert.Contains(1, disposed);
        Assert.Contains(2, disposed);
        Assert.DoesNotContain(3, disposed);
    }

    [Fact]
    internal void Clear_保持している値をすべて破棄する()
    {
        var disposed = new List<int>();
        var cache = new CapacityLimitedCache<int, DisposableValue>(
            key => new DisposableValue(key, disposed));

        _ = cache.GetOrAdd(1);
        _ = cache.GetOrAdd(2);
        cache.Clear();

        Assert.Equal(0, cache.Count);
        Assert.Equal([1, 2], disposed.Order());
    }

    [Fact]
    internal void Capacity_1未満は例外()
    {
        var cache = new CapacityLimitedCache<int, int>(static key => key);

        Assert.Throws<ArgumentOutOfRangeException>(() => cache.Capacity = 0);
    }

    [Fact]
    internal void 生成_はnullの関数を受け付けない()
        => Assert.Throws<ArgumentNullException>(() => new CapacityLimitedCache<int, int>(null!));

    [Fact]
    internal void 容量_は1未満を受け付けない()
        => Assert.Throws<ArgumentOutOfRangeException>(() => new CapacityLimitedCache<int, int>(static key => key, 0));

    /// <summary>破棄を観測するための値。</summary>
    /// <param name="key">この値のキー。</param>
    /// <param name="disposed">破棄されたキーの記録先。</param>
    private sealed class DisposableValue(int key, List<int> disposed) : IDisposable
    {
        /// <summary>破棄されたことを記録する。</summary>
        public void Dispose() => disposed.Add(key);
    }
}
