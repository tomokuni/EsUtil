using Xunit;
using EsUtil.Feature.StyledText.Core.Editing;

namespace EsUtil.Feature.StyledText.Core.Tests;

/// <summary><see cref="TextEditor"/> の規則（本文・選択・書式バーの表示条件・キーの実行）を検証する。</summary>
/// <remarks>UI を介さずに、4 つの UI 版が共有する本文の操作を固定する。</remarks>
public sealed class TextEditorTests
{
    /// <summary>書式バーは編集中でも 1 文字以上を選ぶまで表示しない。</summary>
    [Fact]
    internal void ShouldShowFormatBar_選択ができるまで表示しない()
    {
        var editor = CreateEditor("abcd");

        Assert.False(editor.ShouldShowFormatBar);
        editor.Begin();
        Assert.False(editor.ShouldShowFormatBar);

        editor.Select(0, 1);
        Assert.True(editor.ShouldShowFormatBar);

        editor.ShowFormatBarWhileEditing = false;
        Assert.False(editor.ShouldShowFormatBar);

        editor.ShowFormatBarWhileEditing = true;
        editor.Select(2, 0);
        Assert.False(editor.ShouldShowFormatBar);

        editor.End();
        Assert.False(editor.ShouldShowFormatBar);
    }

    /// <summary>選択は書記素クラスタの境界へ揃える。</summary>
    [Fact]
    internal void Select_サロゲートペアの内側を選ばない()
    {
        var editor = CreateEditor("a😀b");

        editor.Select(2, 1);

        Assert.Equal("😀", editor.GetSelectedText());
        Assert.Equal(1, editor.SelectionStart);
        Assert.Equal(2, editor.SelectionLength);
    }

    /// <summary>変換中の文字列は本文へ入れず、描画とキャレットだけを進める。</summary>
    [Fact]
    internal void PreeditText_本文を変えずに描画のキャレットを進める()
    {
        var editor = CreateEditor("ab");
        editor.Begin();

        editor.PreeditText = "xy";

        Assert.Equal("ab", editor.Text.PlainText);
        Assert.Equal("abxy", editor.DisplayText.PlainText);
        Assert.Equal(4, editor.DisplayCaretIndex);
    }

    /// <summary>キーの割り当てに従って選択範囲の書式を切り替える。</summary>
    [Fact]
    internal void TryExecute_選択範囲の太字を切り替える()
    {
        var editor = CreateEditor("abcd");
        editor.Begin();
        editor.Select(0, 2);

        var change = editor.TryExecute(TextEditKeyMap.Resolve(TextEditKey.B, TextEditModifiers.Control));

        Assert.True((change & TextEditChange.Text) != 0);
        Assert.True(editor.Text.AllMatch(0, 2, static style => style.IsBold));
        Assert.False(editor.Text.StyleAt(2).IsBold);
    }

    /// <summary>本文と選択の両方が変わったことを返す。</summary>
    [Fact]
    internal void DeleteAtCaret_本文と選択の変更を返す()
    {
        var editor = CreateEditor("abcd");
        editor.Begin();
        editor.Select(1, 0);

        var change = editor.DeleteAtCaret(forward: true);

        Assert.Equal(TextEditChange.Text | TextEditChange.Selection, change);
        Assert.Equal("acd", editor.Text.PlainText);
    }

    /// <summary>改行の表し方を本文の規則へ揃える。</summary>
    [Fact]
    internal void Normalize_改行をLFへ揃える()
    {
        Assert.Equal("a\nb\nc", TextInput.Normalize("a\r\nb\rc"));
        Assert.Equal("a\nb", TextInput.Normalize("a\nb"));
    }

    /// <summary>挿入の入口が改行を本文の規則へ揃える（呼び出し側で揃えない）。</summary>
    [Fact]
    internal void ReplaceSelection_改行をLFへ揃えて挿入する()
    {
        var editor = CreateEditor("ab");
        editor.Begin();
        editor.Select(2, 0);

        editor.ReplaceSelection("c\r\nd");

        Assert.Equal("abc\nd", editor.Text.PlainText);
    }

    /// <summary>範囲の置き換えでも改行を本文の規則へ揃える。</summary>
    [Fact]
    internal void ReplaceRange_改行をLFへ揃えて置き換える()
    {
        var editor = CreateEditor("abcd");

        editor.ReplaceRange(1, 2, "x\r\ny");

        Assert.Equal("ax\nycd", editor.Text.PlainText);
    }

    /// <summary>本文とキャレットを持つエディターを作る。</summary>
    /// <param name="plainText">本文。</param>
    /// <returns>エディター。</returns>
    private static TextEditor CreateEditor(string plainText)
    {
        return new TextEditor { Text = RichText.Create(plainText, TextDefaults.DefaultStyle) };
    }
}
