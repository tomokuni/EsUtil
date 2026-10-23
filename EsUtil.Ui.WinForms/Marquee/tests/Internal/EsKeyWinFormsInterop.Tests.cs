using WinFormsKeys = System.Windows.Forms.Keys;
using EsUtil.Platform.Core.Input;
using EsUtil.Ui.WinForms.Marquee.Internal;
using Xunit;

namespace EsUtil.Ui.WinForms.Marquee.Tests.Internal;

/// <summary><see cref="EsKeyWinFormsInterop"/> のキー変換テスト。</summary>
/// <remarks>
/// 変換は名前ベースであるため、値（序数）の一致に依存しない。<br/>
/// <b>未対応を 0 で表さない</b>: 対応先がない場合は <see cref="WinFormsKeys.None"/> を返す（別のキーへ黙って化けない）。
/// </remarks>
public sealed class EsKeyWinFormsInteropTests
{
    // ====================================================================
    // 名前が一致するキー
    // ====================================================================

    /// <summary>名前が一致するキーがそのまま往復することを検証する。</summary>
    /// <param name="key">変換対象のキー。</param>
    /// <param name="expected">期待する WinForms のキー。</param>
    [Theory]
    [InlineData(EsKey.A, WinFormsKeys.A)]
    [InlineData(EsKey.D0, WinFormsKeys.D0)]
    [InlineData(EsKey.NumPad5, WinFormsKeys.NumPad5)]
    [InlineData(EsKey.Left, WinFormsKeys.Left)]
    [InlineData(EsKey.Up, WinFormsKeys.Up)]
    [InlineData(EsKey.Right, WinFormsKeys.Right)]
    [InlineData(EsKey.Down, WinFormsKeys.Down)]
    [InlineData(EsKey.Escape, WinFormsKeys.Escape)]
    [InlineData(EsKey.Tab, WinFormsKeys.Tab)]
    [InlineData(EsKey.Space, WinFormsKeys.Space)]
    [InlineData(EsKey.F12, WinFormsKeys.F12)]
    [InlineData(EsKey.None, WinFormsKeys.None)]
    internal void ToWinFormsKeys_名前が一致するキーを変換する(EsKey key, WinFormsKeys expected)
        => Assert.Equal(expected, key.ToWinFormsKeys());

    /// <summary>WinForms 側から見て同じキーへ戻ることを検証する。</summary>
    /// <param name="key">変換対象のキー。</param>
    [Theory]
    [InlineData(WinFormsKeys.A)]
    [InlineData(WinFormsKeys.Left)]
    [InlineData(WinFormsKeys.Escape)]
    [InlineData(WinFormsKeys.F12)]
    [InlineData(WinFormsKeys.NumPad5)]
    internal void ToEsKey_逆方向でも同じキーになる(WinFormsKeys key)
        => Assert.Equal(key, key.ToEsKey().ToWinFormsKeys());

    /// <summary>Enter は「Return / Enter」の別名（同じ値）を経由しても復元されることを検証する。</summary>
    /// <remarks>
    /// WinForms の <see cref="WinFormsKeys"/> では <c>Enter</c> と <c>Return</c> が同じ値を持つため、<br/>
    /// 代表名はどちらになるか保証されない（どちらも確定要求として扱われる）。
    /// </remarks>
    [Fact]
    internal void ToEsKey_Enterは別名経由でも確定要求のキーになる()
    {
        Assert.Equal(WinFormsKeys.Return, EsKey.Enter.ToWinFormsKeys());
        Assert.Equal(WinFormsKeys.Return, EsKey.Return.ToWinFormsKeys());
        Assert.Contains(WinFormsKeys.Return.ToEsKey(), new[] { EsKey.Enter, EsKey.Return });
    }

    // ====================================================================
    // 名前だけが異なるキー（別名表）
    // ====================================================================

    /// <summary>名前が異なる 6 組（左右の接頭辞）が往復することを検証する。</summary>
    /// <param name="key">変換対象のキー。</param>
    /// <param name="expected">期待する WinForms のキー。</param>
    [Theory]
    [InlineData(EsKey.LeftShift, WinFormsKeys.LShiftKey)]
    [InlineData(EsKey.RightShift, WinFormsKeys.RShiftKey)]
    [InlineData(EsKey.LeftCtrl, WinFormsKeys.LControlKey)]
    [InlineData(EsKey.RightCtrl, WinFormsKeys.RControlKey)]
    [InlineData(EsKey.LeftAlt, WinFormsKeys.LMenu)]
    [InlineData(EsKey.RightAlt, WinFormsKeys.RMenu)]
    internal void 変換_名前だけが異なるキーを往復させる(EsKey key, WinFormsKeys expected)
    {
        Assert.Equal(expected, key.ToWinFormsKeys());
        Assert.Equal(key, expected.ToEsKey());
    }

