using WinFormsWindowState = System.Windows.Forms.FormWindowState;
using EsUtil.Platform.Core.Window;
using Xunit;

namespace EsUtil.Ui.WinForms.Interop.Tests;

/// <summary><see cref="EsWindowStateInterop"/> の変換テスト（UI 初期化不要・並列実行）。</summary>
public sealed class EsWindowStateInteropTests
{
    /// <summary>名前が一致する状態が往復変換で値が保たれることを検証する。</summary>
    /// <param name="state">変換する状態。</param>
    [Theory]
    [InlineData(EsWindowState.Normal)]
    [InlineData(EsWindowState.Minimized)]
    [InlineData(EsWindowState.Maximized)]
    internal void 往復変換で値が保たれる(EsWindowState state)
    {
        Assert.Equal(state, state.ToFormWindowState().ToEsWindowState());
        Assert.Equal(state, state.ToFormWindowState().ToEsWindowState().ToFormWindowState().ToEsWindowState());
    }

    /// <summary>全画面が表現できないため通常表示へ落ちることを検証する。</summary>
    /// <remarks>
    /// WinForms のウィンドウ状態に全画面は無い（枠なし + 最大化で表す）。 書き戻し先を誤らないよう、<br/>
    /// 呼び出し側が <see cref="EsWindowStateInterop.IsRepresentable"/> で先に判定できる。
    /// </remarks>
    [Fact]
    internal void 全画面は通常表示へ落ちる()
    {
        Assert.Equal(WinFormsWindowState.Normal, EsWindowState.FullScreen.ToFormWindowState());
        Assert.False(EsWindowState.FullScreen.IsRepresentable());
    }

    /// <summary>表現可否が状態ごとに正しいことを検証する。</summary>
    [Fact]
    internal void IsRepresentableは表現可否を返す()
    {
        Assert.True(EsWindowState.Normal.IsRepresentable());
        Assert.True(EsWindowState.Minimized.IsRepresentable());
        Assert.True(EsWindowState.Maximized.IsRepresentable());
        Assert.False(EsWindowState.FullScreen.IsRepresentable());
    }

    /// <summary>未定義値が通常表示へフォールバックすることを検証する。</summary>
    [Fact]
    internal void 未定義値は通常表示へフォールバックする()
    {
        Assert.Equal(WinFormsWindowState.Normal, ((EsWindowState)999).ToFormWindowState());
        Assert.Equal(EsWindowState.Normal, ((WinFormsWindowState)999).ToEsWindowState());
    }
}
