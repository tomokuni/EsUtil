using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using EsUtil.Platform.Core.Common;
using EsUtil.Platform.Core.Input;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Input.KeyboardAndMouse;

namespace EsUtil.Platform.Core.Native.Win;

/// <summary>Windows 実装の <see cref="IGlobalHotKeyService{T}"/>。</summary>
/// <remarks>
/// RegisterHotKey / UnregisterHotKey Win32 API でホットキーを登録管理し、<br/>
/// WndProc サブクラス化（<c>SetWindowSubclass</c>）で WM_HOTKEY メッセージを捕捉して <see cref="Subscribe"/> の購読者へ配信する。<br/>
/// <b>Win32 の宣言は CsWin32 が生成する</b>（<c>src/NativeMethods.txt</c>）。 本クラスは <c>PInvoke</c> を直接呼ぶ。<br/>
/// サブクラス プロシージャはフィールドに保持し、呼び出し中に GC されないようにする。<br/>
/// <b>ログを出力しない</b>: 失敗の種類に応じて例外または戻り値で呼び出し側へ渡し、記録は呼び出し側が行う。<br/>
/// 継承不可。<br/>
/// </remarks>
/// <typeparam name="T">ホットキーに割り当てるアクションの識別子。</typeparam>
[SupportedOSPlatform("windows10.0.17763")]
public sealed class WinGlobalHotKeyService<T> : IGlobalHotKeyService<T> where T : notnull
{
    /// <summary><c>SetWindowSubclass</c> に渡すサブクラスの識別値（クラス内で共有する連番）。</summary>
    /// <remarks>識別値は<b>ウィンドウごとに一意</b>であればよい（comctl32 がウィンドウ単位で管理する）。</remarks>
    private static nuint s_lastSubclassId;

    private IntPtr _hWnd;

    /// <summary>サブクラス プロシージャへ渡す文脈。 未初期化時は null。</summary>
    /// <remarks>
    /// <b>デリゲートを保持しない理由</b>: CsWin32 の <c>SUBCLASSPROC</c> はアンマネージド関数ポインターであり、
    /// GC の対象ではない（<see cref="WinSubclass"/>）。 代わりに<b>インスタンスを指す文脈</b>を保持する。
    /// </remarks>
    private WinSubclass.Context? _context;

    /// <summary><c>SetWindowSubclass</c> へ渡した識別値（解除時に同じ値を返す）。</summary>
    private nuint _subclassId;

    private int _currentId;
    private readonly Dictionary<int, T> _registeredHotKeys = [];
    private readonly CallbackList<T> _pressed = new();
    private bool _disposed;

    /// <inheritdoc/>
    public IDisposable Subscribe(Action<T> onPressed) => _pressed.Add(onPressed);

