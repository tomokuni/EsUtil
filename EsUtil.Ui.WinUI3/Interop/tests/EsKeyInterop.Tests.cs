using EsUtil.Platform.Core;
using Xunit;
using VirtualKey = Windows.System.VirtualKey;

namespace EsUtil.Ui.WinUI3.Interop.Tests;

/// <summary><see cref="EsKeyInterop"/> の変換テスト（UI 非依存・並列実行）。</summary>
/// <remarks>
/// キーの対応は名前ベースのため、XAML の初期化は不要（<c>[Fact]</c> で並列実行できる）。<br/>
/// 「名前が異なる組」「対応先がないキー」「未定義値」の扱いを重点的に検証する。
/// </remarks>
public class EsKeyInteropTests
{
    // ====================================================================
    // 名前が一致するキー（往復）
    // ====================================================================

    [Fact]
    internal void キーを表すWinUI3の値はコアへ変換して往復できる()
    {
        // 名前が異なる組（Enter ⇔ Return 等）は別名表が吸収するため、往復で元のキーへ戻る。
        // キー以外の入力（マウス ボタン・ゲームパッド）はコアへ写らないため対象外にする
        // （「キー以外のWinUI3の値はNoneへフォールバックする」が固定する）。
        foreach (var key in Enum.GetValues<VirtualKey>())
        {
            if (key.ToEsKey() == EsKey.None)
            {
                continue;
            }

            Assert.Equal(key, key.ToEsKey().ToVirtualKey());
        }
    }

    [Fact]
    internal void コアの全キーは未定義値を含め例外を出さない()
    {
        // 対応先がないキーは None になる（例外を出さないことの保証）
        foreach (var key in Enum.GetValues<EsKey>())
        {
            _ = key.ToVirtualKey();
        }
    }

    // ====================================================================
    // 名前が異なるキー（代表値の固定）
    // ====================================================================

    [Fact]
    internal void 名前が異なるキーは対応するキーへ変換される()
    {
        Assert.Equal(VirtualKey.Enter, EsKey.Return.ToVirtualKey());
        Assert.Equal(VirtualKey.Enter, EsKey.Enter.ToVirtualKey());
        Assert.Equal(VirtualKey.CapitalLock, EsKey.CapsLock.ToVirtualKey());
        Assert.Equal(VirtualKey.CapitalLock, EsKey.Capital.ToVirtualKey());
        Assert.Equal(VirtualKey.PageUp, EsKey.Prior.ToVirtualKey());
        Assert.Equal(VirtualKey.PageUp, EsKey.PageUp.ToVirtualKey());
        Assert.Equal(VirtualKey.PageDown, EsKey.Next.ToVirtualKey());
        Assert.Equal(VirtualKey.Snapshot, EsKey.PrintScreen.ToVirtualKey());
        Assert.Equal(VirtualKey.NumberKeyLock, EsKey.NumLock.ToVirtualKey());
        Assert.Equal(VirtualKey.Number0, EsKey.D0.ToVirtualKey());
        Assert.Equal(VirtualKey.Number9, EsKey.D9.ToVirtualKey());
        Assert.Equal(VirtualKey.NumberPad0, EsKey.NumPad0.ToVirtualKey());
        Assert.Equal(VirtualKey.NumberPad9, EsKey.NumPad9.ToVirtualKey());
        Assert.Equal(VirtualKey.LeftWindows, EsKey.LWin.ToVirtualKey());
        Assert.Equal(VirtualKey.RightWindows, EsKey.RWin.ToVirtualKey());
        Assert.Equal(VirtualKey.Application, EsKey.Apps.ToVirtualKey());
        Assert.Equal(VirtualKey.LeftControl, EsKey.LeftCtrl.ToVirtualKey());
        Assert.Equal(VirtualKey.RightMenu, EsKey.RightAlt.ToVirtualKey());
        Assert.Equal(VirtualKey.GoBack, EsKey.BrowserBack.ToVirtualKey());
        Assert.Equal(VirtualKey.GoHome, EsKey.BrowserHome.ToVirtualKey());
        Assert.Equal(VirtualKey.Convert, EsKey.ImeConvert.ToVirtualKey());
    }

