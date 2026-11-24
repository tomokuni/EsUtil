using Aprillz.MewUI;
using Xunit;
using EsUtil.Feature.StyledText;
using EsUtil.Feature.StyledText.Core.Defaults;
using EsUtil.Feature.Annotation.Core.Defaults;
using EsUtil.Platform.Core.Geometry;

namespace EsUtil.Ui.MewUI.Annotation.Tests;

/// <summary>本文の表示を書式付き文字列の編集コントロールへ委ねる仕様のテスト。</summary>
/// <remarks>
/// <b>本文の描画を 2 か所に持たない</b>: 選択中の吹き出しの本文はレイヤーが描かず、重ねた編集コントロールが描く。
/// </remarks>
public class AnnotationEditorPanelTests
{
    /// <summary>吹き出しを選ぶと本文の描画が編集コントロールへ委譲されることを検証する。</summary>
    [Fact]
    internal void 吹き出しを選ぶと本文の描画を編集コントロールへ委ねる()
    {
        var editor = CreateEditor(out _, out var bubbleId);

        editor.Layer.Session.SelectedId = bubbleId;
        editor.Layer.ExternalTextId = null;
        editor.Measure(new Size(400, 300));
        editor.Arrange(new Rect(0, 0, 400, 300));
        editor.SyncTextEditor();

        Assert.Equal(bubbleId, editor.Layer.ExternalTextId);
        Assert.True(editor.TextEditor.IsVisible);
        Assert.Same(editor.Layer.Session.TextEditor, editor.TextEditor.Editor);
        Assert.Equal("あいうえお", editor.TextEditor.RichTextValue.PlainText);
    }

    /// <summary>選択を解除すると本文の描画をレイヤーへ戻すことを検証する。</summary>
    [Fact]
    internal void 選択を解除するとレイヤーが本文を描く()
    {
        var editor = CreateEditor(out _, out var bubbleId);
        editor.Layer.Session.SelectedId = bubbleId;
        editor.Measure(new Size(400, 300));
        editor.Arrange(new Rect(0, 0, 400, 300));
        editor.SyncTextEditor();

        editor.Layer.Session.SelectedId = null;
        editor.Measure(new Size(400, 300));
        editor.Arrange(new Rect(0, 0, 400, 300));
        editor.SyncTextEditor();

        Assert.Null(editor.Layer.ExternalTextId);
        Assert.False(editor.TextEditor.IsVisible);
    }

    /// <summary>テスト用の編集コントロールを作る。</summary>
    /// <param name="document">文書。</param>
    /// <param name="bubbleId">吹き出しの識別子。</param>
    /// <returns>コントロール。</returns>
    private static AnnotationEditorPanel CreateEditor(out AnnotationDocument document, out string bubbleId)
    {
        var editor = new AnnotationEditorPanel { Width = 400, Height = 300 };
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
        return editor;
    }
}
