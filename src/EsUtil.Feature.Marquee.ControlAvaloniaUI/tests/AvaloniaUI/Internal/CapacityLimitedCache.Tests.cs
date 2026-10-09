using Avalonia.Media;
using EsUtil.Feature.Marquee.AvaloniaUI.Internal;
using Xunit;

namespace EsUtil.Feature.Marquee.AvaloniaUI.Tests.Internal;

/// <summary><see cref="CapacityLimitedCache{TKey, TValue}"/> の純粋計算テスト（UI 非依存・並列実行）。</summary>
/// <remarks>
/// 容量制限付きキャッシュの規則（同一キーの共有・容量超過時の全クリア・引数の検証）のみを検証するため、<br/>
/// Avalonia ヘッドレス（<c>[AvaloniaFact]</c>）に依存せず <c>[Fact]</c> で並列実行する。<br/>
/// 値の生成にコストがかかる型（ブラシ等）の生成は UI スレッド非依存である。
/// </remarks>
public class CapacityLimitedCacheTests
{
    /// <summary>容量超過の検証に使う既定容量。</summary>
    private const int Capacity = CapacityLimitedCache<int, int>.DefaultCapacity;

    /// <summary>検証用のキャッシュ（キーから値を機械的に生成する）を生成する。</summary>
    /// <param name="capacity">最大保持数。</param>
    /// <returns>生成したキャッシュ。</returns>
    private static CapacityLimitedCache<int, int> CreateCache(int capacity)
        => new(static key => key * 10, capacity);

    [Fact]
    internal void GetOrCreate_同じキーは同じインスタンスを返す()
    {
        var cache = new CapacityLimitedCache<Color, SolidColorBrush>(static color => new SolidColorBrush(color));

        var first = cache.GetOrCreate(Colors.Red);
        var second = cache.GetOrCreate(Colors.Red);

        Assert.Same(first, second);
        Assert.Equal(1, cache.Count);
    }

    [Fact]
    internal void GetOrCreate_異なるキーは別インスタンスを返す()
    {
        var cache = new CapacityLimitedCache<Color, SolidColorBrush>(static color => new SolidColorBrush(color));

        var red = cache.GetOrCreate(Colors.Red);
        var blue = cache.GetOrCreate(Colors.Blue);

        Assert.NotSame(red, blue);
        Assert.Equal(2, cache.Count);
    }

    [Fact]
    internal void GetOrCreate_未登録のキーはファクトリで生成する()
    {
        var cache = CreateCache(2);

        Assert.Equal(30, cache.GetOrCreate(3));
    }

    [Fact]
    internal void GetOrCreate_容量超過時はクリアしてから登録する()
    {
        var cache = CreateCache(Capacity);

        // 容量を超えて登録し続ける（超過時は全クリアされ無制限の増加を防ぐ）
        for (var i = 0; i < Capacity + 5; i++)
        {
            cache.GetOrCreate(i);
        }

        // 全クリアされても例外なく値を返す（クリア後の登録で保持数は容量以下になる）
        Assert.True(cache.Count <= Capacity);
    }

    [Fact]
    internal void DefaultCapacity_は描画リソースの共有に使う既定値である()
    {
        Assert.Equal(128, CapacityLimitedCache<Color, SolidColorBrush>.DefaultCapacity);
    }

    [Fact]
    internal void コンストラクタ_ファクトリがnullなら例外を投げる()
    {
        Assert.Throws<ArgumentNullException>(() => new CapacityLimitedCache<int, int>(null!, 1));
    }

    [Fact]
    internal void コンストラクタ_容量が1未満なら例外を投げる()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new CapacityLimitedCache<int, int>(static key => key, 0));
    }
}
