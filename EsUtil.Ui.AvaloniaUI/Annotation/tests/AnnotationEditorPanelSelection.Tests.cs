using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Xunit;
using EsUtil.Feature.Annotation.Core.Defaults;
using EsUtil.Feature.StyledText;
using EsUtil.Feature.StyledText.Core.Defaults;
using EsUtil.Platform.Core.Geometry;

namespace EsUtil.Ui.AvaloniaUI.Annotation.Tests;

/// <summary>本文の描画先が「選択中の吹き出し」に追随することを検証する。</summary>
/// <remarks>
/// <b>選択を変えたら本文コントロールの位置も変わる</b>: 位置は選択に依存するため、測り直さないと
/// 前の吹き出しの位置に残り、別の吹き出しの本文がそこへ描かれる（2 つの注釈を交互に選ぶと入れ替わる）。
/// </remarks>
[Collection("AvaloniaHeadless")]
public class AnnotationEditorPanelSelectionTests
{
    /// <summary>2 つの吹き出しを交互に選ぶと、本文コントロールが選んだ側の矩形へ移ることを検証する。</summary>
    [AvaloniaFact]
    internal void 選択を変えると本文コントロールが選んだ側の矩形へ移る()
    {
        var window = CreateEditor(out var editor, out var document, out var first, out var second);

        // 1 つ目を選び、レイアウトを確定させる
        editor.Layer.Session.SelectedId = first;
        Dispatcher.UIThread.RunJobs();
        PumpLayout(editor);
        var firstRect = BodyRect(editor, document, first);
        AssertBounds(firstRect, editor.TextEditor.Bounds);
        Assert.Equal(first, editor.Layer.ExternalTextId);

        // 2 つ目へ切り替える。 位置は選択に依存するため、**測り直しが要求される**こと（同期的に無効になる）と、
        // その後のレイアウトで選んだ側の矩形へ移ることを見る
        editor.Layer.Session.SelectedId = second;
        Assert.False(editor.IsMeasureValid);
        Dispatcher.UIThread.RunJobs();
        PumpLayout(editor);
        var secondRect = BodyRect(editor, document, second);
        Assert.Equal(second, editor.Layer.ExternalTextId);
        AssertBounds(secondRect, editor.TextEditor.Bounds);
        Assert.NotEqual(secondRect.X, firstRect.X);

        GC.KeepAlive(window);
    }

    /// <summary>選択中の吹き出しを動かすと、本文コントロールが新しい位置へ追随することを検証する。</summary>
    /// <remarks>
    /// 選択が変わらなくても本文の矩形は動く。 識別子だけで比べると置き直しが要求されず、動かした本文が
    /// 元の位置に取り残される（別の場所を押して再描画されるまで直らない）。
    /// </remarks>
    [AvaloniaFact]
    internal void 選択中の吹き出しを動かすと本文コントロールが追随する()
    {
        var window = CreateEditor(out var editor, out var document, out var first, out _);
        editor.Layer.Session.SelectedId = first;
        Dispatcher.UIThread.RunJobs();
        PumpLayout(editor);
        AssertBounds(BodyRect(editor, document, first), editor.TextEditor.Bounds);

        // 同じ吹き出しを動かす（選択は変わらない）
        editor.Layer.Session.SetBounds(new EsRect(60, 90, 180, 80));
        Assert.False(editor.IsMeasureValid);
        Dispatcher.UIThread.RunJobs();
        PumpLayout(editor);

        AssertBounds(BodyRect(editor, editor.Layer.Document, first), editor.TextEditor.Bounds);
        Assert.Equal(first, editor.Layer.ExternalTextId);
        GC.KeepAlive(window);
    }

    /// <summary>選択を解除すると本文コントロールが隠れ、レイヤーが本文を描く状態へ戻ることを検証する。</summary>
    [AvaloniaFact]
    internal void 選択を解除すると本文コントロールが隠れる()
    {
        var window = CreateEditor(out var editor, out _, out var first, out _);
        editor.Layer.Session.SelectedId = first;
        Dispatcher.UIThread.RunJobs();
        PumpLayout(editor);

        editor.Layer.Session.SelectedId = null;
        Dispatcher.UIThread.RunJobs();
        PumpLayout(editor);

        Assert.Null(editor.Layer.ExternalTextId);
        Assert.False(editor.TextEditor.IsVisible);
        GC.KeepAlive(window);
    }

    /// <summary>レイアウトを 1 度流す（ヘッドレスでは自動で走らないため明示する）。</summary>
    /// <param name="editor">コントロール。</param>
    private static void PumpLayout(AnnotationEditorPanel editor)
    {
        editor.Measure(new Size(400, 300));
        editor.Arrange(new Rect(0, 0, 400, 300));
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>配置が本文の矩形と一致することを検証する（端末ピクセルへの丸めは許す）。</summary>
    /// <param name="expected">本文の矩形。</param>
    /// <param name="actual">本文コントロールの矩形。</param>
    private static void AssertBounds(Rect expected, Rect actual)
    {
        // 配置は端末ピクセルへ丸められるため、1 ピクセルまでの差は許す
        Assert.InRange(actual.X, expected.X - 1, expected.X + 1);
        Assert.InRange(actual.Y, expected.Y - 1, expected.Y + 1);
        Assert.InRange(actual.Width, expected.Width - 1, expected.Width + 1);
        Assert.InRange(actual.Height, expected.Height - 1, expected.Height + 1);
    }

    /// <summary>吹き出しの本文の矩形（このコントロールの座標）を求める。</summary>
    /// <param name="editor">コントロール。</param>
    /// <param name="document">文書。</param>
    /// <param name="id">吹き出しの識別子。</param>
    /// <returns>本文の矩形。</returns>
    private static Rect BodyRect(AnnotationEditorPanel editor, AnnotationDocument document, string id)
        => editor.Layer.GetClientRect(
            Assert.IsType<BubbleAnnotation>(document.Find(id)).ResolveBodyRect());

    /// <summary>テスト用の編集コントロールを作る。</summary>
    /// <param name="editor">コントロール。</param>
    /// <param name="document">文書。</param>
    /// <param name="first">1 つ目の吹き出しの識別子。</param>
    /// <param name="second">2 つ目の吹き出しの識別子。</param>
    /// <returns>ウィンドウ。</returns>
    private static Window CreateEditor(
        out AnnotationEditorPanel editor,
        out AnnotationDocument document,
        out string first,
        out string second)
    {
        editor = new AnnotationEditorPanel { Width = 400, Height = 300 };
        first = "first";
        second = "second";
        var value = new AnnotationDocument(400, 300, "shot.png")
            .Add(new BubbleAnnotation
            {
                Id = first,
                Bounds = new EsRect(10, 10, 180, 80),
                Appearance = AnnotationDefaults.BubbleAppearance,
                BodyPadding = 12,
                Text = RichText.Create("あいうえお", TextDefaults.DefaultStyle),
            })
            .Add(new BubbleAnnotation
            {
                Id = second,
                Bounds = new EsRect(200, 150, 180, 80),
                Appearance = AnnotationDefaults.BubbleAppearance,
                BodyPadding = 12,
                Text = RichText.Create("かきくけこ", TextDefaults.DefaultStyle),
            });

        document = value;
        editor.Layer.SourcePixelWidth = 400;
        editor.Layer.SourcePixelHeight = 300;
        editor.Layer.Document = document;

        var window = new Window { Content = editor, Width = 400, Height = 300 };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return window;
    }
}