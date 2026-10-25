using Xunit;

namespace EsUtil.Feature.Text.Tests;

/// <summary><summary><see cref="ZenHanConverterExtensions"/> の単体テスト。</summary></summary>
public class ZenHanConverterExtensionsTests
{
    [Theory]
    // 文字列のインスタンス メソッドとして呼び出した結果が、静的 API と同一であることを検証する
    [InlineData("ToNormalize", "カ゛\u00A0\u2010", "ガ -")]
    [InlineData("ToNormalize", "ｶﾞ", "ｶﾞ")]
    [InlineData("ToHan", "ＡＢＣ１２３　カナ。", "ABC123 ｶﾅ｡")]
    [InlineData("ToZenWithKatakana", "ABC123 ｶﾀｶﾅ かな", "ＡＢＣ１２３　カタカナ　かな")]
    [InlineData("ToZenWithHiragana", "ABC123 ｶﾀｶﾅ カナ", "ＡＢＣ１２３　かたかな　カナ")]
    [InlineData("ToHanOnlyAscii", "ＡＢＣ１２３ カナ", "ABC123 カナ")]
    [InlineData("ToZenOnlyAscii", "ABC123 カナ", "ＡＢＣ１２３　カナ")]
    [InlineData("ToHanOnlyKana", "カナかな。ー", "ｶﾅｶﾅ｡ｰ")]
    [InlineData("ToHanOnlyKatakana", "カナかな。ー", "ｶﾅかな｡ｰ")]
    [InlineData("ToZenOnlyKatakana", "カナかな ｶﾀｶﾅ", "カナかな カタカナ")]
    [InlineData("ToZenKatakanaOnlyKana", "かな ｶﾅ", "カナ カナ")]
    [InlineData("ToZenHiraganaOnlyKana", "カナ ｶﾅ", "かな かな")]
    [InlineData("ToUpperCase", "abc ａｂｃ", "ABC ＡＢＣ")]
    [InlineData("ToLowerCase", "ABC ＡＢＣ", "abc ａｂｃ")]
    [InlineData("ConvertTabToSpace", "\tABC\t", " ABC ")]
    [InlineData("ConvertBackslashToHanYen", "\\", "¥")]
    internal void 文字列拡張メソッド_静的APIと同じ結果(string name, string input, string expect)
    {
        var actual = name switch
        {
            "ToNormalize" => input.ToNormalize(),
            "ToHan" => input.ToHan(),
            "ToZenWithKatakana" => input.ToZenWithKatakana(),
            "ToZenWithHiragana" => input.ToZenWithHiragana(),
            "ToHanOnlyAscii" => input.ToHanOnlyAscii(),
            "ToZenOnlyAscii" => input.ToZenOnlyAscii(),
            "ToHanOnlyKana" => input.ToHanOnlyKana(),
            "ToHanOnlyKatakana" => input.ToHanOnlyKatakana(),
            "ToZenOnlyKatakana" => input.ToZenOnlyKatakana(),
            "ToZenKatakanaOnlyKana" => input.ToZenKatakanaOnlyKana(),
            "ToZenHiraganaOnlyKana" => input.ToZenHiraganaOnlyKana(),
            "ToUpperCase" => input.ToUpperCase(),
            "ToLowerCase" => input.ToLowerCase(),
            "ConvertTabToSpace" => input.ConvertTabToSpace(),
            "ConvertBackslashToHanYen" => input.ConvertBackslashToHanYen(),
            _ => throw new Xunit.Sdk.XunitException($"未定義の拡張メソッド名です: {name}"),
        };

        Assert.Equal(expect, actual);
    }

    [Fact]
    internal void 文字列拡張メソッド_空文字とNullは元の値を維持()
    {
        Assert.Equal(string.Empty, string.Empty.ToHan());
        Assert.Equal("abc", "abc".ToNormalize());
    }

    [Fact]
    internal void ペア集合_IsEmptyで空を判定()
    {
        IEnumerable<(string Source, string Target)> empty = [];
        IEnumerable<(string Source, string Target)> filled = [("a", "b")];

        Assert.True(empty.IsEmpty);
        Assert.True(new ConvertPairs([]).IsEmpty);
        Assert.True(ConvertPairs.Empty.IsEmpty);
        Assert.False(filled.IsEmpty);
        Assert.False(new ConvertPairs([("a", "b")]).IsEmpty);
    }

    [Fact]
    internal void ペア集合_ToConvertPairsで変換リストを生成()
    {
        var pairs = new[] { ("◎", "○"), ("○", "◯") };

        var converted = pairs.ToConvertPairs();

        Assert.IsType<ConvertPairs>(converted);
        Assert.Equal("○◯", converted.Convert("◎○"));
    }

    [Fact]
    internal void ペア集合_実在する定義マップでも判定できる()
    {
        // GroupOf が返す ConvertPairs は IEnumerable でもあるため、拡張メンバーをそのまま利用できる
        Assert.False(GroupOf.Ascii.ToHanMap.IsEmpty);
        Assert.False(GroupOf.Kana.ToZenMap.IsEmpty);
    }
}
