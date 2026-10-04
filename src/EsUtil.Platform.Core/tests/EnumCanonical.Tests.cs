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
    /// <summary>同じ値を持つ別名を含む検証用の列挙型（変換元）。</summary>
    private enum AliasedSource
    {
        /// <summary>値 1（先に宣言）。</summary>
        Alpha = 1,

        /// <summary>値 1（別名）。</summary>
        Beta = 1,
    }

    /// <summary>同じ値を持つ別名を含む検証用の列挙型（変換先。 値は変換元と異なる）。</summary>
    private enum AliasedTarget
    {
        /// <summary>値 5。</summary>
        Beta = 5,

        /// <summary>値 9。</summary>
        Alpha = 9,
    }

    // ====================================================================
    // 別名の正規化
    // ====================================================================

    [Fact]
    internal void TryParse_同じ値の別名は同じ結果へ正規化される()
    {
        var first = EnumCanonical.TryParse<AliasedSource, AliasedTarget>(AliasedSource.Alpha, out var fromAlpha);
        var second = EnumCanonical.TryParse<AliasedSource, AliasedTarget>(AliasedSource.Beta, out var fromBeta);

        Assert.True(first);
        Assert.True(second);

        // どちらの名前が代表になるかは保証されないため、同じ値の別名が同じ結果になることだけを検証する
        Assert.Equal(fromAlpha, fromBeta);

        // 値は変換先のものであること（名前で解決している証拠）
        Assert.Equal(9, (int)fromAlpha);
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
