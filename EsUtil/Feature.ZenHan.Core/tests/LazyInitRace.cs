using System.Reflection;
using System.Runtime.Loader;

namespace EsUtil.Feature.ZenHan.Tests;

/// <summary>遅延初期化する静的メンバーの「初回アクセスの競合」を再現するヘルパー。</summary>
/// <remarks>
/// <b>独立した読み込み文脈を使う理由</b>: 静的フィールドは読み込み文脈（<see cref="AssemblyLoadContext"/>）ごとに別である。
/// 同じ文脈では最初のテストが値を取得した時点で初期化済みになり、以降は初期化の競合を再現できない。
/// 新しい文脈へ読み込み直すと<b>未初期化の状態</b>を用意できる。<br/>
/// <b>開始の同期</b>: スレッドを立てて開始ゲート（<see cref="Barrier"/>）で揃える。 揃えないと 1 本目が初期化を
/// 終えてから他が読むため、競合が起きない（<c>Parallel.For</c> は開始を揃えない）。
/// </remarks>
internal static class LazyInitRace
{
    /// <summary>同時に取得するスレッド数。</summary>
    private const int Degree = 16;

    /// <summary>未初期化の状態から並行に取得し、得られた値がすべて同一インスタンスかを確かめる。</summary>
    /// <param name="assemblyPath">対象のアセンブリのパス。</param>
    /// <param name="typeName">対象の型の完全名。</param>
    /// <param name="propertyName">取得する静的プロパティ名。</param>
    /// <param name="rounds">未初期化の状態を作り直して繰り返す回数。</param>
    /// <returns>同一インスタンスにならなかった回数（0 なら競合なし）。</returns>
    /// <remarks>
    /// 回数を重ねるのは、競合が必ず起きるわけではないため（起きる確率に依存する）。
    /// 文脈を作り直すたびに静的フィールドが未初期化へ戻る。
    /// </remarks>
    public static int CountMismatches(string assemblyPath, string typeName, string propertyName, int rounds = 20)
    {
        var mismatches = 0;
        for (var round = 0; round < rounds; round++)
        {
            var context = new AssemblyLoadContext($"LazyInitRace.{round}", isCollectible: true);
            try
            {
                var type = context.LoadFromAssemblyPath(assemblyPath).GetType(typeName, throwOnError: true)!;
                var getter = type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Static)!.GetGetMethod()!;
                if (!AllSame(Race(() => getter.Invoke(null, null))))
                {
                    mismatches++;
                }
            }
            finally
            {
                // 収集可能な文脈は明示的に破棄する（読み込み続けるとテスト プロセスのメモリが増える）
                context.Unload();
            }
        }

        return mismatches;
    }

    /// <summary>開始を揃えた複数スレッドで取得し、結果を返す。</summary>
    /// <param name="get">取得する処理。</param>
    /// <returns>各スレッドが得た値。</returns>
    private static object?[] Race(Func<object?> get)
    {
        using var start = new Barrier(Degree);
        var results = new object?[Degree];
        var threads = new Thread[Degree];
        for (var index = 0; index < Degree; index++)
        {
            var slot = index;
            threads[slot] = new Thread(() =>
            {
                // 全スレッドの取得開始を揃える（これが無いと初回アクセスが重ならない）
                start.SignalAndWait();
                results[slot] = get();
            })
            {
                IsBackground = true,
            };
            threads[slot].Start();
        }

        foreach (var thread in threads)
        {
            thread.Join();
        }

        return results;
    }

    /// <summary>すべての値が同じインスタンスかを判定する。</summary>
    /// <param name="values">判定する値。</param>
    /// <returns>すべて同じインスタンスなら true。</returns>
    private static bool AllSame(object?[] values)
    {
        for (var index = 1; index < values.Length; index++)
        {
            if (!ReferenceEquals(values[0], values[index]))
            {
                return false;
            }
        }

        return true;
    }
}
