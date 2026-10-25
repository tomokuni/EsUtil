using Xunit;

using static EsUtil.Feature.ZenHan.ZenHanConverter;

namespace EsUtil.Feature.ZenHan.Tests;


/// <summary><summary><see cref="ZenHanConverter"/> の変換メソッドの単体テスト。</summary></summary>
public class ConvertMethodTests
{
    const string 数字_全 = "０１２３４５６７８９";
    const string 数字_半 = "0123456789";
    const string 英字_大_全 = "ＡＢＣＤＥＦＧＨＩＪＫＬＭＮＯＰＱＲＳＴＵＶＷＸＹＺ";
    const string 英字_大_半 = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    const string 英字_小_全 = "ａｂｃｄｅｆｇｈｉｊｋｌｍｎｏｐｑｒｓｔｕｖｗｘｙｚ";
    const string 英字_小_半 = "abcdefghijklmnopqrstuvwxyz";
    const string 記号_全 = "（）［］｛｝”’‘，．：；＜＞＝＋－？！＃＄％＆＊＠＾＿｜～／　";
    const string 記号_半 = "()[]{}\"'`,.:;<>=+-?!#$%&*@^_|~/ ";
    const string 記号_円_全 = "＼￥";
    const string 記号_円_半 = "\\¥";
    const string ひら_全 = "あいうえおかきくけこさしすせそたちつてとなにぬねのはひふへほまみむめもやゆよらりるれろわをんぁぃぅぇぉっゃゅょ";
    const string カタ_全 = "アイウエオカキクケコサシスセソタチツテトナニヌネノハヒフヘホマミムメモヤユヨラリルレロワヲンァィゥェォッャュョ";
    const string カタ_半 = "ｱｲｳｴｵｶｷｸｹｺｻｼｽｾｿﾀﾁﾂﾃﾄﾅﾆﾇﾈﾉﾊﾋﾌﾍﾎﾏﾐﾑﾒﾓﾔﾕﾖﾗﾘﾙﾚﾛﾜｦﾝｧｨｩｪｫｯｬｭｮ";
    const string 仮名_音_全 = "ー゛゜";
    const string 仮名_音_半 = "ｰﾞﾟ";
    const string 仮名_記_全 = "。、・「」";
    const string 仮名_記_半 = "｡､･｢｣";
    const string ひら_濁_全角 = "がぎぐげござじずぜぞだぢづでどばびぶべぼゔぱぴぷぺぽ";
    const string カタ_濁_全角 = "ガギグゲゴザジズゼゾダヂヅデドバビブベボヴパピプペポ";
    const string カタ_濁_半角 = "ｶﾞｷﾞｸﾞｹﾞｺﾞｻﾞｼﾞｽﾞｾﾞｿﾞﾀﾞﾁﾞﾂﾞﾃﾞﾄﾞﾊﾞﾋﾞﾌﾞﾍﾞﾎﾞｳﾞﾊﾟﾋﾟﾌﾟﾍﾟﾎﾟ";
    const string ひら_濁_全全 = "か゛き゛く゛け゛こ゛さ゛し゛す゛せ゛そ゛た゛ち゛つ゛て゛と゛は゛ひ゛ふ゛へ゛ほ゛う゛は゜ひ゜ふ゜へ゜ほ゜";
    const string ひら_濁_全半 = "かﾞきﾞくﾞけﾞこﾞさﾞしﾞすﾞせﾞそﾞたﾞちﾞつﾞてﾞとﾞはﾞひﾞふﾞへﾞほﾞうﾞはﾟひﾟふﾟへﾟほﾟ";
    const string カタ_濁_全全 = "カ゛キ゛ク゛ケ゛コ゛サ゛シ゛ス゛セ゛ソ゛タ゛チ゛ツ゛テ゛ト゛ハ゛ヒ゛フ゛ヘ゛ホ゛ウ゛ハ゜ヒ゜フ゜ヘ゜ホ゜";
    const string カタ_濁_全半 = "カﾞキﾞクﾞケﾞコﾞサﾞシﾞスﾞセﾞソﾞタﾞチﾞツﾞテﾞトﾞハﾞヒﾞフﾞヘﾞホﾞウﾞハﾟヒﾟフﾟヘﾟホﾟ";
    const string カタ_濁_半全 = "ｶ゛ｷ゛ｸ゛ｹ゛ｺ゛ｻ゛ｼ゛ｽ゛ｾ゛ｿ゛ﾀ゛ﾁ゛ﾂ゛ﾃ゛ﾄ゛ﾊ゛ﾋ゛ﾌ゛ﾍ゛ﾎ゛ｳ゛ﾊ゜ﾋ゜ﾌ゜ﾍ゜ﾎ゜";


