using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Xunit;
using EsUtil.Feature.StyledText;
using EsUtil.Feature.StyledText.Core.Defaults;
using EsUtil.Platform.Core.Geometry;

namespace EsUtil.Ui.AvaloniaUI.StyledText.Tests;

/// <summary>本文の編集 API の仕様テスト（表示と編集の状態の連携）。</summary>
/// <remarks>
/// 状態と規則は <c>TextEditState</c>（EsUtil.Feature.StyledText.Core）が単一所有するため、ここでは「コントロールの API が
/// その規則へ正しくつながっているか」と「本文の書き戻しと通知」を検証する。
/// </remarks>
[Collection("AvaloniaHeadless")]
public class StyledTextControlTests
{
    /// <summary>編集を始めるとキャレットが本文の末尾へ置かれることを検証する。</summary>
    [AvaloniaFact]
    internal void BeginEditing_キャレットを末尾へ置く()
    {
        var editor = CreateEditor("あいう");

        editor.BeginEditing();

        Assert.True(editor.IsEditing);
        Assert.Equal(3, editor.CaretIndex);
        Assert.False(editor.HasSelection);
    }

    /// <summary>文字を入力すると本文が変わり、キャレットが入力した文字の末尾へ移ることを検証する。</summary>
    [AvaloniaFact]
    internal void ReplaceSelection_本文が変わってキャレットが末尾へ移る()
    {
        var editor = CreateEditor("あいう");
        var changed = 0;
        editor.TextChanged += (_, _) => changed++;
        editor.BeginEditing();
        editor.Select(1, 0);

        var updated = editor.ReplaceSelection("X");

        Assert.True(updated);
        Assert.Equal("あXいう", editor.Text.PlainText);
        Assert.Equal(2, editor.CaretIndex);
        Assert.Equal(1, changed);
    }

    /// <summary>本文が変わらない操作では通知しないことを検証する。</summary>
    [AvaloniaFact]
    internal void 本文が変わらない操作では通知しない()
    {
        var editor = CreateEditor("あいう");
        var changed = 0;
        editor.TextChanged += (_, _) => changed++;
        editor.BeginEditing();
        editor.Select(0, 0);

        Assert.False(editor.DeleteAtCaret(forward: false));
        Assert.Equal(0, changed);
    }

    /// <summary>削除の単位が書記素クラスタであることを検証する（絵文字を半分に割らない）。</summary>
    [AvaloniaFact]
    internal void DeleteAtCaret_絵文字を1クラスタとして削除する()
    {
        var editor = CreateEditor("a😀b");
        editor.BeginEditing();
        editor.Select(3, 0);

        editor.DeleteAtCaret(forward: false);

        Assert.Equal("ab", editor.Text.PlainText);
    }

    /// <summary>選択範囲の書式が適用され、選択が無ければ以降の入力へ回ることを検証する。</summary>
    [AvaloniaFact]
    internal void ApplyTextStyle_選択範囲と以降の入力へ適用する()
    {
        var editor = CreateEditor("あいう");
        var bold = TextDefaults.DefaultStyle with { IsBold = true };
        editor.BeginEditing();
        editor.Select(1, 1);

        Assert.True(editor.ApplyTextStyle(bold));
        Assert.True(editor.Text.StyleAt(1).IsBold);
        Assert.False(editor.Text.StyleAt(0).IsBold);

        var italic = TextDefaults.DefaultStyle with { IsItalic = true };
        editor.Select(0, 0);
        Assert.False(editor.ApplyTextStyle(italic));
        Assert.Equal(italic, editor.SelectedTextStyle);
    }

    /// <summary>キャレットの左右移動が書記素クラスタの境界へ揃うことを検証する。</summary>
    [AvaloniaFact]
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
    [AvaloniaFact]
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
    [AvaloniaFact]
    internal void Text_差し替えでキャレットが範囲へ収まる()
    {
        var editor = CreateEditor("あいうえお");
        editor.BeginEditing();
        editor.Select(5, 0);

        editor.Text = RichText.Create("あ", TextDefaults.DefaultStyle);

        Assert.Equal(1, editor.CaretIndex);
        Assert.False(editor.HasSelection);
    }

    /// <summary>指定範囲の書式が適用されることを検証する（ホストが範囲を指定する場合）。</summary>
    [AvaloniaFact]
    internal void ApplyTextStyle_範囲を指定して適用する()
    {
        var editor = CreateEditor("あいうえお");
        var bold = TextDefaults.DefaultStyle with { IsBold = true };

        Assert.True(editor.ApplyTextStyle(1, 2, bold));
        Assert.False(editor.Text.StyleAt(0).IsBold);
        Assert.True(editor.Text.StyleAt(1).IsBold);
        Assert.True(editor.Text.StyleAt(2).IsBold);
        Assert.False(editor.Text.StyleAt(3).IsBold);
    }

