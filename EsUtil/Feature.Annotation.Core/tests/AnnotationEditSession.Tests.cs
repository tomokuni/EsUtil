using EsUtil.Feature.Annotation.Presenter.Selection;
using EsUtil.Platform.Core.Geometry;
using Xunit;
using EsUtil.Feature.StyledText.Core.Defaults;

namespace EsUtil.Feature.Annotation.Core.Tests;

/// <summary><see cref="AnnotationEditSession"/> の編集操作（選択・履歴・本文）の仕様テスト。</summary>
/// <remarks>
/// 4 つの UI 版が同じ順序で文書を変更するための規則を固定する。 選択と本文の編集は提示の状態であり、
/// 文書の値の規則（画像の内側へ収める・安全域へ補正する）は Core が持つ。
/// </remarks>
public sealed class AnnotationEditSessionTests
{
    /// <summary>注釈を追加すると選択され、1 手として元に戻せる。</summary>
    [Fact]
    internal void 追加すると選択され元に戻せる()
    {
        var session = CreateSession();
        var changes = new List<AnnotationEditChange>();
        using var subscription = session.Subscribe(changes.Add);

        var id = session.AddFrame(new EsRect(10, 10, 40, 30));

        Assert.Equal(id, session.SelectedId);
        Assert.Single(session.Document.Items);
        Assert.Contains(changes, change => (change & AnnotationEditChange.Document) != 0);
        Assert.Contains(changes, change => (change & AnnotationEditChange.Selection) != 0);

        Assert.True(session.Undo());
        Assert.Empty(session.Document.Items);
        Assert.Null(session.SelectedId);

        Assert.True(session.Redo());
        Assert.Single(session.Document.Items);
    }

    /// <summary>選択中の注釈を削除すると選択が外れ、元に戻すと選択も復元できる。</summary>
    [Fact]
    internal void 削除すると選択が外れる()
    {
        var session = CreateSession();
        var id = session.AddFrame(new EsRect(10, 10, 40, 30));

        Assert.True(session.DeleteSelected());
        Assert.Null(session.SelectedId);
        Assert.Empty(session.Document.Items);

        Assert.True(session.Undo());
        session.SelectedId = id;
        Assert.NotNull(session.Selected);
    }

    /// <summary>既に最前面の注釈を最前面へ移しても履歴に手が増えない。</summary>
    [Fact]
    internal void 変わらない並び替えは履歴に残さない()
    {
        var session = CreateSession();
        session.AddFrame(new EsRect(10, 10, 40, 30));

        Assert.False(session.MoveSelectedToFront());

        Assert.True(session.Undo());
        Assert.Empty(session.Document.Items);
    }

    /// <summary>本文の置き換えは 1 手として記録され、キャレットは本文の範囲へ収まる。</summary>
    [Fact]
    internal void 本文の置き換えを1手として記録する()
    {
        var session = CreateSession();
        session.AddBubble(new EsRect(10, 10, 120, 80), "abcd");
        Assert.True(session.BeginTextEditing());

        Assert.True(session.SetSelectedText("ab"));
        Assert.Equal("ab", session.SelectedPlainText);
        Assert.Equal(2, session.CaretIndex);

        Assert.True(session.Undo());
        Assert.Equal("abcd", session.SelectedPlainText);
    }

    /// <summary>別の注釈を選ぶと本文の編集を終える。</summary>
    [Fact]
    internal void 別の注釈を選ぶと編集を終える()
    {
        var session = CreateSession();
        var first = session.AddBubble(new EsRect(10, 10, 120, 80), "abc");
        var second = session.AddFrame(new EsRect(150, 10, 40, 30));

        session.SelectedId = first;
        Assert.True(session.BeginTextEditing());
        Assert.True(session.IsEditingText);

        session.SelectedId = second;

        Assert.False(session.IsEditingText);
        Assert.Equal(second, session.SelectedId);
    }

