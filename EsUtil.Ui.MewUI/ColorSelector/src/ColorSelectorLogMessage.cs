using Microsoft.Extensions.Logging;

namespace EsUtil.Ui.MewUI.ColorSelector;

/// <summary>色選択コントロール（MewUI 版）が出力する LoggerMessage 定義（ソース生成ログ）。</summary>
/// <remarks>
/// <b>本アセンブリが単一所有するもの</b>: 入力と状態の同期（<see cref="ColorSelectorControl.SelectedColor"/>）は
/// コントロール固有の概念であり、変化を知っているのは本層だけである（記録も同じ層が担う。<c>.github/ログ規約.md</c> §6）。<br/>
/// 色文字列の解釈と並び順はコアが単一所有する（本クラスは記録だけを持つ）。<br/>
/// ログ基盤（出力先・レベルの制御）はホストが所有する。
/// </remarks>
internal static partial class ColorSelectorLogMessage
{
    /// <summary>接頭辞の既定値（ホストが設定しない場合に使う）。</summary>
    internal const string DefaultPrefix = "[ColorSelector]";

    /// <summary>適用する接頭辞（ホストが <see cref="ColorSelectorControl.LogPrefix"/> で設定する）。</summary>
    internal static string Prefix { get; set; } = DefaultPrefix;

    /// <summary>設定の変更を記録する。</summary>
    /// <remarks>LogLevel: Information<br/>出力書式: <c>[ColorSelector] 設定: {Name:l} {OldValue:l} → {NewValue:l}</c></remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="prefix">接頭辞。</param>
    /// <param name="name">設定名。</param>
    /// <param name="oldValue">変更前の値。</param>
    /// <param name="newValue">変更後の値。</param>
    [LoggerMessage(LogLevel.Information, "{Prefix:l} 設定: {Name:l} {OldValue:l} → {NewValue:l}")]
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

    /// <summary>一覧のポップアップを開いたことを記録する。</summary>
    /// <remarks>LogLevel: Debug<br/>出力書式: <c>[ColorSelector] 一覧を開きました: 色={Color:l}</c></remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="prefix">接頭辞（公開ラッパーが <see cref="Prefix"/> を渡す）。</param>
    /// <param name="color">現在の選択色。</param>
    [LoggerMessage(LogLevel.Debug, "{Prefix:l} 一覧を開きました: 色={Color:l}")]
    private static partial void LogOpenListCore(this ILogger logger, string prefix, string color);

    /// <summary>一覧のポップアップを開いたことを記録する（接頭辞を付ける）。</summary>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="color">現在の選択色。</param>
    internal static void LogOpenList(this ILogger logger, string color)
        => logger.LogOpenListCore(Prefix, color);

    /// <summary>カラーピッカーを開いたことを記録する。</summary>
    /// <remarks>LogLevel: Debug<br/>出力書式: <c>[ColorSelector] 色の選択を開きました: 色={Color:l}</c><br/>一覧とピッカーは別の操作であるため、開いたことを別のメッセージで残す（他 UI と書式を揃える）。</remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="prefix">接頭辞（公開ラッパーが <see cref="Prefix"/> を渡す）。</param>
    /// <param name="color">現在の選択色。</param>
    [LoggerMessage(LogLevel.Debug, "{Prefix:l} 色の選択を開きました: 色={Color:l}")]
    private static partial void LogOpenPickerCore(this ILogger logger, string prefix, string color);

    /// <summary>カラーピッカーを開いたことを記録する（接頭辞を付ける）。</summary>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="color">現在の選択色。</param>
    internal static void LogOpenPicker(this ILogger logger, string color)
        => logger.LogOpenPickerCore(Prefix, color);

    /// <summary>フォーカス喪失時の入力を記録する。</summary>
    /// <remarks>LogLevel: Trace<br/>出力書式: <c>[ColorSelector] フォーカスを失いました: 色={Color:l} 有効={Valid}</c></remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="prefix">接頭辞（公開ラッパーが <see cref="Prefix"/> を渡す）。</param>
    /// <param name="color">現在の色。</param>
    /// <param name="valid">色が有効かどうか。</param>
    [LoggerMessage(LogLevel.Trace, "{Prefix:l} フォーカスを失いました: 色={Color:l} 有効={Valid}")]
    private static partial void LogLostFocusCore(this ILogger logger, string prefix, string color, bool valid);

    /// <summary>フォーカス喪失時の入力を記録する（接頭辞を付ける）。</summary>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="color">現在の色。</param>
    /// <param name="valid">色が有効かどうか。</param>
    internal static void LogLostFocus(this ILogger logger, string color, bool valid)
        => logger.LogLostFocusCore(Prefix, color, valid);
}