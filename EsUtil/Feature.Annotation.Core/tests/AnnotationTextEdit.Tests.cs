using EsUtil.Platform.Core.Geometry;
using Xunit;
using EsUtil.Feature.Annotation.Core.Defaults;
using EsUtil.Feature.Annotation.Presenter.Drawing;
using EsUtil.Feature.Annotation.Presenter.Selection;

namespace EsUtil.Feature.Annotation.Core.Tests;

/// <summary><see cref="AnnotationTextEdit"/> の規則（キャレット・選択・入力・削除・書式）を検証する。</summary>
/// <remarks>
/// 4 つの UI 版が共有する状態機械であるため、UI を介さずに規則だけを固定する。
/// </remarks>
public sealed class AnnotationTextEditTests
{
    /// <summary>編集を始めるとキャレットが本文の末尾へ置かれる。</summary>
    [Fact]
    internal void Begin_キャレットを末尾へ置く()
    {
        var edit = new AnnotationTextEdit();

        edit.Begin("bubble", 4);

        Assert.True(edit.IsActive);
        Assert.Equal("bubble", edit.BubbleId);
        Assert.Equal(4, edit.CaretIndex);
        Assert.False(edit.HasSelection);
    }

    /// <summary>選択を広げた移動は起点を保ち、広げない移動は選択を解除する。</summary>
    [Fact]
    internal void MoveTo_選択を広げるかどうかで起点が変わる()
    {
        var edit = CreateEdit("abcd");
        edit.MoveTo(1, 4, extend: false);
        edit.MoveTo(3, 4, extend: true);

        Assert.Equal((1, 2), edit.SelectionRange);

        edit.MoveTo(0, 4, extend: false);
        Assert.Equal((0, 0), edit.SelectionRange);
    }

    /// <summary>選択範囲を置き換えるとキャレットが挿入した文字列の末尾へ移る。</summary>
    [Fact]
    internal void ReplaceSelection_選択を置き換えてキャレットを末尾へ移す()
    {
        var document = CreateDocument("abcd");
        var edit = CreateEdit("abcd");
        edit.Select(1, 2, 4);

        var updated = edit.ReplaceSelection(document, "X");

        Assert.Equal("aXd", TextOf(updated));
        Assert.Equal(2, edit.CaretIndex);
        Assert.False(edit.HasSelection);
    }

    /// <summary>直前の文字の書式を引き継ぎ、保留した書式があればそれを優先する。</summary>
    [Fact]
    internal void ReplaceSelection_保留した書式を挿入した文字へ適用する()
    {
        var bold = AnnotationDefaults.TextStyle with { IsBold = true };
        var document = CreateDocument("ab");
        var edit = CreateEdit("ab");
        edit.Remember(bold);

        var updated = edit.ReplaceSelection(document, "X");

        var text = TextOf(updated, out var rich);
        Assert.Equal("abX", text);
        Assert.True(rich.StyleAt(2).IsBold);
    }

    /// <summary>選択が無いときの削除は前後の 1 文字だけを消す。</summary>
    /// <param name="forward">キャレットの次の文字を削除する場合は <see langword="true"/>。</param>
    /// <param name="expected">期待する本文。</param>
    [Theory]
    [InlineData(false, "acd")]
    [InlineData(true, "abd")]
    internal void Delete_前後の1文字を消す(bool forward, string expected)
    {
        var document = CreateDocument("abcd");
        var edit = CreateEdit("abcd");
        edit.MoveTo(2, 4, extend: false);

        var updated = edit.Delete(document, forward);

        Assert.Equal(expected, TextOf(updated));
    }

    /// <summary>本文の先頭・末尾の外を削除しようとしても文書は変わらない。</summary>
    [Fact]
    internal void Delete_本文の外は変えない()
    {
        var document = CreateDocument("abcd");
        var edit = CreateEdit("abcd");
        edit.MoveTo(0, 4, extend: false);

        Assert.Same(document, edit.Delete(document, forward: false));

        edit.MoveTo(4, 4, extend: false);
        Assert.Same(document, edit.Delete(document, forward: true));
    }

    /// <summary>上下の移動は同じ桁を保ち、行の長さを超える場合は行末へ寄せる。</summary>
    [Fact]
    internal void MoveByLine_同じ桁を保つ()
    {
        var edit = CreateEdit("abcd\nabcd");
        var lines = CreateLines("abcd\nabcd", "abcd", "abcd");
        edit.MoveTo(3, 9, extend: false);

        edit.MoveByLine(lines, 9, delta: 1, extend: false);
        Assert.Equal(8, edit.CaretIndex);

        edit.MoveByLine(lines, 9, delta: -1, extend: false);
        Assert.Equal(3, edit.CaretIndex);
    }

    /// <summary>行の先頭・末尾へ移す（行が無い場合は本文の先頭・末尾）。</summary>
    [Fact]
    internal void MoveToLineEdge_行の端へ移す()
    {
        var lines = CreateLines("abcd\nabcd", "abcd", "abcd");
        var edit = CreateEdit("abcd\nabcd");
        edit.MoveTo(2, 9, extend: false);

        edit.MoveToLineEdge(lines, 9, first: true, extend: false);
        Assert.Equal(0, edit.CaretIndex);

        edit.MoveToLineEdge(lines, 9, first: false, extend: false);
        Assert.Equal(4, edit.CaretIndex);

        edit.MoveToLineEdge(null, 9, first: false, extend: false);
        Assert.Equal(9, edit.CaretIndex);
    }

