using System.Runtime.Versioning;
using EsUtil.Platform.Core.Native.Win;
using Xunit;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace EsUtil.Platform.Core.Tests.Native;

/// <summary>WndProc サブクラス化（<see cref="WinGlobalHotKeyService{T}"/>）の実機テスト。</summary>
/// <remarks>
/// <b>検証の目的</b>: <c>SetWindowLongPtr</c> による差し替えから <c>SetWindowSubclass</c>（comctl32）へ
/// 移した後も、サブクラス プロシージャが実際に呼ばれることを確かめる
/// （登録が成功してもプロシージャが呼ばれない、という形の失敗はコンパイルでは分からない）。<br/>
/// <b>判定に <c>WM_HOTKEY</c> を使う理由</b>: 本クラスは <c>WM_HOTKEY</c> を処理済み（1）として返すため、
/// サブクラス化が効いているかどうかが戻り値に現れる（<c>DefWindowProc</c> は 0 を返す）。<br/>
/// <b>ウィンドウを作れない環境</b>: <c>CreateWindowEx</c> が失敗する場合は読み飛ばす。
/// </remarks>
[SupportedOSPlatform("windows10.0.17763")]
[Collection("Win32")]
public class WinGlobalHotKeyServiceTests : IDisposable
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

    /// <summary>サブクラス化が設定され、<c>WM_HOTKEY</c> が捕捉されることを検証する。</summary>
    [Fact]
    internal void Initialize_サブクラス化で_WM_HOTKEY_を捕捉する()
    {
        var hwnd = CreateHiddenWindow();
        using var service = new WinGlobalHotKeyService<string>();

        service.Initialize(hwnd);

        Assert.Equal(1, SendMessage(hwnd, PInvoke.WM_HOTKEY).Value);
    }

    /// <summary>破棄するとサブクラス化が解除され、<c>WM_HOTKEY</c> が元の WndProc へ渡ることを検証する。</summary>
    [Fact]
    internal void Dispose_サブクラス化を解除する()
    {
        var hwnd = CreateHiddenWindow();
        var service = new WinGlobalHotKeyService<string>();
        service.Initialize(hwnd);

        service.Dispose();

        Assert.Equal(0, SendMessage(hwnd, PInvoke.WM_HOTKEY).Value);
    }

    /// <summary>ウィンドウ破棄（<c>WM_NCDESTROY</c>）でも例外にならないことを検証する。</summary>
    [Fact]
    internal void ウィンドウ破棄_サブクラス化が自動で解除される()
    {
        var hwnd = CreateHiddenWindow();
        var service = new WinGlobalHotKeyService<string>();
        service.Initialize(hwnd);
        _ = PInvoke.DestroyWindow((HWND)hwnd);
        _window = IntPtr.Zero;

        // 破棄済みのウィンドウを解除しようとしても失敗するだけで、例外にはならない
        service.Dispose();
    }

    /// <summary>メッセージを同期送信する。</summary>
    /// <param name="hwnd">送信先のウィンドウ。</param>
    /// <param name="msg">送信するメッセージ。</param>
    /// <returns>ウィンドウ プロシージャの戻り値。</returns>
    private static LRESULT SendMessage(IntPtr hwnd, uint msg)
    {
        return PInvoke.SendMessage((HWND)hwnd, msg, default, default);
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
