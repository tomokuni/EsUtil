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
        Assert.Equal("[Test] Selection: -1 → 2", Assert.Single(logger.Messages));
    }

    /// <summary>同値の再設定では記録しないことを検証する。</summary>
    [Fact]
    internal void LogSelectionChanged_同値では記録しない()
    {
        var logger = new RecordingLogger();

        logger.LogSelectionChanged(3, 3);

        Assert.Empty(logger.Messages);
    }
}
