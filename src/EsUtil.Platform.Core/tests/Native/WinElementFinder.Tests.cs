using EsUtil.Platform.Core.Native.Win;
using EsUtil.Platform.Core.Geometry;
using Xunit;

namespace EsUtil.Platform.Core.Tests.Native;

/// <summary><see cref="WinElementFinder"/>（Win32 API 実装）の単体テスト。</summary>
/// <remarks>
/// Win32 API（EnumWindows / EnumDisplayMonitors / GetCursorPos）は実環境で動作するため、<br/>
/// 例外なく呼び出せることと、実モニター/実ウィンドウを列挙できることを確認する。<br/>
/// <see cref="WinElementFinder"/> は UI スレッド（Dispatcher）に依存せず <c>PixelRect</c> / <c>PlatformHandle</c> のみを扱うため、<br/>
/// Avalonia ヘッドレス（<c>[AvaloniaFact]</c>）ではなく実 OS 直列コレクション（<see cref="Win32Collection"/>）で <c>[Fact]</c> 検証する。<br/>
/// ヘッドレス環境ではウィンドウ数・モニター構成が OS と完全には同期しない場合があるため、<br/>
/// 件数は「0 件以上」の緩い検証とし、検出結果の契約（矩形・Title）のみを厳密に検証する。<br/>
/// <see cref="WinElementFinder"/> は Win32 へ直接問い合わせるため、モックを介さず実実装を検証する。
/// </remarks>
[Collection("Win32")]
public class WinElementFinderTests
{
    /// <summary>テスト対象のファインダーを生成する。</summary>
    private static WinElementFinder CreateFinder() => new();

    [Fact]
    internal void WinElementFinder_ListScreensが実モニターを列挙する()
    {
        var finder = CreateFinder();

        var screens = finder.ListScreens();

        // 実環境（Windows）では少なくとも 1 つ以上のモニターが存在する（0 件でも例外なく空を返す）
        Assert.True(screens.Count >= 1);
        // 各スクリーンは「Screen {インデックス}」の Title と正のサイズの矩形を持つ
        for (int i = 0; i < screens.Count; i++)
        {
            Assert.True(screens[i].IsDetected);
            Assert.Equal($"Screen {i}", screens[i].Title);
            Assert.True(screens[i].Bounds.Width > 0);
            Assert.True(screens[i].Bounds.Height > 0);
        }
    }

    [Fact]
    internal void WinElementFinder_ListWindowsが実ウィンドウを列挙する()
    {
        var finder = CreateFinder();

        var windows = finder.ListWindows();

        // 0 件でも例外なく列挙できる（ヘッドレス環境ではウィンドウが存在しない場合がある）
        Assert.NotNull(windows);
        foreach (var w in windows)
        {
            // 検出されたウィンドウは正のサイズの矩形を持つ（除外条件が適用されている）
            Assert.True(w.IsDetected);
            Assert.True(w.Bounds.Width > 0);
            Assert.True(w.Bounds.Height > 0);
        }
    }

    [Fact]
    internal void WinElementFinder_ListWindowsがクラス名を設定する()
    {
        var finder = CreateFinder();

        var windows = finder.ListWindows();

        // 実環境（Windows）でウィンドウが 1 つ以上ある場合は、クラス名が空でないことを確認する
        // （デスクトップウィンドウの特定・除外の検証に使用。 ヘッドレス環境では 0 件でスキップ）
        foreach (var w in windows)
        {
            Assert.NotNull(w.ClassName);
            Assert.NotEqual("", w.ClassName);
        }
    }

    [Fact]
    internal void WinElementFinder_FindScreenUnderCursorが実モニター内の位置を検出する()
    {
        var finder = CreateFinder();
        var screens = finder.ListScreens();
        if (screens.Count == 0) return; // モニターが無い環境ではスキップ

        // 最初のモニターの中央を検出できる
        var center = screens[0].Bounds.Center;

        var result = finder.FindScreenUnderCursor(center);

        Assert.True(result.IsDetected);
        Assert.Equal("Screen 0", result.Title);
        Assert.True(result.Bounds.Contains(center));
    }

    [Fact]
    internal void WinElementFinder_FindScreenUnderCursorが実モニター外の位置を未検出にする()
    {
        var finder = CreateFinder();
        var screens = finder.ListScreens();
        if (screens.Count == 0) return; // モニターが無い環境ではスキップ

        // 全モニターの外側の位置（左上に大きく負の位置）を未検出にする
        var left = screens.Min(s => s.Bounds.X);
        var top = screens.Min(s => s.Bounds.Y);
        var outside = new EsPointInt(left - 10000, top - 10000);

        var result = finder.FindScreenUnderCursor(outside);

        Assert.False(result.IsDetected);
    }

    [Fact]
    internal void WinElementFinder_FindWindowUnderCursorが例外なく呼び出せる()
    {
        var finder = CreateFinder();
        var screens = finder.ListScreens();
        if (screens.Count == 0) return; // モニターが無い環境ではスキップ

        var center = screens[0].Bounds.Center;

        // 例外なく呼び出せる（検出・未検出は実環境のウィンドウ構成に依存）
        _ = finder.FindWindowUnderCursor(center);
    }

    [Fact]
    internal void WinElementFinder_TryGetWindowVisibleRectが実ウィンドウで例外なく動作する()
    {
        var finder = CreateFinder();
        var windows = finder.ListWindows();
        if (windows.Count == 0) return; // ウィンドウが無い環境ではスキップ

        // 検出された各ウィンドウで可視矩形が取得できる（最大化は GetWindowRect / 通常は DWM 影除外）
        foreach (var w in windows)
        {
            if (w.Handle is not { } handle) continue;

            var ok = finder.TryGetWindowVisibleRect(handle, out var rect);

            // 除外条件（0x0 サイズ）が適用済みのウィンドウのため、正のサイズの矩形が取得できる
            Assert.True(ok);
            Assert.True(rect.Width > 0);
            Assert.True(rect.Height > 0);
        }
    }
}
