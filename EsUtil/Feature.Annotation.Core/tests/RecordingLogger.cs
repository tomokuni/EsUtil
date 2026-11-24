using Microsoft.Extensions.Logging;

namespace EsUtil.Feature.Annotation.Core.Tests;

/// <summary>ログ出力を収集するテスト用 <see cref="ILogger"/> 実装。</summary>
/// <remarks>整形済みのメッセージとそのレベルを収集し、呼び出し側が内容を検証できるようにする。</remarks>
internal sealed class RecordingLogger : ILogger
{
    /// <summary>収集したログメッセージ。</summary>
    public List<string> Messages { get; } = [];

    /// <summary>収集したログレベル（<see cref="Messages"/> と同じ並び）。</summary>
    public List<LogLevel> Levels { get; } = [];

    /// <inheritdoc />
    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    /// <inheritdoc />
    public bool IsEnabled(LogLevel logLevel) => true;

    /// <inheritdoc />
    public void Log<TState>(
        LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        Messages.Add(formatter(state, exception));
        Levels.Add(logLevel);
    }
}
