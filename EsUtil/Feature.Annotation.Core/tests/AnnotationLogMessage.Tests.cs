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
