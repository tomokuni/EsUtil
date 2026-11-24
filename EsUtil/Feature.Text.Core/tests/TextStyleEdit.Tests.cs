using Xunit;

namespace EsUtil.Feature.Text.Core.Tests;

/// <summary>入力する文字列の挿入と、以降の入力へ効かせる書式の仕様テスト。</summary>
/// <remarks>
/// 選択が無いときに書式を変えると、既存の文字は変えず、次に入力する文字へだけ効く。
/// 規則の所有者は <see cref="TextStyleEdit"/> である。
/// </remarks>
public class TextStyleEditTests
{
    /// <summary>保留した書式が、挿入した文字にだけ適用されることを検証する。</summary>
    [Fact]
    internal void Insert_保留した書式は挿入した文字にだけ効く()
    {
        var bold = TextDefaults.DefaultStyle with { IsBold = true };
        var text = RichText.Create("あい", TextDefaults.DefaultStyle);

        var updated = TextStyleEdit.Insert(text, 1, 0, "う", bold);

        Assert.Equal("あうい", updated.PlainText);
        Assert.False(updated.StyleAt(0).IsBold);
        Assert.True(updated.StyleAt(1).IsBold);
        Assert.False(updated.StyleAt(2).IsBold);
    }

    /// <summary>保留が無ければ直前の文字の書式を引き継ぐことを検証する。</summary>
    [Fact]
    internal void Insert_保留が無ければ直前の文字の書式を引き継ぐ()
    {
        var bold = TextDefaults.DefaultStyle with { IsBold = true };
        var text = RichText.Create("あい", TextDefaults.DefaultStyle).ApplyStyle(0, 1, bold);

        var updated = TextStyleEdit.Insert(text, 1, 0, "う", typingStyle: null);

        Assert.True(updated.StyleAt(1).IsBold);
    }

    /// <summary>選択範囲を置き換えた場合も、挿入した文字だけが保留の書式になることを検証する。</summary>
    [Fact]
    internal void Insert_選択範囲の置き換えでも挿入した文字だけが保留の書式になる()
    {
        var bold = TextDefaults.DefaultStyle with { IsBold = true };
        var text = RichText.Create("あいうえお", TextDefaults.DefaultStyle);

        var updated = TextStyleEdit.Insert(text, 1, 3, "か", bold);

        Assert.Equal("あかお", updated.PlainText);
        Assert.False(updated.StyleAt(0).IsBold);
        Assert.True(updated.StyleAt(1).IsBold);
        Assert.False(updated.StyleAt(2).IsBold);
    }
}