    /// <summary>大文字小文字だけが異なる 9 組が往復することを検証する。</summary>
    /// <param name="key">変換対象のキー。</param>
    /// <param name="expected">期待する WinForms のキー。</param>
    /// <remarks>
    /// 名前解決は大文字小文字を区別するため、WinForms 側の慣習的な綴り（<c>Oemtilde</c> / <c>IMEConvert</c> 等）は<br/>
    /// 別名表で吸収する（吸収しないと <see cref="WinFormsKeys.None"/> へ落ちて入力が無視される）。
    /// </remarks>
    [Theory]
    [InlineData(EsKey.OemTilde, WinFormsKeys.Oemtilde)]
    [InlineData(EsKey.OemPlus, WinFormsKeys.Oemplus)]
    [InlineData(EsKey.OemComma, WinFormsKeys.Oemcomma)]
    [InlineData(EsKey.ImeConvert, WinFormsKeys.IMEConvert)]
    [InlineData(EsKey.ImeNonConvert, WinFormsKeys.IMENonconvert)]
    [InlineData(EsKey.ImeAccept, WinFormsKeys.IMEAccept)]
    [InlineData(EsKey.ImeModeChange, WinFormsKeys.IMEModeChange)]
    [InlineData(EsKey.CrSel, WinFormsKeys.Crsel)]
    [InlineData(EsKey.ExSel, WinFormsKeys.Exsel)]
    internal void 変換_大文字小文字だけが異なるキーを往復させる(EsKey key, WinFormsKeys expected)
    {
        Assert.Equal(expected, key.ToWinFormsKeys());
        Assert.Equal(key, expected.ToEsKey());
    }

    /// <summary>綴り違いの別名（<see cref="WinFormsKeys.IMEAceept"/>）が同じキーとして解決されることを検証する。</summary>
    /// <remarks>WinForms は同じ値に 2 つの綴りを持つため、値が一致する別名も同じ <see cref="EsKey"/> へ解決される。</remarks>
    [Fact]
    internal void ToEsKey_綴り違いの別名も同じキーへ解決される()
        => Assert.Equal(EsKey.ImeAccept, WinFormsKeys.IMEAceept.ToEsKey());

    // ====================================================================
    // 対応先がないキー
    // ====================================================================

    /// <summary>WinForms が表現できないキーが <see cref="WinFormsKeys.None"/> になることを検証する。</summary>
    /// <param name="key">変換対象のキー。</param>
    /// <remarks>別のキーへ化けないこと（規定値へ落とさないこと）を固定する。</remarks>
    [Theory]
    [InlineData(EsKey.DbeRoman)]
    [InlineData(EsKey.FnLeftArrow)]
    [InlineData(EsKey.MediaHome)]
    [InlineData(EsKey.ImeProcessed)]
    [InlineData(EsKey.AbntC1)]
    internal void ToWinFormsKeys_表現できないキーはNoneを返す(EsKey key)
    {
        Assert.Equal(WinFormsKeys.None, key.ToWinFormsKeys());
        Assert.False(key.IsRepresentable());
    }

    /// <summary>キー以外の入力（マウス ボタン・プロセス用のキー）が <see cref="EsKey.None"/> になることを検証する。</summary>
    /// <param name="key">変換対象のキー。</param>
    [Theory]
    [InlineData(WinFormsKeys.LButton)]
    [InlineData(WinFormsKeys.RButton)]
    [InlineData(WinFormsKeys.XButton2)]
    [InlineData(WinFormsKeys.ProcessKey)]
    [InlineData(WinFormsKeys.Packet)]
    internal void ToEsKey_キー以外の入力はNoneを返す(WinFormsKeys key)
        => Assert.Equal(EsKey.None, key.ToEsKey());

    /// <summary>同じ値に 2 つの綴りを持つキーが代表名で解決されることを検証する。</summary>
    /// <remarks>
    /// WinForms は同じ値へ複数の名前を与える（<c>HanguelMode</c> / <c>KanaMode</c>）。<br/>
    /// 値から名前を引くときの代表名は保証されないが、どちらも <see cref="EsKey.KanaMode"/> へ解決される。
    /// </remarks>
    [Fact]
    internal void ToEsKey_同じ値の別名も同じキーへ解決される()
    {
        Assert.Equal(EsKey.KanaMode, WinFormsKeys.KanaMode.ToEsKey());
        Assert.Equal(EsKey.KanaMode, WinFormsKeys.HanguelMode.ToEsKey());
    }

    /// <summary>修飾キー単体とその組合せが <see cref="EsKey.None"/> になることを検証する。</summary>
    /// <remarks>
    /// 本コントロールは <see cref="KeyEventArgs.KeyCode"/>（修飾キーを除いたキー）を変換するため、<br/>
    /// フラグを含む値を渡しても別のキーへ化けないことを固定する。
    /// </remarks>
    [Theory]
    [InlineData(WinFormsKeys.ShiftKey)]
    [InlineData(WinFormsKeys.ControlKey)]
    [InlineData(WinFormsKeys.Menu)]
    [InlineData(WinFormsKeys.Shift)]
    [InlineData(WinFormsKeys.Modifiers)]
    [InlineData(WinFormsKeys.KeyCode)]
    internal void ToEsKey_修飾キーはNoneを返す(WinFormsKeys key)
        => Assert.Equal(EsKey.None, key.ToEsKey());

