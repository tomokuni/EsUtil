using Xunit;

namespace EsUtil.Feature.Text.Tests;

/// <summary><summary><see cref="ConvertPairs"/> の単体テスト。</summary></summary>
public class ConvertPairsTests
{
    [Fact]
    internal void Constructor_複数集合を平坦化()
    {
        var first = new[] { ("a", "A") };
        var second = new[] { ("b", "B"), ("c", "C") };

        var pairs = ConvertPairs.Concat(first, second);

        Assert.Equal([("a", "A"), ("b", "B"), ("c", "C")], pairs.ToList());
    }

    [Fact]
    internal void Chain_マッチのみ連結()
    {
        var first = new ConvertPairs([("a", "x"), ("b", "y")]);
        var second = new ConvertPairs([("x", "1"), ("z", "2")]);

        var chained = first.Chain(second);

        Assert.Equal([("a", "1")], chained.ToList());
    }

    [Fact]
    internal void Chain_未マッチを保持し結合()
    {
        var first = new ConvertPairs([("a", "x"), ("b", "y")]);
        var second = new ConvertPairs([("x", "1"), ("z", "2")]);

        var chained = first.Chain(second, includeUnmatchedFirst: true, includeUnmatchedSecond: true).ToList();

        Assert.Equal(3, chained.Count);
        Assert.Contains(("a", "1"), chained);
        Assert.Contains(("b", "y"), chained);
        Assert.Contains(("z", "2"), chained);
    }

    [Fact]
    internal void ChainMerge_未マッチ含めて統合()
    {
        var first = new ConvertPairs([("p", "q")]);
        var second = new ConvertPairs([("x", "y")]);

        var merged = first.ChainMerge(second).ToList();

        Assert.Contains(("p", "q"), merged);
        Assert.Contains(("x", "y"), merged);
        Assert.Equal(2, merged.Count);
    }

    [Fact]
    internal void Convert_置換とNull入力を処理()
    {
        var pairs = new ConvertPairs([("A", "a"), ("B", "b")]);

        var converted = pairs.Convert("ABZ");
        var convertedNull = pairs.Convert(null!);

        Assert.Equal("abZ", converted);
        Assert.Equal(string.Empty, convertedNull);
    }

    [Fact]
    internal void Convert_Source空は無変換()
    {
        var original = "abc";
        var pairs = new ConvertPairs([(string.Empty, "X")]);

        var converted = pairs.Convert(original);

        Assert.Equal(original, converted);
    }

    [Fact]
    internal void Empty_無変換を維持()
    {
        var result = ConvertPairs.Empty.Convert("abc");

        Assert.Equal("abc", result);
    }

    [Fact]
    internal void Empty_同一インスタンスを返す()
    {
        // 遅延初期化した結果を共有し、呼び出しごとの再生成を避ける
        Assert.Same(ConvertPairs.Empty, ConvertPairs.Empty);
        Assert.Empty(ConvertPairs.Empty.ToList());
    }

    [Fact]
    internal void Func_同一キーはキャッシュ共有()
    {
        var first = ConvertPairs.FromFunc("key", () => new ConvertPairs([("a", "A")]));
        var second = ConvertPairs.FromFunc("key", () => new ConvertPairs([("a", "A")]));

        Assert.Equal([("a", "A")], first.ToList());
        Assert.Same(first, second);
    }

    [Fact]
    internal void FromForwardInverse_エントリ方向を維持()
    {
        var entry = new EntryRecord("Cat", "G", "S", "F", "I", "源", "先", "Name", "Summary");

        var forward = ConvertPairs.FromForward(entry).Single();
        var inverse = ConvertPairs.FromInverse(entry).Single();

        Assert.Equal(("源", "先"), forward);
        Assert.Equal(("先", "源"), inverse);
    }

    [Fact]
    internal void ConvertPairsForward_キーで全角数字を半角に変換()
    {
        var key = EntryRecord.GetEntryKey("ToHan", "Ascii", "Numeric");
        var pairs = ConvertPairs.FromForward(key);

        var result = pairs.Convert("０１Ａ");

        Assert.Equal("01Ａ", result);
    }

    [Fact]
    internal void ConvertPairsInverse_キーで半角数字を全角に変換()
    {
        var key = EntryRecord.GetEntryKey("ToHan", "Ascii", "Numeric");
        var pairs = ConvertPairs.FromInverse(key);

        var result = pairs.Convert("01");

        Assert.Equal("０１", result);
    }
}
