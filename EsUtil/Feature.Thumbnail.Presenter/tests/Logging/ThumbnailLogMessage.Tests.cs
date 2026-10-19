using EsUtil.Feature.Thumbnail.Logging;
using Microsoft.Extensions.Logging;
using Xunit;

namespace EsUtil.Feature.Thumbnail.Tests.Logging;

/// <summary>設定（計算に必要な設定・外観）の変更ログの単体テスト。</summary>
/// <remarks>
/// 「値を所有する主体が記録する」規則により、提示層が単一所有する設定と外観の変更を本層が記録する。<br/>
/// <b>変化した項目だけ</b>を出力する（設定・外観の再適用でログが溢れないため）。
/// </remarks>
[Collection(ThumbnailLogPrefix.Name)]
public class ThumbnailLogMessageTests
{
    /// <summary>既定の接頭辞が規約の値であることを検証する。</summary>
    [Fact]
    internal void DefaultPrefix_規約の接頭辞を持つ()
        => Assert.Equal("[Thumbnail]", ThumbnailLogMessage.DefaultPrefix);

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
                ThumbnailOptions.Default with { ItemMargin = 4, MaximumColumns = 3 });
        }
        finally
        {
            ThumbnailLogMessage.Prefix = ThumbnailLogMessage.DefaultPrefix;
        }

        Assert.Equal(
            ["[Test] Setting: ItemMargin 2 → 4", "[Test] Setting: MaximumColumns 0 → 3"],
            logger.Messages);
        Assert.All(logger.Levels, level => Assert.Equal(LogLevel.Debug, level));
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
                "[Test] Setting: LabelFontSize 11 → 13",
                "[Test] Setting: HighlightedLabelArgb 0x00000000 → 0xFF0078D4",
                "[Test] Setting: Shadow ThumbnailShadowPlan → null",
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
