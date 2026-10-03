using Microsoft.Extensions.Logging;

namespace EsUtil.Feature.Marquee.AvaloniaUI.Sample.Logging;

/// <summary>サンプル画面のログ表示領域へ出力する ILogger 実装。</summary>
/// <remarks>
/// アプリ本体のファイルログ（NLog等）の代わりに、UI上のログ一覧へ表示するためのメモリ内ロガー。<br/>
/// <see cref="EsUtil.Feature.Marquee.AvaloniaUI.MarqueeControl.Log"/> に設定すると、コントロール内部の操作ログを画面で確認できる。<br/>
/// </remarks>
public sealed class InMemoryLogger(Action<string> onLog) : ILogger
{
    /// <inheritdoc />
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    /// <summary>全レベルを有効にする。</summary>
    /// <param name="logLevel">ログレベル。</param>
    /// <returns>常に <see langword="true"/>。</returns>
    public bool IsEnabled(LogLevel logLevel) => true;

    /// <summary>ログを整形して出力コールバックへ渡す（時分秒ミリ秒付き）。</summary>
    /// <typeparam name="TState">ログ状態の型。</typeparam>
    /// <param name="logLevel">ログレベル。</param>
    /// <param name="eventId">イベントID。</param>
    /// <param name="state">ログ状態。</param>
    /// <param name="exception">例外。 null の場合はなし。</param>
    /// <param name="formatter">メッセージ整形デリゲート。</param>
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        var message = formatter(state, exception);
        if (exception is not null)
        {
            message += $" | {exception.GetType().Name}: {exception.Message}";
        }

        onLog($"{DateTime.Now:HH:mm:ss.fff} [{logLevel}] {message}");
    }
}
