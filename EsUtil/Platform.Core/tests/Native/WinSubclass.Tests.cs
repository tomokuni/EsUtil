using EsUtil.Platform.Core.Native.Win;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;
using Xunit;

namespace EsUtil.Platform.Core.Tests.Native;

/// <summary><see cref="WinSubclass"/> の実機テスト（サブクラス化の登録キーと識別値の採番）。</summary>
/// <remarks>
/// <b>検証の目的</b>: <c>SetWindowSubclass</c> の登録キーは「サブクラス プロシージャと識別値の組」である。
/// プロシージャを全コンポーネントで共有するため、<b>識別値がクラスごとの連番だと同じウィンドウへの登録が衝突し</b>、
/// 後から付けた側が先の登録を黙って置き換える（ホットキーやカーソル抑止が片方だけ効かなくなる）。<br/>
/// <b>ウィンドウを作れない環境</b>: <c>CreateWindowEx</c> が失敗する場合は読み飛ばす。
/// </remarks>
[Collection("Win32")]
public class WinSubclassTests : IDisposable
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

    /// <summary>識別値がプロセス全体で一意に採番されることを検証する。</summary>
    [Fact]
    internal void NextId_呼ぶたびに異なる値を返す()
    {
        var first = WinSubclass.NextId();
        var second = WinSubclass.NextId();

        Assert.NotEqual(first, second);
        Assert.NotEqual(0u, first);
        Assert.NotEqual(0u, second);
    }

    /// <summary>同じウィンドウへ 2 つ登録しても、両方を解除できることを検証する。</summary>
    /// <remarks>
    /// 識別値が衝突すると 2 つ目の登録が 1 つ目を置き換え、1 つ目の解除が失敗する（戻り値が false）。
    /// </remarks>
    [Fact]
    internal unsafe void NextId_同じウィンドウへ2つ登録しても両方を解除できる()
    {
        var hwnd = (HWND)CreateHiddenWindow();
        var first = WinSubclass.CreateContext(Passthrough);
        var second = WinSubclass.CreateContext(Passthrough);
        var firstId = WinSubclass.NextId();
        var secondId = WinSubclass.NextId();

        try
        {
            Assert.True(PInvoke.SetWindowSubclass(hwnd, WinSubclass.Procedure, firstId, first.ReferenceData));
            Assert.True(PInvoke.SetWindowSubclass(hwnd, WinSubclass.Procedure, secondId, second.ReferenceData));

            Assert.True(PInvoke.RemoveWindowSubclass(hwnd, WinSubclass.Procedure, firstId));
            Assert.True(PInvoke.RemoveWindowSubclass(hwnd, WinSubclass.Procedure, secondId));
        }
        finally
        {
            first.Release();
            second.Release();
        }
    }

    /// <summary>同じウィンドウへ 2 つの機能を付けても、両方の登録が残ることを検証する。</summary>
    /// <remarks>
    /// 識別値がクラスごとの連番だと、同じウィンドウへ付けた 2 つが同じ <c>(プロシージャ, 識別値)</c> になり、
    /// 後から付けた側が先の登録を置き換える（先に付けた機能が黙って効かなくなる）。<br/>
    /// 採番の範囲を <see cref="WinSubclass.NextId"/> で挟み、その範囲の識別値を解除できるかで登録数を確かめる
    /// （登録が 1 件しか残っていなければ、解除できる識別値も 1 つだけになる）。
    /// </remarks>
    [Fact]
    internal unsafe void NextId_同じウィンドウへ2つの機能を付けても両方の登録が残る()
    {
        var hwnd = (HWND)CreateHiddenWindow();
        var guard = new WinCursorOverrideGuard();
        var hotKeys = new WinGlobalHotKeyService<string>();
        var firstId = WinSubclass.NextId();
        var removed = 0;

        try
        {
            guard.Attach(EsWindow.FromHandle((IntPtr)hwnd));
            hotKeys.Initialize((IntPtr)hwnd);
            var lastId = WinSubclass.NextId();

            for (var id = firstId; id < lastId; id++)
            {
                if (PInvoke.RemoveWindowSubclass(hwnd, WinSubclass.Procedure, id))
                {
                    removed++;
                }
            }

            Assert.Equal(2, removed);
        }
        finally
        {
            hotKeys.Dispose();
            guard.Dispose();
        }
    }

    /// <summary>メッセージを元のプロシージャへ委譲する処理（登録の成否だけを確かめるための中身）。</summary>
    /// <param name="hWnd">ウィンドウハンドル。</param>
    /// <param name="msg">ウィンドウメッセージ。</param>
    /// <param name="wParam">WPARAM。</param>
    /// <param name="lParam">LPARAM。</param>
    /// <returns>元のプロシージャの結果。</returns>
    private static LRESULT Passthrough(HWND hWnd, uint msg, WPARAM wParam, LPARAM lParam)
        => PInvoke.DefSubclassProc(hWnd, msg, wParam, lParam);

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
