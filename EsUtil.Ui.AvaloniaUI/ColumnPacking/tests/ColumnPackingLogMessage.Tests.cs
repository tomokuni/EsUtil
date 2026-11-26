using Microsoft.Extensions.Logging;
using Xunit;

namespace EsUtil.Ui.AvaloniaUI.ColumnPacking.Tests;

/// <summary><see cref="ColumnPackingLogMessage"/> の整形（切り詰め）を検証する。</summary>
/// <remarks>列の区切りと項目ごとの Y 座標は項目数に比例して長くなるため、先頭 100 文字へ切り詰める。</remarks>
public sealed class ColumnPackingLogMessageTests
{
    /// <summary>100 文字を超える値は切り詰め、末尾に省略記号を付ける。</summary>
    [Fact]
    internal void ToPreview_100文字を超えたら切り詰める()
    {
        var preview = ColumnPackingLogMessage.ToPreview(new string('1', 150));

        Assert.Equal(101, preview.Length);
        Assert.EndsWith("…", preview, StringComparison.Ordinal);
    }

    /// <summary>改行は 1 行へ畳む（ログの 1 行を壊さない）。</summary>
    [Fact]
    internal void ToPreview_改行を1行にする()
        => Assert.Equal("a\\nb", ColumnPackingLogMessage.ToPreview("a\r\nb"));

    /// <summary>長い列の区切りを記録しても 1 行に収まる。</summary>
    [Fact]
    internal void LogLayoutChanged_長いセグメントを切り詰める()
    {
        var logger = new RecordingLogger();
        var segments = "[" + string.Join(",", Enumerable.Range(1, 80)) + "]";

        logger.LogLayoutChanged(320, 240, 80, segments);

        Assert.Single(logger.Messages);
        Assert.Contains("…", logger.Messages[0], StringComparison.Ordinal);
        Assert.True(logger.Messages[0].Length < segments.Length);
    }

    /// <summary>項目ごとの Y 座標が多い場合も 1 行に収まる。</summary>
    [Fact]
    internal void LogLayoutItems_長い並びを切り詰める()
    {
        var logger = new RecordingLogger();
        var itemY = Enumerable.Range(0, 80).Select(static index => index * 24.0).ToArray();

        logger.LogLayoutItems(itemY);

        Assert.Single(logger.Messages);
        Assert.Contains("…", logger.Messages[0], StringComparison.Ordinal);
    }

    /// <summary>ログ出力を収集するテスト用の <see cref="ILogger"/>。</summary>
    private sealed class RecordingLogger : ILogger
    {
        /// <summary>収集したメッセージ。</summary>
        internal List<string> Messages { get; } = [];

        /// <inheritdoc />
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        /// <inheritdoc />
        public bool IsEnabled(LogLevel logLevel) => true;

        /// <inheritdoc />
        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
            => Messages.Add(formatter(state, exception));
    }
}
