using Xunit;
using EsUtil.Feature.StyledText;
using EsUtil.Feature.StyledText.Core.Defaults;
using EsUtil.Feature.Annotation.Core.Defaults;
using EsUtil.Platform.Core.Geometry;

namespace EsUtil.Ui.WinForms.Annotation.Tests;

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
        var editor = CreateEditor(out var document, out var bubbleId);

        editor.Layer.Session.SelectedId = bubbleId;
        editor.Layer.ExternalTextId = null;
        editor.PerformLayout();
        editor.SyncTextEditor();

        Assert.Equal(bubbleId, editor.Layer.ExternalTextId);
        Assert.True(editor.TextEditor.Visible);
        Assert.Same(editor.Layer.Session.TextEditState, editor.TextEditor.EditState);
        Assert.Equal("あいうえお", editor.TextEditor.RichTextValue.PlainText);
        GC.KeepAlive(document);
    }

    /// <summary>選択を解除すると本文の描画をレイヤーへ戻すことを検証する。</summary>
    [Fact]
    internal void 選択を解除するとレイヤーが本文を描く()
    {
        var editor = CreateEditor(out _, out var bubbleId);
        editor.Layer.Session.SelectedId = bubbleId;
        editor.PerformLayout();
        editor.SyncTextEditor();

        editor.Layer.Session.SelectedId = null;
        editor.PerformLayout();
        editor.SyncTextEditor();

        Assert.Null(editor.Layer.ExternalTextId);
        Assert.False(editor.TextEditor.Visible);
    }

    /// <summary>編集コントロールの矩形が本文の矩形と一致することを検証する。</summary>
    [Fact]
    internal void 編集コントロールの矩形が本文の矩形と一致する()
    {
        var editor = CreateEditor(out var document, out var bubbleId);
        editor.Layer.Session.SelectedId = bubbleId;
        editor.PerformLayout();
        editor.SyncTextEditor();

        var bubble = Assert.IsType<BubbleAnnotation>(document.Find(bubbleId));
        var expected = editor.Layer.GetClientRect(bubble.ResolveBodyRect());

        // 配置は端末ピクセルへ丸められるため、1 ピクセルまでの差は許す
        Assert.InRange(editor.TextEditor.Bounds.X, (int)expected.X - 1, (int)expected.X + 1);
        Assert.InRange(editor.TextEditor.Bounds.Y, (int)expected.Y - 1, (int)expected.Y + 1);
        Assert.True(editor.TextEditor.Bounds.Width > 0);
        Assert.True(editor.TextEditor.Bounds.Height > 0);
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
