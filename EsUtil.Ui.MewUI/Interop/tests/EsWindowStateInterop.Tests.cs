using MewUIWindowState = Aprillz.MewUI.Controls.WindowState;
using EsUtil.Platform.Core.Window;
using Xunit;

namespace EsUtil.Ui.MewUI.Interop.Tests;

/// <summary><see cref="EsWindowStateInterop"/> の変換テスト（UI 非依存・並列実行）。</summary>
/// <remarks>
/// コア enum と MewUI enum の相互変換のみを検証するため、ウィンドウ生成（ホスト初期化）に依存せず<br/>
/// <c>[Fact]</c> で並列実行する。<br/>
/// メンバー名の一致（設定ファイルの文字列表現の互換性）を保証する。
/// </remarks>
public class EsWindowStateInteropTests
{
    [Fact]
    internal void EsWindowState_往復変換で値が一致する()
    {
        foreach (var state in Enum.GetValues<EsWindowState>())
        {
            Assert.Equal(state, state.ToMewUIWindowState().ToEsWindowState());
        }
    }

    [Fact]
    internal void MewUIWindowState_往復変換で値が一致する()
    {
        foreach (var state in Enum.GetValues<MewUIWindowState>())
        {
            Assert.Equal(state, state.ToEsWindowState().ToMewUIWindowState());
        }
    }

    [Fact]
    internal void メンバー名がコアと完全に一致する()
    {
        // JsonStringEnumConverter で名前シリアライズするため、メンバー名の一致が必須
        var esNames = Enum.GetNames<EsWindowState>().OrderBy(static n => n, StringComparer.Ordinal);
        var mewuiNames = Enum.GetNames<MewUIWindowState>().OrderBy(static n => n, StringComparer.Ordinal);

        Assert.Equal(mewuiNames, esNames);
    }

    [Theory]
    [InlineData(EsWindowState.Normal, MewUIWindowState.Normal)]
    [InlineData(EsWindowState.Minimized, MewUIWindowState.Minimized)]
    [InlineData(EsWindowState.Maximized, MewUIWindowState.Maximized)]
    [InlineData(EsWindowState.FullScreen, MewUIWindowState.FullScreen)]
    internal void ToMewUIWindowState_対応する値を返す(EsWindowState source, MewUIWindowState expected)
    {
        Assert.Equal(expected, source.ToMewUIWindowState());
    }

    [Theory]
    [InlineData(MewUIWindowState.Normal, EsWindowState.Normal)]
    [InlineData(MewUIWindowState.Minimized, EsWindowState.Minimized)]
    [InlineData(MewUIWindowState.Maximized, EsWindowState.Maximized)]
    [InlineData(MewUIWindowState.FullScreen, EsWindowState.FullScreen)]
    internal void ToEsWindowState_対応する値を返す(MewUIWindowState source, EsWindowState expected)
    {
        Assert.Equal(expected, source.ToEsWindowState());
    }

    [Fact]
    internal void ToMewUIWindowState_未定義値はNormalへフォールバックする()
    {
        var undefined = (EsWindowState)999;

        Assert.Equal(MewUIWindowState.Normal, undefined.ToMewUIWindowState());
    }

    [Fact]
    internal void ToEsWindowState_未定義値はNormalへフォールバックする()
    {
        var undefined = (MewUIWindowState)999;

        Assert.Equal(EsWindowState.Normal, undefined.ToEsWindowState());
    }
}
