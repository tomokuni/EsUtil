using Avalonia.Threading;
using Microsoft.Extensions.Logging;
using Xunit;

namespace EsUtil.Feature.Loupe.AvaloniaUI.Tests;

/// <summary>テスト用の共通ヘルパー。</summary>
public static class TestHelper
{
    /// <summary>再描画の完了（<see cref="LoupeControl.Rendered"/> の発火）を待つ。</summary>
    /// <param name="control">対象のコントロール。</param>
    /// <remarks>
    /// 描画は Dispatcher へのポスト（描画フェーズ）と非同期生成で行われるため、テストは通知（<see cref="LoupeControl.Rendered"/>）を<br/>
    /// 待つ（ホストと同じ契約で待機する）。 待機中は Dispatcher をポンプして描画ジョブと非同期の継続を進める。<br/>
    /// 描画要求が出ていない場合（非表示・変更なし）は完了しないため、呼び出し前に状態変更で要求を発生させること。
    /// </remarks>
    public static async Task WaitForRenderedAsync(this LoupeControl control)
    {
        var rendered = false;
        void OnRendered(object? sender, LoupeRenderedEventArgs e) => rendered = true;

        control.Rendered += OnRendered;
        try
        {
            await PumpUntilAsync(() => rendered);

            Assert.True(rendered, "描画完了（Rendered）が発火しませんでした");
        }
        finally
        {
            control.Rendered -= OnRendered;
        }
    }

    /// <summary>条件が成立するまで Dispatcher をポンプする（上限で打ち切る）。</summary>
    /// <param name="condition">成立を待つ条件。</param>
    /// <remarks>
    /// 描画ジョブの発生源（コントロール）を必要としないため、拡張メソッドにはしない（receiver を使わない<br/>
    /// 拡張メソッドは読み手に「対象が使われる」と誤解させる）。 上限に達しても例外にはしない（成立の検証は呼び出し側が行う）。
    /// </remarks>
    internal static async Task PumpUntilAsync(Func<bool> condition)
    {
        for (var i = 0; i < PumpLimit && !condition(); i++)
        {
            Dispatcher.UIThread.RunJobs();
            await Task.Yield();
        }
    }

    /// <summary>保留中の描画ジョブを数回実行する（描画が発生しないことの検証用）。</summary>
    /// <remarks><see cref="PumpUntilAsync"/> と同じ理由で拡張メソッドにはしない。</remarks>
    internal static async Task PumpAsync()
    {
        for (var i = 0; i < PumpOnce; i++)
        {
            Dispatcher.UIThread.RunJobs();
            await Task.Yield();
        }
    }

    /// <summary>Dispatcher をポンプする回数の上限（完了しない場合に無限ループにしない）。</summary>
    private const int PumpLimit = 200;

    /// <summary>描画が発生しないことの確認に使うポンプ回数。</summary>
    private const int PumpOnce = 8;

    /// <summary>ログ出力を収集するテスト用 ILogger 実装（例外ログの検証用）。</summary>
    public sealed class ListLogger : ILogger
    {
        /// <summary>収集したログメッセージ（読み取り専用で公開し、追加は本クラスだけが行う）。</summary>
        public IReadOnlyList<string> Messages => _messages;

        private readonly List<string> _messages = [];

        /// <summary>Error レベルのログが記録されたか。</summary>
        public bool HasError { get; private set; }

        /// <inheritdoc />
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        /// <inheritdoc />
        public bool IsEnabled(LogLevel logLevel) => true;

        /// <inheritdoc />
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            // LoggerMessage 生成コードが state を IReadOnlyList として走査する経路を実行する（Count・インデクサ・列挙・範囲外）
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

            _messages.Add(formatter(state, exception));
            if (logLevel == LogLevel.Error)
            {
                HasError = true;
            }
        }
    }
}

