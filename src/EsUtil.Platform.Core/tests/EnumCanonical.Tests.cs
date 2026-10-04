using Xunit;

namespace EsUtil.Platform.Core.Tests;

/// <summary><see cref="EnumCanonical"/> と <see cref="EsKeyExtensions.Normalize"/> の単体テスト。</summary>
/// <remarks>
/// <b>検証の目的</b>: 「同じ値を持つ別名」と「未定義値」の扱いを固定する。<br/>
/// 別名は<b>どちらの名前が代表になるかを保証しない</b>（.NET の仕様）ため、<b>同じ値の別名が同じ結果へ<br/>
/// 正規化されること</b>を検証する（特定の名前を期待しない）。
/// </remarks>
public class EnumCanonicalTests
{
    /// <summary>
    /// 同じ値を持つ別名を含む検証用の列挙型（変換元）。<br/>
    /// <b><see cref="EsKey"/> 自身は別名を持たない</b>（値の指定が 1 つも無く 223 名 = 223 値。 実測）ため、<br/>
    /// 別名の解決を検証するには実在の別名（<c>Avalonia.Input.Key</c> の <c>Return</c> / <c>Enter</c> 等）を<br/>
    /// 模した検証用の列挙型を用意する必要がある。
    /// </summary>
    private enum AliasedSource
    {
        /// <summary>値 13。</summary>
        Return = 13,

        /// <summary>値 13（<c>Return</c> の別名）。</summary>
        Enter = 13,
    }

    /// <summary>
    /// 検証用の列挙型（変換先）。 名前は変換元と一致させ、<b>値は変換元と変える</b>（名前で解決した証拠にするため）。<br/>
    /// 宣言順は変換元と逆にしてある（<b>代表名は宣言順で決まらない</b>ことを検証が前提にしないため）。
    /// </summary>
    private enum AliasedTarget
    {
        /// <summary>値 5。</summary>
        Enter = 5,

        /// <summary>値 9。</summary>
        Return = 9,
    }

    // ====================================================================
    // 別名の正規化
    // ====================================================================

    [Fact]
    internal void TryParse_同じ値の別名は同じ結果へ正規化される()
    {
        var first = EnumCanonical.TryParse<AliasedSource, AliasedTarget>(AliasedSource.Enter, out var fromEnter);
        var second = EnumCanonical.TryParse<AliasedSource, AliasedTarget>(AliasedSource.Return, out var fromReturn);

        Assert.True(first);
        Assert.True(second);

        // どちらの名前が代表になるかは保証されないため、代表名ではなく「別名が同じ結果へ寄ること」を検証する
        Assert.Equal(fromEnter, fromReturn);

        // 変換先で定義済みの値であること（= 解決できている）
        Assert.True(Enum.IsDefined(fromReturn));

        // 値ではなく名前で解決している証拠: 変換元の値 13 は変換先に定義されていない
        Assert.NotEqual((int)AliasedSource.Return, (int)fromReturn);
    }

    [Fact]
    internal void TryParse_値を保ったまま別の列挙型へ解決できる()
    {
        var ok = EnumCanonical.TryParse<EsKey, EsKey>(EsKey.OemTilde, out var result);

        Assert.True(ok);
        Assert.Equal(EsKey.OemTilde, result);
    }

    // ====================================================================
    // 未定義値
    // ====================================================================

    [Theory]
    [InlineData(9999)]
    [InlineData(-1)]
    internal void TryParse_未定義値はfalse(int value)
    {
        // `Enum.TryParse` は数値文字列を受理するため、`Enum.IsDefined` が無いと未定義値が通る
        Assert.False(EnumCanonical.TryParse<EsKey, EsKey>((EsKey)value, out _));
    }

    // ====================================================================
    // EsKey の正規化
    // ====================================================================

    [Fact]
    internal void Normalize_定義済みのキーはそのまま返す()
    {
        foreach (var key in Enum.GetValues<EsKey>())
        {
            Assert.Equal(key, key.Normalize());
        }
    }

    [Theory]
    [InlineData(9999)]
    [InlineData(-1)]
    internal void Normalize_未定義値はNoneへ寄せる(int value)
        => Assert.Equal(EsKey.None, ((EsKey)value).Normalize());

    [Fact]
    internal void Normalize_冪等である()
    {
        foreach (var value in new[] { EsKey.A, EsKey.None, (EsKey)9999 })
        {
            var once = value.Normalize();

            Assert.Equal(once, once.Normalize());
        }
    }
}
