using System.Runtime.Versioning;
using EsUtil.Platform.Core.Native.Win;
using EsUtil.Platform.Core.Window;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;
using Xunit;

namespace EsUtil.Platform.Core.Tests.Native;

/// <summary>WndProc サブクラス化（<see cref="WinCursorOverrideGuard"/>）の実機テスト。</summary>
/// <remarks>
/// <b>検証の目的</b>: <c>SetWindowSubclass</c>（comctl32）によるサブクラス化が<b>実 OS のウィンドウで成功する</b>こと。
/// 失敗すると <see cref="WinCursorOverrideGuard.IsAttached"/> が false のままになり、
/// カーソル上書きの抑止だけが静かに効かなくなる（例外にはならない）。<br/>
/// <b>ウィンドウを作れない環境</b>: <c>CreateWindowEx</c> が失敗する場合は読み飛ばす。
/// </remarks>
[SupportedOSPlatform("windows10.0.17763")]
[Collection("Win32")]
public class WinCursorOverrideGuardTests : IDisposable
{
    /// <summary>テストで作ったウィンドウ（終了時に破棄する）。</summary>
    private IntPtr _window;

    /// <summary>作ったウィンドウを破棄する。</summary>
    public void Dispose()
    {
        if (_window != IntPtr.Zero)
        {
            _ = PInvoke.DestroyWindow((HWND)_window);
            _window = IntPtr.Zero;
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>アタッチと解除で状態が切り替わることを検証する。</summary>
    [Fact]
    internal void Attach_サブクラス化を設定し_Dispose_で解除する()
    {
        var handle = new EsPlatformHandle(CreateHiddenWindow());
        using var guard = new WinCursorOverrideGuard();

        guard.Attach(handle);

        Assert.True(guard.IsAttached);
        guard.Dispose();
        Assert.False(guard.IsAttached);
    }

    /// <summary>ハンドルが 0 の場合は何もしない（デザインモード等）ことを検証する。</summary>
    [Fact]
    internal void Attach_ハンドルがゼロなら何もしない()
    {
        using var guard = new WinCursorOverrideGuard();

        guard.Attach(new EsPlatformHandle(IntPtr.Zero));

        Assert.False(guard.IsAttached);
    }

    /// <summary>二重解除でも例外にならないことを検証する。</summary>
    [Fact]
    internal void Dispose_二重に呼んでも例外にしない()
    {
        var handle = new EsPlatformHandle(CreateHiddenWindow());
        var guard = new WinCursorOverrideGuard();
        guard.Attach(handle);

        guard.Dispose();
        guard.Dispose();

        Assert.False(guard.IsAttached);
    }

    /// <summary>非表示の自前ウィンドウを作る（作れない環境では読み飛ばす）。</summary>
    /// <returns>作ったウィンドウのハンドル。</returns>
    private unsafe IntPtr CreateHiddenWindow()
    {
        _window = PInvoke.CreateWindowEx(
            WINDOW_EX_STYLE.WS_EX_TOOLWINDOW,
            "STATIC",
            "テスト用のウィンドウ",
            WINDOW_STYLE.WS_POPUP,
            0,
            0,
            64,
            64,
            default,
            default,
            default,
            null);

        if (_window == IntPtr.Zero)
        {
            Assert.Skip("この環境ではウィンドウを作成できない（ヘッドレス セッション）。");
        }

        return _window;
    }
}
