using DrawingColor = System.Drawing.Color;
using Xunit;
using EsUtil.Feature.Annotation.Core.Defaults;

namespace EsUtil.Feature.Annotation.Core.Tests;

/// <summary><see cref="RichText"/> の仕様テスト（文字単位の書式変更と run の分割）。</summary>
/// <remarks>
/// 書式は run として保存されるため、範囲適用のたびに run が増え続けないこと（同じ書式は連結されること）も
/// 併せて固定する（保存する JSON の大きさに効く）。
/// </remarks>
public class RichTextTests
{
    /// <summary>範囲の途中だけ書式を変えると run が 3 つへ分かれることを検証する。</summary>
    [Fact]
    internal void ApplyStyle_範囲の途中はrunを分割する()
    {
        var red = AnnotationDefaults.TextStyle with { Color = DrawingColor.FromArgb(255, 255, 0, 0) };
        var text = RichText.Create("あいうえお", AnnotationDefaults.TextStyle);

        var updated = text.ApplyStyle(1, 3, red);

        Assert.Equal(3, updated.Runs.Length);
        Assert.Equal("あ", updated.Runs[0].Text);
        Assert.Equal("いうえ", updated.Runs[1].Text);
        Assert.Equal(red, updated.Runs[1].Style);
        Assert.Equal("お", updated.Runs[2].Text);
        Assert.Equal("あいうえお", updated.PlainText);
    }

    /// <summary>同じ書式の隣接する run が連結されることを検証する。</summary>
    [Fact]
    internal void ApplyStyle_同じ書式はrunを連結する()
    {
        var bold = AnnotationDefaults.TextStyle with { IsBold = true };
        var text = RichText.Create("あいう", AnnotationDefaults.TextStyle).ApplyStyle(2, 1, bold);

        var updated = text.ApplyStyle(1, 1, bold);

        Assert.Equal(2, updated.Runs.Length);
        Assert.Equal("あ", updated.Runs[0].Text);
        Assert.Equal("いう", updated.Runs[1].Text);
        Assert.True(updated.Runs[1].Style.IsBold);
    }

    /// <summary>長さ 0 は全体へ適用することを検証する。</summary>
    [Fact]
    internal void ApplyStyle_長さ0は全体へ適用する()
    {
        var bold = AnnotationDefaults.TextStyle with { IsBold = true };
        var updated = RichText.Create("あいう", AnnotationDefaults.TextStyle).ApplyStyle(0, 0, bold);

        Assert.Single(updated.Runs);
        Assert.True(updated.Runs[0].Style.IsBold);
    }

    /// <summary>範囲のすべての文字が条件を満たすかを判定できることを検証する。</summary>
    /// <remarks>
    /// 書式ツールバーの押下状態は「範囲のすべてが同じ属性か」で決める（一部だけ太字の範囲で
    /// ボタンが押されて見えるのを防ぐ）。
    /// </remarks>
    [Fact]
    internal void AllMatch_範囲のすべてが条件を満たすかで判定する()
    {
        var bold = AnnotationDefaults.TextStyle with { IsBold = true };
        var text = RichText.Create("あいうえお", AnnotationDefaults.TextStyle).ApplyStyle(1, 2, bold);

        Assert.True(text.AllMatch(1, 2, style => style.IsBold));
        Assert.False(text.AllMatch(0, 3, style => style.IsBold));
        Assert.False(text.AllMatch(3, 2, style => style.IsBold));
        Assert.True(text.AllMatch(3, 2, style => !style.IsBold));
    }

    /// <summary>長さ 0 は全体、文字が無い範囲は false になることを検証する。</summary>
    [Fact]
    internal void AllMatch_長さ0は全体で空の範囲はfalse()
    {
        var bold = AnnotationDefaults.TextStyle with { IsBold = true };
        var text = RichText.Create("あいう", bold);

        Assert.True(text.AllMatch(0, 0, style => style.IsBold));
        Assert.False(text.AllMatch(1, 0, style => !style.IsBold));
        Assert.False(RichText.Empty.AllMatch(0, 0, style => style.IsBold));
    }

    /// <summary>範囲が文字数を超えても例外にせず末尾までへ丸めることを検証する。</summary>
    [Fact]
    internal void ApplyStyle_範囲外は末尾までへ丸める()
    {
        var bold = AnnotationDefaults.TextStyle with { IsBold = true };
        var updated = RichText.Create("あい", AnnotationDefaults.TextStyle).ApplyStyle(1, 100, bold);

        Assert.Equal("あい", updated.PlainText);
        Assert.False(updated.Runs[0].Style.IsBold);
        Assert.True(updated.Runs[1].Style.IsBold);
    }

    /// <summary>指定位置の書式を取得できることを検証する。</summary>
    [Fact]
    internal void StyleAt_位置の書式を返す()
    {
        var bold = AnnotationDefaults.TextStyle with { IsBold = true };
        var text = RichText.Create("あい", AnnotationDefaults.TextStyle).ApplyStyle(1, 1, bold);

        Assert.False(text.StyleAt(0).IsBold);
        Assert.True(text.StyleAt(1).IsBold);
        // 範囲外は末尾の書式へフォールバックする（ツールバーの初期表示用）
        Assert.True(text.StyleAt(99).IsBold);
    }

