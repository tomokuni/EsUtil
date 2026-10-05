using System.Diagnostics;
using EsUtil.Platform.Core;
using Microsoft.Extensions.Logging;

namespace EsUtil.Feature.Loupe.AvaloniaUI.Sample.Logging;

/// <summary>デバッグ出力とログファイルへログを転送する <see cref="ILogger"/> 実装（LoupeControl.Log の設定例）。</summary>
/// <remarks>
/// <see cref="LoupeControl.Log"/> に設定すると、描画エラー（<c>NotSupportedException</c> 等）や
/// 診断ログが Visual Studio の出力ウィンドウ（<c>Trace.WriteLine</c>）とログファイルへ書き込まれる。<br/>
/// <b>ログファイル</b>: 実行ディレクトリの <c>loupe_debug.log</c> に追記される（マウス移動中の描画診断用）。<br/>
/// 実アプリでは NLog 等の ILogger プロバイダーへ注入してファイル・コンソールへ出力する。
/// </remarks>
public sealed class DebugLogger : ILogger
{
    /// <summary>ログファイルへ書くための排他ロック。</summary>
    private static readonly Lock s_fileLock = new();

    /// <summary>全ログレベルを受け入れる（フィルタなし）。</summary>
    /// <param name="logLevel">ログレベル。</param>
    public bool IsEnabled(LogLevel logLevel) => true;

    /// <summary>ログスコープは未使用（null を返す）。</summary>
    /// <typeparam name="TState">状態の型。</typeparam>
    /// <param name="state">状態。</param>
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    /// <summary>整形済みログをデバッグ出力とログファイルへ書き込む。</summary>
    /// <typeparam name="TState">状態の型。</typeparam>
    /// <param name="logLevel">ログレベル。</param>
    /// <param name="eventId">イベント ID。</param>
    /// <param name="state">状態。</param>
    /// <param name="exception">例外（未使用。 formatter が含める）。</param>
    /// <param name="formatter">ログメッセージ整形関数。</param>
    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        var message = $"[{logLevel}] {formatter(state, exception)}";
        Trace.WriteLine(message);
        WriteToFile(message);
    }

    /// <summary>ログメッセージを実行ディレクトリの <c>loupe_debug.log</c> へ追記する。</summary>
    /// <param name="message">ログメッセージ。</param>
    private static void WriteToFile(string message)
    {
        try
        {
            lock (s_fileLock)
            {
                File.AppendAllText(
                    Path.Combine(AppContext.BaseDirectory, "loupe_debug.log"),
                    $"{DateTime.Now:HH:mm:ss.fff} {message}{Environment.NewLine}");
            }
        }
        catch (Exception ex) when (BoundaryCatch.IsRecoverable(ex))
        {
            // ログファイル書き込み失敗（権限・パス不足等）は無視する（描画診断を妨げない）。
        }
    }
}
