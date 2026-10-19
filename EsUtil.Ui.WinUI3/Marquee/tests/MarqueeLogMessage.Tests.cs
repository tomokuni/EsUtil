using Microsoft.Extensions.Logging;
using Xunit;

namespace EsUtil.Ui.WinUI3.Marquee.Tests;

/// <summary><see cref="MarqueeLogMessage"/> のテスト（UI 非依存・並列実行）。</summary>
/// <remarks>
/// 本 UI 版は公開操作を提示層へ委譲するため、本コントロールのログ定義が出力するのは<br/>
/// 「委譲した操作の例外」だけである。 接頭辞の既定値と、記録されるレベル・書式を検証する。
/// </remarks>
public class MarqueeLogMessageTests
{
    /// <summary>接頭辞の既定値が他の UI 版と同一であることを検証する。</summary>
    [Fact]
    internal void DefaultPrefix_他のUI版と同一の接頭辞を持つ()
    {
        Assert.Equal("[Marquee]", MarqueeLogMessage.DefaultPrefix);
        Assert.Equal("[Marquee]", MarqueeLogMessage.Prefix);
    }

    /// <summary>操作の例外を Error として書式どおりに記録することを検証する。</summary>
    [Fact]
    internal void LogOperationError_操作の例外を記録する()
    {
        var original = MarqueeLogMessage.Prefix;
        var logger = new RecordingLogger();
        var exception = new InvalidOperationException("座標変換ポートが不正です");

        try
        {
            MarqueeLogMessage.Prefix = "[Test]";

            logger.LogOperationError(exception, "SetCoordTransform");
        }
        finally
        {
            MarqueeLogMessage.Prefix = original;
        }

        Assert.Equal(LogLevel.Error, Assert.Single(logger.Levels));
        Assert.Equal("[Test] SetCoordTransform で例外が発生", Assert.Single(logger.Messages));
        // 例外はスタックトレースのために例外オブジェクトのまま渡す
        Assert.Same(exception, Assert.Single(logger.Exceptions));
    }

    /// <summary>接頭辞を既定値へ戻すと既定の書式で記録することを検証する。</summary>
    [Fact]
    internal void LogOperationError_接頭辞を既定値へ戻せる()
    {
        var logger = new RecordingLogger();

        MarqueeLogMessage.Prefix = MarqueeLogMessage.DefaultPrefix;
        logger.LogOperationError(new InvalidOperationException("失敗"), "ResetSelection");

        Assert.Equal("[Marquee] ResetSelection で例外が発生", Assert.Single(logger.Messages));
    }
}
