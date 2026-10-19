using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;

namespace EsUtil.Ui.WinForms.Marquee.Sample.Logging;

/// <summary>出力されたログを保持し、表示用に 1 件ずつ取り出せるようにするロガー。</summary>
/// <remarks>
/// ログの出力先はホスト（アプリ）が所有する（ライブラリは <see cref="ILogger"/> を受け取るだけである）。<br/>
/// 保持はスレッド セーフな待ち行列で行い、表示は UI 側がタイマーで取り出す<br/>
/// （ログは UI 以外のスレッドからも出力されるため、コントロールへ直接触らない）。
/// </remarks>
internal sealed class InMemoryLogger : ILogger
{
    /// <summary>まだ表示していないログの待ち行列。</summary>
    private readonly ConcurrentQueue<string> _pending = new();

    /// <inheritdoc />
    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    /// <inheritdoc />
    public bool IsEnabled(LogLevel logLevel) => true;

    /// <inheritdoc />
    public void Log<TState>(
        LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        => _pending.Enqueue($"{logLevel}: {formatter(state, exception)}");

    /// <summary>未表示のログを 1 件取り出す。</summary>
    /// <param name="message">取り出したログ。 無かった場合は null。</param>
    /// <returns>取り出せた場合は true。</returns>
    public bool TryDequeue([NotNullWhen(true)] out string? message) => _pending.TryDequeue(out message);
}
