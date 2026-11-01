using Microsoft.Extensions.Logging;

namespace EsUtil.Ui.WinUI3.ColorComboBox;

/// <summary>色選択コントロール（WinUI 3 版）が出力する LoggerMessage 定義（ソース生成ログ）。</summary>
/// <remarks>
/// <b>本アセンブリが単一所有するもの</b>: 入力と状態の同期（<see cref="ColorComboBoxControl.SelectedColor"/>）は
/// コントロール固有の概念であり、変化を知っているのは本層だけである（記録も同じ層が担う。<c>.github/ログ規約.md</c> §6）。<br/>
/// 色文字列の解釈と並び順はコアが単一所有する（本クラスは記録だけを持つ）。<br/>
/// ログ基盤（出力先・レベルの制御）はホストが所有する。
/// </remarks>
internal static partial class LogMessage
{
    /// <summary>接頭辞の既定値（ホストが設定しない場合に使う）。</summary>
    internal const string DefaultPrefix = "[ColorComboBox]";

    /// <summary>適用する接頭辞（ホストが <see cref="ColorComboBoxControl.LogPrefix"/> で設定する）。</summary>
    internal static string Prefix { get; set; } = DefaultPrefix;

    /// <summary>設定の変更を記録する。</summary>
    /// <remarks>LogLevel: Debug<br/>出力書式: <c>[ColorComboBox] Setting: {Name} {OldValue} → {NewValue}</c></remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="prefix">接頭辞。</param>
    /// <param name="name">設定名。</param>
    /// <param name="oldValue">変更前の値。</param>
    /// <param name="newValue">変更後の値。</param>
    [LoggerMessage(LogLevel.Debug, "{Prefix} Setting: {Name} {OldValue} → {NewValue}")]
    private static partial void LogSettingChangedCore(
        this ILogger logger, string prefix, string name, string oldValue, string newValue);

    /// <summary>設定の変更を記録する（同じ値では出力しない）。</summary>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="name">設定名。</param>
    /// <param name="oldValue">変更前の値。</param>
    /// <param name="newValue">変更後の値。</param>
    internal static void LogSettingChanged(this ILogger logger, string name, string? oldValue, string? newValue)
    {
        if (string.Equals(oldValue, newValue, StringComparison.Ordinal))
        {
            return;
        }

        logger.LogSettingChangedCore(Prefix, name, oldValue ?? "null", newValue ?? "null");
    }
    /// <summary>一覧の Flyout を開いたことを記録する。</summary>
    /// <remarks>LogLevel: Debug<br/>出力書式: <c>[ColorComboBox] OpenList: color={Color}</c></remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="prefix">接頭辞（公開ラッパーが <see cref="Prefix"/> を渡す）。</param>
    /// <param name="color">現在の選択色。</param>
    [LoggerMessage(LogLevel.Debug, "{Prefix} OpenList: color={Color}")]
    private static partial void LogOpenListCore(this ILogger logger, string prefix, string color);

    /// <summary>一覧の Flyout を開いたことを記録する（接頭辞を付ける）。</summary>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="color">現在の選択色。</param>
    internal static void LogOpenList(this ILogger logger, string color)
        => logger.LogOpenListCore(Prefix, color);

    /// <summary>フォーカス喪失時の入力を記録する。</summary>
    /// <remarks>LogLevel: Trace<br/>出力書式: <c>[ColorComboBox] LostFocus: color={Color} valid={Valid}</c></remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="prefix">接頭辞（公開ラッパーが <see cref="Prefix"/> を渡す）。</param>
    /// <param name="color">現在の色。</param>
    /// <param name="valid">色が有効かどうか。</param>
    [LoggerMessage(LogLevel.Trace, "{Prefix} LostFocus: color={Color} valid={Valid}")]
    private static partial void LogLostFocusCore(this ILogger logger, string prefix, string color, bool valid);

    /// <summary>フォーカス喪失時の入力を記録する（接頭辞を付ける）。</summary>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="color">現在の色。</param>
    /// <param name="valid">色が有効かどうか。</param>
    internal static void LogLostFocus(this ILogger logger, string color, bool valid)
        => logger.LogLostFocusCore(Prefix, color, valid);
}