    /// <inheritdoc/>
    public unsafe void Initialize(IntPtr hWnd)
    {
        Detach();
        _hWnd = hWnd;
        if (_hWnd == IntPtr.Zero) return;

        var context = _context ??= WinSubclass.CreateContext(WndProc);
        var id = ++s_lastSubclassId;
        if (!PInvoke.SetWindowSubclass((HWND)hWnd, WinSubclass.Procedure, id, context.ReferenceData))
        {
            // サブクラス化に失敗すると、ホットキーを登録できても押下を検出できない（回復できないため例外にする）
            _hWnd = IntPtr.Zero;
            throw new Win32Exception(
                Marshal.GetLastWin32Error(), "ウィンドウ プロシージャを差し替えられませんでした（ホットキーを受信できません）。");
        }

        _subclassId = id;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// 失敗した組を返す（他のアプリが同じ組み合わせを使用している場合にも起こり、
    /// <b>そのホットキーが機能しないだけ</b>でアプリは継続できる）。
    /// </remarks>
    public IReadOnlyList<(T Action, HotKeySetting Setting)> RegisterAll(
        IReadOnlyList<(T Action, HotKeySetting Setting)> registrations)
    {
        ArgumentNullException.ThrowIfNull(registrations);
        // 全解除は一度だけ行う（RegisterCore 内では解除しない。 空辞書の無駄なスキャンを回避）
        UnregisterAll();

        var failed = new List<(T Action, HotKeySetting Setting)>();
        foreach (var (action, setting) in registrations)
        {
            if (!RegisterCore(action, setting))
            {
                failed.Add((action, setting));
            }
        }

        return failed;
    }

    /// <inheritdoc/>
    public void UnregisterAll()
    {
        foreach (var id in _registeredHotKeys.Keys)
        {
            _ = PInvoke.UnregisterHotKey((HWND)_hWnd, id);
        }
        _registeredHotKeys.Clear();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (!_disposed)
        {
            UnregisterAll();
            Detach();
            _pressed.Clear();
            _disposed = true;
        }
    }

    /// <summary>WndProc サブクラス化を解除する。</summary>
    /// <param name="hWnd">サブクラス化したウィンドウのハンドル。</param>
    private unsafe void DetachCore(IntPtr hWnd)
    {
        if (_context is null) return;
        _ = PInvoke.RemoveWindowSubclass((HWND)hWnd, WinSubclass.Procedure, _subclassId);
        _subclassId = 0;
    }

    /// <summary>WndProc サブクラス化を解除する。</summary>
    private void Detach()
    {
        if (_hWnd != IntPtr.Zero)
        {
            DetachCore(_hWnd);
            _hWnd = IntPtr.Zero;
        }

        _context?.Release();
        _context = null;
    }

    /// <summary>サブクラス化した WndProc。 WM_HOTKEY メッセージを捕捉して対応するアクションを配信する。</summary>
    /// <param name="hWnd">ウィンドウハンドル。</param>
    /// <param name="msg">ウィンドウメッセージ。</param>
    /// <param name="wParam">WPARAM（ホットキーの ID）。</param>
    /// <param name="lParam">LPARAM。</param>
    /// <returns>メッセージの処理結果。</returns>
    private LRESULT WndProc(HWND hWnd, uint msg, WPARAM wParam, LPARAM lParam)
    {
        // WM_NCDESTROY: ウィンドウ破棄の最終段階。 解除しないと comctl32 が本プロシージャへの参照を保持し続ける。
        if (msg == PInvoke.WM_NCDESTROY)
        {
            DetachCore((IntPtr)hWnd);
            _hWnd = IntPtr.Zero;
            _context?.Release();
            _context = null;
        }
        else if (msg == PInvoke.WM_HOTKEY)
        {
            var id = checked((int)(nint)(nuint)wParam);
            if (_registeredHotKeys.TryGetValue(id, out var action))
                _pressed.Raise(action);
            return (LRESULT)1;
        }
        return PInvoke.DefSubclassProc(hWnd, msg, wParam, lParam);
    }

    /// <summary>1 件のホットキーを登録する。</summary>
    /// <param name="action">押下時に配信するアクション。</param>
    /// <param name="setting">登録する組み合わせ。</param>
    /// <returns>登録できた場合は <see langword="true"/>。</returns>
    /// <remarks>
    /// <see cref="RegisterAll"/> が事前に全解除しているため、個別解除は行わない。<br/>
    /// 登録に失敗した場合は <see langword="false"/> を返す（失敗したホットキーは <see cref="Subscribe"/> の購読者へ配信されない）。
    /// </remarks>
    private bool RegisterCore(T action, HotKeySetting setting)
    {
        var id = ++_currentId;
        var modifiers = (int)setting.Modifiers;
        var key = WinVirtualKey.KeyToWin32VirtualKey(setting.Key);
        if (!PInvoke.RegisterHotKey((HWND)_hWnd, id, (HOT_KEY_MODIFIERS)modifiers, key))
        {
            return false;
        }

        _registeredHotKeys[id] = action;
        return true;
    }
}
