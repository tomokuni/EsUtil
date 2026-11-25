using Xunit;
using Microsoft.Extensions.Logging;
using EsUtil.Feature.Annotation.Logging;

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

    /// <summary>注釈の削除と重なり順の変更は、操作の対象（種別・識別子）を Debug で記録する。</summary>
    [Fact]
    internal void LogAnnotationRemoved_LogOrderChanged_対象をDebugで出力する()
    {
        var logger = new RecordingLogger();

        logger.LogAnnotationRemoved(AnnotationKind.Bubble, "abc");
        logger.LogOrderChanged(toFront: true, "abc");
        logger.LogOrderChanged(toFront: false, "abc");

        Assert.Equal(3, logger.Messages.Count);
        Assert.All(logger.Levels, level => Assert.Equal(LogLevel.Debug, level));
        Assert.Contains("注釈を削除: 種別=Bubble 識別子=abc", logger.Messages[0], StringComparison.Ordinal);
        Assert.Contains("重なり順: 最前面へ 識別子=abc", logger.Messages[1], StringComparison.Ordinal);
        Assert.Contains("重なり順: 最背面へ 識別子=abc", logger.Messages[2], StringComparison.Ordinal);
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
