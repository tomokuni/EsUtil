using Microsoft.UI.Windowing;
using Xunit;

namespace EsUtil.Ui.WinUI3.Interop.Tests;

/// <summary><see cref="EsWindowStateInterop"/> の変換テスト（UI 非依存・並列実行）。</summary>
/// <remarks>
/// コア enum と WinUI 3 の enum の相互変換のみを検証するため、ウィンドウ生成（XAML の初期化）に依存せず<br/>
/// <c>[Fact]</c> で並列実行する。<br/>
/// WinUI 3 は 1 つの列挙型でウィンドウ状態を表さないため、全画面（提示の種類）と<br/>
/// 通常・最小化・最大化（重ね表示の状態）の 2 系統を固定する。
/// </remarks>
public class EsWindowStateInteropTests
{
    // ====================================================================
    // 提示の種類（全画面）
    // ====================================================================

    [Theory]
    [InlineData(EsWindowState.Normal, AppWindowPresenterKind.Default)]
    [InlineData(EsWindowState.Minimized, AppWindowPresenterKind.Default)]
    [InlineData(EsWindowState.Maximized, AppWindowPresenterKind.Default)]
    [InlineData(EsWindowState.FullScreen, AppWindowPresenterKind.FullScreen)]
    internal void ToAppWindowPresenterKind_対応する値を返す(EsWindowState source, AppWindowPresenterKind expected)
    {
        Assert.Equal(expected, source.ToAppWindowPresenterKind());
    }

    [Theory]
    [InlineData(AppWindowPresenterKind.Default, EsWindowState.Normal)]
    [InlineData(AppWindowPresenterKind.CompactOverlay, EsWindowState.Normal)]
    [InlineData(AppWindowPresenterKind.Overlapped, EsWindowState.Normal)]
    [InlineData(AppWindowPresenterKind.FullScreen, EsWindowState.FullScreen)]
    internal void ToEsWindowState_提示の種類を対応する値へ変換する(AppWindowPresenterKind source, EsWindowState expected)
    {
        Assert.Equal(expected, source.ToEsWindowState());
    }

    [Fact]
    internal void ToAppWindowPresenterKind_未定義値はDefaultへフォールバックする()
    {
        var undefined = (EsWindowState)999;

        Assert.Equal(AppWindowPresenterKind.Default, undefined.ToAppWindowPresenterKind());
    }

    // ====================================================================
    // 重ね表示の状態（通常・最小化・最大化）
    // ====================================================================

    [Theory]
    [InlineData(EsWindowState.Normal, OverlappedPresenterState.Restored)]
    [InlineData(EsWindowState.Minimized, OverlappedPresenterState.Minimized)]
    [InlineData(EsWindowState.Maximized, OverlappedPresenterState.Maximized)]
    [InlineData(EsWindowState.FullScreen, OverlappedPresenterState.Restored)]
    internal void ToOverlappedPresenterState_対応する値を返す(EsWindowState source, OverlappedPresenterState expected)
    {
        Assert.Equal(expected, source.ToOverlappedPresenterState());
    }

    [Theory]
    [InlineData(OverlappedPresenterState.Restored, EsWindowState.Normal)]
    [InlineData(OverlappedPresenterState.Minimized, EsWindowState.Minimized)]
    [InlineData(OverlappedPresenterState.Maximized, EsWindowState.Maximized)]
    internal void ToEsWindowState_重ね表示の状態を対応する値へ変換する(
        OverlappedPresenterState source, EsWindowState expected)
    {
        Assert.Equal(expected, source.ToEsWindowState());
    }

    [Fact]
    internal void 重ね表示の状態は全画面を除いて往復変換で値が一致する()
    {
        // 全画面は重ね表示の状態にメンバーがない（提示の種類が担う）ため、通常へ写って戻る
        Assert.Equal(EsWindowState.Normal, EsWindowState.Normal.ToOverlappedPresenterState().ToEsWindowState());
        Assert.Equal(EsWindowState.Minimized, EsWindowState.Minimized.ToOverlappedPresenterState().ToEsWindowState());
        Assert.Equal(EsWindowState.Maximized, EsWindowState.Maximized.ToOverlappedPresenterState().ToEsWindowState());
        Assert.Equal(
            EsWindowState.Normal,
            EsWindowState.FullScreen.ToOverlappedPresenterState().ToEsWindowState());
    }

    [Fact]
    internal void ToEsWindowState_重ね表示の未定義値はNormalへフォールバックする()
    {
        var undefined = (OverlappedPresenterState)999;

        Assert.Equal(EsWindowState.Normal, undefined.ToEsWindowState());
    }
}