    /// <summary>修飾キーとキーの組合せ（<see cref="WinFormsKeys"/> のフラグ）が化けないことを検証する。</summary>
    [Fact]
    internal void ToEsKey_フラグ付きの組合せはNoneを返す()
    {
        Assert.Equal(EsKey.None, (WinFormsKeys.Shift | WinFormsKeys.A).ToEsKey());
        Assert.Equal(EsKey.None, (WinFormsKeys.Control | WinFormsKeys.Shift).ToEsKey());
    }

    // ====================================================================
    // 未定義値
    // ====================================================================

    /// <summary>未定義値（列挙に無い値）が <see cref="WinFormsKeys.None"/> になることを検証する。</summary>
    [Fact]
    internal void ToWinFormsKeys_未定義値はNoneを返す()
    {
        Assert.Equal(WinFormsKeys.None, ((EsKey)9999).ToWinFormsKeys());
        Assert.False(((EsKey)9999).IsRepresentable());
    }

    /// <summary>未定義値（列挙に無い値）が <see cref="EsKey.None"/> になることを検証する。</summary>
    [Fact]
    internal void ToEsKey_未定義値はNoneを返す() => Assert.Equal(EsKey.None, ((WinFormsKeys)9999).ToEsKey());

    /// <summary><see cref="EsKey.None"/> だけが「対応先が無い」ではなく「None そのもの」として扱われることを検証する。</summary>
    [Fact]
    internal void IsRepresentable_Noneは表現可能として扱う()
    {
        Assert.True(EsKey.None.IsRepresentable());
        Assert.True(EsKey.Left.IsRepresentable());
        Assert.False(EsKey.MediaHome.IsRepresentable());
    }

    // ====================================================================
    // 全メンバーの網羅（例外を出さない）
    // ====================================================================

    /// <summary>綴り（大文字小文字）だけが異なる組を取りこぼさないことを検証する。</summary>
    /// <remarks>
    /// 名前解決は大文字小文字を区別するため、別名表からの漏れは入力の取りこぼしになる（実際に発生した不具合）。<br/>
    /// 「WinForms に大文字小文字違いの同名がある」キーは、必ず変換できることを固定する（組の実測と表の同期）。
    /// </remarks>
    [Fact]
    internal void 変換_綴り違いの組を取りこぼさない()
    {
        var keyNames = Enum.GetNames<WinFormsKeys>();
        var caseInsensitiveKeys = new HashSet<string>(keyNames, StringComparer.OrdinalIgnoreCase);
        var missingEsToKeys = new List<string>();

        foreach (var name in Enum.GetNames<EsKey>())
        {
            // 完全一致は名前解決で解決できるため対象外
            if (Array.IndexOf(keyNames, name) >= 0)
            {
                continue;
            }

            if (caseInsensitiveKeys.Contains(name) && !Enum.Parse<EsKey>(name).IsRepresentable())
            {
                missingEsToKeys.Add(name);
            }
        }

        var caseInsensitiveEsKeys = new HashSet<string>(Enum.GetNames<EsKey>(), StringComparer.OrdinalIgnoreCase);
        var missingKeysToEs = new List<string>();

        foreach (var name in keyNames)
        {
            if (Enum.TryParse<EsKey>(name, out _))
            {
                continue;
            }

            if (caseInsensitiveEsKeys.Contains(name) && Enum.Parse<WinFormsKeys>(name).ToEsKey() == EsKey.None)
            {
                missingKeysToEs.Add(name);
            }
        }

        Assert.Empty(missingEsToKeys);
        Assert.Empty(missingKeysToEs);
    }
    /// <summary>全メンバーが例外なく変換でき、逆変換でも例外が出ないことを検証する。</summary>
    /// <remarks>変換表の欠落・名前解決の失敗は <c>None</c> として現れるため、例外の有無だけを固定する。</remarks>
    [Fact]
    internal void 変換_全メンバーを例外なく扱う()
    {
        // 表現できるキーとできないキーの両方を数えて、表が空になっていないことも確かめる
        var representable = 0;

        foreach (var key in Enum.GetValues<EsKey>())
        {
            var converted = key.ToWinFormsKeys();
            _ = converted.ToEsKey();

            if (key.IsRepresentable())
            {
                representable++;
            }
        }

        Assert.True(representable > 0);
        Assert.True(representable < Enum.GetValues<EsKey>().Length);

        foreach (var key in Enum.GetValues<WinFormsKeys>())
        {
            _ = key.ToEsKey().ToWinFormsKeys();
        }
    }
}
