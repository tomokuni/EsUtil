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

    /// <summary>実行中に切り替えられる最小レベル（サンプルの設定パネルが操作する）。</summary>
    private readonly LoggingLevelSwitch _levelSwitch = new(LogEventLevel.Debug);

    /// <summary>ログを初期化する。</summary>
    /// <param name="capacity">バッファが保持する行の上限。</param>
    public SampleLogging(int capacity = 2000)
    {
        _buffer = new SampleLogBuffer(capacity);
        _serilog = new LoggerConfiguration()
            .MinimumLevel.ControlledBy(_levelSwitch)
            .WriteTo.Sink(new SampleLogBufferSink(_buffer))
            .CreateLogger();

        _factory = LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Trace);
            builder.AddSerilog(_serilog, dispose: false);
        });

        Logger = _factory.CreateLogger("Sample");
    }

    /// <summary>選択できるログ レベルの一覧（設定パネルの選択肢）。</summary>
    public static IReadOnlyList<string> LevelChoices { get; } = ["Trace", "Debug", "Information", "Warning", "Error"];

    /// <summary>表示する最小レベルを取得または設定する（実行中に切り替えられる）。</summary>
    /// <remarks>
    /// <b>Trace は入力のたびに出力される</b>ため、既定の Debug のままにするとログ欄が溢れて<br/>
    /// ポインターの追従が落ちる（実測 2026-10-31）。 表示レベルはサンプルの設定パネルから切り替える。
    /// </remarks>
    public LogLevel MinimumLevel
    {
        get => _levelSwitch.MinimumLevel switch
        {
            LogEventLevel.Verbose => LogLevel.Trace,
            LogEventLevel.Debug => LogLevel.Debug,
            LogEventLevel.Information => LogLevel.Information,
            LogEventLevel.Warning => LogLevel.Warning,
            LogEventLevel.Error => LogLevel.Error,
            _ => LogLevel.Critical,
        };
        set => _levelSwitch.MinimumLevel = value switch
        {
            LogLevel.Trace => LogEventLevel.Verbose,
            LogLevel.Debug => LogEventLevel.Debug,
            LogLevel.Information => LogEventLevel.Information,
            LogLevel.Warning => LogEventLevel.Warning,
            LogLevel.Error => LogEventLevel.Error,
            _ => LogEventLevel.Fatal,
        };
    }

    /// <summary>一覧のインデックスで最小レベルを設定する（設定パネルの選択肢の位置）。</summary>
    /// <param name="index">選択されたインデックス。</param>
    public void SetMinimumLevel(int index)
        => MinimumLevel = index switch
        {
            0 => LogLevel.Trace,
            1 => LogLevel.Debug,
            2 => LogLevel.Information,
            3 => LogLevel.Warning,
            _ => LogLevel.Error,
        };

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
            // Serilog のレベル名は Verbose / Fatal であるが、サンプルの表示は
            // Microsoft.Extensions.Logging の名前（Trace / Critical）に揃える（設定パネルの「ログ レベル」と同じ名前にする）
            buffer.Write($"{timestamp} [{ToDisplayLevel(logEvent.Level)}] {logEvent.RenderMessage(System.Globalization.CultureInfo.InvariantCulture)}");
        }

        /// <summary>Serilog のレベルを表示用の名前（Microsoft.Extensions.Logging と同じ）へ変換する。</summary>
        /// <param name="level">Serilog のレベル。</param>
        /// <returns>表示用の名前。</returns>
        private static string ToDisplayLevel(LogEventLevel level) => level switch
        {
            LogEventLevel.Verbose => "Trace",
            LogEventLevel.Fatal => "Critical",
            _ => level.ToString(),
        };
    }
}
