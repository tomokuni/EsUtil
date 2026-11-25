using Xunit;
using Microsoft.Extensions.Logging;
using EsUtil.Feature.Annotation.Logging;
using EsUtil.Platform.Core.Geometry;

namespace EsUtil.Feature.Annotation.Core.Tests;

/// <summary>本文の操作のログ（操作名・キャレット・選択・本文の先頭）を検証する。</summary>
public sealed class AnnotationLogMessageTests
{
    /// <summary>操作名と値を 1 行で出力し、長い本文は先頭 100 文字へ切り詰める。</summary>
    [Fact]
    internal void LogTextOperation_操作の内容と値を出力する()
    {
        var logger = new RecordingLogger();
        var text = new string('あ', 120);

        logger.LogTextOperation("貼り付け", caret: 3, selectionStart: 1, selectionLength: 2, text);

        var message = Assert.Single(logger.Messages);
        Assert.Equal(LogLevel.Debug, Assert.Single(logger.Levels));
        Assert.Contains("操作: 貼り付け キャレット=3 選択=(1, 2) 本文=", message, StringComparison.Ordinal);
        Assert.Contains("…", message, StringComparison.Ordinal);
        Assert.Contains(new string('あ', 100), message, StringComparison.Ordinal);
    }

    /// <summary>書式を適用した範囲だけを出力し、長さ 0 の範囲は本文全体を出力する。</summary>
    [Fact]
    internal void LogTextStyleApplied_適用した範囲の文字だけを出力する()
    {
        var logger = new RecordingLogger();

        logger.LogTextStyleApplied("abcdef", start: 2, length: 3);
        logger.LogTextStyleApplied("abcdef", start: 0, length: 0);

        Assert.Equal(2, logger.Messages.Count);
        Assert.Contains("書式を適用: 開始=2 長さ=3 本文=cde", logger.Messages[0], StringComparison.Ordinal);
        Assert.Contains("書式を適用: 開始=0 長さ=0 本文=abcdef", logger.Messages[1], StringComparison.Ordinal);
    }

    /// <summary>注釈の削除と重なり順の変更は、操作の対象（種別・識別子）と値を Debug で記録する。</summary>
    [Fact]
    internal void LogAnnotationRemoved_LogOrderChanged_対象と値をDebugで出力する()
    {
        var logger = new RecordingLogger();

        logger.LogAnnotationRemoved(AnnotationKind.Bubble, "abc", new EsRect(10, 20, 30, 40), "xy");
        logger.LogOrderChanged(toFront: true, "abc");
        logger.LogOrderChanged(toFront: false, "abc");

        Assert.Equal(3, logger.Messages.Count);
        Assert.All(logger.Levels, level => Assert.Equal(LogLevel.Debug, level));
        Assert.Contains(
            "注釈を削除: 種別=Bubble 識別子=abc 位置=(10, 20) 大きさ=(30, 40) 本文=xy",
            logger.Messages[0], StringComparison.Ordinal);
        Assert.Contains("重なり順: 最前面へ 識別子=abc", logger.Messages[1], StringComparison.Ordinal);
        Assert.Contains("重なり順: 最背面へ 識別子=abc", logger.Messages[2], StringComparison.Ordinal);
    }

    /// <summary>注釈の作成は対象（種別・識別子）と値（位置・大きさ）を Debug で記録する。</summary>
    [Fact]
    internal void LogAnnotationCreated_対象と位置と大きさをDebugで出力する()
    {
        var logger = new RecordingLogger();

        logger.LogAnnotationCreated(AnnotationKind.Frame, "abc", new EsRect(1, 2, 30, 40));

        var message = Assert.Single(logger.Messages);
        Assert.Equal(LogLevel.Debug, Assert.Single(logger.Levels));
        Assert.Contains(
            "注釈を作成: 種別=Frame 識別子=abc 位置=(1, 2) 大きさ=(30, 40)", message, StringComparison.Ordinal);
    }

    /// <summary>ドラッグ中の位置と大きさの変更は、操作の対象と値を Trace で記録する。</summary>
    [Fact]
    internal void LogAnnotationBoundsChanged_位置と大きさをTraceで出力する()
    {
        var logger = new RecordingLogger();

        logger.LogAnnotationBoundsChanged(new EsRect(10, 20, 30.5, 40));

        var message = Assert.Single(logger.Messages);
        Assert.Equal(LogLevel.Trace, Assert.Single(logger.Levels));
        Assert.Contains("位置と大きさ: 位置=(10, 20) 大きさ=(30.5, 40)", message, StringComparison.Ordinal);
    }

    /// <summary>しっぽと外観の変更は、変更後の値を Trace で記録する。</summary>
    [Fact]
    internal void LogAnnotationTailChanged_LogAnnotationAppearanceChanged_値をTraceで出力する()
    {
        var logger = new RecordingLogger();

        logger.LogAnnotationTailChanged(new BubbleTail(new EsPoint(5, 6), BubbleEdge.Left, 0.5, 26));
        logger.LogAnnotationAppearanceChanged(new AnnotationAppearance(
            System.Drawing.Color.FromArgb(255, 255, 0, 0), 3, System.Drawing.Color.Transparent, 4));

        Assert.Equal(2, logger.Messages.Count);
        Assert.All(logger.Levels, level => Assert.Equal(LogLevel.Trace, level));
        Assert.Contains("しっぽ: 先端=(5, 6) 辺=Left 付け根=0.5 幅=26", logger.Messages[0], StringComparison.Ordinal);
        Assert.Contains("外観: 枠線=#FFFF0000 太さ=3 内側=#00FFFFFF 角丸=4", logger.Messages[1], StringComparison.Ordinal);
    }

    /// <summary>注釈の選択の変更は、操作の対象（種別・識別子）を Debug で記録する。</summary>
    [Fact]
    internal void LogSelectionChanged_対象をDebugで出力する()
    {
        var logger = new RecordingLogger();

        logger.LogSelectionChanged(AnnotationKind.Bubble, "abc");
        logger.LogSelectionChanged(kind: null, id: null);

        Assert.Equal(2, logger.Messages.Count);
        Assert.All(logger.Levels, level => Assert.Equal(LogLevel.Debug, level));
        Assert.Contains("選択: 種別=Bubble 識別子=abc", logger.Messages[0], StringComparison.Ordinal);
        Assert.Contains("選択: 種別=none 識別子=none", logger.Messages[1], StringComparison.Ordinal);
    }

    /// <summary>本文をドラッグで選択している間に書式バーが移動した位置を Trace で記録する。</summary>
    [Fact]
    internal void LogPanelMoved_移動先をTraceで出力する()
    {
        var logger = new RecordingLogger();

        logger.LogPanelMoved("FormatBar", new EsPoint(10, 20));

        var message = Assert.Single(logger.Messages);
        Assert.Equal(LogLevel.Trace, Assert.Single(logger.Levels));
        Assert.Contains("パネル移動: FormatBar 位置=(10, 20)", message, StringComparison.Ordinal);
    }

    /// <summary>真偽の設定値は変化したときだけ出力する。</summary>
    [Fact]
    internal void LogSettingChanged_変化したときだけ出力する()
    {
        var logger = new RecordingLogger();

        logger.LogSettingChanged("ShowFormatBarWhileEditing", false, false);
        Assert.Empty(logger.Messages);

        logger.LogSettingChanged("ShowFormatBarWhileEditing", true, false);
        var message = Assert.Single(logger.Messages);
        Assert.Equal(LogLevel.Information, Assert.Single(logger.Levels));
        Assert.Contains("設定: ShowFormatBarWhileEditing True → False", message, StringComparison.Ordinal);
    }
}
