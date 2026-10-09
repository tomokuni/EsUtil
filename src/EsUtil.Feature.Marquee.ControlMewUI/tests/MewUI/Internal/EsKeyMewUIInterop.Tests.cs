using EsUtil.Feature.Marquee.MewUI.Internal;
using EsUtil.Platform.Core;
using MewUIKey = Aprillz.MewUI.Key;
using Xunit;

namespace EsUtil.Feature.Marquee.MewUI.Tests.Internal;

/// <summary><see cref="EsKeyMewUIInterop"/> の変換テスト（UI 非依存・並列実行）。</summary>
/// <remarks>
/// キーの対応は名前ベースのため、ウィンドウ・バックエンドの初期化は不要（<c>[Fact]</c> で並列実行できる）。<br/>
/// 「名前が異なる組」と「対応先がないキー」の扱いを重点的に検証する。
/// </remarks>
public class EsKeyMewUIInteropTests
{
    // ====================================================================
    // 名前が一致するキー（往復）
    // ====================================================================

    [Fact]
    internal void MewUIの全キーはコアへ変換して往復できる()
    {
        // Backspace は EsKey.Back へ対応するため、往復で MewUI の Backspace に戻る
        foreach (var key in Enum.GetValues<MewUIKey>())
        {
            Assert.Equal(key, key.ToEsKey().ToMewUIKey());
        }
    }

    [Fact]
    internal void コアの全キーは未定義値を含め例外を出さない()
    {
        // 対応先がないキーは None になる（例外を出さないことの保証）
        foreach (var key in Enum.GetValues<EsKey>())
        {
            _ = key.ToMewUIKey();
        }
    }

    // ====================================================================
    // 名前が異なるキー（Back ⇔ Backspace）
    // ====================================================================

    [Fact]
    internal void BackはBackspaceへ変換される()
    {
        Assert.Equal(MewUIKey.Backspace, EsKey.Back.ToMewUIKey());
    }

    [Fact]
    internal void BackspaceはBackへ変換される()
    {
        Assert.Equal(EsKey.Back, MewUIKey.Backspace.ToEsKey());
    }

    [Fact]
    internal void Backは往復変換で値が保たれる()
    {
        Assert.Equal(EsKey.Back, EsKey.Back.ToMewUIKey().ToEsKey());
    }

    [Fact]
    internal void Backspaceは往復変換で値が保たれる()
    {
        Assert.Equal(MewUIKey.Backspace, MewUIKey.Backspace.ToEsKey().ToMewUIKey());
    }

    // ====================================================================
    // 対応先がないキー
    // ====================================================================

    [Fact]
    internal void MewUIが表現できないキーはNoneへフォールバックする()
    {
        // MewUI の Key は抽象化された小さな集合（独自採番）のため、ブラウザ系キーは対応先がない
        Assert.Equal(MewUIKey.None, EsKey.BrowserBack.ToMewUIKey());
        Assert.Equal(MewUIKey.None, EsKey.DbeRoman.ToMewUIKey());
    }

    [Fact]
    internal void IsRepresentableは対応可否を返す()
    {
        Assert.True(EsKey.Back.IsRepresentable());
        Assert.True(EsKey.A.IsRepresentable());
        Assert.True(EsKey.None.IsRepresentable());
        Assert.False(EsKey.BrowserBack.IsRepresentable());
        Assert.False(EsKey.DbeRoman.IsRepresentable());
    }

    [Fact]
    internal void Noneは互いに変換される()
    {
        Assert.Equal(MewUIKey.None, EsKey.None.ToMewUIKey());
        Assert.Equal(EsKey.None, MewUIKey.None.ToEsKey());
    }

    // ====================================================================
    // 未定義値（Enum.TryParse の数値文字列許容への対策）
    // ====================================================================

    [Fact]
    internal void 未定義のコア値はNoneへフォールバックする()
    {
        // Enum.TryParse<MewUIKey>("999") は成功して (MewUIKey)999 を返すため、IsDefined による防御が必要
        var undefined = (EsKey)999;

        Assert.Equal(MewUIKey.None, undefined.ToMewUIKey());
    }

    [Fact]
    internal void 未定義のMewUI値はNoneへフォールバックする()
    {
        var undefined = (MewUIKey)999;

        Assert.Equal(EsKey.None, undefined.ToEsKey());
    }

    // ====================================================================
    // 名前の一致率（回帰検知）
    // ====================================================================

    [Fact]
    internal void 名前が一致しないMewUIのキーはBackspaceのみである()
    {
        // MewUI 側のキーは 1 件（Backspace）を除き、すべてコアと同名（別名表の追加漏れを検知する）
        var esNames = Enum.GetNames<EsKey>().ToHashSet(StringComparer.Ordinal);
        var mewuiOnly = Enum.GetNames<MewUIKey>()
            .Where(static n => !string.Equals(n, nameof(MewUIKey.Backspace), StringComparison.Ordinal))
            .Where(n => !esNames.Contains(n))
            .ToList();

        Assert.Empty(mewuiOnly);
    }

    [Fact]
    internal void MewUIのキー数は91である()
    {
        // MewUI 側のキー集合が変わった場合は別名表・ドキュメントの見直しが必要（0.x の破壊的変更の検知）
        Assert.Equal(91, Enum.GetNames<MewUIKey>().Length);
    }
}
