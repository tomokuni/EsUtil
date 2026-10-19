using System.Runtime.InteropServices;

namespace EsUtil.Platform.Core.Native.Win;

/// <summary>ホストウィンドウのカーソル上書き抑止（Windows 実装: WndProc サブクラス化）。</summary>
/// <remarks>
/// 選択操作（Crop モード等）中の WM_SETCURSOR によるカーソル上書きを防ぐために使用する。<br/>
/// <c>SetWindowLongPtrW</c> で元の WndProc を置き換え、<c>CallWindowProcW</c> で元の WndProc に委譲する。<br/>
/// デリゲートはフィールドに保持し、WndProc の呼び出し中に GC されないようにする。<br/>
/// 破棄時（Detach / Dispose）に元の WndProc を復元する。<br/>
/// <br/>
/// Windows 固有のメッセージ番号（WM_SETCURSOR）とヒットテスト値（HTCLIENT）の判定は本クラスに内蔵し、<br/>
/// 利用側にはアタッチのみを公開する（プラットフォーム分離）。<br/>
/// <b>失敗は <see cref="IsAttached"/> で判別できる</b>: 差し替えに失敗した場合は false のままになり、
/// 例外にしない（抑止できないだけで選択操作は成立するため）。 記録は呼び出し側が行う。<br/>
/// ホスト（アプリ）が生成して注入する（テストではモックを注入できる）。<br/>
/// 継承不可。<br/>
/// </remarks>
public sealed class WinCursorOverrideGuard : ICursorOverrideGuard
{
    /// <summary>lParam 下位ワード（ヒットテスト結果）を取得するための 16bit マスク。</summary>
    private const int LowWordMask = 0xFFFF;

    /// <summary>サブクラス化中のウィンドウハンドル。 未アタッチ時は <see cref="IntPtr.Zero"/>。</summary>
    private IntPtr _hwnd;

    /// <summary>元の WndProc アドレス。</summary>
    private IntPtr _originalWndProc;

    /// <summary>サブクラス化した WndProc デリゲート。 GC 防止のためフィールドに保持する。</summary>
    private WinNative.WndProcDelegate? _wndProcDelegate;

    /// <summary>サブクラス化が設定されているかどうか。</summary>
    /// <remarks><see cref="Attach"/> が成功したかどうかを表す唯一の値である（差し替えに失敗した場合は false のまま）。</remarks>
    public bool IsAttached => _hwnd != IntPtr.Zero;

    /// <summary>指定したウィンドウの WndProc をサブクラス化する。</summary>
    /// <param name="handle">対象ウィンドウのネイティブハンドル。</param>
    /// <remarks>
    /// ネイティブハンドルを取得できない場合（デザインモード等）は何もしない（<see cref="IsAttached"/> は false のまま）。<br/>
    /// 既にアタッチ済みの場合は何もしない。<br/>
    /// <b>例外にしない理由</b>: 差し替えに失敗しても<b>カーソル形状が上書きされるだけ</b>で、
    /// 選択操作そのものは成立する（呼び出し側は <see cref="IsAttached"/> で成否を判断できる）。
    /// </remarks>
    public void Attach(EsPlatformHandle handle)
    {
        if (IsAttached) return;
        var hwnd = handle.Handle;
        if (hwnd == IntPtr.Zero) return;
        _hwnd = hwnd;
        _wndProcDelegate = WndProc;
        _originalWndProc = WinNative.SetWindowLongPtr(
            hwnd, WinNative.GWLP_WNDPROC, Marshal.GetFunctionPointerForDelegate(_wndProcDelegate));

        if (_originalWndProc == IntPtr.Zero)
        {
            // 差し替えに失敗した状態を残さない（IsAttached を false に戻し、呼び出し側が判別できるようにする）
            _hwnd = IntPtr.Zero;
            _wndProcDelegate = null;
        }
    }

    /// <summary>サブクラス化を解除し、元の WndProc を復元する。</summary>
    private void Detach()
    {
        if (!IsAttached) return;
        if (_originalWndProc != IntPtr.Zero)
            WinNative.SetWindowLongPtr(_hwnd, WinNative.GWLP_WNDPROC, _originalWndProc);
        _originalWndProc = IntPtr.Zero;
        _wndProcDelegate = null;
        _hwnd = IntPtr.Zero;
    }

    /// <summary>サブクラス化を解除する。</summary>
    public void Dispose() => Detach();

    /// <summary>サブクラス化した WndProc。 フックしたメッセージ以外は元の WndProc へ委譲する。</summary>
    /// <remarks>
    /// WM_SETCURSOR かつクライアント領域（HTCLIENT）内ではカーソル上書きを抑止し、<br/>
    /// メッセージを処理済み（TRUE）として元の WndProc へ委譲しない。<br/>
    /// アタッチ中は常に抑止する（現在のカーソル形状を維持する）。
    /// </remarks>
    private IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        // WM_SETCURSOR: OS がカーソル形状を上書きしようとしたときに送られる。
        if (msg == WinNative.WM_SETCURSOR)
        {
            // lParam 下位ワード = ヒットテスト結果。 クライアント領域内のときのみ抑止する。
            int hitTest = (int)(lParam.ToInt64() & LowWordMask);
            if (hitTest == WinNative.HTCLIENT)
                return new IntPtr(1); // 処理済み（TRUE）: OS によるカーソル上書きを抑止
        }
        return WinNative.CallWindowProc(_originalWndProc, hWnd, msg, wParam, lParam);
    }
}
