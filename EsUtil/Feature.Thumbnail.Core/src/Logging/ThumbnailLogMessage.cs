using Microsoft.Extensions.Logging;

namespace EsUtil.Feature.Thumbnail.Logging;

/// <summary>サムネイル一覧のコアが出力する LoggerMessage 定義（ソース生成ログ）。</summary>
/// <remarks>
/// <b>本層が単一所有する状態</b>: 選択（<see cref="ThumbnailSelection"/>）はコアが単一所有する。<br/>
/// 「値を所有する主体が記録する」規則により、選択の変更は本層が記録する（UI 層は記録しない。 ログ規約 §6）。<br/>
/// 配置（<see cref="ThumbnailLayout"/>）は<b>純粋計算のため出力しない</b>（同じ入力からは同じ結果が得られ、記録すべき状態がない）。<br/>
/// ログ基盤（ファイル出力・レベル制御）はホストアプリケーションが所有する（本クラスは定義のみ）。<br/>
/// 各メソッドは <see cref="ILogger"/> の拡張メソッド（<c>this ILogger</c> 第一引数）として定義し、呼び出し側では logger 引数を省略する。
/// </remarks>
internal static partial class ThumbnailLogMessage
{
    /// <summary>接頭辞の既定値（ホストが設定しない場合に使う）。</summary>
    internal const string DefaultPrefix = "[Thumbnail]";

    /// <summary>適用する接頭辞（ホストが <c>LogPrefix</c> で設定する）。</summary>
    internal static string Prefix { get; set; } = DefaultPrefix;

    /// <summary>選択の変更を記録する（生成メソッド。 接頭辞は呼び出し側から受け取る）。</summary>
    /// <remarks>LogLevel: Debug<br/>出力書式: <c>[Thumbnail] Selection: {OldValue} → {NewValue}</c></remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="prefix">接頭辞（公開ラッパーが <see cref="Prefix"/> を渡す）。</param>
    /// <param name="oldValue">変更前の添字（選択が無い場合は -1）。</param>
    /// <param name="newValue">変更後の添字（選択が無い場合は -1）。</param>
    [LoggerMessage(LogLevel.Debug, "{Prefix} Selection: {OldValue} → {NewValue}")]
    private static partial void LogSelectionChangedCore(this ILogger logger, string prefix, int oldValue, int newValue);

    /// <summary>選択の変更を記録する（同値の再設定では出力しない）。</summary>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="oldValue">変更前の添字（選択が無い場合は -1）。</param>
    /// <param name="newValue">変更後の添字（選択が無い場合は -1）。</param>
    /// <remarks>同値の判定は値を所有する本層が行う（呼び出し側で前値を比較しない。 ログ規約 §6）。</remarks>
    internal static void LogSelectionChanged(this ILogger logger, int oldValue, int newValue)
    {
        if (oldValue == newValue)
        {
            return;
        }

        logger.LogSelectionChangedCore(Prefix, oldValue, newValue);
    }
}
