using EsUtil.Platform.Core.Geometry;
using Xunit;
using EsUtil.Feature.Annotation.Core.History;
using EsUtil.Feature.StyledText;
using EsUtil.Feature.StyledText.Core.Defaults;

namespace EsUtil.Feature.Annotation.Core.Tests;

/// <summary><see cref="AnnotationHistory"/> の仕様テスト（元に戻す・やり直す）。</summary>
/// <remarks>
/// 文書は不変であるため、履歴は記録した文書をそのまま保持できる（記録した過去の状態が
/// その後の編集で書き換わらない）。
/// </remarks>
public class AnnotationHistoryTests
{
    /// <summary>記録した文書へ戻せることを検証する。</summary>
    [Fact]
    internal void Undo_記録した文書へ戻る()
    {
        var history = new AnnotationHistory();
        var empty = new AnnotationDocument(100, 100, "a.png");
        history.Record(empty);

        var withItem = empty.Add(new FrameAnnotation { Bounds = new EsRect(0, 0, 10, 10) });
        var restored = history.Undo(withItem);

        Assert.NotNull(restored);
        Assert.Empty(restored.Items);
        Assert.False(history.CanUndo);
        Assert.True(history.CanRedo);
    }

    /// <summary>元に戻した変更をやり直せることを検証する。</summary>
    [Fact]
    internal void Redo_元に戻した変更をやり直す()
    {
        var history = new AnnotationHistory();
        var empty = new AnnotationDocument(100, 100, "a.png");
        history.Record(empty);

        var withItem = empty.Add(new FrameAnnotation { Bounds = new EsRect(0, 0, 10, 10) });
        var restored = history.Undo(withItem)!;
        var redone = history.Redo(restored);

        Assert.NotNull(redone);
        Assert.Single(redone.Items);
        Assert.True(history.CanUndo);
        Assert.False(history.CanRedo);
    }

    /// <summary>新しい変更を記録するとやり直しが消えることを検証する。</summary>
    [Fact]
    internal void Record_やり直しを消す()
    {
        var history = new AnnotationHistory();
        var empty = new AnnotationDocument(100, 100, "a.png");
        history.Record(empty);
        var withItem = empty.Add(new FrameAnnotation { Bounds = new EsRect(0, 0, 10, 10) });
        var restored = history.Undo(withItem)!;

        history.Record(restored);

        Assert.False(history.CanRedo);
    }

    /// <summary>記録した後に文書を変更しても、記録が変わらないことを検証する。</summary>
    [Fact]
    internal void Record_記録は変更の影響を受けない()
    {
        var history = new AnnotationHistory();
        var document = new AnnotationDocument(100, 100, "a.png")
            .Add(new FrameAnnotation { Bounds = new EsRect(0, 0, 10, 10) });
        history.Record(document);

        // 記録後に文書を変更する（ドラッグ中の移動と同じ）
        var changed = document.WithBounds(document.Items[0].Id, new EsRect(50, 50, 20, 20));

        var restored = history.Undo(changed)!;
        Assert.Equal(new EsRect(0, 0, 10, 10), restored.Items[0].Bounds);
    }

    /// <summary>保持数を超えたら古い記録から捨てることを検証する。</summary>
    [Fact]
    internal void Record_保持数を超えたら古い記録を捨てる()
    {
        var history = new AnnotationHistory(capacity: 2);
        var document = new AnnotationDocument(100, 100);
        history.Record(document);
        history.Record(document);
        history.Record(document);

        Assert.Equal(2, history.UndoCount);
    }

    /// <summary>履歴が空のときは何も返さないことを検証する。</summary>
    [Fact]
    internal void Undo_履歴が空なら何も返さない()
    {
        var history = new AnnotationHistory();
        var document = new AnnotationDocument(100, 100);

        Assert.Null(history.Undo(document));
        Assert.Null(history.Redo(document));
    }

    /// <summary>注釈を変更しても、変更前の文書は変わらないことを検証する。</summary>
    [Fact]
    internal void With_変更前の文書を変えない()
    {
        var bubble = new BubbleAnnotation
        {
            Bounds = new EsRect(0, 0, 100, 50),
            Text = RichText.Create("あい", TextDefaults.DefaultStyle),
            Tail = new BubbleTail(new EsPoint(10, 10), BubbleEdge.Top, 0.5, 20),
        };
        var document = new AnnotationDocument(100, 100).Add(bubble);

        var changed = document
            .WithText(bubble.Id, RichText.Create("うえ", TextDefaults.DefaultStyle))
            .WithTail(bubble.Id, bubble.Tail with { Width = 30 });

        var original = Assert.IsType<BubbleAnnotation>(Assert.Single(document.Items));
        var updated = Assert.IsType<BubbleAnnotation>(Assert.Single(changed.Items));
        Assert.Equal("あい", original.Text.PlainText);
        Assert.Equal(20, original.Tail.Width);
        Assert.Equal("うえ", updated.Text.PlainText);
        Assert.Equal(30, updated.Tail.Width);
        Assert.Equal(bubble.Id, updated.Id);
    }
}
