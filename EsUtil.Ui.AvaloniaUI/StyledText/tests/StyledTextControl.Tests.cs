using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Xunit;
using EsUtil.Feature.StyledText;
using EsUtil.Feature.StyledText.Core.Defaults;
using EsUtil.Feature.StyledText.Presenter.Formatting;
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
        editor.RichTextChanged += (_, _) => changed++;
        editor.BeginEditing();
        editor.Select(1, 0);

        var updated = editor.ReplaceSelection("X");

        Assert.True(updated);
        Assert.Equal("あXいう", editor.RichTextValue.PlainText);
        Assert.Equal(2, editor.CaretIndex);
        Assert.Equal(1, changed);
    }

    /// <summary>本文が変わらない操作では通知しないことを検証する。</summary>
    [AvaloniaFact]
    internal void 本文が変わらない操作では通知しない()
    {
        var editor = CreateEditor("あいう");
        var changed = 0;
        editor.RichTextChanged += (_, _) => changed++;
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

        Assert.Equal("ab", editor.RichTextValue.PlainText);
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
        Assert.True(editor.RichTextValue.StyleAt(1).IsBold);
        Assert.False(editor.RichTextValue.StyleAt(0).IsBold);

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

        editor.RichTextValue = RichText.Create("あ", TextDefaults.DefaultStyle);

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
        Assert.False(editor.RichTextValue.StyleAt(0).IsBold);
        Assert.True(editor.RichTextValue.StyleAt(1).IsBold);
        Assert.True(editor.RichTextValue.StyleAt(2).IsBold);
        Assert.False(editor.RichTextValue.StyleAt(3).IsBold);
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
        Assert.Equal("あいう", editor.RichTextValue.PlainText);
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

    /// <summary>書式バーは 1 文字以上を選ぶまで表示せず、自動表示を切ると選んでも表示しないことを検証する。</summary>
    [AvaloniaFact]
    internal void ShouldShowFormatBar_1文字以上を選んだときだけ表示する()
    {
        var editor = CreateEditor("あいう");
        editor.BeginEditing();

        Assert.False(editor.ShouldShowFormatBar);

        editor.Select(0, 1);
        Assert.True(editor.ShouldShowFormatBar);

        editor.Select(0, 0);
        Assert.False(editor.ShouldShowFormatBar);

        editor.Select(0, 1);
        editor.ShowFormatBarWhileEditing = false;
        Assert.False(editor.ShouldShowFormatBar);
    }
    /// <summary>書式バーの項目をすべて出さない場合は、選んでいても表示しないことを検証する。</summary>
    [AvaloniaFact]
    internal void ShouldShowFormatBar_項目をすべて出さない場合は表示しない()
    {
        var editor = CreateEditor("あいう");
        editor.BeginEditing();
        editor.Select(0, 1);

        Assert.True(editor.ShouldShowFormatBar);

        editor.FormatBarItems = new TextFormatBarItemVisibility
        {
            ShowBold = false,
            ShowItalic = false,
            ShowUnderline = false,
            ShowStrike = false,
            ShowColor = false,
            ShowFontSize = false,
            ShowFont = false,
        };

        Assert.False(editor.ShouldShowFormatBar);
    }

    /// <summary>共有した編集器の本文と状態がそのままコントロールの値になることを検証する。</summary>
    /// <remarks>
    /// ホスト（注釈の編集セッションなど）が本文と状態を持つ場合、同じ実体を渡して 1 つにする
    /// （別の実体を持つとキャレットと本文が食い違う）。
    /// </remarks>
    [AvaloniaFact]
    internal void Editor_共有した実体の本文を使う()
    {
        var shared = new EsUtil.Feature.StyledText.Core.Editing.TextEditor
        {
            Text = RichText.Create("共有", TextDefaults.DefaultStyle),
        };
        var editor = new StyledTextControl
        {
            Editor = shared,
            MeasureText = FixedTextMetrics.Measure,
        };

        editor.BeginEditing();

        Assert.Same(shared, editor.Editor);
        Assert.Equal("共有", editor.RichTextValue.PlainText);
        Assert.Equal(2, editor.CaretIndex);
    }

    /// <summary>書式バーは本コントロールが単一所有し、項目を変えると作り直されることを検証する。</summary>
    /// <remarks>ホストは <c>FormatBar.View</c> を置くだけでよい（生成と配線を書き写さない）。</remarks>
    [AvaloniaFact]
    internal void FormatBar_コントロールが所有し項目を変えると作り直す()
    {
        var editor = CreateEditor("あいう");

        var first = editor.FormatBar;
        Assert.Same(first, editor.FormatBar);

        editor.FormatBarItems = TextFormatBarItemVisibility.Default with { ShowColor = false };

        Assert.NotSame(first, editor.FormatBar);
        Assert.Same(editor.FormatBar.View, editor.FormatBar.View);
    }

    /// <summary>ポップアップで出す設定では、1 文字以上を選んでいる間だけバーを出すことを検証する。</summary>
    /// <remarks>出す条件はホストが置く場合と同じ（<c>ShouldShowFormatBar</c>）である。</remarks>
    [AvaloniaFact]
    internal void FormatBarHosting_ポップアップは選択がある間だけ出す()
    {
        var editor = CreateEditor("あいう");
        var window = new Window { Content = editor, Width = 300, Height = 100 };
        window.Show();
        try
        {
            editor.FormatBarHosting = TextFormatBarHosting.Popup;
            editor.BeginEditing();
            Pump();

            Assert.False(editor.IsFormatBarOpen);

            editor.Select(0, 2);
            Pump();

            Assert.True(editor.IsFormatBarOpen);

            editor.Select(0, 0);
            Pump();

            Assert.False(editor.IsFormatBarOpen);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>自前で置く設定では、コントロールがポップアップを出さないことを検証する。</summary>
    [AvaloniaFact]
    internal void FormatBarHosting_自前で置く場合はポップアップを出さない()
    {
        var editor = CreateEditor("あいう");
        var window = new Window { Content = editor, Width = 300, Height = 100 };
        window.Show();
        try
        {
            editor.BeginEditing();
            editor.Select(0, 2);
            Pump();

            Assert.Equal(TextFormatBarHosting.Custom, editor.FormatBarHosting);
            Assert.True(editor.ShouldShowFormatBar);
            Assert.False(editor.IsFormatBarOpen);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>自前で置く設定へ戻すと、ポップアップから外したバーを自分のレイアウトへ置き直せることを検証する。</summary>
    /// <remarks>バーの要素は 1 つの親しか持てないため、出し方を切り替えても付け替えで失敗しない。</remarks>
    [AvaloniaFact]
    internal void FormatBarHosting_自前へ戻すとバーを置き直せる()
    {
        var editor = CreateEditor("あいう");
        var host = new Border();
        var window = new Window
        {
            Width = 300,
            Height = 100,
            Content = new StackPanel { Children = { host, editor } },
        };
        window.Show();
        try
        {
            editor.FormatBarHosting = TextFormatBarHosting.Popup;
            editor.BeginEditing();
            editor.Select(0, 2);
            Pump();
            Assert.True(editor.IsFormatBarOpen);

            editor.FormatBarHosting = TextFormatBarHosting.Custom;

            Assert.False(editor.IsFormatBarOpen);
            host.Child = editor.FormatBar.View;
            Assert.Same(host, editor.FormatBar.View.Parent);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>ホストが既にバーを置いている状態からポップアップへ切り替えても落ちないことを検証する。</summary>
    /// <remarks>
    /// バーの要素は 1 つの親しか持てない。 ホストのレイアウトに置かれたままポップアップのウィンドウへ
    /// 載せようとすると <see cref="InvalidOperationException"/> になるため、載せる前に外す。
    /// </remarks>
    [AvaloniaFact]
    internal void FormatBarHosting_ホストが置いている状態からポップアップへ切り替えられる()
    {
        var editor = CreateEditor("あいう");
        var host = new Border { Child = editor.FormatBar.View };
        var window = new Window
        {
            Width = 300,
            Height = 100,
            Content = new StackPanel { Children = { host, editor } },
        };
        window.Show();
        try
        {
            editor.FormatBarHosting = TextFormatBarHosting.Popup;
            editor.BeginEditing();
            editor.Select(0, 2);
            Pump();

            Assert.True(editor.IsFormatBarOpen);
            Assert.True(
                host.Child is null,
                $"host.Child = {host.Child?.GetType().Name}, parent = {editor.FormatBar.View.Parent?.GetType().Name}");
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>ポップアップのまま同じバーを表示し直しても落ちないことを検証する。</summary>
    /// <remarks>選択が変わるたびに表示し直すため、要素の付け替えが繰り返し起きる。</remarks>
    [AvaloniaFact]
    internal void FormatBarHosting_ポップアップのまま選択を変えても落ちない()
    {
        var editor = CreateEditor("あいうえお");
        var window = new Window { Width = 300, Height = 100, Content = editor };
        window.Show();
        try
        {
            editor.FormatBarHosting = TextFormatBarHosting.Popup;
            editor.BeginEditing();

            editor.Select(0, 1);
            Pump();
            editor.Select(1, 2);
            Pump();
            editor.Select(0, 0);
            Pump();
            editor.Select(2, 2);
            Pump();

            Assert.True(editor.IsFormatBarOpen);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>ポップアップのままバーを作り直しても落ちないことを検証する。</summary>
    /// <remarks>
    /// バーの項目を変えるとバーは作り直される。 そのたびにウィンドウの中身を差し替えると、要素の付け替えが
    /// 重なって「既に親がある」で落ちるため、中身は変わらない根へ載せ替える。
    /// </remarks>
    [AvaloniaFact]
    internal void FormatBarHosting_ポップアップのままバーを作り直しても落ちない()
    {
        var editor = CreateEditor("あいうえお");
        var window = new Window { Width = 300, Height = 100, Content = editor };
        window.Show();
        try
        {
            editor.FormatBarHosting = TextFormatBarHosting.Popup;
            editor.BeginEditing();
            editor.Select(0, 2);
            Pump();
            Assert.True(editor.IsFormatBarOpen);

            var first = editor.FormatBar.View;
            editor.FormatBarItems = editor.FormatBarItems with { ShowColor = false };
            Pump();

            Assert.True(editor.IsFormatBarOpen);
            Assert.NotSame(first, editor.FormatBar.View);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>パネルの余白を掴んでドラッグすると、バーが移動することを検証する（実入力の経路）。</summary>
    /// <remarks>
    /// <b>実ヒットテストを通す</b>: ハンドラーを直接呼ばず、ウィンドウ経由の入力で確かめる。
    /// 余白（枠の内側の何も無いところ）を掴めないと、利用者はバーを動かせない。
    /// </remarks>
    [AvaloniaFact]
    internal void FormatBarHosting_ポップアップの余白をドラッグするとバーが動く()
    {
        var editor = CreateEditor("あいうえお");
        var main = new Window { Width = 400, Height = 200, Content = editor };
        main.Show();
        try
        {
            editor.FormatBarHosting = TextFormatBarHosting.Popup;
            editor.BeginEditing();
            editor.Select(0, 2);
            Pump();

            var popup = editor.FormatBarPopupWindow;
            Assert.NotNull(popup);
            Assert.True(popup!.IsVisible, "書式バーのウィンドウが出ていない");

            // 余白の中央（枠 1 DIP + 余白 3/6 DIP の内側で、中身に掛からない位置）を掴んで 20 DIP 動かす
            var before = editor.FormatBarManualPosition;
            var grab = new Point(4, 4);
            popup.MouseMove(grab);
            popup.MouseDown(grab, MouseButton.Left);
            popup.MouseMove(new Point(grab.X + 20, grab.Y + 20));
            popup.MouseUp(new Point(grab.X + 20, grab.Y + 20), MouseButton.Left);
            Pump();

            Assert.True(editor.FormatBarManualPosition is not null, "余白のドラッグが書式バーへ届いていない");
            if (before is { } start)
            {
                Assert.Equal(20, editor.FormatBarManualPosition!.Value.X - start.X, 1);
                Assert.Equal(20, editor.FormatBarManualPosition!.Value.Y - start.Y, 1);
            }
        }
        finally
        {
            main.Close();
        }
    }

    /// <summary>パネルの枠（下地と枠線）が外観どおりに付くことを検証する。</summary>
    /// <remarks>枠が無いと余白の範囲が分からず、掴む場所も見えない。</remarks>
    [AvaloniaFact]
    internal void FormatBarHosting_ポップアップに枠と余白が付く()
    {
        var editor = CreateEditor("あいうえお");
        var main = new Window { Width = 400, Height = 200, Content = editor };
        main.Show();
        try
        {
            editor.FormatBarHosting = TextFormatBarHosting.Popup;
            editor.BeginEditing();
            editor.Select(0, 2);
            Pump();

            var popup = editor.FormatBarPopupWindow;
            Assert.NotNull(popup);
            var root = Assert.IsType<Border>(popup!.Content);
            Assert.NotNull(root.Background);
            Assert.NotNull(root.BorderBrush);
            Assert.True(root.BorderThickness.Left > 0, "枠線が無い");
            Assert.True(root.Padding.Left > 0, "掴む余白が無い");
            Assert.True(
                popup.Width >= editor.FormatBar.View.Bounds.Width + root.Padding.Left + root.Padding.Right,
                $"パネルの幅に余白が含まれていない（window={popup.Width}）");
        }
        finally
        {
            main.Close();
        }
    }

    /// <summary>レイアウトと保留中の処理を流す。</summary>
    private static void Pump() => Dispatcher.UIThread.RunJobs();

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
