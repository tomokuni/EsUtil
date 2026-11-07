using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;

namespace EsUtil.Platform.Core.Native.Win;

/// <summary>ウィンドウ プロシージャのサブクラス化（comctl32 の <c>SetWindowSubclass</c>）の共通部分。</summary>
/// <remarks>
/// <b>コールバックはアンマネージド関数ポインターにする</b>: CsWin32 は <c>SUBCLASSPROC</c> を
/// <c>delegate* unmanaged[Stdcall]</c> として生成する（実行時マーシャリングを使わない。 Native AOT で動く）。
/// そのため<b>デリゲートをフィールドに保持する必要はない</b>（関数ポインターは GC の対象ではない）。<br/>
/// <b>インスタンスは <c>dwRefData</c> で渡す</b>: <c>[UnmanagedCallersOnly]</c> を付けたメソッドは
/// <b>静的であり、ジェネリック型の中に置けない</b>。 そのため実体を 1 つの静的メソッドに集約し、
/// <b>どのインスタンスのメッセージかは <c>GCHandle</c> で渡す</b>（<see cref="Context"/>）。<br/>
/// <b>解除したら <c>GCHandle</c> を解放する</b>（解放しないとインスタンスが回収されない）。
/// </remarks>
internal static unsafe class WinSubclass
{
    /// <summary>サブクラス プロシージャが受け取るメッセージの処理。</summary>
    /// <param name="hWnd">ウィンドウハンドル。</param>
    /// <param name="msg">ウィンドウメッセージ。</param>
    /// <param name="wParam">WPARAM。</param>
    /// <param name="lParam">LPARAM。</param>
    /// <returns>メッセージの処理結果。</returns>
    internal delegate LRESULT Handler(HWND hWnd, uint msg, WPARAM wParam, LPARAM lParam);

    /// <summary>サブクラス プロシージャとして登録する関数ポインター。</summary>
    internal static readonly delegate* unmanaged[Stdcall]<HWND, uint, WPARAM, LPARAM, nuint, nuint, LRESULT> Procedure
        = &Dispatch;

    /// <summary>サブクラス識別値の採番（プロセス全体で一意）。</summary>
    /// <remarks>
    /// <c>SetWindowSubclass</c> の登録キーは「サブクラス プロシージャと識別値の組」であり、本クラスの
    /// プロシージャは全コンポーネントで共有する。 そのため識別値は<b>クラスごとではなくプロセス全体で一意</b>にする
    /// （クラス内の連番だと、同じウィンドウへ 2 つの機能を付けたときに後から付けた側が先の登録を置き換えてしまう）。
    /// </remarks>
    private static long s_lastSubclassId;

    /// <summary>サブクラス識別値を新しく採番する。</summary>
    /// <returns>0 以外の識別値（プロセス全体で一意）。</returns>
    internal static nuint NextId() => (nuint)Interlocked.Increment(ref s_lastSubclassId);

    /// <summary>インスタンスを指す文脈を生成する（<c>dwRefData</c> へ渡す値を持つ）。</summary>
    /// <param name="handler">メッセージの処理。</param>
    /// <returns>生成した文脈（<c>GCHandle</c> を保持する）。 使い終わったら <see cref="Context.Release"/> を呼ぶ。</returns>
    internal static Context CreateContext(Handler handler) => new(handler);

    /// <summary>サブクラス プロシージャの実体（すべてのインスタンスのメッセージがここへ来る）。</summary>
    /// <param name="hWnd">ウィンドウハンドル。</param>
    /// <param name="msg">ウィンドウメッセージ。</param>
    /// <param name="wParam">WPARAM。</param>
    /// <param name="lParam">LPARAM。</param>
    /// <param name="uIdSubclass">サブクラスの識別値（未使用）。</param>
    /// <param name="dwRefData">文脈（<see cref="Context"/> の <c>GCHandle</c>）。</param>
    /// <returns>メッセージの処理結果。</returns>
    /// <remarks>
    /// <b>例外をアンマネージド境界の外へ出さない</b>: 処理の中には利用者のコード（ホットキーの購読など）が入る。
    /// 境界を越えた例外はプロセスを終了させるため、捕捉して既定の処理へ落とす（記録は購読側が行う）。
    /// </remarks>
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static LRESULT Dispatch(
        HWND hWnd, uint msg, WPARAM wParam, LPARAM lParam, nuint uIdSubclass, nuint dwRefData)
    {
        _ = uIdSubclass;
        var context = dwRefData == 0 ? null : GCHandle.FromIntPtr((nint)dwRefData).Target as Context;
        if (context is null)
        {
            return PInvoke.DefSubclassProc(hWnd, msg, wParam, lParam);
        }

        try
        {
            return context.Handler(hWnd, msg, wParam, lParam);
        }
        catch (Exception ex) when (EsErrors.IsRecoverable(ex))
        {
            // アンマウント境界（UnmanagedCallersOnly）を越えて例外を伝播させない（越えるとプロセスが終了する）。
            // メッセージを既定の処理へ委ね、ウィンドウの操作は継続する（記録は利用者側の処理が行う）。
            return PInvoke.DefSubclassProc(hWnd, msg, wParam, lParam);
        }
    }

    /// <summary>サブクラス プロシージャへ渡す文脈（インスタンスと処理の組）。</summary>
    internal sealed class Context
    {
        /// <summary>文脈を指す <c>GCHandle</c>（<c>dwRefData</c> へ渡す値を作る）。</summary>
        private GCHandle _handle;

        /// <summary>文脈を生成する。</summary>
        /// <param name="handler">メッセージの処理。</param>
        internal Context(Handler handler)
        {
            Handler = handler;
            _handle = GCHandle.Alloc(this);
        }

        /// <summary>メッセージの処理。</summary>
        internal Handler Handler { get; }

        /// <summary><c>dwRefData</c> へ渡す値を取得する。</summary>
        internal nuint ReferenceData => (nuint)GCHandle.ToIntPtr(_handle);

        /// <summary>文脈を解放する（サブクラス化を解除した後に呼ぶ。 冪等）。</summary>
        internal void Release()
        {
            if (_handle.IsAllocated)
            {
                _handle.Free();
            }
        }
    }
}
