using EsUtil.Platform.Core.Native;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace EsUtil.Platform.Core.Tests.Native;

/// <summary><see cref="NativeLogMessage"/> の LoggerMessage 生成コード（IsEnabled 両パス）を検証するテスト。</summary>
/// <remarks>
/// ログ定義は internal のため、<see cref="RecordingLogger"/>（IsEnabled=true）と
/// <see cref="NullLogger"/>（IsEnabled=false）の両方で全メソッドを直接呼び、
/// LoggerMessage 生成コードの state 走査（Count・インデクサ・列挙・範囲外アクセス）と
/// IsEnabled 分岐の両パスを網羅する。<br/>
/// <c>Win</c> 系サービスのログ経路（<see cref="EsUtil.Platform.Core.Native.Win.WinCursorService"/> / <see cref="EsUtil.Platform.Core.Native.Win.WinWindowService"/>）は
/// 実機検証が必要なため、本テストで定義を直接検証する（カバレッジ維持）。<br/>
/// 例外を受け取る定義（<c>LogCaptureFailed</c>）も例外を渡して経路を通す。
/// </remarks>
public class NativeLogMessageTests
{
    [Fact]
    internal void 実ロガー設定時に全ログメソッドが実行される()
    {
        var logger = new RecordingLogger();
        InvokeAll(logger);

        Assert.True(logger.IsEnabled(LogLevel.Trace));
        Assert.NotEmpty(logger.Messages);
    }

    [Fact]
    internal void 無効ロガーでも全ログメソッドが安全に実行される()
    {
        // IsEnabled=false（NullLogger）でも生成コードの IsEnabled 分岐（false パス）が実行される
        InvokeAll(NullLogger.Instance);
    }

    /// <summary>全ログメソッドを指定ロガーで呼び出す。</summary>
    /// <param name="logger">呼び出しに使うロガー。</param>
    private static void InvokeAll(ILogger logger)
    {
        NativeLogMessage.LogCursorVisibilityChanged(logger, show: true);
        NativeLogMessage.LogCursorVisibilityQueryFailed(logger);
        NativeLogMessage.LogCursorPositionQueryFailed(logger);
        NativeLogMessage.LogCursorPositionSetFailed(logger, 10, 20);

        NativeLogMessage.LogWindowHiddenFromTaskSwitcher(logger, "1A2B", success: true);
        NativeLogMessage.LogWindowStyleQueryFailed(logger, "1A2B");
        NativeLogMessage.LogCaptureExclusionChanged(logger, "1A2B", exclude: true, success: true);
        NativeLogMessage.LogWindowBoundsChanged(logger, "1A2B", 0, 0, 1920, 1080, success: true);
        NativeLogMessage.LogWindowEvacuated(logger, "1A2B", unsetTopmost: true, wasVisible: true, posted: true);

        NativeLogMessage.LogVirtualScreenBounds(logger, 0, 0, 1920, 1080);
        NativeLogMessage.LogVirtualScreenBoundsInvalid(logger, 0, 0, 0, 0);
        NativeLogMessage.LogCaptureArea(logger, 0, 0, 1920, 1080);
        NativeLogMessage.LogCaptureAreaDone(logger, 1920, 1080);
        NativeLogMessage.LogCaptureFailed(logger, 0, 0, 1920, 1080, new InvalidOperationException("test"));

        NativeLogMessage.LogMonitorEnumerationFailed(logger);
        NativeLogMessage.LogWindowEnumerationFailed(logger, "Find");
        NativeLogMessage.LogWindowRectUnavailable(logger, "1A2B");

        NativeLogMessage.LogHotKeyRegisterFailed(logger, 1, 0x0002, 0x51);
        NativeLogMessage.LogHotKeyUnregisterFailed(logger, 1);
        NativeLogMessage.LogHotKeySubclassFailed(logger, "1A2B");
        NativeLogMessage.LogCursorGuardSubclassFailed(logger, "1A2B");
    }

    /// <summary>LoggerMessage 生成コードの state 走査（Count・インデクサ・IEnumerable・範囲外）を網羅するテスト用ロガー。</summary>
    private sealed class RecordingLogger : ILogger
    {
        /// <summary>収集したログメッセージ。</summary>
        public List<string> Messages { get; } = [];

        /// <inheritdoc/>
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        /// <inheritdoc/>
        public bool IsEnabled(LogLevel logLevel) => true;

        /// <inheritdoc/>
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            // 生成コードが state を IReadOnlyList として走査する経路を実行する
            if (state is IReadOnlyList<KeyValuePair<string, object?>> list)
            {
                _ = list.Count;
                for (int i = 0; i < list.Count; i++)
                {
                    _ = list[i];
                }
                foreach (var kv in list)
                {
                    _ = kv.Key;
                }
                try
                {
                    _ = list[list.Count];
                }
                catch (IndexOutOfRangeException)
                {
                    // 範囲外アクセスの例外経路も生成コードの網羅対象として実行する
                }
            }

            Messages.Add(formatter(state, exception));
        }
    }
}
