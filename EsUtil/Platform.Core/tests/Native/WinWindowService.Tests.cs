using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Native.Win;
using EsUtil.Platform.Core.Window;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;
using Xunit;

namespace EsUtil.Platform.Core.Tests.Native;

/// <summary><see cref="WinWindowService"/> の実機テスト（自前のウィンドウを作って操作する）。</summary>
/// <remarks>
/// <b>自前のウィンドウを使う理由</b>: 他のアプリのウィンドウを操作すると利用者の環境を壊す。
/// 本クラスは <c>CreateWindowEx</c> で<b>非表示の自前ウィンドウ</b>を作り、終了時に破棄する。<br/>
/// <b>検証の目的</b>: Win32 の宣言を CsWin32 の生成へ移した後も、
/// <b>拡張スタイル・キャプチャ除外・位置とサイズ・退避</b>が実際に効くことを確かめる
/// （列挙型の値の取り違えは「呼び出しが成功するが意図と違う」形で現れ、コンパイルでは分からない）。<br/>
/// <b>ウィンドウを作れない環境</b>: 破棄できない・生成できない場合は <see cref="Assert.Skip(string)"/> で読み飛ばす
/// （ヘッドレスのセッションでは <c>CreateWindowEx</c> が失敗する）。
/// </remarks>
[Collection("Win32")]
public class WinWindowServiceTests : IDisposable
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

    /// <summary>拡張スタイルを設定できることを検証する（Alt+Tab の候補から除外する）。</summary>
    [Fact]
    internal void HideFromTaskSwitcher_拡張スタイルを設定する()
    {
        var handle = CreateHiddenWindow();

        new WinWindowService().HideFromTaskSwitcher(handle);

        var style = PInvoke.GetWindowLongPtr((HWND)handle.Handle, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE).ToInt64();
        Assert.NotEqual(0, style & (int)WINDOW_EX_STYLE.WS_EX_TOOLWINDOW);
    }

    /// <summary>キャプチャ除外の設定と解除が成功することを検証する。</summary>
    [Fact]
    internal void SetCaptureExclusion_設定と解除が成功する()
    {
        var handle = CreateHiddenWindow();
        var service = new WinWindowService();

        Assert.True(service.SetCaptureExclusion(handle, exclude: true));
        Assert.True(service.SetCaptureExclusion(handle, exclude: false));
    }

    /// <summary>位置とサイズを設定できることを検証する（要求した矩形がそのまま反映される）。</summary>
    [Fact]
    internal void SetBounds_位置とサイズを設定する()
    {
        var handle = CreateHiddenWindow();

        new WinWindowService().SetBounds(handle, new EsRectInt(40, 60, 120, 90));

        Assert.True(PInvoke.GetWindowRect((HWND)handle.Handle, out var rect));
        Assert.Equal(40, rect.left);
        Assert.Equal(60, rect.top);
        Assert.Equal(120, rect.Width);
        Assert.Equal(90, rect.Height);
    }

    /// <summary>退避でウィンドウが非表示になることを検証する。</summary>
    [Fact]
    internal void EvacuateWindow_ウィンドウを非表示にする()
    {
        var handle = CreateHiddenWindow();
        new WinWindowService().SetBounds(handle, new EsRectInt(0, 0, 64, 64));

        new WinWindowService().EvacuateWindow(handle);

        Assert.False(PInvoke.IsWindowVisible((HWND)handle.Handle));
    }

    /// <summary>クライアント座標と画面物理座標を往復変換できることを検証する。</summary>
    [Fact]
    internal void Coordinates_クライアントと画面を往復変換する()
    {
        var handle = CreateHiddenWindow();
        var service = new WinWindowService();

        Assert.True(service.TryClientToScreen(handle, new EsPointInt(10, 20), out var screen));
        Assert.True(service.TryScreenToClient(handle, screen, out var client));
        Assert.Equal(new EsPointInt(10, 20), client);
    }

    /// <summary>識別子が未設定の場合は変換せず、入力をそのまま返すことを検証する。</summary>
    [Fact]
    internal void Coordinates_識別子が未設定なら入力を返す()
    {
        var handle = default(EsPlatformHandle);
        var service = new WinWindowService();

        Assert.False(service.TryClientToScreen(handle, new EsPointInt(1, 2), out var screen));
        Assert.Equal(new EsPointInt(1, 2), screen);

        Assert.False(service.TryScreenToClient(handle, new EsPointInt(3, 4), out var client));
        Assert.Equal(new EsPointInt(3, 4), client);
    }

    /// <summary>非表示の自前ウィンドウを作る（作れない環境では読み飛ばす）。</summary>
    /// <returns>作ったウィンドウのハンドル。</returns>
    private unsafe EsPlatformHandle CreateHiddenWindow()
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

        return EsWindow.FromHandle(_window);
    }
}
