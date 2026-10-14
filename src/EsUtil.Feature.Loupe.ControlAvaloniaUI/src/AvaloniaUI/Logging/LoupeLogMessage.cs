using Avalonia.Media;
using Microsoft.Extensions.Logging;

namespace EsUtil.Feature.Loupe.AvaloniaUI.Logging;

/// <summary>ルーペ（Avalonia 版）が出力する LoggerMessage 定義（ソース生成ログ）。</summary>
/// <remarks>
/// <b>本アセンブリが単一所有するもの</b>: 拡大元画像（<see cref="LoupeControl.Source"/>）はコントロール固有の概念であり、<br/>
/// 「画像が差し替わった」ことを知っているのは画像を所有する UI 層だけである（記録も同じ層が担う）。<br/>
/// 描画の例外（<c>LogUpdateError</c>）も描画を実行する UI 層が記録する。<br/>
/// ログ基盤（ファイル出力・レベル制御）はホストアプリケーションが所有する（本クラスは定義のみ）。<br/>
/// 各メソッドは <see cref="ILogger"/> の拡張メソッド（<c>this ILogger</c> 第一引数）として定義し、呼び出し側では logger 引数を省略する。
/// </remarks>
internal static partial class LoupeLogMessage
{
    /// <summary>接頭辞の既定値（ホストが設定しない場合に使う）。</summary>
    internal const string DefaultPrefix = "[Loupe]";

    /// <summary>適用する接頭辞（ホストが <c>LogPrefix</c> で設定する）。</summary>
    internal static string Prefix { get; set; } = DefaultPrefix;

    // ====================================================================
    // 例外・操作
    // ====================================================================

    /// <summary>公開ファサードの操作で例外が発生したことを記録する（生成メソッド）。</summary>
    /// <remarks>LogLevel: Error<br/>出力書式: <c>[Loupe] {Operation} で例外が発生</c>（例外スタックトレース付き）</remarks>
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
    internal static void LogOperationError(this ILogger logger, Exception exception, string operation)
        => logger.LogOperationErrorCore(exception, Prefix, operation);

    // ====================================================================
    // 拡大元画像（UI が単一所有する値）
    // ====================================================================

    /// <summary>拡大元画像の変更ログ（同値の再設定では出力しない）。</summary>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="oldValue">変更前の拡大元画像（未設定は null）。</param>
    /// <param name="newValue">変更後の拡大元画像（未設定は null）。</param>
    /// <remarks>
    /// LogLevel: Debug<br/>出力書式: <c>[Loupe] Setting: Source {OldValue} → {NewValue}</c><br/>
    /// 「別のインスタンスに差し替わったか」の判断（参照の同一性）を本メソッドが単一所有する。
    /// </remarks>
    internal static void LogSourceChanged(this ILogger logger, IImage? oldValue, IImage? newValue)
    {
        if (ReferenceEquals(oldValue, newValue))
        {
            return;
        }

        logger.LogSettingChanged("Source", Describe(oldValue), Describe(newValue));
    }

    /// <summary>設定変更のログ（生成メソッド）。</summary>
    /// <remarks>LogLevel: Debug<br/>出力書式: <c>[Loupe] Setting: {Name} {OldValue} → {NewValue}</c></remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="prefix">接頭辞（公開ラッパーが <see cref="Prefix"/> を渡す）。</param>
    /// <param name="name">設定名。</param>
    /// <param name="oldValue">変更前の値（整形済み）。</param>
    /// <param name="newValue">変更後の値（整形済み）。</param>
    [LoggerMessage(LogLevel.Debug, "{Prefix} Setting: {Name} {OldValue} → {NewValue}")]
    private static partial void LogSettingChangedCore(this ILogger logger, string prefix, string name, string oldValue, string newValue);

    /// <summary>設定変更のログ（整形済みの値を渡す）。</summary>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="name">設定名。</param>
    /// <param name="oldValue">変更前の値（整形済み）。</param>
    /// <param name="newValue">変更後の値（整形済み）。</param>
    internal static void LogSettingChanged(this ILogger logger, string name, string oldValue, string newValue)
        => logger.LogSettingChangedCore(Prefix, name, oldValue, newValue);

    /// <summary>ルーペ描画の例外エラーログ（生成メソッド）。</summary>
    /// <remarks>LogLevel: Error<br/>出力書式: <c>[Loupe] Update exception</c>（例外スタックトレース付き）</remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="prefix">接頭辞（公開ラッパーが <see cref="Prefix"/> を渡す）。</param>
    /// <param name="exception">発生した例外。</param>
    [LoggerMessage(LogLevel.Error, "{Prefix} Update exception")]
    private static partial void LogUpdateErrorCore(this ILogger logger, Exception exception, string prefix);

    /// <summary>ルーペ描画の例外エラーログ。</summary>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="exception">発生した例外。</param>
    /// <remarks>例外の記録は描画を実行する UI が行う（コアは描画 API を持たず例外を検知しない）。</remarks>
    internal static void LogUpdateError(this ILogger logger, Exception exception)
        => logger.LogUpdateErrorCore(exception, Prefix);

    /// <summary>ソース画像の寸法をログ用の文字列（<c>幅x高さ</c>）へ整形する。</summary>
    /// <param name="source">対象のソース画像。 null は「画像なし」。</param>
    /// <returns><c>幅x高さ</c> 形式の文字列。 null は <c>null</c>、破棄済みは型名。</returns>
    /// <remarks>
    /// <see cref="IImage.Size"/> の参照は破棄済みビットマップで例外になるため、<br/>
    /// 例外時は型名を返す（ログ出力で例外を発生させない）。
    /// </remarks>
    private static string Describe(IImage? source)
    {
        if (source is null)
        {
            return "null";
        }

        try
        {
            var size = source.Size;
            return $"{(int)size.Width}x{(int)size.Height}";
        }
        catch (ObjectDisposedException)
        {
            // 破棄済みビットマップ: 寸法を取得できないため型名で代用する（ログは継続する）
            return source.GetType().Name;
        }
    }
}
