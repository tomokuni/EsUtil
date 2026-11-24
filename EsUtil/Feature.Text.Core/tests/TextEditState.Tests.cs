using EsUtil.Platform.Core.Geometry;
using Xunit;
using EsUtil.Feature.Text.Presenter.Editing;

namespace EsUtil.Feature.Text.Core.Tests;

/// <summary><see cref="TextEditState"/> の規則（キャレット・選択・入力・削除・書式）を検証する。</summary>
/// <remarks>
/// UI を介さずに規則だけを固定する（4 つの UI 版が同じ状態機械を使う）。<br/>
/// 本文の操作は <see cref="RichText"/> を受け取って返す（文書への書き戻しは呼び出し側が行う）。
/// </remarks>
public sealed class TextEditStateTests
{
    /// <summary>編集を始めるとキャレットが本文の末尾へ置かれる。</summary>
    [Fact]
    internal void Begin_キャレットを末尾へ置く()
    {
        var edit = new TextEditState();

        edit.Begin(4);

        Assert.True(edit.IsActive);
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
        var text = CreateText("abcd");
        var edit = CreateEdit("abcd");
        edit.Select(1, 2, 4);

        var updated = edit.ReplaceSelection(text, "X");

        Assert.Equal("aXd", updated.PlainText);
        Assert.Equal(2, edit.CaretIndex);
        Assert.False(edit.HasSelection);
    }

    /// <summary>直前の文字の書式を引き継ぎ、保留した書式があればそれを優先する。</summary>
    [Fact]
    internal void ReplaceSelection_保留した書式を挿入した文字へ適用する()
    {
        var bold = TextDefaults.DefaultStyle with { IsBold = true };
        var text = CreateText("ab");
        var edit = CreateEdit("ab");
        edit.Remember(bold);

        var updated = edit.ReplaceSelection(text, "X");

        Assert.Equal("abX", updated.PlainText);
        Assert.True(updated.StyleAt(2).IsBold);
    }

    /// <summary>選択が無いときの削除は前後の 1 文字だけを消す。</summary>
    /// <param name="forward">キャレットの次の文字を削除する場合は <see langword="true"/>。</param>
    /// <param name="expected">期待する本文。</param>
    [Theory]
    [InlineData(false, "acd")]
    [InlineData(true, "abd")]
    internal void Delete_前後の1文字を消す(bool forward, string expected)
    {
        var text = CreateText("abcd");
        var edit = CreateEdit("abcd");
        edit.MoveTo(2, 4, extend: false);

        var updated = edit.Delete(text, forward);

        Assert.Equal(expected, updated.PlainText);
    }

    /// <summary>本文の先頭・末尾の外を削除しようとしても本文は変わらない。</summary>
    [Fact]
    internal void Delete_本文の外は変えない()
    {
        var text = CreateText("abcd");
        var edit = CreateEdit("abcd");
        edit.MoveTo(0, 4, extend: false);

        Assert.Same(text, edit.Delete(text, forward: false));

        edit.MoveTo(4, 4, extend: false);
        Assert.Same(text, edit.Delete(text, forward: true));
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
        var bold = TextDefaults.DefaultStyle with { IsBold = true };
        var text = CreateText("abcd");
        var edit = CreateEdit("abcd");
        edit.MoveTo(4, 4, extend: false);

        var updated = edit.ApplyStyle(text, bold);

        Assert.Same(text, updated);
        Assert.Equal(bold, edit.TypingStyle);
    }

    /// <summary>選択範囲への書式変更は、その範囲だけを変える。</summary>
    [Fact]
    internal void ApplyStyle_選択範囲だけを変える()
    {
        var bold = TextDefaults.DefaultStyle with { IsBold = true };
        var text = CreateText("abcd");
        var edit = CreateEdit("abcd");
        edit.Select(1, 2, 4);

        var updated = edit.ApplyStyle(text, bold);

        Assert.False(updated.StyleAt(0).IsBold);
        Assert.True(updated.StyleAt(1).IsBold);
        Assert.True(updated.StyleAt(2).IsBold);
        Assert.False(updated.StyleAt(3).IsBold);
    }

    /// <summary>装飾の切り替えは範囲の先頭の状態で 1 度だけ決める。</summary>
    [Fact]
    internal void TransformStyle_切り替えを1度だけ決める()
    {
        var bold = TextDefaults.DefaultStyle with { IsBold = true };
        var text = CreateText("abcd", (1, 2, bold));
        var edit = CreateEdit("abcd");
        edit.Select(0, 4, 4);

        // 範囲の先頭は通常 → 全体を太字にする
        var updated = edit.TransformStyle(text, style => style with { IsBold = !style.IsBold });

        Assert.All(Enumerable.Range(0, 4), index => Assert.True(updated.StyleAt(index).IsBold));
    }

    /// <summary>編集を終えると状態が初期化される。</summary>
    [Fact]
    internal void End_状態を初期化する()
    {
        var edit = CreateEdit("abcd");
        edit.Remember(TextDefaults.DefaultStyle);

        edit.End();

        Assert.False(edit.IsActive);
        Assert.Null(edit.TypingStyle);
        Assert.Equal((0, 0), edit.SelectionRange);
    }

    /// <summary>検証用の本文を生成する。</summary>
    /// <param name="text">本文。</param>
    /// <param name="bold">太字にする範囲（開始位置・長さ・書式）。</param>
    /// <returns>書式付きの本文。</returns>
    private static RichText CreateText(string text, (int Start, int Length, TextStyle Style)? bold = null)
    {
        var rich = RichText.Create(text, TextDefaults.DefaultStyle);
        return bold is { } range ? rich.ApplyStyle(range.Start, range.Length, range.Style) : rich;
    }

    /// <summary>選択が無い間は、覚えた書式を表示に使うことを検証する。</summary>
    [Fact]
    internal void ResolveDisplayStyle_選択が無ければ覚えた書式を返す()
    {
        var bold = TextDefaults.DefaultStyle with { IsBold = true };
        var text = RichText.Create("あい", TextDefaults.DefaultStyle);
        var edit = CreateEdit("あい");
        edit.Remember(bold);

        Assert.Equal(bold, edit.ResolveDisplayStyle(text));

        edit.Select(0, 1, 2);
        Assert.Equal(TextDefaults.DefaultStyle, edit.ResolveDisplayStyle(text));
    }

    /// <summary>本文を編集する状態を作る。</summary>
    /// <param name="text">本文。</param>
    /// <returns>編集の状態。</returns>
    private static TextEditState CreateEdit(string text)
    {
        var edit = new TextEditState();
        edit.Begin(text.Length);
        return edit;
    }

    /// <summary>行と文字位置の対応を作る。</summary>
    /// <param name="plain">本文。</param>
    /// <param name="lineTexts">各行の文字列。</param>
    /// <returns>行と文字位置の対応。</returns>
    private static TextLineIndex CreateLines(string plain, params string[] lineTexts)
    {
        var lines = lineTexts
            .Select((text, index) => new TextLinePlan(
                new EsRect(0, index * 20, 100, 20),
                [new TextSegmentPlan(text, TextDefaults.DefaultStyle, new EsRect(0, index * 20, 100, 20))]))
            .ToArray();

        return Assert.IsType<TextLineIndex>(TextLineIndex.Create(lines, plain));
    }
}