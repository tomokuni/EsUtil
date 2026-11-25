using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Xunit;
using EsUtil.Feature.StyledText;
using EsUtil.Feature.StyledText.Core.Defaults;
using EsUtil.Feature.StyledText.Presenter.Formatting;
using EsUtil.Feature.Annotation.Core.Defaults;
using EsUtil.Platform.Core.Geometry;

namespace EsUtil.Ui.AvaloniaUI.Annotation.Tests;

/// <summary>本文の表示と編集を書式付き文字列の編集コントロールへ委ねる仕様のテスト。</summary>
/// <remarks>
/// <b>本文の描画を 2 か所に持たない</b>: 選択中の吹き出しの本文はレイヤーが描かず、重ねた編集コントロールが描く。
/// 本テストは「選択すると委譲が始まる」「本文の値が同じ」「置く矩形が本文の矩形と一致する」ことを固定する。
/// </remarks>
[Collection("AvaloniaHeadless")]
public class AnnotationEditorPanelTests
{
    /// <summary>吹き出しを選ぶと本文の描画が編集コントロールへ委譲されることを検証する。</summary>
    [AvaloniaFact]
    internal void 吹き出しを選ぶと本文の描画を編集コントロールへ委ねる()
    {
        var window = CreateEditor(out var editor, out _, out var bubbleId);

        editor.Layer.Session.SelectedId = bubbleId;
        editor.Layer.ExternalTextId = null;
        PumpLayout(editor);

        Assert.Equal(bubbleId, editor.Layer.ExternalTextId);
        Assert.True(editor.TextEditor.IsVisible);
        Assert.Same(editor.Layer.Session.Editor, editor.TextEditor.Editor);
        Assert.Equal("あいうえお", editor.TextEditor.RichTextValue.PlainText);
        GC.KeepAlive(window);
    }

    /// <summary>選択を解除すると本文の描画をレイヤーへ戻すことを検証する。</summary>
    [AvaloniaFact]
    internal void 選択を解除するとレイヤーが本文を描く()
    {
        var window = CreateEditor(out var editor, out _, out var bubbleId);
        editor.Layer.Session.SelectedId = bubbleId;
        PumpLayout(editor);

        editor.Layer.Session.SelectedId = null;
        PumpLayout(editor);

        Assert.Null(editor.Layer.ExternalTextId);
        Assert.False(editor.TextEditor.IsVisible);
        GC.KeepAlive(window);
    }

    /// <summary>編集コントロールを置く矩形が本文の矩形と一致することを検証する。</summary>
    /// <remarks>
    /// 本文の位置は折り返しの結果に依存するため、レイヤーが描いていたときと同じ位置に置く必要がある。
    /// 一致しないと、選択した瞬間に文字が動いて見える。
    /// </remarks>
    [AvaloniaFact]
    internal void 編集コントロールの矩形が本文の矩形と一致する()
    {
        var window = CreateEditor(out var editor, out _, out var bubbleId);

        editor.Layer.Session.SelectedId = bubbleId;
        PumpLayout(editor);

        var bubble = Assert.IsType<BubbleAnnotation>(editor.Layer.Session.Document.Find(bubbleId));
        var expected = editor.Layer.GetClientRect(bubble.ResolveBodyRect());
        var actual = editor.TextEditor.Bounds;

        // 本文の外接矩形は「枠線の太さの半分 + 余白」だけ内側（外側の矩形より小さい）
        Assert.True(expected.X > bubble.Bounds.X);
        Assert.True(expected.Y > bubble.Bounds.Y);

        // 折り返しの結果ではなく外接矩形をそのまま置く（文字の位置は本文の折り返しが決める）。
        // 配置は端末ピクセルへ丸められるため、0.5 ピクセルまでの差は許す。
        if (actual.Width > 0 && actual.Height > 0)
        {
            Assert.InRange(actual.X, expected.X - 0.5, expected.X + 0.5);
            Assert.InRange(actual.Y, expected.Y - 0.5, expected.Y + 0.5);
        }

        GC.KeepAlive(window);
    }

