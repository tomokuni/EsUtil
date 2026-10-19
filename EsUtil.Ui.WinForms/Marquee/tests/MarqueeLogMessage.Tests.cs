using Microsoft.Extensions.Logging;
using Xunit;

namespace EsUtil.Ui.WinForms.Marquee.Tests;

/// <summary><see cref="MarqueeLogMessage"/> の出力テスト。</summary>
/// <remarks>
/// 接頭辞は定義クラスの <c>static</c> プロパティ（プロセス全体の状態）であるため、<br/>
/// 設定するテストはコレクションで直列化し、<c>try</c> / <c>finally</c> で既定値へ戻す（ログ規約 §7）。
/// </remarks>
[Collection(MarqueeLogPrefixDefinition.Name)]
public sealed class MarqueeLogMessageTests
{
    /// <summary>既定の接頭辞が規約の値であることを検証する。</summary>
    [Fact]
    internal void DefaultPrefix_規約の接頭辞を持つ()
        => Assert.Equal("[Marquee]", MarqueeLogMessage.DefaultPrefix);

    /// <summary>操作の例外が接頭辞と操作名を含む Error として出力されることを検証する。</summary>
    [Fact]
    internal void LogOperationError_接頭辞と操作名を含むメッセージを出力する()
    {
        var logger = new RecordingLogger();
        var exception = new InvalidOperationException("失敗");

        try
        {
            MarqueeLogMessage.Prefix = "[Test]";
            logger.LogOperationError(exception, "SetSelection");
        }
        finally
        {
            MarqueeLogMessage.Prefix = MarqueeLogMessage.DefaultPrefix;
        }

        Assert.Equal("[Test] SetSelection で例外が発生", logger.Messages[0]);
        Assert.Equal(LogLevel.Error, logger.Levels[0]);
        Assert.Same(exception, logger.Exceptions[0]);
    }

    /// <summary>接頭辞が未設定の場合は既定の接頭辞が使われることを検証する。</summary>
    [Fact]
    internal void LogOperationError_既定の接頭辞を使う()
    {
        var logger = new RecordingLogger();

        MarqueeLogMessage.Prefix = MarqueeLogMessage.DefaultPrefix;
        logger.LogOperationError(new InvalidOperationException("失敗"), "RequestCancel");

        Assert.Equal("[Marquee] RequestCancel で例外が発生", logger.Messages[0]);
    }
}