    /// <summary>選択が無いときの書式変更は本文を変えず、以降の入力へ回す。</summary>
    [Fact]
    internal void ApplyStyle_選択が無ければ本文を変えない()
    {
        var bold = AnnotationDefaults.TextStyle with { IsBold = true };
        var document = CreateDocument("abcd");
        var edit = CreateEdit("abcd");
        edit.MoveTo(4, 4, extend: false);

        var updated = edit.ApplyStyle(document, bold);

        Assert.Same(document, updated);
        Assert.Equal(bold, edit.TypingStyle);
    }

    /// <summary>選択範囲への書式変更は、その範囲だけを変える。</summary>
    [Fact]
    internal void ApplyStyle_選択範囲だけを変える()
    {
        var bold = AnnotationDefaults.TextStyle with { IsBold = true };
        var document = CreateDocument("abcd");
        var edit = CreateEdit("abcd");
        edit.Select(1, 2, 4);

        var updated = edit.ApplyStyle(document, bold);

        var rich = RichOf(updated);
        Assert.False(rich.StyleAt(0).IsBold);
        Assert.True(rich.StyleAt(1).IsBold);
        Assert.True(rich.StyleAt(2).IsBold);
        Assert.False(rich.StyleAt(3).IsBold);
    }

    /// <summary>装飾の切り替えは範囲の先頭の状態で 1 度だけ決める。</summary>
    [Fact]
    internal void TransformStyle_切り替えを1度だけ決める()
    {
        var bold = AnnotationDefaults.TextStyle with { IsBold = true };
        var document = CreateDocument("abcd", (1, 2, bold));
        var edit = CreateEdit("abcd");
        edit.Select(0, 4, 4);

        // 範囲の先頭は通常 → 全体を太字にする
        var updated = edit.TransformStyle(document, style => style with { IsBold = !style.IsBold });

        var rich = RichOf(updated);
        Assert.All(Enumerable.Range(0, 4), index => Assert.True(rich.StyleAt(index).IsBold));
    }

    /// <summary>編集を終えると状態が初期化される。</summary>
    [Fact]
    internal void End_状態を初期化する()
    {
        var edit = CreateEdit("abcd");
        edit.Remember(AnnotationDefaults.TextStyle);

        edit.End();

        Assert.False(edit.IsActive);
        Assert.Null(edit.TypingStyle);
        Assert.Equal((0, 0), edit.SelectionRange);
    }

    /// <summary>検証用の文書を生成する。</summary>
    /// <param name="text">本文。</param>
    /// <param name="bold">太字にする範囲（開始位置・長さ・書式）。</param>
    /// <returns>吹き出し 1 件を持つ文書。</returns>
    private static AnnotationDocument CreateDocument(string text, (int Start, int Length, AnnotationTextStyle Style)? bold = null)
    {
        var rich = RichText.Create(text, AnnotationDefaults.TextStyle);
        if (bold is { } range)
        {
            rich = rich.ApplyStyle(range.Start, range.Length, range.Style);
        }

        return new AnnotationDocument(200, 100).Add(new BubbleAnnotation
        {
            Id = "bubble",
            Bounds = new EsRect(0, 0, 200, 100),
            Text = rich,
        });
    }

    /// <summary>文書の吹き出しを編集する状態を作る。</summary>
    /// <param name="text">本文。</param>
    /// <returns>編集の状態。</returns>
    private static AnnotationTextEdit CreateEdit(string text)
    {
        var edit = new AnnotationTextEdit();
        edit.Begin("bubble", text.Length);
        return edit;
    }

    /// <summary>文書の本文を取得する。</summary>
    /// <param name="document">文書。</param>
    /// <returns>本文（書式を除く）。</returns>
    private static string TextOf(AnnotationDocument document)
        => Assert.IsType<BubbleAnnotation>(Assert.Single(document.Items)).Text.PlainText;

    /// <summary>文書の本文を取得する。</summary>
    /// <param name="document">文書。</param>
    /// <param name="rich">本文（書式付き）。</param>
    /// <returns>本文（書式を除く）。</returns>
    private static string TextOf(AnnotationDocument document, out RichText rich)
    {
        rich = RichOf(document);
        return rich.PlainText;
    }

    /// <summary>文書の本文（書式付き）を取得する。</summary>
    /// <param name="document">文書。</param>
    /// <returns>本文。</returns>
    private static RichText RichOf(AnnotationDocument document)
        => Assert.IsType<BubbleAnnotation>(Assert.Single(document.Items)).Text;

    /// <summary>行と文字位置の対応を作る。</summary>
    /// <param name="plain">本文。</param>
    /// <param name="lineTexts">各行の文字列。</param>
    /// <returns>行と文字位置の対応。</returns>
    private static AnnotationTextLineIndex CreateLines(string plain, params string[] lineTexts)
    {
        var lines = lineTexts
            .Select((text, index) => new AnnotationTextLinePlan(
                new EsRect(0, index * 20, 100, 20),
                [new AnnotationTextSegmentPlan(text, AnnotationDefaults.TextStyle, new EsRect(0, index * 20, 100, 20))]))
            .ToArray();
        var shape = new AnnotationShapePlan(
            "bubble",
            AnnotationKind.Bubble,
            new EsRect(0, 0, 100, 100),
            0,
            System.Drawing.Color.Transparent,
            System.Drawing.Color.Black,
            0,
            [],
            lines);

        return Assert.IsType<AnnotationTextLineIndex>(AnnotationTextLineIndex.Create(shape, plain));
    }
}
