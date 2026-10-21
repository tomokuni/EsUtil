using System.Collections.Concurrent;

namespace EsUtil.Platform.Core.Caching;

/// <summary>生成にコストがかかる値をキー単位で共有する容量制限付きのキャッシュ（Flyweight・スレッド安全）。</summary>
/// <remarks>
/// <b>容量管理を単一所有する</b>: 上限に達した状態で新しいキーを要求されたら全体をクリアする
/// （無制限のメモリ増加を防ぐ）。 利用側は同じ処理を書き直さない。<br/>
/// <b>値が <see cref="IDisposable"/> なら破棄する</b>: 破棄しないと画素や GDI ハンドルが残る
/// （クリア時と、生成の競合で捨てる値の両方で破棄する）。<br/>
/// <b>生成はロックの外で行う</b>: 復号のような重い生成を直列化しない（上限判定・破棄・登録だけを直列化する）。<br/>
/// <b>UI に依存しない</b>: 4 つの UI 版とコントロールから使える（<c>EsUtil.Platform.Core</c> は最下層のため、
/// アプリ固有の層へ置くとコントロールから参照できない）。
/// </remarks>
/// <typeparam name="TKey">キャッシュのキー型（値の等価比較ができる型）。</typeparam>
/// <typeparam name="TValue">キャッシュする値の型。</typeparam>
public sealed class CapacityLimitedCache<TKey, TValue>
    where TKey : notnull
{
    /// <summary>既定の最大保持数。</summary>
    public const int DefaultCapacity = 128;

    /// <summary>上限判定・破棄・登録を直列化するロック。</summary>
    private readonly Lock _trimLock = new();

    /// <summary>キーから値を生成する関数。</summary>
    private readonly Func<TKey, TValue> _factory;

    /// <summary>キー → 値のキャッシュ。</summary>
    private readonly ConcurrentDictionary<TKey, TValue> _cache;

    /// <summary>最大保持数（1 以上）。</summary>
    private int _capacity;

    /// <summary>既定の容量（<see cref="DefaultCapacity"/>）のキャッシュを生成する。</summary>
    /// <param name="factory">キーから値を生成する関数。 null 不可。</param>
    /// <param name="comparer">キーの比較（既定は <see cref="EqualityComparer{T}.Default"/>）。</param>
    /// <exception cref="ArgumentNullException"><paramref name="factory"/> が null の場合。</exception>
    public CapacityLimitedCache(Func<TKey, TValue> factory, IEqualityComparer<TKey>? comparer = null)
        : this(factory, DefaultCapacity, comparer)
    {
    }

    /// <summary>容量を指定してキャッシュを生成する。</summary>
    /// <param name="factory">キーから値を生成する関数。 null 不可。</param>
    /// <param name="capacity">最大保持数（1 以上）。</param>
    /// <param name="comparer">キーの比較（既定は <see cref="EqualityComparer{T}.Default"/>）。</param>
    /// <exception cref="ArgumentNullException"><paramref name="factory"/> が null の場合。</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="capacity"/> が 1 未満の場合。</exception>
    public CapacityLimitedCache(Func<TKey, TValue> factory, int capacity, IEqualityComparer<TKey>? comparer = null)
    {
        ArgumentNullException.ThrowIfNull(factory);
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);

        _factory = factory;
        _capacity = capacity;
        _cache = comparer is null
            ? []
            : new ConcurrentDictionary<TKey, TValue>(comparer);
    }

    /// <summary>現在保持している値の数を取得する。</summary>
    /// <value>キャッシュ内の値の数（上限以下）。</value>
    public int Count => _cache.Count;

    /// <summary>最大保持数を取得または設定する。</summary>
    /// <value>1 以上の保持数。</value>
    /// <remarks>
    /// <b>設定できる理由</b>: 一覧が表示する件数のように<b>呼び出しのたびに決まる上限</b>がある
    /// （固定すると、表示件数より小さい値で表示中の値を破棄してしまう）。 値を小さくしても既存の値は破棄しない
    /// （次の登録で上限を超えていれば全体をクリアする）。
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">1 未満を設定した場合。</exception>
    public int Capacity
    {
        get
        {
            lock (_trimLock)
            {
                return _capacity;
            }
        }

        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, 1);
            lock (_trimLock)
            {
                _capacity = value;
            }
        }
    }

    /// <summary>キーに対応する値を取得する（未登録の場合はファクトリで生成して登録する）。</summary>
    /// <param name="key">キャッシュのキー。</param>
    /// <returns>キーに対応する値（同じキーには同じインスタンス）。</returns>
    /// <remarks>
    /// 1. 登録済みなら保持している値を返す（ファクトリを呼ばない）。<br/>
    /// 2. 無ければファクトリで生成する（ロックの外。 重い生成を直列化しない）。<br/>
    /// 3. 上限に達していれば全体をクリアしてから登録する（破棄できる値は破棄してから消す）。<br/>
    /// 4. 待っている間に別のスレッドが同じキーを登録していた場合は、その値を返す（作った値は破棄する）。
    /// </remarks>
    public TValue GetOrAdd(TKey key)
    {
        if (_cache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var value = _factory(key);

        lock (_trimLock)
        {
            if (_cache.TryGetValue(key, out var registered))
            {
                Dispose(value);
                return registered;
            }

            if (_cache.Count >= _capacity)
            {
                ClearCore();
            }

            _cache[key] = value;
            return value;
        }
    }

    /// <summary>キーに対応する値を取得する（生成しない）。</summary>
    /// <param name="key">キャッシュのキー。</param>
    /// <param name="value">保持している値。 無い場合は既定値。</param>
    /// <returns>保持している場合は true。</returns>
    public bool TryGetValue(TKey key, out TValue value) => _cache.TryGetValue(key, out value!);

    /// <summary>保持している値をすべて破棄して消す。</summary>
    /// <remarks>破棄できる値（<see cref="IDisposable"/>）は先に破棄する。</remarks>
    public void Clear()
    {
        lock (_trimLock)
        {
            ClearCore();
        }
    }

    /// <summary>破棄できる値であれば破棄する。</summary>
    /// <param name="value">対象の値。</param>
    private static void Dispose(TValue? value)
    {
        if (value is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    /// <summary>ロックを保持したまま、すべての値を破棄して消す。</summary>
    private void ClearCore()
    {
        foreach (var cached in _cache.Values)
        {
            Dispose(cached);
        }

        _cache.Clear();
    }
}
