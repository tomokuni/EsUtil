using Microsoft.Extensions.Logging;

namespace EsUtil.Feature.Annotation.Logging;

/// <summary>注釈の LoggerMessage 定義（ソース生成ログ。 コアと提示層で共有する）。</summary>
/// <remarks>
/// <b>出力の方針</b>: ファサードの生成（Information）・文書の読み込みと保存（Information）・注釈の追加（Debug）・<br/>
/// 設定の変更（Information）とする。 毎フレーム呼ばれる描画計画の解決（<c>ResolveDrawPlan</c>）と当たり判定は出力しない。<br/>
/// ログ基盤（ファイル出力・レベル制御）はホストが所有する（本クラスは定義のみ）。<br/>
/// <b>可視性</b>: 本定義を呼ぶのはコアと提示層だけであり、UI 層・ホストはロガーを渡すだけでよいため internal とする。
/// </remarks>
internal static partial class AnnotationLogMessage
{
    /// <summary>接頭辞の既定値（ホストが設定しない場合に使う）。</summary>
    internal const string DefaultPrefix = "[Annotation]";

    /// <summary>適用する接頭辞（ホストが <c>LogPrefix</c> で設定する）。</summary>
    internal static string Prefix { get; set; } = DefaultPrefix;

    /// <summary>ファサードの生成を記録する（生成メソッド）。</summary>
    /// <remarks>LogLevel: Debug<br/>出力書式: <c>[Annotation] ファサードを作成しました</c></remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="prefix">接頭辞（公開ラッパーが <see cref="Prefix"/> を渡す）。</param>
    [LoggerMessage(LogLevel.Debug, "{Prefix:l} ファサードを作成しました")]
    private static partial void LogFacadeCreatedCore(this ILogger logger, string prefix);

    /// <summary>ファサードの生成を記録する。</summary>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    internal static void LogFacadeCreated(this ILogger logger)
        => logger.LogFacadeCreatedCore(Prefix);

    /// <summary>提示層の生成を記録する（生成メソッド）。</summary>
    /// <remarks>LogLevel: Debug<br/>出力書式: <c>[Annotation] 提示層を作成しました</c></remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="prefix">接頭辞（公開ラッパーが <see cref="Prefix"/> を渡す）。</param>
    [LoggerMessage(LogLevel.Debug, "{Prefix:l} 提示層を作成しました")]
    private static partial void LogPresenterCreatedCore(this ILogger logger, string prefix);

    /// <summary>提示層の生成を記録する。</summary>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    internal static void LogPresenterCreated(this ILogger logger)
        => logger.LogPresenterCreatedCore(Prefix);

    /// <summary>出力先（ロガー）の設定を記録する（生成メソッド）。</summary>
    /// <remarks>LogLevel: Debug<br/>出力書式: <c>[Annotation] ロガーを接続しました</c></remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="prefix">接頭辞（公開ラッパーが <see cref="Prefix"/> を渡す）。</param>
    [LoggerMessage(LogLevel.Debug, "{Prefix:l} ロガーを接続しました")]
    private static partial void LogLoggerAttachedCore(this ILogger logger, string prefix);

    /// <summary>出力先（ロガー）の設定を記録する。</summary>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    internal static void LogLoggerAttached(this ILogger logger)
        => logger.LogLoggerAttachedCore(Prefix);

    /// <summary>注釈の文書の生成を記録する（生成メソッド）。</summary>
    /// <remarks>LogLevel: Debug<br/>出力書式: <c>[Annotation] 文書を作成: {Width}x{Height}</c></remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="prefix">接頭辞（公開ラッパーが <see cref="Prefix"/> を渡す）。</param>
    /// <param name="width">元画像の幅（ピクセル）。</param>
    /// <param name="height">元画像の高さ（ピクセル）。</param>
    [LoggerMessage(LogLevel.Debug, "{Prefix:l} 文書を作成: {Width}x{Height}")]
    private static partial void LogDocumentCreatedCore(this ILogger logger, string prefix, int width, int height);

    /// <summary>注釈の文書の生成を記録する。</summary>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="width">元画像の幅（ピクセル）。</param>
    /// <param name="height">元画像の高さ（ピクセル）。</param>
    internal static void LogDocumentCreated(this ILogger logger, int width, int height)
        => logger.LogDocumentCreatedCore(Prefix, width, height);

    /// <summary>注釈ファイルの読み込みを記録する（生成メソッド）。</summary>    /// <remarks>LogLevel: Information<br/>出力書式: <c>[Annotation] 読み込み: {FileName} 注釈={Count} 件</c></remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="prefix">接頭辞（公開ラッパーが <see cref="Prefix"/> を渡す）。</param>
    /// <param name="fileName">読み込んだファイルの名前。</param>
    /// <param name="count">読み込んだ注釈の件数。</param>
    [LoggerMessage(LogLevel.Information, "{Prefix:l} 読み込み: {FileName:l} 注釈={Count} 件")]
    private static partial void LogDocumentLoadedCore(this ILogger logger, string prefix, string fileName, int count);

