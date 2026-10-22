using EsUtil.Platform.Core;
using Xunit;
using WinFormsKeys = System.Windows.Forms.Keys;

namespace EsUtil.Ui.WinForms.Interop.Tests;

/// <summary><see cref="EsKeyInterop"/> の変換テスト（UI 非初期化・並列実行）。</summary>
/// <remarks>
/// キーの対応は名前ベースのため、ウィンドウ・メッセージ ループの初期化は不要（<c>[Fact]</c> で並列実行できる）。<br/>
/// 「名前が異なる組」と「対応先がないキー」の扱いを重点的に検証する。
/// </remarks>
public sealed class EsKeyInteropTests
{
    // ====================================================================
    // 名前が一致するキー（往復）
    // ====================================================================

    /// <summary>名前が一致するコアのキーは WinForms と往復できることを検証する。</summary>
    /// <remarks>
    /// 別名（同じ値を持つ複数の名前）を持つ側は代表名へ正規化されるため、<br/>
    /// 両側とも代表名であるキーだけを対象とする（例: <c>Keys.Return</c> は値 13 の代表名が <c>Enter</c> である）。<br/>
    /// WinForms 側に同じ名前が無いキー（対応先がないキー）も対象外とする。
    /// </remarks>
    [Fact]
    internal void 名前が一致するコアのキーは往復できる()
    {
        foreach (var name in Enum.GetNames<EsKey>())
        {
            if (!Enum.TryParse(name, ignoreCase: false, out WinFormsKeys key) || !Enum.IsDefined(key))
            {
                continue;
            }

            var es = Enum.Parse<EsKey>(name);
            if (es.ToString() != name || key.ToString() != name)
            {
                continue;
            }

            Assert.Equal(key, es.ToWinFormsKeys());
            Assert.Equal(es, key.ToEsKey());
        }
    }

    /// <summary>WinForms の全キーが例外なくコアへ変換できることを検証する。</summary>
    [Fact]
    internal void WinFormsの全キーは例外を出さない()
    {
        foreach (var key in Enum.GetValues<WinFormsKeys>())
        {
            _ = key.ToEsKey();
        }
    }

    // ====================================================================
    // 名前が異なるキー（別名表）
    // ====================================================================

    /// <summary>左右を区別する修飾キーが別名表で解決されることを検証する。</summary>
    [Fact]
    internal void 左右の修飾キーは別名で解決される()
    {
        Assert.Equal(WinFormsKeys.LShiftKey, EsKey.LeftShift.ToWinFormsKeys());
        Assert.Equal(WinFormsKeys.RShiftKey, EsKey.RightShift.ToWinFormsKeys());
        Assert.Equal(WinFormsKeys.LControlKey, EsKey.LeftCtrl.ToWinFormsKeys());
        Assert.Equal(WinFormsKeys.RControlKey, EsKey.RightCtrl.ToWinFormsKeys());
        Assert.Equal(WinFormsKeys.LMenu, EsKey.LeftAlt.ToWinFormsKeys());
        Assert.Equal(WinFormsKeys.RMenu, EsKey.RightAlt.ToWinFormsKeys());

        Assert.Equal(EsKey.LeftShift, WinFormsKeys.LShiftKey.ToEsKey());
        Assert.Equal(EsKey.RightAlt, WinFormsKeys.RMenu.ToEsKey());
    }

    /// <summary>大文字小文字だけが異なるキーが別名表で解決されることを検証する。</summary>
    [Fact]
    internal void 綴りが異なるキーは別名で解決される()
    {
        Assert.Equal(WinFormsKeys.Oemtilde, EsKey.OemTilde.ToWinFormsKeys());
        Assert.Equal(WinFormsKeys.Oemplus, EsKey.OemPlus.ToWinFormsKeys());
        Assert.Equal(WinFormsKeys.Oemcomma, EsKey.OemComma.ToWinFormsKeys());
        Assert.Equal(EsKey.OemTilde, WinFormsKeys.Oemtilde.ToEsKey());
        Assert.Equal(EsKey.OemPlus, WinFormsKeys.Oemplus.ToEsKey());
    }