    [Fact]
    internal void 独自変換_空文字()
    {
        var pm = new ConvertPairs([("a", ""), ("b", "B")]); // duplicate key, first wins
        Assert.Equal("Bc", pm.Convert("abac"));

        pm = new ConvertPairs([("a", "b"), ("c", "d")]);
        Assert.Equal("", pm.Convert("")); // empty inp

        pm = new ConvertPairs(ConvertPairs.Empty);
        Assert.Equal("abc", pm.Convert("abc")); // no pairs, should return original
    }

    [Fact]
    internal void 独自変換_一致なし()
    {
        var pm = new ConvertPairs([("x", "y"), ("z", "w")]);
        Assert.Equal("abc", pm.Convert("abc")); // no matches, should return original
    }

    [Fact]
    internal void 独自変換_特別記号()
    {
        var pm = new ConvertPairs([("\"", "#"), ("\\", "&"), ("‘", "`")]);
        Assert.Equal("#&&", pm.Convert("\"\\&"));
    }

    [Fact]
    internal void 特殊文字()
    {
        Assert.Equal("---,,,...\t ", GroupOf.Kana.ToAsciiMap.Convert("ーｰ-、､,。｡.\t "));

        Assert.Equal("ーｰ-、､,。｡.  ", GroupOf.Ascii.ToSpaceFromTabMap.Convert("ーｰ-、､,。｡.\t "));

        Assert.Equal("ーー-、、,。。.\t ", GroupOf.Kana.ToZenMap.Convert("ーｰ-、､,。｡.\t "));
        Assert.Equal("ｰｰ-､､,｡｡.\t ", GroupOf.Kana.ToHanMap.Convert("ーｰ-、､,。｡.\t "));
    }

    [Fact]
    internal void 正規化()
    {
        Assert.Equal("               ", GroupOf.Ascii.ToASpaceMap.Convert("\u00A0\u00AD\u2000\u2001\u2002\u2003\u2004\u2005\u2006\u2007\u2008\u2009\u200A\u202F\u205F"));
        Assert.Equal("-----", GroupOf.Ascii.ToAHyphenMap.Convert("\u2010\u2011\u2013\u2014\u2212"));
    }