    /// <summary>範囲へ 1 つの属性だけを書くとき、他の属性が保たれることを検証する。</summary>
    [Fact]
    internal void TransformStyle_変えない属性を保つ()
    {
        var red = AnnotationDefaults.TextStyle with { Color = DrawingColor.FromArgb(255, 255, 0, 0) };
        var text = RichText.Create("あいう", AnnotationDefaults.TextStyle).ApplyStyle(1, 1, red);

        // 「全体を太字にする」: 赤い run の色はそのまま残る（ApplyStyle なら色が既定へ戻る）
        var updated = text.TransformStyle(0, 0, style => style with { IsBold = true });

        Assert.Equal(3, updated.Runs.Length);
        Assert.All(updated.Runs, run => Assert.True(run.Style.IsBold));
        Assert.Equal(AnnotationDefaults.TextStyle.Color, updated.Runs[0].Style.Color);
        Assert.Equal(red.Color, updated.Runs[1].Style.Color);
    }

    /// <summary>run ごとに別の属性を保ったまま、指定した属性だけが同じ値になることを検証する。</summary>
    [Fact]
    internal void TransformStyle_範囲内のrunごとに属性を保つ()
    {
        var first = AnnotationDefaults.TextStyle with { FontSize = 30 };
        var text = RichText.Create("あいう", AnnotationDefaults.TextStyle).ApplyStyle(0, 1, first);

        var updated = text.TransformStyle(0, 0, style => style with { Color = DrawingColor.FromArgb(255, 0, 0, 255) });

        Assert.Equal(2, updated.Runs.Length);
        Assert.Equal(30, updated.Runs[0].Style.FontSize);
        Assert.Equal(AnnotationDefaults.TextStyle.FontSize, updated.Runs[1].Style.FontSize);
        Assert.All(updated.Runs, run => Assert.Equal(DrawingColor.FromArgb(255, 0, 0, 255), run.Style.Color));
    }

    /// <summary>変換しても書式が同じ run は連結されることを検証する。</summary>
    [Fact]
    internal void TransformStyle_同じ書式になったrunは連結する()
    {
        var text = RichText.Create("あいう", AnnotationDefaults.TextStyle)
            .ApplyStyle(1, 1, AnnotationDefaults.TextStyle with { IsItalic = true });

        var updated = text.TransformStyle(0, 0, style => style with { IsItalic = false });

        Assert.Single(updated.Runs);
        Assert.Equal("あいう", updated.Runs[0].Text);
    }

    /// <summary>挿入した文字が位置の書式を引き継ぐことを検証する。</summary>
    [Fact]
    internal void Insert_位置の書式を引き継ぐ()
    {
        var bold = AnnotationDefaults.TextStyle with { IsBold = true };
        var text = RichText.Create("あいう", AnnotationDefaults.TextStyle).ApplyStyle(1, 1, bold);

        var updated = text.Insert(2, "X");

        Assert.Equal("あいXう", updated.PlainText);
        Assert.True(updated.StyleAt(2).IsBold);
        Assert.False(updated.StyleAt(0).IsBold);
    }

    /// <summary>範囲外の位置へ挿入しても例外にせず端へ丸めることを検証する。</summary>
    [Fact]
    internal void Insert_範囲外は端へ丸める()
    {
        var text = RichText.Create("あい", AnnotationDefaults.TextStyle);

        Assert.Equal("Xあい", text.Insert(-5, "X").PlainText);
        Assert.Equal("あいX", text.Insert(99, "X").PlainText);
        Assert.Equal("あい", text.Insert(1, string.Empty).PlainText);
    }

    /// <summary>指定範囲を削除すると残りの書式が保たれることを検証する。</summary>
    [Fact]
    internal void Remove_範囲を削除して書式を保つ()
    {
        var red = AnnotationDefaults.TextStyle with { Color = DrawingColor.FromArgb(255, 255, 0, 0) };
        var text = RichText.Create("あいうえ", AnnotationDefaults.TextStyle).ApplyStyle(0, 1, red);

        var updated = text.Remove(1, 2);

        Assert.Equal("あえ", updated.PlainText);
        Assert.Equal(red.Color, updated.Runs[0].Style.Color);
        Assert.Equal(AnnotationDefaults.TextStyle.Color, updated.Runs[1].Style.Color);
    }

    /// <summary>改行を含む文字列の削除で行が詰まることを検証する。</summary>
    [Fact]
    internal void Remove_改行を削除する()
    {
        var text = RichText.Create("あ\nい", AnnotationDefaults.TextStyle);

        var updated = text.Remove(1, 1);

        Assert.Equal("あい", updated.PlainText);
        Assert.Single(updated.Runs);
    }
}
