using Xunit;

namespace EsUtil.Feature.Annotation.Core.Tests;

/// <summary>書式バーの変更を「選択範囲」と「以降の入力」のどちらへ回すかの仕様テスト。</summary>
/// <remarks>
/// 選択が無いときに書式を変えると、既存の文字は変えず、次に入力する文字へだけ効く（実測 2026-11-22）。
/// 規則の所有者は <see cref="AnnotationTextStyleEdit"/> と <see cref="AnnotationTypingStyle"/> である。
/// </remarks>
public class AnnotationTextStyleEditTests
{
    /// <summary>保留した書式が、挿入した文字にだけ適用されることを検証する。</summary>
    [Fact]
    internal void Insert_保留した書式は挿入した文字にだけ効く()
    {
        var bold = AnnotationDefaults.TextStyle with { IsBold = true };
        var text = RichText.Create("あい", AnnotationDefaults.TextStyle);

        var updated = AnnotationTextStyleEdit.Insert(text, 1, 0, "う", bold);

        Assert.Equal("あうい", updated.PlainText);
        Assert.False(updated.StyleAt(0).IsBold);
        Assert.True(updated.StyleAt(1).IsBold);
        Assert.False(updated.StyleAt(2).IsBold);
    }

    /// <summary>保留が無ければ直前の文字の書式を引き継ぐことを検証する。</summary>
    [Fact]
    internal void Insert_保留が無ければ直前の文字の書式を引き継ぐ()
    {
        var bold = AnnotationDefaults.TextStyle with { IsBold = true };
        var text = RichText.Create("あい", AnnotationDefaults.TextStyle).ApplyStyle(0, 1, bold);

        var updated = AnnotationTextStyleEdit.Insert(text, 1, 0, "う", typingStyle: null);

        Assert.True(updated.StyleAt(1).IsBold);
    }

    /// <summary>選択範囲を置き換えた場合も、挿入した文字だけが保留の書式になることを検証する。</summary>
    [Fact]
    internal void Insert_選択範囲の置き換えでも挿入した文字だけが保留の書式になる()
    {
        var bold = AnnotationDefaults.TextStyle with { IsBold = true };
        var text = RichText.Create("あいうえお", AnnotationDefaults.TextStyle);

        var updated = AnnotationTextStyleEdit.Insert(text, 1, 3, "か", bold);

        Assert.Equal("あかお", updated.PlainText);
        Assert.False(updated.StyleAt(0).IsBold);
        Assert.True(updated.StyleAt(1).IsBold);
        Assert.False(updated.StyleAt(2).IsBold);
    }

    /// <summary>選択が無い間は、覚えた書式を表示に使うことを検証する。</summary>
    [Fact]
    internal void ResolveDisplay_選択が無ければ覚えた書式を返す()
    {
        var typing = new AnnotationTypingStyle();
        var bold = AnnotationDefaults.TextStyle with { IsBold = true };

        typing.Remember(bold);

        Assert.Equal(bold, typing.ResolveDisplay(AnnotationDefaults.TextStyle, hasSelection: false));
        Assert.Equal(AnnotationDefaults.TextStyle, typing.ResolveDisplay(AnnotationDefaults.TextStyle, hasSelection: true));
    }
}