    /// <summary>注釈ファイルの読み込みを記録する。</summary>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="fileName">読み込んだファイルの名前。</param>
    /// <param name="count">読み込んだ注釈の件数。</param>
    internal static void LogDocumentLoaded(this ILogger logger, string fileName, int count)
        => logger.LogDocumentLoadedCore(Prefix, fileName, count);

    /// <summary>注釈ファイルの保存を記録する（生成メソッド）。</summary>
    /// <remarks>LogLevel: Information<br/>出力書式: <c>[Annotation] 保存: {FileName} 注釈={Count} 件</c></remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="prefix">接頭辞（公開ラッパーが <see cref="Prefix"/> を渡す）。</param>
    /// <param name="fileName">保存したファイルの名前。</param>
    /// <param name="count">保存した注釈の件数。</param>
    [LoggerMessage(LogLevel.Information, "{Prefix:l} 保存: {FileName:l} 注釈={Count} 件")]
    private static partial void LogDocumentSavedCore(this ILogger logger, string prefix, string fileName, int count);

    /// <summary>注釈ファイルの保存を記録する。</summary>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="fileName">保存したファイルの名前。</param>
    /// <param name="count">保存した注釈の件数。</param>
    internal static void LogDocumentSaved(this ILogger logger, string fileName, int count)
        => logger.LogDocumentSavedCore(Prefix, fileName, count);

    /// <summary>注釈の追加を記録する（生成メソッド）。</summary>
    /// <remarks>LogLevel: Debug<br/>出力書式: <c>[Annotation] 注釈を追加: 種別={Kind} 識別子={Id}</c></remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="prefix">接頭辞（公開ラッパーが <see cref="Prefix"/> を渡す）。</param>
    /// <param name="kind">注釈の種別。</param>
    /// <param name="id">追加した注釈の識別子。</param>
    [LoggerMessage(LogLevel.Debug, "{Prefix:l} 注釈を追加: 種別={Kind:l} 識別子={Id:l}")]
    private static partial void LogAnnotationAddedCore(this ILogger logger, string prefix, string kind, string id);

    /// <summary>注釈の追加を記録する。</summary>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="kind">注釈の種別。</param>
    /// <param name="id">追加した注釈の識別子。</param>
    internal static void LogAnnotationAdded(this ILogger logger, AnnotationKind kind, string id)
        => logger.LogAnnotationAddedCore(Prefix, kind.ToString(), id);

    /// <summary>設定値の変更を記録する（生成メソッド。 整形済みの値を渡す）。</summary>
    /// <remarks>LogLevel: Debug<br/>出力書式: <c>[Annotation] 設定: {Name} {OldValue} → {NewValue}</c></remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="prefix">接頭辞（公開ラッパーが <see cref="Prefix"/> を渡す）。</param>
    /// <param name="name">変更された設定名。</param>
    /// <param name="oldValue">変更前の値（整形済み）。</param>
    /// <param name="newValue">変更後の値（整形済み）。</param>
    [LoggerMessage(LogLevel.Debug, "{Prefix:l} 設定: {Name:l} {OldValue:l} → {NewValue:l}")]
    private static partial void LogSettingChangedCore(this ILogger logger, string prefix, string name, string oldValue, string newValue);

    /// <summary>設定値の変更を記録する。</summary>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="name">変更された設定名。</param>
    /// <param name="oldValue">変更前の値（整形済み）。</param>
    /// <param name="newValue">変更後の値（整形済み）。</param>
    internal static void LogSettingChanged(this ILogger logger, string name, string oldValue, string newValue)
        => logger.LogSettingChangedCore(Prefix, name, oldValue, newValue);

    /// <summary>公開ファサードの操作で例外が発生したことを記録する（生成メソッド）。</summary>
    /// <remarks>LogLevel: Error<br/>出力書式: <c>[Annotation] {Operation} で例外が発生</c>（例外スタックトレース付き）</remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="exception">発生した例外。</param>
    /// <param name="prefix">接頭辞（公開ラッパーが <see cref="Prefix"/> を渡す）。</param>
    /// <param name="operation">操作名（<c>nameof(操作)</c>）。</param>
    [LoggerMessage(LogLevel.Error, "{Prefix:l} {Operation:l} で例外が発生")]
    private static partial void LogOperationErrorCore(this ILogger logger, Exception exception, string prefix, string operation);

    /// <summary>公開ファサードの操作で例外が発生したことを記録する。</summary>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="exception">発生した例外。</param>
    /// <param name="operation">操作名（<c>nameof(操作)</c>）。</param>
    internal static void LogOperationError(this ILogger logger, Exception exception, string operation)
        => logger.LogOperationErrorCore(exception, Prefix, operation);
}
