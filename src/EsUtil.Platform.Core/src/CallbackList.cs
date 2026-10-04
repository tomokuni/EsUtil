namespace EsUtil.Platform.Core;

/// <summary>値型・<see cref="EventArgs"/> 非派生の値（<c>record</c> 等）を通知する購読の一覧を単一所有する。</summary>
/// <typeparam name="T">通知する値の型（値型・<see cref="EventArgs"/> 非派生のクラスを許容する）。</typeparam>
/// <remarks>
/// <b>目的</b>: <c>event</c> の型は <see cref="EventHandler"/> / <see cref="EventHandler{T}"/>（<c>T</c> は
/// <see cref="EventArgs"/> 派生）に限るという規則（CA1003）を守りつつ、<b>値型の通知でヒープ確保を避ける</b>。<br/>
/// <c>EventArgs</c> はクラス派生を要求するため、値型（<c>record struct</c>）の通知は <c>event</c> で公開できない。<br/>
/// そこで購読を <see cref="Add"/> で受け取り、通知を <see cref="Raise"/> で配る（<c>event</c> を使わない）。<br/>
/// <br/>
/// <b>通知中の購読変更に耐える</b>: <see cref="Raise"/> は登録のスナップショットへ通知するため、通知中に
/// <see cref="Add"/> した購読者や購読を解除した購読者がいても、その回の通知は影響を受けない。<br/>
/// 購読が 0 件のときはスナップショットを作らない（毎フレーム呼ばれる用途でも確保しない）。<br/>
/// <br/>
/// <b>購読の解除</b>: <see cref="Add"/> が返す <see cref="IDisposable"/> を破棄して行う（<c>-=</c> に相当する）。<br/>
/// 長期生存の対象へ購読する場合は、コントロールのビジュアルツリー切断などで必ず解除する。
/// </remarks>
public sealed class CallbackList<T>
{
    /// <summary>登録済みの購読（登録順に通知する）。</summary>
    private readonly List<Action<T>> _callbacks = [];

    /// <summary>購読を追加する。</summary>
    /// <param name="callback">通知を受け取る処理。 null 不可。</param>
    /// <returns>購読を解除する <see cref="IDisposable"/>（破棄すると <paramref name="callback"/> を一覧から外す）。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="callback"/> が null の場合。</exception>
    public IDisposable Add(Action<T> callback)
    {
        ArgumentNullException.ThrowIfNull(callback);
        _callbacks.Add(callback);
        return new Subscription(this, callback);
    }

    /// <summary>登録済みの全購読者へ通知する。</summary>
    /// <param name="value">通知する値。</param>
    /// <remarks>通知中の <see cref="Add"/> / 解除に影響されないよう、登録のスナップショットへ通知する。</remarks>
    public void Raise(T value)
    {
        if (_callbacks.Count == 0)
        {
            return;
        }

        foreach (var callback in _callbacks.ToArray())
        {
            callback(value);
        }
    }

    /// <summary>全購読を解除する（破棄時に購読者への参照を残さないために使う）。</summary>
    public void Clear() => _callbacks.Clear();

    /// <summary>1 件の購読を表す（破棄で解除する）。</summary>
    private sealed class Subscription : IDisposable
    {
        /// <summary>購読の一覧。</summary>
        private readonly CallbackList<T> _owner;

        /// <summary>通知を受け取る処理。</summary>
        private readonly Action<T> _callback;

        /// <summary>解除済みかどうか。</summary>
        private bool _disposed;

        /// <summary>購読を生成する。</summary>
        /// <param name="owner">購読の一覧。</param>
        /// <param name="callback">通知を受け取る処理。</param>
        internal Subscription(CallbackList<T> owner, Action<T> callback)
        {
            _owner = owner;
            _callback = callback;
        }

        /// <summary>購読を解除する（二重の破棄は無害）。</summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _owner._callbacks.Remove(_callback);
        }
    }
}
