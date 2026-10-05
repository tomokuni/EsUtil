using Microsoft.Extensions.Logging;

namespace EsUtil.Feature.Marquee.MewUI;

/// <summary>選択矩形コントロール（MewUI 版）が出力する LoggerMessage 定義（ソース生成ログ）。</summary>
/// <remarks>
/// 本コントロールは公開操作を提示層へ委譲するため、記録するのは<b>委譲した操作の例外</b>だけである<br/>
/// （設定・入力・描画の記録はコア・提示層が単一所有する。 ログ規約 §6）。<br/>
/// ログ基盤（ファイル出力・レベル制御）はホストアプリケーションが所有する（本クラスは定義のみ）。<br/>
/// 各メソッドは <see cref="ILogger"/> の拡張メソッド（<c>this ILogger</c> 第一引数）として定義し、呼び出し側では logger 引数を省略する。
/// </remarks>
internal static partial class MarqueeLogMessage
{
    /// <summary>接頭辞の既定値（ホストが設定しない場合に使う）。</summary>
    internal const string DefaultPrefix = "[Marquee]";

    /// <summary>適用する接頭辞（ホストが <c>LogPrefix</c> で設定する）。</summary>
    internal static string Prefix { get; set; } = DefaultPrefix;

    /// <summary>公開ファサードの操作で例外が発生したことを記録する（生成メソッド）。</summary>
    /// <remarks>LogLevel: Error<br/>出力書式: <c>[Marquee] {Operation} で例外が発生</c>（例外スタックトレース付き）</remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="exception">発生した例外。</param>
    /// <param name="prefix">接頭辞（公開ラッパーが <see cref="Prefix"/> を渡す）。</param>
    /// <param name="operation">操作名（<c>nameof(操作)</c>）。</param>
    [LoggerMessage(LogLevel.Error, "{Prefix} {Operation} で例外が発生")]
    private static partial void LogOperationErrorCore(this ILogger logger, Exception exception, string prefix, string operation);

    /// <summary>公開ファサードの操作で例外が発生したことを記録する。</summary>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="exception">発生した例外。</param>
    /// <param name="operation">操作名（<c>nameof(操作)</c>）。</param>
    /// <remarks>公開ファサードが例外を集約して記録し、上位へ伝搬するために使う（[`例外処理規約.md`] §4）。</remarks>
    internal static void LogOperationError(this ILogger logger, Exception exception, string operation)
        => logger.LogOperationErrorCore(exception, Prefix, operation);
}
