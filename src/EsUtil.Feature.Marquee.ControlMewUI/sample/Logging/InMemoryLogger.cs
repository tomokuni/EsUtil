using Microsoft.Extensions.Logging;

namespace EsUtil.Feature.Marquee.MewUI.Sample.Logging;

/// <summary>サンプル画面のログ表示領域へ出力する <see cref="ILogger"/> 実装。</summary>
/// <remarks>
/// アプリ本体のファイル ログの代わりに、画面上のログ表示へ渡すためのメモリ内ロガーである。<br/>
/// <c>LoupeControl.Log</c> へ設定すると、コントロール内部の設定・環境値・拡大中心の変更ログを画面で確認できる。<br/>
/// ログは UI スレッド（プロパティ変更・入力）から出力されるため、出力先の要素更新もそのまま行える。
/// </remarks>
/// <param name="onLog">整形済みの 1 行を受け取る出力先。</param>
internal sealed class InMemoryLogger(Action<string> onLog) : ILogger
{
    /// <inheritdoc/>
    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    /// <summary>全レベルを有効にする。</summary>
    /// <param name="logLevel">ログ レベル。</param>
    /// <returns>常に true。</returns>
    public bool IsEnabled(LogLevel logLevel) => true;

    /// <summary>ログを整形して出力先へ渡す（時分秒ミリ秒付き）。</summary>
    /// <typeparam name="TState">ログ状態の型。</typeparam>
    /// <param name="logLevel">ログ レベル。</param>
    /// <param name="eventId">イベント ID。</param>
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
