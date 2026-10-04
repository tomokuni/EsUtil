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
            await control.PumpUntilAsync(() => rendered);

            Assert.True(rendered, "描画完了（Rendered）が発火しませんでした");
        }
        finally
        {
            control.Rendered -= OnRendered;
        }
    }

    /// <summary>条件が成立するまで Dispatcher をポンプする（上限で打ち切る）。</summary>
    /// <param name="control">対象のコントロール（描画ジョブの発生源）。</param>
    /// <param name="condition">成立を待つ条件。</param>
    /// <remarks>
    /// 上限に達しても例外にはしない（成立の検証は呼び出し側が行う）。<br/>
    /// <b>receiver を本文で使わない</b>が、拡張メソッドの呼び出し記法（<c>control.PumpUntilAsync()</c>）のために引数は必要である。
    /// </remarks>
#pragma warning disable IDE0060 // 未使用のパラメーター（拡張メソッドの receiver は呼び出し記法のために必要）
    public static async Task PumpUntilAsync(this LoupeControl control, Func<bool> condition)
#pragma warning restore IDE0060
    {
        for (var i = 0; i < PumpLimit && !condition(); i++)
        {
            Dispatcher.UIThread.RunJobs();
            await Task.Yield();
        }
    }

    /// <summary>保留中の描画ジョブを数回実行する（描画が発生しないことの検証用）。</summary>
    /// <param name="control">対象のコントロール。</param>
    /// <remarks>receiver を本文で使わない理由は <see cref="PumpUntilAsync"/> と同じ。</remarks>
#pragma warning disable IDE0060 // 未使用のパラメーター（拡張メソッドの receiver は呼び出し記法のために必要）
    public static async Task PumpAsync(this LoupeControl control)
#pragma warning restore IDE0060
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
        /// <summary>収集したログメッセージ。</summary>
        public List<string> Messages { get; } = [];

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

            Messages.Add(formatter(state, exception));
            if (logLevel == LogLevel.Error)
            {
                HasError = true;
            }
        }
    }
}

