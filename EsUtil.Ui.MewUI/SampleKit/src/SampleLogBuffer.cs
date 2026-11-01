using System.Collections.Concurrent;

namespace EsUtil.Ui.MewUI.SampleKit;

/// <summary>ログの行を保持する非同期のバッファ（書き込み側を待たせない）。</summary>
/// <remarks>
/// <b>目的</b>: ログの出力（Serilog のシンク）を UI スレッドの描画から切り離す。 書き込みは
/// <see cref="ConcurrentQueue{T}"/> への追加だけで、ロックも待機もしない（コントロールやアプリの動作を阻害しない）。<br/>
/// <b>上限</b>: 保持する行数には上限があり、超えたら古い行から捨てる（画面の更新が追い付かないときに
/// メモリが増え続けない）。<br/>
/// <b>読み出し</b>: UI 側が一定間隔で <see cref="Drain"/> を呼び、まとめて表示する。
/// </remarks>
public sealed class SampleLogBuffer
{
    /// <summary>保持する行の上限。</summary>
    private readonly int _capacity;

    /// <summary>書き込み待ちの行。</summary>
    private readonly ConcurrentQueue<string> _lines = new();

    /// <summary>現在保持している行数。</summary>
    private int _count;

    /// <summary>バッファを初期化する。</summary>
    /// <param name="capacity">保持する行の上限。</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="capacity"/> が 0 以下の場合。</exception>
    public SampleLogBuffer(int capacity = 2000)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        _capacity = capacity;
    }

    /// <summary>1 行を書き込む（待機しない）。</summary>
    /// <param name="line">書き込む行。</param>
    public void Write(string line)
    {
        _lines.Enqueue(line);
        Interlocked.Increment(ref _count);

        while (Volatile.Read(ref _count) > _capacity && _lines.TryDequeue(out _))
        {
            Interlocked.Decrement(ref _count);
        }
    }

    /// <summary>書き込み済みの行を取り出す（最大 <paramref name="max"/> 行）。</summary>
    /// <param name="into">取り出した行の追加先。</param>
    /// <param name="max">1 回で取り出す上限（画面の更新を 1 フレームに収めるため）。</param>
    /// <returns>取り出した行数。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="into"/> が null の場合。</exception>
    public int Drain(ICollection<string> into, int max = 200)
    {
        ArgumentNullException.ThrowIfNull(into);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(max);

        var taken = 0;
        while (taken < max && _lines.TryDequeue(out var line))
        {
            Interlocked.Decrement(ref _count);
            into.Add(line);
            taken++;
        }

        return taken;
    }
}
