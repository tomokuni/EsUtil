using Microsoft.Extensions.Logging;

namespace EsUtil.Ui.MewUI.SampleKit;

/// <summary>サンプルが購読するイベントを記録するログ定義（サンプル アプリごとに 1 つ）。</summary>
/// <remarks>
/// <b>値を構造化ログで渡す</b>: 文字列補間をするとメッセージ テンプレートが呼び出しごとに変わり、
/// ログの検索と集計ができない（CA2254）。<br/>
/// <b>レベル</b>: 本文と編集状態の変化は <c>Debug</c>、選択の変化は高頻度のため <c>Trace</c>
/// （規則は <c>.github/ログ規約.md</c> §2）。
/// </remarks>
public static partial class SampleEventLog
{
    /// <summary>本文が変わったことを記録する。</summary>
    /// <remarks>LogLevel: Debug<br/>出力書式: <c>本文が変わりました。 文字数={Length}</c></remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="length">変更後の本文の文字数。</param>
    [LoggerMessage(LogLevel.Debug, "本文が変わりました。 文字数={Length}")]
    public static partial void LogTextChanged(this ILogger logger, int length);

    /// <summary>選択が変わったことを記録する。</summary>
    /// <remarks>LogLevel: Trace<br/>出力書式: <c>選択が変わりました。 開始={Start} 長さ={Length}</c></remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="start">選択範囲の開始位置（文字数）。</param>
    /// <param name="length">選択範囲の長さ（文字数）。</param>
    [LoggerMessage(LogLevel.Trace, "選択が変わりました。 開始={Start} 長さ={Length}")]
    public static partial void LogSelectionChanged(this ILogger logger, int start, int length);

    /// <summary>編集の状態が変わったことを記録する。</summary>
    /// <remarks>LogLevel: Debug<br/>出力書式: <c>編集の状態が変わりました。 編集中={IsEditing}</c></remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="isEditing">編集中の場合は <see langword="true"/>。</param>
    [LoggerMessage(LogLevel.Debug, "編集の状態が変わりました。 編集中={IsEditing}")]
    public static partial void LogEditingChanged(this ILogger logger, bool isEditing);
}