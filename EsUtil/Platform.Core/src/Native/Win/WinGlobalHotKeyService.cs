using System.ComponentModel;
using System.Runtime.InteropServices;

namespace EsUtil.Platform.Core.Native.Win;

/// <summary>Windows 実装の <see cref="IGlobalHotKeyService{T}"/>。</summary>
/// <remarks>
/// RegisterHotKey / UnregisterHotKey Win32 API でホットキーを登録管理し、<br/>
/// WndProc サブクラス化で WM_HOTKEY メッセージを捕捉して <see cref="Subscribe"/> の購読者へ配信する。<br/>
/// P/Invoke 呼び出しは <c>WinNative</c> に委譲する。<br/>
/// デリゲートはフィールドに保持し、WndProc の呼び出し中に GC されないようにする。<br/>
/// <b>ログを出力しない</b>: 失敗の種類に応じて例外または戻り値で呼び出し側へ渡し、記録は呼び出し側が行う。<br/>
/// 継承不可。<br/>
/// </remarks>
/// <typeparam name="T">ホットキーに割り当てるアクションの識別子。</typeparam>
public sealed class WinGlobalHotKeyService<T> : IGlobalHotKeyService<T> where T : notnull
{
    private IntPtr _hWnd;
    private IntPtr _originalWndProc;
    private WinNative.WndProcDelegate? _wndProcDelegate;
    private int _currentId;
    private readonly Dictionary<int, T> _registeredHotKeys = [];
    private readonly CallbackList<T> _pressed = new();
    private bool _disposed;

    /// <inheritdoc/>
    public IDisposable Subscribe(Action<T> onPressed) => _pressed.Add(onPressed);

    /// <inheritdoc/>
    public void Initialize(IntPtr hWnd)
    {
        Detach();
        _hWnd = hWnd;
        if (_hWnd == IntPtr.Zero) return;

        _wndProcDelegate = WndProc;
        _originalWndProc = WinNative.SetWindowLongPtr(
            hWnd, WinNative.GWLP_WNDPROC, Marshal.GetFunctionPointerForDelegate(_wndProcDelegate));

        if (_originalWndProc == IntPtr.Zero)
        {
            // 差し替えに失敗すると、ホットキーを登録できても押下を検出できない（回復できないため例外にする）
            _hWnd = IntPtr.Zero;
            _wndProcDelegate = null;
            throw new Win32Exception(
                Marshal.GetLastWin32Error(), "ウィンドウ プロシージャを差し替えられませんでした（ホットキーを受信できません）。");
        }
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
            _ = WinNative.UnregisterHotKey(_hWnd, id);
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

    /// <summary>WndProc サブクラス化を解除し、元の WndProc を復元する。</summary>
    private void Detach()
    {
        if (_hWnd != IntPtr.Zero && _originalWndProc != IntPtr.Zero)
        {
            WinNative.SetWindowLongPtr(_hWnd, WinNative.GWLP_WNDPROC, _originalWndProc);
            _originalWndProc = IntPtr.Zero;
            _hWnd = IntPtr.Zero;
        }
        _wndProcDelegate = null;
    }

    /// <summary>サブクラス化した WndProc。 WM_HOTKEY メッセージを捕捉して対応するアクションを配信する。</summary>
    private IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == WinNative.WM_HOTKEY)
        {
            var id = checked((int)wParam);
            if (_registeredHotKeys.TryGetValue(id, out var action))
                _pressed.Raise(action);
            return new IntPtr(1);
        }
        return WinNative.CallWindowProc(_originalWndProc, hWnd, msg, wParam, lParam);
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
        var key = WinNative.KeyToWin32VirtualKey(setting.Key);
        if (!WinNative.RegisterHotKey(_hWnd, id, modifiers, key))
        {
            return false;
        }

        _registeredHotKeys[id] = action;
        return true;
    }
}
