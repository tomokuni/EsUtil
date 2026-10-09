using System.Globalization;
using System.Runtime.InteropServices;
using EsUtil.Platform.Core.Native.Logging;
using Microsoft.Extensions.Logging;

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
/// <b>OS 操作の失敗は記録する</b>（WndProc の差し替え）。<br/>
/// ホスト（アプリ）が生成して注入する（テストではモックを注入できる）。<br/>
/// 継承不可。<br/>
/// </remarks>
/// <remarks><see cref="WinCursorOverrideGuard"/> の新しいインスタンスを初期化します。</remarks>
/// <param name="logger">ロガー。 null の場合は出力なし。</param>
public sealed class WinCursorOverrideGuard(ILogger? logger = null) : ICursorOverrideGuard
{
    /// <summary>lParam 下位ワード（ヒットテスト結果）を取得するための 16bit マスク。</summary>
    private const int LowWordMask = 0xFFFF;

    /// <summary>サブクラス化中のウィンドウハンドル。 未アタッチ時は <see cref="IntPtr.Zero"/>。</summary>
    private IntPtr _hwnd;

    /// <summary>元の WndProc アドレス。</summary>
    private IntPtr _originalWndProc;

    /// <summary>サブクラス化した WndProc デリゲート。 GC 防止のためフィールドに保持する。</summary>
    private WinNative.WndProcDelegate? _wndProcDelegate;

    /// <summary>ロガー（null の場合は出力なし）。</summary>
    private readonly ILogger _log = logger.OrNullLogger();

    /// <summary>サブクラス化が設定されているかどうか。</summary>
    public bool IsAttached => _hwnd != IntPtr.Zero;

    /// <summary>指定したウィンドウの WndProc をサブクラス化する。</summary>
    /// <param name="handle">対象ウィンドウのネイティブハンドル。</param>
    /// <remarks>
    /// ネイティブハンドルを取得できない場合（デザインモード等）は何もしない（<see cref="IsAttached"/> は false のまま）。<br/>
    /// 既にアタッチ済みの場合は何もしない。<br/>
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
            // 差し替えに失敗すると OS によるカーソル上書きを抑止できない（OS 操作の失敗）
            _log.LogCursorGuardSubclassFailed(hwnd.ToString("X", CultureInfo.InvariantCulture));
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



