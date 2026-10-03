using EsUtil.Platform.Core.Native.Win;
using Xunit;

namespace EsUtil.Platform.Core.Tests.Native;

/// <summary><see cref="WinElementFinder.IsExcludedClassName"/>（タスクバー / デスクトップウィンドウの除外判定）のテスト。</summary>
/// <remarks>
/// クラス名の文字列判定のみを検証する純粋関数のため、Avalonia ヘッドレス（<c>[AvaloniaFact]</c>）に依存せず<br/>
/// <c>[Fact]</c> で並列実行する（テスト全体の高速化）。<br/>
/// デスクトップにカーソルを合わせたとき WorkerW（デスクトップ壁紙）が選択されないことを保証する回帰テスト。
/// </remarks>
public class WinElementFinderClassNameTests
{
    [Fact]
    public void IsExcludedClassName_タスクバーとデスクトップのクラス名を除外する()
    {
        // タスクバー（プライマリ / セカンダリ / Windows 8/10）とデスクトップ（Progman / WorkerW）
        Assert.True(WinElementFinder.IsExcludedClassName("Shell_TrayWnd"));
        Assert.True(WinElementFinder.IsExcludedClassName("Shell_SecondaryTrayWnd"));
        Assert.True(WinElementFinder.IsExcludedClassName("Shell_ImmersiveTrayWnd"));
        Assert.True(WinElementFinder.IsExcludedClassName("Progman"));
        Assert.True(WinElementFinder.IsExcludedClassName("WorkerW"));
    }

    [Fact]
    public void IsExcludedClassName_通常のウィンドウクラスは除外しない()
    {
        Assert.False(WinElementFinder.IsExcludedClassName("CabinetWClass"));  // エクスプローラー
        Assert.False(WinElementFinder.IsExcludedClassName("Chrome_WidgetWin_1"));
        Assert.False(WinElementFinder.IsExcludedClassName("ApplicationFrameWindow"));
        Assert.False(WinElementFinder.IsExcludedClassName(""));
    }

    [Fact]
    public void IsExcludedClassName_部分一致や大文字小文字の違いは除外しない()
    {
        // 完全一致のみ除外（前方一致・大文字小文字の違いは対象外）
        Assert.False(WinElementFinder.IsExcludedClassName("WorkerW2"));
        Assert.False(WinElementFinder.IsExcludedClassName("workerw"));
        Assert.False(WinElementFinder.IsExcludedClassName("Shell_Tray"));
    }

    [Fact]
    public void IsExcludedFrame_タイトル空のApplicationFrameWindowを除外する()
    {
        // スタートメニュー / タスクビュー等の UWP オーバーレイ（タイトル空）は除外
        Assert.True(WinElementFinder.IsExcludedFrame("ApplicationFrameWindow", ""));
        Assert.True(WinElementFinder.IsExcludedFrame("ApplicationFrameWindow", null));
    }

    [Fact]
    public void IsExcludedFrame_タイトル付きのUWPアプリは除外しない()
    {
        // 実際の UWP アプリ（設定・電卓等）はタイトル付きで選択対象に残す
        Assert.False(WinElementFinder.IsExcludedFrame("ApplicationFrameWindow", "設定"));
        Assert.False(WinElementFinder.IsExcludedFrame("ApplicationFrameWindow", "電卓"));
    }

    [Fact]
    public void IsExcludedFrame_他クラスのタイトル空ウィンドウは除外しない()
    {
        // タイトル空でも他クラス（Win32 アプリ等）は対象外（IsExcludedClassName が別途判定）
        Assert.False(WinElementFinder.IsExcludedFrame("CabinetWClass", ""));
        Assert.False(WinElementFinder.IsExcludedFrame("Chrome_WidgetWin_1", ""));
    }
}
