using Microsoft.Extensions.Logging;

namespace EsUtil.Ui.AvaloniaUI.ColorSelector.Logging;

/// <summary>ColorSelectorControl の LoggerMessage 定義（ソース生成ログ）。</summary>
/// <remarks>
/// 色選択・Flyout 開閉・入力イベント・設定変更をログ出力する。<br/>
/// ログ基盤はホストアプリケーションが所有する（本クラスは定義のみ）。
/// </remarks>
internal static partial class ColorSelectorLogMessage
{
    /// <summary>接頭辞の既定値（ホストが設定しない場合に使う）。</summary>
    internal const string DefaultPrefix = "[ColorSelector]";

    /// <summary>適用する接頭辞（ホストが <c>LogPrefix</c> で設定する）。</summary>
    internal static string Prefix { get; set; } = DefaultPrefix;

    /// <summary>一覧（▼）を開いたログ。</summary>
    /// <remarks>
    /// LogLevel: Debug <br/>
    /// 出力書式: <c>[ColorSelector] 一覧を開きました: 色={Color:l}</c><br/>
    /// 一覧とピッカーは別の操作であるため、開いたことを別のメッセージで残す（他 UI と書式を揃える）。
    /// </remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="prefix">接頭辞（公開ラッパーが <see cref="Prefix"/> を渡す）。</param>
    /// <param name="color">現在の選択色。</param>
    [LoggerMessage(LogLevel.Debug, "{Prefix:l} 一覧を開きました: 色={Color:l}")]
    private static partial void LogOpenListCore(this ILogger logger, string prefix, string color);

    /// <summary>一覧を開いたことを記録する。</summary>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="color">現在の選択色。</param>
    internal static void LogOpenList(this ILogger logger, string color)
        => logger.LogOpenListCore(Prefix, color);

    /// <summary>選択色変更のログ（変更前後）。</summary>
    /// <remarks>
    /// LogLevel: Information <br/>
    /// 出力書式: <c>[ColorSelector] 設定: 選択色 {OldValue} → {NewValue}</c>
    /// </remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="prefix">接頭辞（公開ラッパーが <see cref="Prefix"/> を渡す）。</param>
    /// <param name="oldValue">変更前の色。</param>
    /// <param name="newValue">変更後の色。</param>
    [LoggerMessage(LogLevel.Information, "{Prefix:l} 設定: 選択色 {OldValue} → {NewValue}")]
    private static partial void LogSelectedColorChangedCore(this ILogger logger, string prefix, string oldValue, string newValue);

    public static void LogSelectedColorChanged(this ILogger logger, string oldValue, string newValue)
        => logger.LogSelectedColorChangedCore(Prefix, oldValue, newValue);

    /// <summary>カラーピッカー Flyout を開くボタンのクリックログ。</summary>
    /// <remarks>
    /// LogLevel: Debug <br/>
    /// 出力書式: <c>[ColorSelector] 色の選択を開きました: 色={Color:l}</c><br/>
    /// 一覧とピッカーは別の操作であるため、開いたことを別のメッセージで残す（他 UI と書式を揃える）。
    /// </remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="prefix">接頭辞（公開ラッパーが <see cref="Prefix"/> を渡す）。</param>
    /// <param name="color">現在の選択色。</param>
    [LoggerMessage(LogLevel.Debug, "{Prefix:l} 色の選択を開きました: 色={Color:l}")]
    private static partial void LogOpenPickerCore(this ILogger logger, string prefix, string color);

    /// <summary>カラーピッカーを開いたことを記録する。</summary>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="color">現在の選択色。</param>
    internal static void LogOpenPicker(this ILogger logger, string color)
        => logger.LogOpenPickerCore(Prefix, color);

    /// <summary>Flyout の OK ボタンで色が確定されたログ。</summary>
    /// <remarks>
    /// LogLevel: Debug <br/>
    /// 出力書式: <c>[ColorSelector] ポップアップ: 決定: 色={Color:l}</c>
    /// </remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="prefix">接頭辞（公開ラッパーが <see cref="Prefix"/> を渡す）。</param>
    /// <param name="color">確定された色。</param>
    [LoggerMessage(LogLevel.Debug, "{Prefix:l} ポップアップ: 決定: 色={Color:l}")]
    private static partial void LogFlyoutOkCore(this ILogger logger, string prefix, string color);

    public static void LogFlyoutOk(this ILogger logger, string color)
        => logger.LogFlyoutOkCore(Prefix, color);

    /// <summary>Flyout のキャンセルボタンでキャンセルされたログ。</summary>
    /// <remarks>
    /// LogLevel: Debug <br/>
    /// 出力書式: <c>[ColorSelector] ポップアップ: キャンセル</c>
    /// </remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="prefix">接頭辞（公開ラッパーが <see cref="Prefix"/> を渡す）。</param>
    [LoggerMessage(LogLevel.Debug, "{Prefix:l} ポップアップ: キャンセル")]
    private static partial void LogFlyoutCancelCore(this ILogger logger, string prefix);

    public static void LogFlyoutCancel(this ILogger logger)
        => logger.LogFlyoutCancelCore(Prefix);

    /// <summary>フォーカス喪失時のログ（無効色の復帰等）。</summary>
    /// <remarks>
    /// LogLevel: Trace <br/>
    /// 出力書式: <c>[ColorSelector] フォーカスを失いました: 色={Color:l} 有効={Valid}</c>
    /// </remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="prefix">接頭辞（公開ラッパーが <see cref="Prefix"/> を渡す）。</param>
    /// <param name="color">現在の色。</param>
    /// <param name="valid">色が有効かどうか。</param>
    [LoggerMessage(LogLevel.Trace, "{Prefix:l} フォーカスを失いました: 色={Color:l} 有効={Valid}")]
    private static partial void LogLostFocusCore(this ILogger logger, string prefix, string color, bool valid);

    public static void LogLostFocus(this ILogger logger, string color, bool valid)
        => logger.LogLostFocusCore(Prefix, color, valid);
}
