using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using EsUtil.Platform.Core.Imaging;
using Microsoft.Extensions.Logging;
using Xunit;

namespace EsUtil.Ui.AvaloniaUI.Loupe.Tests;

/// <summary>テスト用の共通ヘルパー。</summary>
public static class TestHelper
{
    /// <summary>単色のコア画像（拡大元。 読み取り専用の画素の面）を生成する。</summary>
    /// <param name="width">幅（ピクセル）。 正の値。</param>
    /// <param name="height">高さ（ピクセル）。 正の値。</param>
    /// <param name="color">塗りつぶし色（コアの色型）。</param>
    /// <returns>生成した画像（読み取り専用の面）。</returns>
    /// <remarks>
    /// 書き換え可能な画像で作って凍結し、<b>0 コピー</b>で読み取り専用の面へ渡す（書き換え可能の実体はその場で解放する。 画素は共有して生存する）。<br/>
    /// ルーペの拡大元（<see cref="LoupeControl.Source"/>）はコアの画像であり、表示用の射影は別のプロパティで渡す（`EsUtil/README.md` §5）。
    /// </remarks>
    public static EsReadOnlyBitmap CreateSource(int width, int height, System.Drawing.Color color)
    {
        using var bitmap = EsBitmap.Create(width, height, color);
        bitmap.SetReadOnly();
        return EsReadOnlyBitmap.FromBitmap(bitmap);
    }
    /// <summary>コントロールをウィンドウへ載せて表示する（描画が行われる状態にする）。</summary>
    /// <param name="control">対象のコントロール。</param>
    /// <param name="size">ウィンドウと親パネルの一辺のサイズ（DIP）。</param>
    /// <returns>表示したウィンドウ。</returns>
    /// <remarks>ルーペの描画はウィンドウのフレームで行われるため、描画を待つテストは本メソッドを使う。</remarks>
    internal static Window ShowInWindow(this LoupeControl control, double size = 300)
    {
        var panel = new Panel { Width = size, Height = size };
        panel.Children.Add(control);
        var window = new Window { Width = size, Height = size, Content = panel };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    /// <summary>ウィンドウのフレームを 1 回進める（描画を実行する）。</summary>
    /// <param name="control">対象のコントロール（ウィンドウへ接続されていること）。</param>
    /// <remarks>描画の要求は行わない（要求の回数を検証するテストが使う）。</remarks>
    internal static void RenderOnce(this LoupeControl control)
    {
        Dispatcher.UIThread.RunJobs();
        TopLevel.GetTopLevel(control)?.CaptureRenderedFrame();
    }

    /// <summary>描画が落ち着くまで（新しい描画が一定回数続けて起きなくなるまで）実行キューとフレームを進める。</summary>
    /// <param name="control">対象のコントロール（ウィンドウへ接続されていること）。</param>
    /// <param name="renderedCount">描画回数を返す関数（<see cref="LoupeControl.Rendered"/> の購読で数える）。</param>
    /// <param name="quietPumps">「新しい描画が起きない」状態が続けば落ち着いたと見なす回数。</param>
    /// <remarks>
    /// 描画要求の消化と画像の非同期生成は数フレームにまたがるため、固定回数のポンプでは<br/>
    /// 途中で打ち切ることがある（打ち切った状態で状態を変えると、後から描画が 1 回だけ起きる）。<br/>
    /// 回数を数えて落ち着くまで待つと、実行環境の速度に依存しない。
    /// </remarks>
    internal static async Task SettleRenderingAsync(this LoupeControl control, Func<int> renderedCount, int quietPumps = 4)
    {
        var quiet = 0;
        for (var i = 0; i < PumpLimit && quiet < quietPumps; i++)
        {
            var before = renderedCount();
            control.RenderOnce();
            await Task.Yield();
            quiet = renderedCount() == before ? quiet + 1 : 0;
        }
    }

    /// <summary>再描画の完了（<see cref="LoupeControl.Rendered"/> の発火）を待つ。</summary>
    /// <param name="control">対象のコントロール。</param>
    /// <remarks>
    /// 描画はウィンドウのフレームで行われるため、テストは実行キューを消化しながら描画フレームを進める<br/>
    /// （コントロールがウィンドウへ接続されていない場合は完了しない。 描画を待つテストはウィンドウへ載せること）。<br/>
    /// 描画要求が出ていない場合（非表示・変更なし）は完了しないため、呼び出し前に状態変更で要求を発生させること。
    /// </remarks>
    public static async Task WaitForRenderedAsync(this LoupeControl control)
    {
        var rendered = false;
        void OnRendered(object? sender, LoupeRenderedEventArgs e) => rendered = true;

        control.Rendered += OnRendered;
        try
        {
            for (var i = 0; i < PumpLimit && !rendered; i++)
            {
                // 描画を要求してから 1 フレーム進める（要求は 1 フレーム 1 回の描画へ結合される）
                control.RequestRender();
                Dispatcher.UIThread.RunJobs();

                // 描画はフレーム（ヘッドレスでは明示的な 1 フレーム）で行われる
                if (TopLevel.GetTopLevel(control) is { } topLevel)
                {
                    topLevel.CaptureRenderedFrame();
                }

                await Task.Yield();
            }

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

    /// <summary>保留中のジョブを数回実行する（描画が発生しないことの検証用）。</summary>
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

