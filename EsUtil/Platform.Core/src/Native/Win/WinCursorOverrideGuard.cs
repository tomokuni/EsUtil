using Windows.Win32.Foundation;
using Windows.Win32.UI.Shell;

namespace EsUtil.Platform.Core.Native.Win;

/// <summary>ホストウィンドウのカーソル上書き抑止（Windows 実装: WndProc サブクラス化）。</summary>
/// <remarks>
/// 選択操作（Crop モード等）中の WM_SETCURSOR によるカーソル上書きを防ぐために使用する。<br/>
/// <c>SetWindowSubclass</c>（comctl32）でサブクラス化し、<c>DefSubclassProc</c> で元の WndProc に委譲する。<br/>
/// サブクラス プロシージャはフィールドに保持し、呼び出し中に GC されないようにする。<br/>
/// 破棄時（Detach / Dispose）とウィンドウ破棄時（WM_NCDESTROY）にサブクラス化を解除する。<br/>
/// <br/>
/// Windows 固有のメッセージ番号（WM_SETCURSOR）とヒットテスト値（HTCLIENT）の判定は本クラスに内蔵し、<br/>
/// 利用側にはアタッチのみを公開する（プラットフォーム分離）。<br/>
/// <b>失敗は <see cref="IsAttached"/> で判別できる</b>: サブクラス化に失敗した場合は false のままになり、
/// 例外にしない（抑止できないだけで選択操作は成立するため）。 記録は呼び出し側が行う。<br/>
/// ホスト（アプリ）が生成して注入する（テストではモックを注入できる）。<br/>
/// 継承不可。<br/>
/// </remarks>
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

    /// <summary>サブクラス プロシージャ。 GC 防止と解除のためフィールドに保持する。</summary>
    private SUBCLASSPROC? _subclassProc;

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
    public void Attach(EsPlatformHandle handle)
    {
        if (IsAttached) return;
        var hwnd = handle.Handle;
        if (hwnd == IntPtr.Zero) return;

        // サブクラス プロシージャは 1 回だけ作る（WM_SETCURSOR はマウス移動のたびに届く）
        var subclassProc = _subclassProc ??= WndProc;
        var id = ++s_lastSubclassId;
        if (!WinNative.SetWindowSubclass(hwnd, subclassProc, id, 0))
        {
            // 失敗した状態を残さない（IsAttached を false に戻し、呼び出し側が判別できるようにする）
            return;
        }

        _hwnd = hwnd;
        _subclassId = id;
    }

    /// <summary>サブクラス化を解除する。</summary>
    /// <param name="hwnd">サブクラス化したウィンドウのハンドル。</param>
    private void DetachCore(IntPtr hwnd)
    {
        if (_subclassProc is null) return;
        _ = WinNative.RemoveWindowSubclass(hwnd, _subclassProc, _subclassId);
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
    /// <param name="uIdSubclass">サブクラスの識別値（<see cref="Attach"/> で渡した値）。</param>
    /// <param name="dwRefData"><see cref="Attach"/> で渡したコンテキスト値（本クラスでは未使用）。</param>
    /// <returns>メッセージの処理結果。</returns>
    /// <remarks>
    /// WM_SETCURSOR かつクライアント領域（HTCLIENT）内ではカーソル上書きを抑止し、<br/>
    /// メッセージを処理済み（TRUE）として元の WndProc へ委譲しない。<br/>
    /// アタッチ中は常に抑止する（現在のカーソル形状を維持する）。
    /// </remarks>
    private LRESULT WndProc(HWND hWnd, uint msg, WPARAM wParam, LPARAM lParam, nuint uIdSubclass, nuint dwRefData)
    {
        // WM_NCDESTROY: ウィンドウ破棄の最終段階。 解除しないと comctl32 が本プロシージャへの参照を保持し続ける。
        if (msg == WinNative.WM_NCDESTROY)
        {
            DetachCore((IntPtr)hWnd);
            _hwnd = IntPtr.Zero;
            _subclassId = 0;
        }
        // WM_SETCURSOR: OS がカーソル形状を上書きしようとしたときに送られる。
        else if (msg == WinNative.WM_SETCURSOR)
        {
            // lParam 下位ワード = ヒットテスト結果。 クライアント領域内のときのみ抑止する。
            int hitTest = (int)(nint)lParam & LowWordMask;
            if (hitTest == WinNative.HTCLIENT)
                return (LRESULT)1; // 処理済み（TRUE）: OS によるカーソル上書きを抑止
        }

        return WinNative.DefSubclassProc((IntPtr)hWnd, msg, (nint)(nuint)wParam, lParam);
    }
}