    /// <summary>変換中の文字列が描画用の本文にだけ入り、本文へは入らないことを検証する。</summary>
    /// <remarks>
    /// 変換中の文字列を本文へ混ぜて表示すると、確定前に本文が変わってしまう。 表示用の値だけに入れ、
    /// キャレットも変換中の文字列の末尾へ置く。
    /// </remarks>
    [AvaloniaFact]
    internal void PreeditText_変換中の文字列は表示にだけ入る()
    {
        var editor = CreateEditor("あいう");
        editor.BeginEditing();
        editor.Select(1, 0);

        editor.PreeditText = "かき";

        // 本文は変わらず、表示用の値にだけ変換中の文字列が入る
        Assert.Equal("あいう", editor.Text.PlainText);
        Assert.Equal("あかきいう", editor.DisplayText.PlainText);

        // 編集のキャレットは入力位置のまま（描画のキャレットだけ変換中の文字列の末尾へ進む）
        Assert.Equal(1, editor.CaretIndex);
    }

    /// <summary>斜体にしても行の高さ（実寸の高さ）が変わらないことを検証する。</summary>
    /// <remarks>斜体は字を傾けるだけであり、行の間隔を変えてはいけない。</remarks>
    [AvaloniaFact]
    internal void 斜体にしても行の高さが変わらない()
    {
        var normal = TextDefaults.DefaultStyle;
        var italic = normal with { IsItalic = true };

        Assert.Equal(
            RichTextPainter.Measure("文字", normal).Height,
            RichTextPainter.Measure("文字", italic).Height,
            3);
    }

    /// <summary>編集を始めるとフォーカスを持ち、キー入力の宛先になることを検証する。</summary>
    /// <remarks>
    /// Avalonia の <see cref="Control"/> は既定でフォーカスを持てない。 フォーカスを持てないと
    /// <c>Focus()</c> が失敗し、キー入力も IME も本コントロールへ届かない（編集を開始しても文字を入力できない）。
    /// </remarks>
    [AvaloniaFact]
    internal void BeginEditing_フォーカスを持つ()
    {
        var editor = CreateEditor("あいう");
        var window = new Window { Content = editor };

        window.Show();
        try
        {
            editor.BeginEditing();

            Assert.True(editor.IsEditing);
            Assert.True(editor.IsFocused);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>本文を押すと編集が始まり、フォーカスが本文へ移ることを検証する。</summary>
    /// <remarks>編集の入口は API だけではない（利用者は本文を押して書き始める）。</remarks>
    [AvaloniaFact]
    internal void 本文の押下で編集が始まる()
    {
        var editor = CreateEditor("あいう");
        var window = new Window { Content = editor };

        window.Show();
        try
        {
            Dispatcher.UIThread.RunJobs();

            // 位置はウィンドウの座標で渡す（本文は中央に置かれる）
            window.MouseDown(editor.TranslatePoint(new Point(10, 10), window)!.Value, MouseButton.Left);

            Assert.True(editor.IsEditing);
            Assert.True(editor.IsFocused);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>入力を受け付けない設定では編集を始めないことを検証する（表示専用で使う場合）。</summary>
    [AvaloniaFact]
    internal void 入力を受け付けない設定では編集を始めない()
    {
        var editor = CreateEditor("あいう");
        editor.IsInputEnabled = false;
        var window = new Window { Content = editor };

        window.Show();
        try
        {
            editor.BeginEditing();
            window.MouseDown(editor.TranslatePoint(new Point(10, 10), window)!.Value, MouseButton.Left);

            Assert.False(editor.IsEditing);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>変換中の文字列があると、変換候補の位置が変換中の文字の末尾になることを検証する。</summary>
    /// <remarks>
    /// 候補の位置はキャレットの矩形から決まる。 変換中の文字列を入れない本文で位置を求めると、
    /// キャレットの位置が本文の範囲の外になって候補が画面の隅へ飛ぶ（入力中の文字の近くに出ない）。
    /// </remarks>
    [AvaloniaFact]
    internal void CaretRect_変換中は変換中の文字の末尾になる()
    {
        var editor = CreateEditor("あい");
        var window = new Window { Content = editor };

        window.Show();
        try
        {
            Dispatcher.UIThread.RunJobs();
            editor.BeginEditing();
            editor.Select(2, 0);
            var before = editor.CaretRect;
            Assert.True(before.Width > 0, $"before={before}");

            // 本文の末尾で変換を始める（変換中の文字列の位置は本文の長さを超える）
            editor.PreeditText = "かき";
            var after = editor.CaretRect;

            // 変換中の文字（2 文字）の分だけ右へ進む
            Assert.True(after.X > before.X, $"before={before} after={after}");
            Assert.InRange(after.Y, before.Y - 1, before.Y + 1);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>本文と大きさを持つコントロールを作る。</summary>
    /// <param name="text">本文。</param>
    /// <returns>コントロール。</returns>
    private static StyledTextControl CreateEditor(string text)
        => new()
        {
            Text = RichText.Create(text, TextDefaults.DefaultStyle),
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