    [Fact]
    internal void 名前が異なるキーはコアへ戻る()
    {
        Assert.Equal(EsKey.Return, VirtualKey.Enter.ToEsKey());
        Assert.Equal(EsKey.CapsLock, VirtualKey.CapitalLock.ToEsKey());
        Assert.Equal(EsKey.PageUp, VirtualKey.PageUp.ToEsKey());
        Assert.Equal(EsKey.PageDown, VirtualKey.PageDown.ToEsKey());
        Assert.Equal(EsKey.Snapshot, VirtualKey.Snapshot.ToEsKey());
        Assert.Equal(EsKey.NumLock, VirtualKey.NumberKeyLock.ToEsKey());
        Assert.Equal(EsKey.D0, VirtualKey.Number0.ToEsKey());
        Assert.Equal(EsKey.NumPad0, VirtualKey.NumberPad0.ToEsKey());
        Assert.Equal(EsKey.LWin, VirtualKey.LeftWindows.ToEsKey());
        Assert.Equal(EsKey.LeftAlt, VirtualKey.LeftMenu.ToEsKey());
        Assert.Equal(EsKey.BrowserBack, VirtualKey.GoBack.ToEsKey());
    }

    [Fact]
    internal void 名前が一致するキーはそのまま往復する()
    {
        Assert.Equal(EsKey.Escape, VirtualKey.Escape.ToEsKey());
        Assert.Equal(EsKey.A, VirtualKey.A.ToEsKey());
        Assert.Equal(EsKey.F24, VirtualKey.F24.ToEsKey());
        Assert.Equal(EsKey.Space, VirtualKey.Space.ToEsKey());
        Assert.Equal(EsKey.Multiply, VirtualKey.Multiply.ToEsKey());
        Assert.Equal(VirtualKey.A, EsKey.A.ToVirtualKey());
        Assert.Equal(VirtualKey.F24, EsKey.F24.ToVirtualKey());
    }

    // ====================================================================
    // 対応先がないキー
    // ====================================================================

    [Fact]
    internal void WinUI3が表現できないキーはNoneへフォールバックする()
    {
        // VirtualKey は記号キー（Oem*）とメディア キー（Volume* / Media* / Launch*）を持たない
        Assert.Equal(VirtualKey.None, EsKey.OemSemicolon.ToVirtualKey());
        Assert.Equal(VirtualKey.None, EsKey.VolumeUp.ToVirtualKey());
        Assert.Equal(VirtualKey.None, EsKey.MediaPlayPause.ToVirtualKey());
        Assert.Equal(VirtualKey.None, EsKey.LaunchMail.ToVirtualKey());
        Assert.Equal(VirtualKey.None, EsKey.DbeRoman.ToVirtualKey());
    }

    [Fact]
    internal void キー以外のWinUI3の値はNoneへフォールバックする()
    {
        // VirtualKey はマウス ボタンとゲームパッドの入力も表す（コアのキーには対応先がない）
        Assert.Equal(EsKey.None, VirtualKey.LeftButton.ToEsKey());
        Assert.Equal(EsKey.None, VirtualKey.GamepadA.ToEsKey());
    }

    [Fact]
    internal void IsRepresentableは対応可否を返す()
    {
        Assert.True(EsKey.A.IsRepresentable());
        Assert.True(EsKey.Return.IsRepresentable());
        Assert.True(EsKey.None.IsRepresentable());
        Assert.False(EsKey.OemSemicolon.IsRepresentable());
        Assert.False(EsKey.VolumeUp.IsRepresentable());
    }

    [Fact]
    internal void Noneは互いに変換される()
    {
        Assert.Equal(VirtualKey.None, EsKey.None.ToVirtualKey());
        Assert.Equal(EsKey.None, VirtualKey.None.ToEsKey());
    }

    // ====================================================================
    // 未定義値（Enum.TryParse の数値文字列許容への対策）
    // ====================================================================

    [Fact]
    internal void 未定義のコア値はNoneへフォールバックする()
    {
        // Enum.TryParse<VirtualKey>("999") は成功して (VirtualKey)999 を返すため、IsDefined による防御が必要
        var undefined = (EsKey)999;

        Assert.Equal(VirtualKey.None, undefined.ToVirtualKey());
    }

    [Fact]
    internal void 未定義のWinUI3の値はNoneへフォールバックする()
    {
        var undefined = (VirtualKey)999;

        Assert.Equal(EsKey.None, undefined.ToEsKey());
    }

    // ====================================================================
    // 名前の一致率（回帰検知）
    // ====================================================================

    [Fact]
    internal void WinUI3のキー数は172である()
    {
        // WinUI 3 のキー集合が変わった場合は別名表・ドキュメントの見直しが必要（破壊的変更の検知）
        Assert.Equal(172, Enum.GetNames<VirtualKey>().Length);
    }

    [Fact]
    internal void WinUI3のキーはすべてコアへ変換して例外を出さない()
    {
        foreach (var key in Enum.GetValues<VirtualKey>())
        {
            _ = key.ToEsKey();
        }
    }
}