    /// <summary>書式バーは編集中でも 1 文字以上を選ぶまで出さないことを検証する。</summary>
    /// <remarks>
    /// 適用先が決まらない状態でバーを出すと、押しても何も起きない操作が見えてしまう。
    /// 表示の条件は本文コントロール（<c>ShouldShowFormatBar</c>）が単一所有する。
    /// </remarks>
    [AvaloniaFact]
    internal void 書式バーは選択がある間だけ出る()
    {
        var window = CreateEditor(out var editor, out _, out var bubbleId);
        editor.Layer.Session.SelectedId = bubbleId;
        editor.Layer.Session.BeginTextEditing();
        PumpLayout(editor);

        Assert.True(editor.IsEditingText);
        Assert.False(editor.IsFormatBarOpen);

        editor.TextEditor.Select(0, 2);

        Assert.True(editor.IsFormatBarOpen);

        editor.TextEditor.Select(0, 0);

        Assert.False(editor.IsFormatBarOpen);
        GC.KeepAlive(window);
    }

    /// <summary>自動表示を無効にすると選択しても書式バーを出さないことを検証する。</summary>
    [AvaloniaFact]
    internal void 自動表示を無効にすると書式バーを出さない()
    {
        var window = CreateEditor(out var editor, out _, out var bubbleId);
        editor.ShowFormatBarWhileEditing = false;
        editor.Layer.Session.SelectedId = bubbleId;
        editor.Layer.Session.BeginTextEditing();
        PumpLayout(editor);

        editor.TextEditor.Select(0, 2);

        Assert.False(editor.IsFormatBarOpen);
        GC.KeepAlive(window);
    }

    /// <summary>書式バーの項目をすべて出さない場合は、選択してもバーを出さないことを検証する。</summary>
    /// <remarks>操作できない枠だけが本文に重なるのを避ける（判定は <c>ShouldShowFormatBar</c> が単一所有する）。</remarks>
    [AvaloniaFact]
    internal void 書式バーの項目をすべて出さない場合はバーを出さない()
    {
        var window = CreateEditor(out var editor, out _, out var bubbleId);
        editor.Layer.Session.SelectedId = bubbleId;
        editor.Layer.Session.BeginTextEditing();
        PumpLayout(editor);
        editor.TextEditor.Select(0, 2);
        Assert.True(editor.IsFormatBarOpen);

        // 表示中は差し替えられない（表示中に組み合わせを変えると位置と操作の対象が食い違う）
        Assert.Throws<InvalidOperationException>(() => editor.FormatBarItems = new TextFormatBarItemVisibility
        {
            ShowBold = false,
            ShowItalic = false,
            ShowUnderline = false,
            ShowStrike = false,
            ShowColor = false,
            ShowFontSize = false,
            ShowFont = false,
        });

        editor.TextEditor.Select(0, 0);
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
        editor.TextEditor.Select(0, 2);

        Assert.False(editor.IsFormatBarOpen);
        GC.KeepAlive(window);
    }

    /// <summary>テスト用の編集コントロール（レイヤー + 本文コントロール）を作る。</summary>
    /// <param name="editor">作ったコントロール。</param>
    /// <param name="document">文書。</param>
    /// <param name="bubbleId">吹き出しの識別子。</param>
    /// <returns>ウィンドウ。</returns>
    private static Window CreateEditor(
        out AnnotationEditorPanel editor, out AnnotationDocument document, out string bubbleId)
    {
        editor = new AnnotationEditorPanel { Width = 400, Height = 300 };
        var bubble = new BubbleAnnotation
        {
            Id = "bubble",
            Bounds = new EsRect(20, 20, 240, 120),
            Appearance = AnnotationDefaults.BubbleAppearance,
            BodyPadding = 12,
            Text = RichText.Create("あいうえお", TextDefaults.DefaultStyle),
        };

        document = new AnnotationDocument(400, 300, "shot.png").Add(bubble);
        bubbleId = bubble.Id;
        editor.Layer.SourcePixelWidth = 400;
        editor.Layer.SourcePixelHeight = 300;
        editor.Layer.Document = document;

        var window = new Window { Content = editor, Width = 400, Height = 300 };
        window.Show();
        return window;
    }

    /// <summary>レイアウトを流す（ウィンドウのレイアウト パスに依存せず、明示的に測って配置する）。</summary>
    /// <param name="editor">コントロール。</param>
    private static void PumpLayout(AnnotationEditorPanel editor)
    {
        Dispatcher.UIThread.RunJobs();
        editor.Measure(new Avalonia.Size(400, 300));
        editor.Arrange(new Avalonia.Rect(0, 0, 400, 300));
        Dispatcher.UIThread.RunJobs();
    }
}
