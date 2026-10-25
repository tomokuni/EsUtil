using Xunit;

namespace EsUtil.Feature.Text.Tests;

/// <summary><summary><see cref="EntryRecord"/> の単体テスト。</summary></summary>
public class EntryRecordTests
{
    [Fact]
    internal void Constructor_Unicode表記を変換()
    {
        var entry = new EntryRecord(
            "Category",
            "Group",
            "SubGroup",
            "Forward",
            "Inverse",
            "U+3042 U+3044",
            "U+0041",
            "Name",
            "Summary");

        Assert.Equal("Category", entry.Category);
        Assert.Equal("Group", entry.Group);
        Assert.Equal("SubGroup", entry.SubGroup);
        Assert.Equal("Forward", entry.Forward);
        Assert.Equal("Inverse", entry.Inverse);
        Assert.Equal("あい", entry.Source);
        Assert.Equal("A", entry.Target);
        Assert.Equal("Name", entry.Name);
        Assert.Equal("Summary", entry.Summary);
    }

    [Fact]
    internal void Constructor_Nullは空文字()
    {
        var entry = new EntryRecord(null!, null!, null!, null!, null!, null!, null!, null!, null!);

        Assert.Equal(string.Empty, entry.Category);
        Assert.Equal(string.Empty, entry.Forward);
        Assert.Equal(string.Empty, entry.Group);
        Assert.Equal(string.Empty, entry.SubGroup);
        Assert.Equal(string.Empty, entry.Inverse);
        Assert.Equal(string.Empty, entry.Source);
        Assert.Equal(string.Empty, entry.Target);
        Assert.Equal(string.Empty, entry.Name);
        Assert.Equal(string.Empty, entry.Summary);
    }

    [Fact]
    internal void Constructor_不正Unicodeはそのまま()
    {
        var entry = new EntryRecord("Category", "Group", "SubGroup", "Forward", "Inverse", "U+110000", "U+D800", "Name", "Summary");

        Assert.Equal("U+110000", entry.Source);
        Assert.Equal("U+D800", entry.Target);
    }

    [Fact]
    internal void ImplicitOperator_タプル入力を保持()
    {
        EntryRecord entry = ("Cat", "G", "S", "C", "I", "源", "先", "Name", "概要");

        entry.Deconstruct(out var category, out var group, out var subGroup, out var convert, out var inverse, out var source, out var target, out var name, out var summary);
        var (sourceOnly, targetOnly) = entry;

        Assert.Equal("Cat", category);
        Assert.Equal("G", group);
        Assert.Equal("S", subGroup);
        Assert.Equal("C", convert);
        Assert.Equal("I", inverse);
        Assert.Equal("源", source);
        Assert.Equal("先", target);
        Assert.Equal("Name", name);
        Assert.Equal("概要", summary);
        Assert.Equal("源", sourceOnly);
        Assert.Equal("先", targetOnly);
    }

    [Fact]
    internal void ForwardConvert_一致箇所を置換()
    {
        var entry = new EntryRecord("Cat", "G", "S", "C", "I", "Ａ", "A", "Name", "Summary");

        var actual = ConvertPairs.FromForward(entry).Convert("０Ａ１Ａ");

        Assert.Equal("０A１A", actual);
    }

    [Fact]
    internal void ForwardConvert_Source空は無変更()
    {
        var entry = new EntryRecord("Cat", "G", "S", "C", "I", string.Empty, "A", "Name", "Summary");
        var original = "０Ａ１Ａ";

        var actual = ConvertPairs.FromForward(entry).Convert(original);

        Assert.Equal(original, actual);
    }

    [Fact]
    internal void InverseConvert_一致箇所を置換()
    {
        var entry = new EntryRecord("Cat", "G", "S", "C", "I", "Ａ", "A", "Name", "Summary");

        var actual = ConvertPairs.FromInverse(entry).Convert("０A１A");

        Assert.Equal("０Ａ１Ａ", actual);
    }

    [Fact]
    internal void InverseConvert_Target空は無変更()
    {
        var entry = new EntryRecord("Cat", "G", "S", "C", "I", "Ａ", string.Empty, "Name", "Summary");
        var original = "０A１A";

        var actual = ConvertPairs.FromInverse(entry).Convert(original);

        Assert.Equal(original, actual);
    }

    [Fact]
    internal void GetEntryList_カテゴリ絞り込み()
    {
        var key = EntryRecord.GetEntryKey("ToHan", "Ascii", "Numeric");
        var list = EntryRecord.GetEntryList(key);

        Assert.All(list, e => Assert.Equal("Ascii", e.Category));
        Assert.Contains(list, e => e.Source == "０" && e.Target == "0" && e.Name == "n0");
        Assert.Equal(10, list.Count);
    }

    [Fact]
    internal void GetEntryList_カテゴリとタイプでキャッシュ利用()
    {
        var key = EntryRecord.GetEntryKey("ToHan", "Ascii", "Numeric");
        var first = EntryRecord.GetEntryList(key);
        var second = EntryRecord.GetEntryList(key);

        Assert.Same(first, second);
        Assert.All(first, e =>
        {
            Assert.Equal("ToHan", e.Forward);
            Assert.Equal("Ascii", e.Category);
            Assert.Equal("Numeric", e.Group);
        });
        Assert.Equal(["n0", "n1", "n2"], [.. first.Select(e => e.Name).OrderBy(x => x).Take(3)]);
    }
}
