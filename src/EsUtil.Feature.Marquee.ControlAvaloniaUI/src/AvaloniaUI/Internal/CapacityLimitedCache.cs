using System.Collections.Concurrent;

// 複製元: EsUtil.SaveEvidence.Platform.AvaloniaUI.Imaging（独立パッケージ用の意図的な複製。 .github/依存関係.md §4.1）
namespace EsUtil.Feature.Marquee.AvaloniaUI.Internal;

/// <summary>生成にコストがかかる値をキー単位で共有する容量制限付きキャッシュ（Flyweight・スレッド安全）。</summary>
/// <remarks>
/// 値はキーを受け取るファクトリ関数で生成する（ブラシ・ペン等の型ごとにキャッシュクラスを作らない）。<br/>
/// 容量に達した状態で新しいキーを要求された場合は全体をクリアする（無制限のメモリ増加を防ぐ）。<br/>
/// 容量管理（しきい値・超過時の全クリア）を本型が単一所有するため、利用側は同じ処理を書き直さない。<br/>
/// UI スレッドに依存しないため <c>[Fact]</c> で並列検証できる。
/// </remarks>
/// <typeparam name="TKey">キャッシュのキー型（値の等価比較ができる型）。</typeparam>
/// <typeparam name="TValue">キャッシュする値の型。</typeparam>
internal sealed class CapacityLimitedCache<TKey, TValue> where TKey : notnull
{
    /// <summary>キャッシュの既定の最大保持数。</summary>
    /// <remarks>描画リソース（ブラシ・ペン）の共有に使う既定値である。</remarks>
    public const int DefaultCapacity = 128;

    /// <summary>キーから値を生成する関数。</summary>
    private readonly Func<TKey, TValue> _factory;

    /// <summary>キャッシュの最大保持数（1 以上）。</summary>
    private readonly int _capacity;

    /// <summary>キー → 値のキャッシュ。</summary>
    private readonly ConcurrentDictionary<TKey, TValue> _cache = [];

    /// <summary>既定容量（<see cref="DefaultCapacity"/>）のキャッシュを生成する。</summary>
    /// <param name="factory">キーから値を生成する関数。 null 不可。</param>
    /// <exception cref="ArgumentNullException"><paramref name="factory"/> が null の場合。</exception>
    public CapacityLimitedCache(Func<TKey, TValue> factory) : this(factory, DefaultCapacity)
    {
    }

    /// <summary>指定した容量のキャッシュを生成する。</summary>
    /// <param name="factory">キーから値を生成する関数。 null 不可。</param>
    /// <param name="capacity">キャッシュの最大保持数（1 以上）。</param>
    /// <exception cref="ArgumentNullException"><paramref name="factory"/> が null の場合。</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="capacity"/> が 1 未満の場合。</exception>
    public CapacityLimitedCache(Func<TKey, TValue> factory, int capacity)
    {
        ArgumentNullException.ThrowIfNull(factory);

        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);

        _factory = factory;
        _capacity = capacity;
    }

    /// <summary>現在保持している値の数を取得する。</summary>
    /// <value>キャッシュ内の値の数（容量以下）。</value>
    public int Count => _cache.Count;

    /// <summary>キーに対応する値を取得する（未登録の場合はファクトリで生成して登録する）。</summary>
    /// <param name="key">キャッシュのキー。</param>
    /// <returns>キーに対応する値（同じキーには常に同じインスタンス）。</returns>
    /// <remarks>
    /// 処理フロー:<br/>
    /// 1. 登録済みの場合は保持している値を返す（ファクトリを呼ばない）。<br/>
    /// 2. 保持数が容量に達している場合は全体をクリアしてから登録する（登録直後に容量を超えない）。<br/>
    /// 3. ファクトリで生成した値を登録して返す。
    /// </remarks>
    public TValue GetOrCreate(TKey key)
    {
        if (_cache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var value = _factory(key);
        if (_cache.Count >= _capacity)
        {
            _cache.Clear();
        }

        _cache[key] = value;
        return value;
    }
}
