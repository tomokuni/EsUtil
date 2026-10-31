using Microsoft.Extensions.Logging;
using ILogger = Microsoft.Extensions.Logging.ILogger;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace EsUtil.Ui.WinUI3.SampleKit;

/// <summary>サンプルのログ（Serilog + Microsoft.Extensions.Logging）を組み立てる。</summary>
/// <remarks>
/// <b>非同期に表示する</b>: Serilog のシンクは <see cref="SampleLogBuffer"/> へ 1 行を追加するだけで、
/// 書式化以外の処理（UI の更新）を呼び出し側のスレッドで行わない。 画面の更新は
/// <see cref="SampleLogPanel"/> が一定間隔でまとめて行う。<br/>
/// <b>コントロールへ渡す</b>: サンプルは <see cref="Logger"/>（<see cref="ILogger"/>）をコントロールの
/// <c>Log</c> プロパティへ設定する。<br/>
/// <b>後始末</b>: <see cref="LoggerFactory"/> を <see cref="IDisposable.Dispose"/> すると Serilog のロガーも破棄する。
/// </remarks>
public sealed class SampleLogging : IDisposable
{
    /// <summary>ログの行を保持するバッファ。</summary>
    private readonly SampleLogBuffer _buffer;

    /// <summary>Microsoft.Extensions.Logging のファクトリ（コントロールへ渡すロガーの生成元）。</summary>
    private readonly ILoggerFactory _factory;

    /// <summary>Serilog のロガー（破棄の対象）。</summary>
    private readonly Logger _serilog;

    /// <summary>ログを初期化する。</summary>
    /// <param name="capacity">バッファが保持する行の上限。</param>
    public SampleLogging(int capacity = 2000)
    {
        _buffer = new SampleLogBuffer(capacity);
        _serilog = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Sink(new SampleLogBufferSink(_buffer))
            .CreateLogger();

        _factory = LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Debug);
            builder.AddSerilog(_serilog, dispose: false);
        });

        Logger = _factory.CreateLogger("Sample");
    }

    /// <summary>コントロールへ設定するロガー。</summary>
    public ILogger Logger { get; }

    /// <summary>画面の表示が取り出すバッファ。</summary>
    public SampleLogBuffer Buffer => _buffer;

    /// <summary>ロガーを破棄する。</summary>
    public void Dispose()
    {
        _factory.Dispose();
        _serilog.Dispose();
    }

    /// <summary>Serilog のイベントをバッファへ 1 行として書き込むシンク。</summary>
    /// <param name="buffer">書き込み先のバッファ。</param>
    private sealed class SampleLogBufferSink(SampleLogBuffer buffer) : ILogEventSink
    {
        /// <summary>イベントを書式化して書き込む。</summary>
        /// <param name="logEvent">Serilog のイベント。</param>
        public void Emit(LogEvent logEvent)
        {
            ArgumentNullException.ThrowIfNull(logEvent);

            var timestamp = logEvent.Timestamp.ToString("HH:mm:ss.fff", System.Globalization.CultureInfo.InvariantCulture);
            buffer.Write($"{timestamp} [{logEvent.Level}] {logEvent.RenderMessage(System.Globalization.CultureInfo.InvariantCulture)}");
        }
    }
}
