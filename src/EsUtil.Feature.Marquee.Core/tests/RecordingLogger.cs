using Microsoft.Extensions.Logging;

namespace EsUtil.Feature.Marquee.Tests;

/// <summary>ログ出力を収集するテスト用 <see cref="ILogger"/> 実装。</summary>
/// <remarks>
/// LoggerMessage ソース生成コードの state 走査（Count・インデクサ・列挙・範囲外アクセス）を実行させつつ、<br/>
/// 整形済みのメッセージとそのレベルを収集して内容を検証できるようにする。
/// </remarks>
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
        // LoggerMessage 生成コードが state を走査する経路を実行する（Count・インデクサ・列挙・範囲外）
        if (state is IReadOnlyList<KeyValuePair<string, object?>> list)
        {
            _ = list.Count;
            for (var i = 0; i < list.Count; i++)
            {
                _ = list[i];
            }

            try
            {
                // 範囲外アクセスは例外になる（生成コードの防御経路の確認）
                _ = list[list.Count];
            }
            catch (IndexOutOfRangeException)
            {
                // 期待どおり（範囲外は例外）
            }
        }

        Messages.Add(formatter(state, exception));
        Levels.Add(logLevel);
    }
}