    /// <summary>キャレットだけの状態で書式を変えると、既存の文字は変えず以降の入力へ回す。</summary>
    [Fact]
    internal void 選択が無い書式は以降の入力へ回す()
    {
        var session = CreateSession();
        session.AddBubble(new EsRect(10, 10, 200, 80), "abcd");
        session.MoveCaret(2, extend: false);

        Assert.False(session.ApplyTextStyle(TextDefaults.DefaultStyle with { IsBold = true }));
        Assert.False(Assert.IsType<BubbleAnnotation>(session.Selected).Text.StyleAt(2).IsBold);

        Assert.True(session.ReplaceSelection("X"));
        Assert.True(Assert.IsType<BubbleAnnotation>(session.Selected).Text.StyleAt(2).IsBold);
    }

    /// <summary>キャレットの移動と削除はサロゲート ペアを分割しない。</summary>
    [Fact]
    internal void キャレットと削除はサロゲートペアを分割しない()
    {
        var session = CreateSession();
        session.AddBubble(new EsRect(10, 10, 200, 80), "a😀b");
        Assert.True(session.BeginTextEditing());

        session.MoveCaret(0, extend: false);
        session.MoveCaret(session.CaretIndex + 1, extend: false);
        Assert.Equal(1, session.CaretIndex);

        session.MoveCaret(session.CaretIndex + 1, extend: false);
        Assert.Equal(3, session.CaretIndex);

        Assert.True(session.DeleteAtCaret(forward: false));
        Assert.Equal("ab", session.SelectedPlainText);
    }

    /// <summary>編集している注釈が履歴から消えたら、本文の編集を終える。</summary>
    [Fact]
    internal void 履歴で選択が消えたら編集を終える()
    {
        var session = CreateSession();
        session.AddFrame(new EsRect(10, 10, 40, 30));
        session.AddBubble(new EsRect(60, 10, 120, 80), "abc");
        Assert.True(session.BeginTextEditing());

        Assert.True(session.Undo());

        Assert.Null(session.SelectedId);
        Assert.False(session.IsEditingText);
    }

    /// <summary>本文の編集と書式の適用は、操作の内容と値をログへ残す。</summary>
    /// <remarks>
    /// 障害の切り分けでは「どの位置を・どの文字に対して操作したか」が要る。 本文は大きくなりうるため、
    /// 先頭 100 文字へ切り詰める（整形はログ定義が単一所有する）。
    /// </remarks>
    [Fact]
    internal void 本文の編集と書式の適用は値をログへ残す()
    {
        var logger = new RecordingLogger();
        var session = new AnnotationEditSession(new AnnotationDocument(400, 300, "shot.png"), () => logger);
        session.AddBubble(new EsRect(10, 10, 120, 80), "abcd");

        Assert.True(session.BeginTextEditing());
        session.SelectText(0, 2);
        session.ApplyTextStyle(TextDefaults.DefaultStyle with { IsBold = true });
        session.ReplaceSelection("X");

        Assert.Contains(logger.Messages, message => message.Contains("本文の編集: 開始", StringComparison.Ordinal));
        Assert.Contains(
            logger.Messages,
            message => message.Contains("本文の選択: キャレット=2 選択=(0, 2) 本文=abcd", StringComparison.Ordinal));
        Assert.Contains(
            logger.Messages,
            message => message.Contains("書式を適用: 開始=0 長さ=2 本文=ab", StringComparison.Ordinal));
        Assert.Contains(
            logger.Messages,
            message => message.Contains("本文を変更: 文字数=3 本文=Xcd", StringComparison.Ordinal));
    }

    /// <summary>本文の改行は本文の規則（<c>\n</c>）へ揃えて文書へ入れる。</summary>
    [Fact]
    internal void 本文の改行をLFへ揃える()
    {
        var session = CreateSession();

        session.AddBubble(new EsRect(10, 10, 120, 80), "a\r\nb");
        Assert.Equal("a\nb", session.SelectedPlainText);

        Assert.True(session.SetSelectedText("c\r\nd"));
        Assert.Equal("c\nd", session.SelectedPlainText);
    }

    /// <summary>編集セッションを作る（画像は 400x300）。</summary>
    /// <returns>作成したセッション。</returns>
    private static AnnotationEditSession CreateSession()
        => new(new AnnotationDocument(400, 300, "shot.png"));
}
