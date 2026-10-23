using Avalonia.Controls;
using EsUtil.Platform.Core.Window;
using Xunit;

namespace EsUtil.Ui.AvaloniaUI.Interop.Tests;

/// <summary><see cref="EsWindowStateInterop"/> の変換テスト。</summary>
/// <remarks>
/// コア enum と Avalonia enum の相互変換のみを検証するため、ウィンドウ生成（ヘッドレス）に依存せず<br/>
/// <c>[Fact]</c> で並列実行する。<br/>
/// メンバー名の一致（設定ファイルの文字列表現の互換性）を保証する。
/// </remarks>
public class EsWindowStateInteropTests
{
    // ====================================================================
    // 相互変換（往復）
    // ====================================================================

    [Fact]
    internal void EsWindowState_往復変換で値が一致する()
    {
        foreach (var state in Enum.GetValues<EsWindowState>())
        {
            Assert.Equal(state, state.ToAvaloniaWindowState().ToEsWindowState());
        }
    }

    [Fact]
    internal void AvaloniaWindowState_往復変換で値が一致する()
    {
        foreach (var state in Enum.GetValues<WindowState>())
        {
            Assert.Equal(state, state.ToEsWindowState().ToAvaloniaWindowState());
        }
    }

    // ====================================================================
    // メンバー名の一致（設定ファイル互換）
    // ====================================================================

    [Fact]
    internal void メンバー名がAvaloniaと完全に一致する()
    {
        // JsonStringEnumConverter で名前シリアライズするため、メンバー名の一致が必須
        var esNames = Enum.GetNames<EsWindowState>().OrderBy(static n => n, StringComparer.Ordinal);
        var avaloniaNames = Enum.GetNames<WindowState>().OrderBy(static n => n, StringComparer.Ordinal);

        Assert.Equal(avaloniaNames, esNames);
    }

    [Theory]
    [InlineData(EsWindowState.Normal, WindowState.Normal)]
    [InlineData(EsWindowState.Minimized, WindowState.Minimized)]
    [InlineData(EsWindowState.Maximized, WindowState.Maximized)]
    [InlineData(EsWindowState.FullScreen, WindowState.FullScreen)]
    internal void ToAvaloniaWindowState_対応する値を返す(EsWindowState source, WindowState expected)
    {
        Assert.Equal(expected, source.ToAvaloniaWindowState());
    }

    [Theory]
    [InlineData(WindowState.Normal, EsWindowState.Normal)]
    [InlineData(WindowState.Minimized, EsWindowState.Minimized)]
    [InlineData(WindowState.Maximized, EsWindowState.Maximized)]
    [InlineData(WindowState.FullScreen, EsWindowState.FullScreen)]
    internal void ToEsWindowState_対応する値を返す(WindowState source, EsWindowState expected)
    {
        Assert.Equal(expected, source.ToEsWindowState());
    }

    [Fact]
    internal void ToAvaloniaWindowState_名前が一致しない値はNormalへフォールバックする()
    {
        // 未定義値（名前が解決できない）は Normal へフォールバックする
        var undefined = (EsWindowState)999;

        Assert.Equal(WindowState.Normal, undefined.ToAvaloniaWindowState());
    }

    [Fact]
    internal void ToEsWindowState_名前が一致しない値はNormalへフォールバックする()
    {
        var undefined = (WindowState)999;

        Assert.Equal(EsWindowState.Normal, undefined.ToEsWindowState());
    }
}