    /// <summary>IME 系のキーが別名表で解決されることを検証する。</summary>
    [Fact]
    internal void IME系のキーは別名で解決される()
    {
        Assert.Equal(WinFormsKeys.IMEConvert, EsKey.ImeConvert.ToWinFormsKeys());
        Assert.Equal(WinFormsKeys.IMENonconvert, EsKey.ImeNonConvert.ToWinFormsKeys());
        Assert.Equal(WinFormsKeys.IMEAccept, EsKey.ImeAccept.ToWinFormsKeys());
        Assert.Equal(WinFormsKeys.IMEModeChange, EsKey.ImeModeChange.ToWinFormsKeys());
        Assert.Equal(EsKey.ImeNonConvert, WinFormsKeys.IMENonconvert.ToEsKey());
    }

    /// <summary>別名表の組が往復変換で値が保たれることを検証する。</summary>
    [Fact]
    internal void 別名表の組は往復変換で値が保たれる()
    {
        Assert.Equal(EsKey.LeftShift, EsKey.LeftShift.ToWinFormsKeys().ToEsKey());
        Assert.Equal(EsKey.ImeConvert, EsKey.ImeConvert.ToWinFormsKeys().ToEsKey());
        Assert.Equal(WinFormsKeys.Oemcomma, WinFormsKeys.Oemcomma.ToEsKey().ToWinFormsKeys());
        Assert.Equal(WinFormsKeys.Crsel, WinFormsKeys.Crsel.ToEsKey().ToWinFormsKeys());
    }

    // ====================================================================
    // 対応先がないキー
    // ====================================================================

    /// <summary>WinForms が表現できないキーが None へフォールバックすることを検証する。</summary>
    [Fact]
    internal void WinFormsが表現できないキーはNoneへフォールバックする()
    {
        // WinForms の Keys は Win32 仮想キーコードを持つが、DBE 系・Fn 系のキーは名前を持たない
        Assert.Equal(WinFormsKeys.None, EsKey.DbeRoman.ToWinFormsKeys());
        Assert.Equal(WinFormsKeys.None, EsKey.FnLeftArrow.ToWinFormsKeys());
        Assert.Equal(WinFormsKeys.None, EsKey.MediaHome.ToWinFormsKeys());
    }

    /// <summary>左右を区別しない修飾キーが対応先なしになることを検証する。</summary>
    /// <remarks>
    /// <see cref="EsKey"/> は左右を区別する名前しか持たないため、<c>ShiftKey</c> を既定の矢印キーへ化けさせない。
    /// </remarks>
    [Fact]
    internal void 左右を区別しない修飾キーはコアへ変換しない()
    {
        Assert.Equal(EsKey.None, WinFormsKeys.ShiftKey.ToEsKey());
        Assert.Equal(EsKey.None, WinFormsKeys.ControlKey.ToEsKey());
        Assert.Equal(EsKey.None, WinFormsKeys.Menu.ToEsKey());
    }

    /// <summary>対応可否を問い合わせる <see cref="EsKeyInterop.IsRepresentable"/> の結果を検証する。</summary>
    [Fact]
    internal void IsRepresentableは対応可否を返す()
    {
        Assert.True(EsKey.A.IsRepresentable());
        Assert.True(EsKey.LeftShift.IsRepresentable());
        Assert.True(EsKey.None.IsRepresentable());
        Assert.False(EsKey.DbeRoman.IsRepresentable());
        Assert.False(EsKey.FnLeftArrow.IsRepresentable());
    }

    /// <summary>None が互いに変換されることを検証する。</summary>
    [Fact]
    internal void Noneは互いに変換される()
    {
        Assert.Equal(WinFormsKeys.None, EsKey.None.ToWinFormsKeys());
        Assert.Equal(EsKey.None, WinFormsKeys.None.ToEsKey());
    }

    // ====================================================================
    // 未定義値（Enum.TryParse の数値文字列許容への対策）
    // ====================================================================

    /// <summary>未定義のコア値が None へフォールバックすることを検証する。</summary>
    [Fact]
    internal void 未定義のコア値はNoneへフォールバックする()
    {
        // Enum.TryParse<WinFormsKeys>("999") は成功して (WinFormsKeys)999 を返すため、IsDefined による防御が必要
        var undefined = (EsKey)999;

        Assert.Equal(WinFormsKeys.None, undefined.ToWinFormsKeys());
    }

    /// <summary>未定義の WinForms 値が None へフォールバックすることを検証する。</summary>
    [Fact]
    internal void 未定義のWinForms値はNoneへフォールバックする()
    {
        var undefined = (WinFormsKeys)999;

        Assert.Equal(EsKey.None, undefined.ToEsKey());
    }
}
