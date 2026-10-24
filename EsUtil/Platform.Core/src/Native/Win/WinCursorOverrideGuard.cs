using System.Runtime.Versioning;
using EsUtil.Platform.Core.Window;
using Windows.Win32;
using Windows.Win32.Foundation;

namespace EsUtil.Platform.Core.Native.Win;

/// <summary>ホストウィンドウのカーソル上書き抑止（Windows 実装: WndProc サブクラス化）。</summary>
/// <remarks>
/// 選択操作（Crop モード等）中の WM_SETCURSOR によるカーソル上書きを防ぐために使用する。<br/>
/// <c>SetWindowSubclass</c>（comctl32）でサブクラス化し、<c>DefSubclassProc</c> で元の WndProc に委譲する。<br/>
/// サブクラス プロシージャは <see cref="WinSubclass"/> が単一所有する（アンマネージド関数ポインターであり、GC の対象ではない）。<br/>
/// 破棄時（Detach / Dispose）とウィンドウ破棄時（WM_NCDESTROY）にサブクラス化を解除する。<br/>
/// <br/>
/// Windows 固有のメッセージ番号（WM_SETCURSOR）とヒットテスト値（HTCLIENT）の判定は本クラスに内蔵し、<br/>
/// 利用側にはアタッチのみを公開する（プラットフォーム分離）。<br/>
/// <b>失敗は <see cref="IsAttached"/> で判別できる</b>: サブクラス化に失敗した場合は false のままになり、
/// 例外にしない（抑止できないだけで選択操作は成立するため）。 記録は呼び出し側が行う。<br/>
/// ホスト（アプリ）が生成して注入する（テストではモックを注入できる）。<br/>
/// 継承不可。<br/>
/// </remarks>
[SupportedOSPlatform("windows10.0.17763")]
public sealed class WinCursorOverrideGuard : ICursorOverrideGuard
{
    /// <summary>lParam 下位ワード（ヒットテスト結果）を取得するための 16bit マスク。</summary>
    private const int LowWordMask = 0xFFFF;

    /// <summary><c>SetWindowSubclass</c> に渡すサブクラスの識別値（クラス内で共有する連番）。</summary>
    /// <remarks>
    /// 識別値は<b>ウィンドウごとに一意</b>であればよい（comctl32 がウィンドウ単位で管理する）。
    /// 衝突を避けるため連番にする。
    /// </remarks>
    private static nuint s_lastSubclassId;

    /// <summary>サブクラス化中のウィンドウハンドル。 未アタッチ時は <see cref="IntPtr.Zero"/>。</summary>
    private IntPtr _hwnd;

    /// <summary>サブクラス プロシージャへ渡す文脈。 未アタッチ時は null。</summary>
    /// <remarks>
    /// <b>デリゲートを保持しない理由</b>: CsWin32 の <c>SUBCLASSPROC</c> はアンマネージド関数ポインターであり、
    /// GC の対象ではない（<see cref="WinSubclass"/>）。 代わりに<b>インスタンスを指す文脈</b>を保持する。
    /// </remarks>
    private WinSubclass.Context? _context;

    /// <summary><c>SetWindowSubclass</c> へ渡した識別値（解除時に同じ値を返す）。</summary>
    private nuint _subclassId;

    /// <summary>サブクラス化が設定されているかどうか。</summary>
    /// <remarks><see cref="Attach"/> が成功したかどうかを表す唯一の値である（失敗した場合は false のまま）。</remarks>
    public bool IsAttached => _hwnd != IntPtr.Zero;

    /// <summary>指定したウィンドウの WndProc をサブクラス化する。</summary>
    /// <param name="handle">対象ウィンドウのネイティブハンドル。</param>
    /// <remarks>
    /// ネイティブハンドルを取得できない場合（デザインモード等）は何もしない（<see cref="IsAttached"/> は false のまま）。<br/>
    /// 既にアタッチ済みの場合は何もしない。<br/>
    /// <b>例外にしない理由</b>: サブクラス化に失敗しても<b>カーソル形状が上書きされるだけ</b>で、
    /// 選択操作そのものは成立する（呼び出し側は <see cref="IsAttached"/> で成否を判断できる）。
    /// </remarks>
    public unsafe void Attach(EsPlatformHandle handle)
    {
        if (IsAttached) return;
        var hwnd = handle.Handle;
        if (hwnd == IntPtr.Zero) return;

        // 文脈は 1 回だけ作る（WM_SETCURSOR はマウス移動のたびに届く）
        var context = _context ??= WinSubclass.CreateContext(WndProc);
        var id = ++s_lastSubclassId;
        if (!PInvoke.SetWindowSubclass((HWND)hwnd, WinSubclass.Procedure, id, context.ReferenceData))
        {
            // 失敗した状態を残さない（IsAttached を false に戻し、呼び出し側が判別できるようにする）
            return;
        }

        _hwnd = hwnd;
        _subclassId = id;
    }

    /// <summary>サブクラス化を解除する。</summary>
    /// <param name="hwnd">サブクラス化したウィンドウのハンドル。</param>
    private unsafe void DetachCore(IntPtr hwnd)
    {
        if (_context is null) return;
        _ = PInvoke.RemoveWindowSubclass((HWND)hwnd, WinSubclass.Procedure, _subclassId);
    }

    /// <summary>サブクラス化を解除する。</summary>
    private void Detach()
    {
        if (!IsAttached) return;
        DetachCore(_hwnd);
        _hwnd = IntPtr.Zero;
        _subclassId = 0;
    }

    /// <summary>サブクラス化を解除する。</summary>
    public void Dispose() => Detach();

    /// <summary>サブクラス化した WndProc。 フックしたメッセージ以外は元の WndProc へ委譲する。</summary>
    /// <param name="hWnd">ウィンドウハンドル。</param>
    /// <param name="msg">ウィンドウメッセージ。</param>
    /// <param name="wParam">WPARAM。</param>
    /// <param name="lParam">LPARAM。</param>
    /// <returns>メッセージの処理結果。</returns>
    /// <remarks>
    /// WM_SETCURSOR かつクライアント領域（HTCLIENT）内ではカーソル上書きを抑止し、<br/>
    /// メッセージを処理済み（TRUE）として元の WndProc へ委譲しない。<br/>
    /// アタッチ中は常に抑止する（現在のカーソル形状を維持する）。
    /// </remarks>
    private LRESULT WndProc(HWND hWnd, uint msg, WPARAM wParam, LPARAM lParam)
    {
        // WM_NCDESTROY: ウィンドウ破棄の最終段階。 解除しないと comctl32 が本プロシージャへの参照を保持し続ける。
        if (msg == PInvoke.WM_NCDESTROY)
        {
            DetachCore((IntPtr)hWnd);
            _hwnd = IntPtr.Zero;
            _subclassId = 0;
        }
        // WM_SETCURSOR: OS がカーソル形状を上書きしようとしたときに送られる。
        else if (msg == PInvoke.WM_SETCURSOR)
        {
            // lParam 下位ワード = ヒットテスト結果。 クライアント領域内のときのみ抑止する。
            int hitTest = (int)(nint)lParam & LowWordMask;
            if (hitTest == (int)PInvoke.HTCLIENT)
                return (LRESULT)1; // 処理済み（TRUE）: OS によるカーソル上書きを抑止
        }

        return PInvoke.DefSubclassProc(hWnd, msg, wParam, lParam);
    }
}