    [Fact]
    internal void 円記号()
    {
        Assert.Equal(@"\", NameOf.Ascii.Bslash.ToHanMap.Convert("＼"));
        Assert.Equal("＼", NameOf.Ascii.Bslash.ToZenMap.Convert(@"\"));

        Assert.Equal(@"¥", NameOf.Ascii.Yen.ToHanMap.Convert("￥"));
        Assert.Equal("￥", NameOf.Ascii.Yen.ToZenMap.Convert(@"¥"));

        Assert.Equal("＼", NameOf.Ascii.Yen.ToBslashFromYenMap.Convert("￥"));
        Assert.Equal(@"\", NameOf.Ascii.Yen.ToBslashFromYenMap.Convert(@"¥"));

        Assert.Equal("￥", NameOf.Ascii.Bslash.ToYenFromBslashMap.Convert("＼"));
        Assert.Equal(@"¥", NameOf.Ascii.Bslash.ToYenFromBslashMap.Convert(@"\"));

        Assert.Equal(@"\", GroupOf.Ascii.Symbol.ToHanMap.Convert("＼"));
        Assert.Equal("＼", GroupOf.Ascii.Symbol.ToZenMap.Convert(@"\"));

        Assert.Equal(@"¥", GroupOf.Ascii.Symbol.ToHanMap.Convert("￥"));
        Assert.Equal("￥", GroupOf.Ascii.Symbol.ToZenMap.Convert(@"¥"));

        Assert.Equal(記号_円_半, GroupOf.Ascii.ToHanMap.Convert(記号_円_半));
        Assert.Equal(記号_円_全, GroupOf.Ascii.ToZenMap.Convert(記号_円_半));
    }

    [Fact]
    internal void 変換不要文字混在()
    {
        var 入力 = "ＡＢＣabc１２３!@#ｶﾀｶﾅカタカナひらがな";
        var 期待値 = "ABCabc123!@#ｶﾀｶﾅｶﾀｶﾅひらがな";
        var pm = ConvertPairs.Concat(GroupOf.Ascii.ToHanMap, GroupOf.Kana.Kata.ToHanMap);

        Assert.Equal(期待値, pm.Convert(入力));
    }

    [Theory]
    [InlineData(数字_全, 数字_半)]
    [InlineData(数字_半, 数字_半)]
    [InlineData(英字_大_全, 英字_大_半)]
    [InlineData(英字_大_半, 英字_大_半)]
    [InlineData(英字_小_全, 英字_小_半)]
    [InlineData(英字_小_半, 英字_小_半)]
    [InlineData(記号_全, 記号_半)]
    [InlineData(記号_半, 記号_半)]
    [InlineData(記号_円_全, 記号_円_半)]
    [InlineData(記号_円_半, 記号_円_半)]
    [InlineData(カタ_全, カタ_半)]
    [InlineData(カタ_半, カタ_半)]
    [InlineData(ひら_全, カタ_半)]
    [InlineData(仮名_音_全, 仮名_音_半)]
    [InlineData(仮名_音_半, 仮名_音_半)]
    [InlineData(仮名_記_全, 仮名_記_半)]
    [InlineData(仮名_記_半, 仮名_記_半)]
    [InlineData(カタ_濁_全角, カタ_濁_半角)]
    [InlineData(カタ_濁_半角, カタ_濁_半角)]
    [InlineData(ひら_濁_全角, カタ_濁_半角)]
    [InlineData(カタ_濁_全全, カタ_濁_半角)]
    [InlineData(カタ_濁_全半, カタ_濁_半角)]
    [InlineData(カタ_濁_半全, カタ_濁_半角)]
    [InlineData(ひら_濁_全全, カタ_濁_半角)]
    [InlineData(ひら_濁_全半, カタ_濁_半角)]
    internal void MethodTest_ToHan(string input, string expect)
        => Assert.Equal(expect, ToHan(input));

    [Theory]
    [InlineData(数字_全, 数字_全)]
    [InlineData(数字_半, 数字_全)]
    [InlineData(英字_大_全, 英字_大_全)]
    [InlineData(英字_大_半, 英字_大_全)]
    [InlineData(英字_小_全, 英字_小_全)]
    [InlineData(英字_小_半, 英字_小_全)]
    [InlineData(記号_全, 記号_全)]
    [InlineData(記号_半, 記号_全)]
    [InlineData(記号_円_全, 記号_円_全)]
    [InlineData(記号_円_半, 記号_円_全)]
    [InlineData(カタ_全, カタ_全)]
    [InlineData(カタ_半, カタ_全)]
    [InlineData(ひら_全, ひら_全)]
    [InlineData(仮名_音_全, 仮名_音_全)]
    [InlineData(仮名_音_半, 仮名_音_全)]
    [InlineData(仮名_記_全, 仮名_記_全)]
    [InlineData(仮名_記_半, 仮名_記_全)]
    [InlineData(カタ_濁_全角, カタ_濁_全角)]
    [InlineData(カタ_濁_半角, カタ_濁_全角)]
    [InlineData(ひら_濁_全角, ひら_濁_全角)]
    [InlineData(カタ_濁_全全, カタ_濁_全角)]
    [InlineData(カタ_濁_全半, カタ_濁_全角)]
    [InlineData(カタ_濁_半全, カタ_濁_全角)]
    [InlineData(ひら_濁_全全, ひら_濁_全角)]
    [InlineData(ひら_濁_全半, ひら_濁_全角)]
    internal void MethodTest_ToZenWithKatakana(string input, string expect)
        => Assert.Equal(expect, ToZenWithKatakana(input));

    [Theory]
    [InlineData(数字_全, 数字_全)]
    [InlineData(数字_半, 数字_全)]
    [InlineData(英字_大_全, 英字_大_全)]
    [InlineData(英字_大_半, 英字_大_全)]
    [InlineData(英字_小_全, 英字_小_全)]
    [InlineData(英字_小_半, 英字_小_全)]
    [InlineData(記号_全, 記号_全)]
    [InlineData(記号_半, 記号_全)]
    [InlineData(記号_円_全, 記号_円_全)]
    [InlineData(記号_円_半, 記号_円_全)]
    [InlineData(カタ_全, カタ_全)]
    [InlineData(カタ_半, ひら_全)]
    [InlineData(ひら_全, ひら_全)]
    [InlineData(仮名_音_全, 仮名_音_全)]
    [InlineData(仮名_音_半, 仮名_音_全)]
    [InlineData(仮名_記_全, 仮名_記_全)]
    [InlineData(仮名_記_半, 仮名_記_全)]
    [InlineData(カタ_濁_全角, カタ_濁_全角)]
    [InlineData(カタ_濁_半角, ひら_濁_全角)]
    [InlineData(ひら_濁_全角, ひら_濁_全角)]
    [InlineData(カタ_濁_全全, カタ_濁_全角)]
    [InlineData(カタ_濁_全半, カタ_濁_全角)]
    [InlineData(カタ_濁_半全, ひら_濁_全角)]
    [InlineData(ひら_濁_全全, ひら_濁_全角)]
    [InlineData(ひら_濁_全半, ひら_濁_全角)]
    internal void MethodTest_ToZenWithHiragana(string input, string expect)
        => Assert.Equal(expect, ToZenWithHiragana(input));

    [Theory]
    [InlineData(数字_全, 数字_半)]
    [InlineData(数字_半, 数字_半)]
    [InlineData(英字_大_全, 英字_大_半)]
    [InlineData(英字_大_半, 英字_大_半)]
    [InlineData(英字_小_全, 英字_小_半)]
    [InlineData(英字_小_半, 英字_小_半)]
    [InlineData(記号_全, 記号_半)]
    [InlineData(記号_半, 記号_半)]
    [InlineData(記号_円_全, 記号_円_半)]
    [InlineData(記号_円_半, 記号_円_半)]
    [InlineData(カタ_全, カタ_全)]
    [InlineData(カタ_半, カタ_半)]
    [InlineData(ひら_全, ひら_全)]
    [InlineData(仮名_音_全, 仮名_音_全)]
    [InlineData(仮名_音_半, 仮名_音_半)]
    [InlineData(仮名_記_全, 仮名_記_全)]
    [InlineData(仮名_記_半, 仮名_記_半)]
    [InlineData(カタ_濁_全角, カタ_濁_全角)]
    [InlineData(カタ_濁_半角, カタ_濁_半角)]
    [InlineData(ひら_濁_全角, ひら_濁_全角)]
    [InlineData(カタ_濁_全全, カタ_濁_全角)]
    [InlineData(カタ_濁_全半, カタ_濁_全角)]
    [InlineData(カタ_濁_半全, カタ_濁_半角)]
    [InlineData(ひら_濁_全全, ひら_濁_全角)]
    [InlineData(ひら_濁_全半, ひら_濁_全角)]
    internal void MethodTest_ToHanOnlyAscii(string input, string expect)
        => Assert.Equal(expect, ToHanOnlyAscii(input));

    [Theory]
    [InlineData(数字_全, 数字_全)]
    [InlineData(数字_半, 数字_全)]
    [InlineData(英字_大_全, 英字_大_全)]
    [InlineData(英字_大_半, 英字_大_全)]
    [InlineData(英字_小_全, 英字_小_全)]
    [InlineData(英字_小_半, 英字_小_全)]
    [InlineData(記号_全, 記号_全)]
    [InlineData(記号_半, 記号_全)]
    [InlineData(記号_円_全, 記号_円_全)]
    [InlineData(記号_円_半, 記号_円_全)]
    [InlineData(カタ_全, カタ_全)]
    [InlineData(カタ_半, カタ_半)]
    [InlineData(ひら_全, ひら_全)]
    [InlineData(仮名_音_全, 仮名_音_全)]
    [InlineData(仮名_音_半, 仮名_音_半)]
    [InlineData(仮名_記_全, 仮名_記_全)]
    [InlineData(仮名_記_半, 仮名_記_半)]
    [InlineData(カタ_濁_全角, カタ_濁_全角)]
    [InlineData(カタ_濁_半角, カタ_濁_半角)]
    [InlineData(ひら_濁_全角, ひら_濁_全角)]
    [InlineData(カタ_濁_全全, カタ_濁_全角)]
    [InlineData(カタ_濁_全半, カタ_濁_全角)]
    [InlineData(カタ_濁_半全, カタ_濁_半角)]
    [InlineData(ひら_濁_全全, ひら_濁_全角)]
    [InlineData(ひら_濁_全半, ひら_濁_全角)]
    internal void MethodTest_ToZenOnlyAscii(string input, string expect)
        => Assert.Equal(expect, ToZenOnlyAscii(input));

    [Theory]
    [InlineData(数字_全, 数字_全)]
    [InlineData(数字_半, 数字_半)]
    [InlineData(英字_大_全, 英字_大_全)]
    [InlineData(英字_大_半, 英字_大_半)]
    [InlineData(英字_小_全, 英字_小_全)]
    [InlineData(英字_小_半, 英字_小_半)]
    [InlineData(記号_全, 記号_全)]
    [InlineData(記号_半, 記号_半)]
    [InlineData(記号_円_全, 記号_円_全)]
    [InlineData(記号_円_半, 記号_円_半)]
    [InlineData(カタ_全, カタ_半)]
    [InlineData(カタ_半, カタ_半)]
    [InlineData(ひら_全, カタ_半)]
    [InlineData(仮名_音_全, 仮名_音_半)]
    [InlineData(仮名_音_半, 仮名_音_半)]
    [InlineData(仮名_記_全, 仮名_記_半)]
    [InlineData(仮名_記_半, 仮名_記_半)]
    [InlineData(カタ_濁_全角, カタ_濁_半角)]
    [InlineData(カタ_濁_半角, カタ_濁_半角)]
    [InlineData(ひら_濁_全角, カタ_濁_半角)]
    [InlineData(カタ_濁_全全, カタ_濁_半角)]
    [InlineData(カタ_濁_全半, カタ_濁_半角)]
    [InlineData(カタ_濁_半全, カタ_濁_半角)]
    [InlineData(ひら_濁_全全, カタ_濁_半角)]
    [InlineData(ひら_濁_全半, カタ_濁_半角)]
    internal void MethodTest_ToHanOnlyKana(string input, string expect)
        => Assert.Equal(expect, ToHanOnlyKana(input));

    [Theory]
    [InlineData(数字_全, 数字_全)]
    [InlineData(数字_半, 数字_半)]
    [InlineData(英字_大_全, 英字_大_全)]
    [InlineData(英字_大_半, 英字_大_半)]
    [InlineData(英字_小_全, 英字_小_全)]
    [InlineData(英字_小_半, 英字_小_半)]
    [InlineData(記号_全, 記号_全)]
    [InlineData(記号_半, 記号_半)]
    [InlineData(記号_円_全, 記号_円_全)]
    [InlineData(記号_円_半, 記号_円_半)]
    [InlineData(カタ_全, カタ_半)]
    [InlineData(カタ_半, カタ_半)]
    [InlineData(ひら_全, ひら_全)]
    [InlineData(仮名_音_全, 仮名_音_半)]
    [InlineData(仮名_音_半, 仮名_音_半)]
    [InlineData(仮名_記_全, 仮名_記_半)]
    [InlineData(仮名_記_半, 仮名_記_半)]
    [InlineData(カタ_濁_全角, カタ_濁_半角)]
    [InlineData(カタ_濁_半角, カタ_濁_半角)]
    [InlineData(ひら_濁_全角, ひら_濁_全角)]
    [InlineData(カタ_濁_全全, カタ_濁_半角)]
    [InlineData(カタ_濁_全半, カタ_濁_半角)]
    [InlineData(カタ_濁_半全, カタ_濁_半角)]
    [InlineData(ひら_濁_全全, ひら_濁_全角)]
    [InlineData(ひら_濁_全半, ひら_濁_全角)]
    internal void MethodTest_ToHanOnlyKatakana(string input, string expect)
        => Assert.Equal(expect, ToHanOnlyKatakana(input));

    [Theory]
    [InlineData(数字_全, 数字_全)]
    [InlineData(数字_半, 数字_半)]
    [InlineData(英字_大_全, 英字_大_全)]
    [InlineData(英字_大_半, 英字_大_半)]
    [InlineData(英字_小_全, 英字_小_全)]
    [InlineData(英字_小_半, 英字_小_半)]
    [InlineData(記号_全, 記号_全)]
    [InlineData(記号_半, 記号_半)]
    [InlineData(記号_円_全, 記号_円_全)]
    [InlineData(記号_円_半, 記号_円_半)]
    [InlineData(カタ_全, カタ_全)]
    [InlineData(カタ_半, カタ_全)]
    [InlineData(ひら_全, ひら_全)]
    [InlineData(仮名_音_全, 仮名_音_全)]
    [InlineData(仮名_音_半, 仮名_音_全)]
    [InlineData(仮名_記_全, 仮名_記_全)]
    [InlineData(仮名_記_半, 仮名_記_全)]
    [InlineData(カタ_濁_全角, カタ_濁_全角)]
    [InlineData(カタ_濁_半角, カタ_濁_全角)]
    [InlineData(ひら_濁_全角, ひら_濁_全角)]
    [InlineData(カタ_濁_全全, カタ_濁_全角)]
    [InlineData(カタ_濁_全半, カタ_濁_全角)]
    [InlineData(カタ_濁_半全, カタ_濁_全角)]
    [InlineData(ひら_濁_全全, ひら_濁_全角)]
    [InlineData(ひら_濁_全半, ひら_濁_全角)]
    internal void MethodTest_ToZenOnlyKatakana(string input, string expect)
        => Assert.Equal(expect, ToZenOnlyKatakana(input));

    [Theory]
    [InlineData(数字_全, 数字_全)]
    [InlineData(数字_半, 数字_半)]
    [InlineData(英字_大_全, 英字_大_全)]
    [InlineData(英字_大_半, 英字_大_半)]
    [InlineData(英字_小_全, 英字_小_全)]
    [InlineData(英字_小_半, 英字_小_半)]
    [InlineData(記号_全, 記号_全)]
    [InlineData(記号_半, 記号_半)]
    [InlineData(記号_円_全, 記号_円_全)]
    [InlineData(記号_円_半, 記号_円_半)]
    [InlineData(カタ_全, カタ_全)]
    [InlineData(カタ_半, カタ_全)]
    [InlineData(ひら_全, カタ_全)]
    [InlineData(仮名_音_全, 仮名_音_全)]
    [InlineData(仮名_音_半, 仮名_音_全)]
    [InlineData(仮名_記_全, 仮名_記_全)]
    [InlineData(仮名_記_半, 仮名_記_全)]
    [InlineData(カタ_濁_全角, カタ_濁_全角)]
    [InlineData(カタ_濁_半角, カタ_濁_全角)]
    [InlineData(ひら_濁_全角, カタ_濁_全角)]
    [InlineData(カタ_濁_全全, カタ_濁_全角)]
    [InlineData(カタ_濁_全半, カタ_濁_全角)]
    [InlineData(カタ_濁_半全, カタ_濁_全角)]
    [InlineData(ひら_濁_全全, カタ_濁_全角)]
    [InlineData(ひら_濁_全半, カタ_濁_全角)]
    internal void MethodTest_ToZenKatakanaOnlyKana(string input, string expect)
        => Assert.Equal(expect, ToZenKatakanaOnlyKana(input));

    [Theory]
    [InlineData(数字_全, 数字_全)]
    [InlineData(数字_半, 数字_半)]
    [InlineData(英字_大_全, 英字_小_全)]
    [InlineData(英字_大_半, 英字_小_半)]
    [InlineData(英字_小_全, 英字_小_全)]
    [InlineData(英字_小_半, 英字_小_半)]
    [InlineData(記号_全, 記号_全)]
    [InlineData(記号_半, 記号_半)]
    [InlineData(記号_円_全, 記号_円_全)]
    [InlineData(記号_円_半, 記号_円_半)]
    [InlineData(カタ_全, カタ_全)]
    [InlineData(カタ_半, カタ_半)]
    [InlineData(ひら_全, ひら_全)]
    [InlineData(仮名_音_全, 仮名_音_全)]
    [InlineData(仮名_音_半, 仮名_音_半)]
    [InlineData(仮名_記_全, 仮名_記_全)]
    [InlineData(仮名_記_半, 仮名_記_半)]
    [InlineData(カタ_濁_全角, カタ_濁_全角)]
    [InlineData(カタ_濁_半角, カタ_濁_半角)]
    [InlineData(ひら_濁_全角, ひら_濁_全角)]
    [InlineData(カタ_濁_全全, カタ_濁_全角)]
    [InlineData(カタ_濁_全半, カタ_濁_全角)]
    [InlineData(カタ_濁_半全, カタ_濁_半角)]
    [InlineData(ひら_濁_全全, ひら_濁_全角)]
    [InlineData(ひら_濁_全半, ひら_濁_全角)]
    internal void MethodTest_ToLowerCase(string input, string expect)
        => Assert.Equal(expect, ToLowerCase(input));

    [Theory]
    [InlineData("\u00A0\u00AD\u2000\u2001\u2002\u2003\u2004\u2005\u2006\u2007\u2008\u2009\u200A\u202F\u205F", "               ")]
    [InlineData("\u2010\u2011\u2013\u2014\u2212", "-----")]
    [InlineData("カ゛", "ガ")]
    internal void MethodTest_ToNormalize(string input, string expect)
        => Assert.Equal(expect, ToNormalize(input));

    [Theory]
    [InlineData("A\tB", "A B")]
    [InlineData("\tA\tB\t", " A B ")]
    internal void MethodTest_ConvertTabToSpace(string input, string expect)
        => Assert.Equal(expect, ConvertTabToSpace(input));

    [Theory]
    [InlineData("＼", "￥")]
    [InlineData("\\", "¥")]
    [InlineData("A＼B\\", "A￥B¥")]
    internal void MethodTest_ConvertBackslashToHanYen(string input, string expect)
        => Assert.Equal(expect, ConvertBackslashToHanYen(input));


}
