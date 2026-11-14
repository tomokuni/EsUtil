using EsUtil.Feature.Thumbnail.Logging;
using Microsoft.Extensions.Logging;
using Xunit;

namespace EsUtil.Feature.Thumbnail.Tests.Logging;

/// <summary>選択のログ定義（接頭辞・レベル・書式）の単体テスト。</summary>
/// <remarks>ログは UI 初期化なしで検証できる（LoggerMessage の定義は UI フレームワークに依存しない）。</remarks>
[Collection(ThumbnailLogPrefix.Name)]
public class ThumbnailLogMessageTests
{
    /// <summary>既定の接頭辞が規約の値であることを検証する。</summary>
    [Fact]
    internal void DefaultPrefix_規約の接頭辞を持つ()
        => Assert.Equal("[Thumbnail]", ThumbnailLogMessage.DefaultPrefix);

    /// <summary>選択の変更が Debug として書式どおりに記録されることを検証する。</summary>
    [Fact]
    internal void LogSelectionChanged_接頭辞と添字を含むメッセージを出力する()
    {
        var logger = new RecordingLogger();

        try
        {
            ThumbnailLogMessage.Prefix = "[Test]";
            logger.LogSelectionChanged(-1, 2);
        }
        finally
        {
            ThumbnailLogMessage.Prefix = ThumbnailLogMessage.DefaultPrefix;
        }

        Assert.Equal(LogLevel.Debug, Assert.Single(logger.Levels));
        Assert.Equal("[Test] 選択: -1 → 2", Assert.Single(logger.Messages));
    }

    /// <summary>同値の再設定では記録しないことを検証する。</summary>
    [Fact]
    internal void LogSelectionChanged_同値では記録しない()
    {
        var logger = new RecordingLogger();

        logger.LogSelectionChanged(3, 3);

        Assert.Empty(logger.Messages);
    }

    /// <summary>設定の変更が変化した項目だけ記録されることを検証する。</summary>
    [Fact]
    internal void LogOptionsChanged_変化した項目だけを記録する()
    {
        var logger = new RecordingLogger();

        try
        {
            ThumbnailLogMessage.Prefix = "[Test]";
            logger.LogOptionsChanged(
                ThumbnailOptions.Default,
                ThumbnailOptions.Default with { RowSpace = 4, MaximumColumns = 3 });
        }
        finally
        {
            ThumbnailLogMessage.Prefix = ThumbnailLogMessage.DefaultPrefix;
        }

        Assert.Equal(
            ["[Test] 設定: RowSpace 8 → 4", "[Test] 設定: MaximumColumns 0 → 3"],
            logger.Messages);
        Assert.All(logger.Levels, level => Assert.Equal(LogLevel.Information, level));
    }

    /// <summary>同値の設定では何も記録しないことを検証する。</summary>
    [Fact]
    internal void LogOptionsChanged_同値では記録しない()
    {
        var logger = new RecordingLogger();

        logger.LogOptionsChanged(ThumbnailOptions.Default, ThumbnailOptions.Default);

        Assert.Empty(logger.Messages);
    }

    /// <summary>外観の変更が色・体裁・影を含めて変化した項目だけ記録されることを検証する。</summary>
    [Fact]
    internal void LogAppearanceChanged_変化した項目だけを記録する()
    {
        var logger = new RecordingLogger();
        var before = ThumbnailAppearance.Default;

        try
        {
            ThumbnailLogMessage.Prefix = "[Test]";
            logger.LogAppearanceChanged(
                before,
                before with { LabelFontSize = 13, HighlightedLabelArgb = 0xFF0078D4, Shadow = null });
        }
        finally
        {
            ThumbnailLogMessage.Prefix = ThumbnailLogMessage.DefaultPrefix;
        }

        Assert.Equal(
            [
                "[Test] 設定: LabelFontSize 11 → 13",
                "[Test] 設定: HighlightedLabelArgb 0x00000000 → 0xFF0078D4",
                "[Test] 設定: Shadow ThumbnailShadowPlan → null",
            ],
            logger.Messages);
    }

    /// <summary>外観が同値の場合は何も記録しないことを検証する。</summary>
    [Fact]
    internal void LogAppearanceChanged_同値では記録しない()
    {
        var logger = new RecordingLogger();

        logger.LogAppearanceChanged(ThumbnailAppearance.Default, ThumbnailAppearance.Default);

        Assert.Empty(logger.Messages);
    }
}
