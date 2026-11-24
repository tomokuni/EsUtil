using Xunit;
using EsUtil.Feature.StyledText.Core.Defaults;
using EsUtil.Platform.Core.Geometry;

namespace EsUtil.Ui.WinForms.StyledText.Tests;

/// <summary>本文の編集 API の仕様テスト（表示と編集の状態の連携）。</summary>
/// <remarks>
/// 状態と規則は <c>TextEditState</c>（EsUtil.Feature.StyledText.Core）が単一所有するため、ここでは
/// 「コントロールの API がその規則へ正しくつながっているか」と「本文の書き戻しと通知」を検証する。
/// </remarks>
public class StyledTextControlTests
{
    /// <summary>編集を始めるとキャレットが本文の末尾へ置かれることを検証する。</summary>
    [Fact]
    internal void BeginEditing_キャレットを末尾へ置く()
    {
        var editor = CreateEditor("あいう");

        editor.BeginEditing();

        Assert.True(editor.IsEditing);
        Assert.Equal(3, editor.CaretIndex);
        Assert.False(editor.HasSelection);
    }

    /// <summary>文字を入力すると本文が変わり、キャレットが入力した文字の末尾へ移ることを検証する。</summary>
    [Fact]
    internal void ReplaceSelection_本文が変わってキャレットが末尾へ移る()
    {
        var editor = CreateEditor("あいう");
        var changed = 0;
        editor.RichTextChanged += (_, _) => changed++;
        editor.BeginEditing();
        editor.Select(1, 0);

        var updated = editor.ReplaceSelection("X");

        Assert.True(updated);
        Assert.Equal("あXいう", editor.RichTextValue.PlainText);
        Assert.Equal(2, editor.CaretIndex);
        Assert.Equal(1, changed);
    }

    /// <summary>削除の単位が書記素クラスタであることを検証する（絵文字を半分に割らない）。</summary>
    [Fact]
    internal void DeleteAtCaret_絵文字を1クラスタとして削除する()
    {
        var editor = CreateEditor("a😀b");
        editor.BeginEditing();
        editor.Select(3, 0);

        editor.DeleteAtCaret(forward: false);

        Assert.Equal("ab", editor.RichTextValue.PlainText);
    }

    /// <summary>選択範囲の書式が適用され、選択が無ければ以降の入力へ回ることを検証する。</summary>
    [Fact]
    internal void ApplyTextStyle_選択範囲と以降の入力へ適用する()
    {
        var editor = CreateEditor("あいう");
        var bold = TextDefaults.DefaultStyle with { IsBold = true };
        editor.BeginEditing();
        editor.Select(1, 1);

        Assert.True(editor.ApplyTextStyle(bold));
        Assert.True(editor.RichTextValue.StyleAt(1).IsBold);
        Assert.False(editor.RichTextValue.StyleAt(0).IsBold);

        var italic = TextDefaults.DefaultStyle with { IsItalic = true };
        editor.Select(0, 0);
        Assert.False(editor.ApplyTextStyle(italic));
        Assert.Equal(italic, editor.SelectedTextStyle);
    }

    /// <summary>キャレットの左右移動が書記素クラスタの境界へ揃うことを検証する。</summary>
    [Fact]
    internal void MoveCaretByCluster_クラスタの境界へ移る()
    {
        var editor = CreateEditor("a😀b");
        editor.BeginEditing();
        editor.Select(3, 0);

        editor.MoveCaretByCluster(-1, extend: false);
        Assert.Equal(1, editor.CaretIndex);

        editor.MoveCaretByCluster(-1, extend: false);
        Assert.Equal(0, editor.CaretIndex);
    }

    /// <summary>選択があるときの左右移動が選択の端へ畳まれることを検証する。</summary>
    [Fact]
    internal void MoveCaretByCluster_選択は端へ畳まれる()
    {
        var editor = CreateEditor("あいうえお");
        editor.BeginEditing();
        editor.Select(1, 3);

        editor.MoveCaretByCluster(-1, extend: false);

        Assert.Equal(1, editor.CaretIndex);
        Assert.False(editor.HasSelection);
    }

    /// <summary>本文を差し替えるとキャレットが本文の範囲へ収まることを検証する。</summary>
    [Fact]
    internal void RichTextValue_差し替えでキャレットが範囲へ収まる()
    {
        var editor = CreateEditor("あいうえお");
        editor.BeginEditing();
        editor.Select(5, 0);

        editor.RichTextValue = RichText.Create("あ", TextDefaults.DefaultStyle);

        Assert.Equal(1, editor.CaretIndex);
        Assert.False(editor.HasSelection);
    }

    /// <summary>指定範囲の書式が適用されることを検証する（ホストが範囲を指定する場合）。</summary>
    [Fact]
    internal void ApplyTextStyle_範囲を指定して適用する()
    {
        var editor = CreateEditor("あいうえお");
        var bold = TextDefaults.DefaultStyle with { IsBold = true };

        Assert.True(editor.ApplyTextStyle(1, 2, bold));
        Assert.False(editor.RichTextValue.StyleAt(0).IsBold);
        Assert.True(editor.RichTextValue.StyleAt(1).IsBold);
        Assert.True(editor.RichTextValue.StyleAt(2).IsBold);
        Assert.False(editor.RichTextValue.StyleAt(3).IsBold);
    }

    /// <summary>本文と大きさを持つコントロールを作る。</summary>
    /// <param name="text">本文。</param>
    /// <returns>コントロール。</returns>
    private static StyledTextControl CreateEditor(string text)
        => new()
        {
            RichTextValue = RichText.Create(text, TextDefaults.DefaultStyle),
            Width = 300,
            Height = 100,
            MeasureText = FixedTextMetrics.Measure,
        };
}

/// <summary>テスト用の文字の実寸（1 文字 = 文字サイズの半分の幅、行の高さ = 文字サイズ）。</summary>
internal static class FixedTextMetrics
{
    /// <summary>文字列と書式から実寸を測る。</summary>
    /// <param name="text">文字列。</param>
    /// <param name="style">書式。</param>
    /// <returns>実寸（元画像ピクセル）。</returns>
    internal static EsSize Measure(string text, TextStyle style)
        => new(text.Length * style.FontSize * 0.5, style.FontSize);
}
