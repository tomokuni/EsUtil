using EsUtil.Platform.Core.Common;
using EsUtil.Platform.Core.Geometry;
using Microsoft.Extensions.Logging;

namespace EsUtil.Feature.Annotation.Logging;

/// <summary>注釈の LoggerMessage 定義（ソース生成ログ。 コア・提示層・UI で共有する）。</summary>
/// <remarks>
/// <b>レベルは頻度で決める</b>（規則の所有者は <c>.github/ログ規約.md</c> §2）。<br/>
/// - Information: 利用者の明示的な操作（注釈ファイルの読み込み・保存）・プロセスで 1 度だけの生成・設定（行間）の変更<br/>
/// - Debug: 状態の変化（選択・本文の編集・書式の適用・パネルの表示・押下と解放）と提示層の生成<br/>
/// - Trace: 高頻度の値（ドラッグ中の文書の変更）<br/>
/// - Error: 公開ファサードの操作で発生した例外<br/>
/// <b>障害の切り分け</b>: 既定（Information）では操作の記録だけが出る。 利用者から症状の報告を受けたら、
/// Debug で「どの操作が起きたか」、Trace で「ドラッグが文書へ反映されているか」を確認できる。<br/>
/// 毎フレーム呼ばれる描画計画の解決（<c>ResolveDrawPlan</c>）と当たり判定は出力しない。<br/>
/// ログ基盤（ファイル出力・レベル制御）はホストが所有する（本クラスは定義のみ）。<br/>
/// <b>可視性</b>: UI 層が呼ぶ定義だけ <c>public</c> とし、コアと提示層だけが使う定義は <c>internal</c> とする。
/// </remarks>
public static partial class AnnotationLogMessage
{
    /// <summary>接頭辞の既定値（ホストが設定しない場合に使う）。</summary>
    internal const string DefaultPrefix = "[Annotation]";

    /// <summary>適用する接頭辞（ホストが <c>LogPrefix</c> で設定する）。</summary>
    internal static string Prefix { get; set; } = DefaultPrefix;

    /// <summary>ファサードの生成を記録する（生成メソッド）。</summary>
    /// <remarks>LogLevel: Information<br/>出力書式: <c>[Annotation] ファサードを作成しました</c></remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="prefix">接頭辞（公開ラッパーが <see cref="Prefix"/> を渡す）。</param>
    [LoggerMessage(LogLevel.Information, "{Prefix:l} ファサードを作成しました")]
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

    /// <summary>注釈ファイルの読み込みを記録する（生成メソッド）。</summary>
    /// <remarks>LogLevel: Information<br/>出力書式: <c>[Annotation] 読み込み: {FileName} 注釈={Count} 件</c></remarks>
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

    /// <summary>注釈の生成を記録する（生成メソッド）。</summary>
    /// <remarks>LogLevel: Debug<br/>出力書式: <c>[Annotation] 注釈を作成: 種別={Kind} 識別子={Id}</c></remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="prefix">接頭辞（公開ラッパーが <see cref="Prefix"/> を渡す）。</param>
    /// <param name="kind">注釈の種別。</param>
    /// <param name="id">生成した注釈の識別子。</param>
    [LoggerMessage(LogLevel.Debug, "{Prefix:l} 注釈を作成: 種別={Kind:l} 識別子={Id:l}")]
    private static partial void LogAnnotationCreatedCore(this ILogger logger, string prefix, string kind, string id);

    /// <summary>注釈の生成を記録する。</summary>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="kind">注釈の種別。</param>
    /// <param name="id">生成した注釈の識別子。</param>
    internal static void LogAnnotationCreated(this ILogger logger, AnnotationKind kind, string id)
        => logger.LogAnnotationCreatedCore(Prefix, kind.ToString(), id);

    /// <summary>付随ファイルを削除したことを記録する（生成メソッド）。</summary>
    /// <remarks>LogLevel: Information<br/>出力書式: <c>[Annotation] 削除: {FileName}</c></remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="prefix">接頭辞（公開ラッパーが <see cref="Prefix"/> を渡す）。</param>
    /// <param name="fileName">削除したファイルの名前。</param>
    [LoggerMessage(LogLevel.Information, "{Prefix:l} 削除: {FileName:l}")]
    private static partial void LogCompanionFileRemovedCore(this ILogger logger, string prefix, string fileName);

    /// <summary>付随ファイルを削除したことを記録する。</summary>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="fileName">削除したファイルの名前。</param>
    internal static void LogCompanionFileRemoved(this ILogger logger, string fileName)
        => logger.LogCompanionFileRemovedCore(Prefix, fileName);

    /// <summary>設定値の変更を記録する（生成メソッド。 整形済みの値を渡す）。</summary>
    /// <remarks>LogLevel: Information<br/>出力書式: <c>[Annotation] 設定: {Name} {OldValue} → {NewValue}</c></remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="prefix">接頭辞（公開ラッパーが <see cref="Prefix"/> を渡す）。</param>
    /// <param name="name">変更された設定名。</param>
    /// <param name="oldValue">変更前の値（整形済み）。</param>
    /// <param name="newValue">変更後の値（整形済み）。</param>
    [LoggerMessage(LogLevel.Information, "{Prefix:l} 設定: {Name:l} {OldValue:l} → {NewValue:l}")]
    private static partial void LogSettingChangedCore(this ILogger logger, string prefix, string name, string oldValue, string newValue);

    /// <summary>設定値の変更を記録する。</summary>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="name">変更された設定名。</param>
    /// <param name="oldValue">変更前の値（整形済み）。</param>
    /// <param name="newValue">変更後の値（整形済み）。</param>
    internal static void LogSettingChanged(this ILogger logger, string name, string oldValue, string newValue)
        => logger.LogSettingChangedCore(Prefix, name, oldValue, newValue);

    /// <summary>実数設定値の変更を記録する（同値の再設定では出力しない）。</summary>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="name">変更された設定名。</param>
    /// <param name="oldValue">変更前の値。</param>
    /// <param name="newValue">変更後の値。</param>
    /// <remarks>
    /// 書式は <see cref="LogSettingChanged(ILogger, string, string, string)"/> と同一であり、
    /// 「変化したときだけ出力する」判定と数値の整形を本メソッドが単一所有する
    /// （呼び出し側に整形と無効なレベルの判定を書かせない。 規則は <c>.github/ログ規約.md</c> §5）。
    /// </remarks>
    internal static void LogSettingChanged(this ILogger logger, string name, double oldValue, double newValue)
    {
        if (oldValue.Equals(newValue) || !logger.IsEnabled(LogLevel.Information))
        {
            return;
        }

        logger.LogSettingChangedCore(Prefix, name, NumberText.Format(oldValue), NumberText.Format(newValue));
    }

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
    /// <summary>注釈の選択の変更を記録する（生成メソッド）。</summary>
    /// <remarks>LogLevel: Debug<br/>出力書式: <c>[Annotation] 選択: {Id}</c></remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="prefix">接頭辞（公開ラッパーが <see cref="Prefix"/> を渡す）。</param>
    /// <param name="id">選択した注釈の識別子（解除は <c>none</c>）。</param>
    [LoggerMessage(LogLevel.Debug, "{Prefix:l} 選択: {Id:l}")]
    private static partial void LogSelectionChangedCore(this ILogger logger, string prefix, string id);

    /// <summary>注釈の選択の変更を記録する。</summary>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="id">選択した注釈の識別子。 解除は <see langword="null"/>。</param>
    public static void LogSelectionChanged(this ILogger logger, string? id)
        => logger.LogSelectionChangedCore(Prefix, id ?? "none");

    /// <summary>本文の編集の開始・終了を記録する（生成メソッド）。</summary>
    /// <remarks>LogLevel: Debug<br/>出力書式: <c>[Annotation] 本文の編集: {State}</c></remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="prefix">接頭辞（公開ラッパーが <see cref="Prefix"/> を渡す）。</param>
    /// <param name="state">状態（<c>開始</c> / <c>終了</c>）。</param>
    [LoggerMessage(LogLevel.Debug, "{Prefix:l} 本文の編集: {State:l}")]
    private static partial void LogEditingChangedCore(this ILogger logger, string prefix, string state);

    /// <summary>本文の編集の開始・終了を記録する。</summary>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="editing">編集中の場合は <see langword="true"/>。</param>
    public static void LogEditingChanged(this ILogger logger, bool editing)
        => logger.LogEditingChangedCore(Prefix, editing ? "開始" : "終了");

    /// <summary>本文の書式の適用を記録する（生成メソッド）。</summary>
    /// <remarks>LogLevel: Debug<br/>出力書式: <c>[Annotation] 書式を適用: 開始={Start} 長さ={Length}</c></remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="prefix">接頭辞（公開ラッパーが <see cref="Prefix"/> を渡す）。</param>
    /// <param name="start">適用する範囲の開始位置（文字数）。</param>
    /// <param name="length">適用する範囲の長さ（文字数）。</param>
    [LoggerMessage(LogLevel.Debug, "{Prefix:l} 書式を適用: 開始={Start} 長さ={Length}")]
    private static partial void LogTextStyleAppliedCore(this ILogger logger, string prefix, int start, int length);

    /// <summary>本文の書式の適用を記録する。</summary>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="start">適用する範囲の開始位置（文字数）。</param>
    /// <param name="length">適用する範囲の長さ（文字数）。</param>
    public static void LogTextStyleApplied(this ILogger logger, int start, int length)
        => logger.LogTextStyleAppliedCore(Prefix, start, length);

    /// <summary>フローティング パネルの表示・終了を記録する（生成メソッド）。</summary>
    /// <remarks>LogLevel: Debug<br/>出力書式: <c>[Annotation] パネル: {Kind} {State}</c></remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="prefix">接頭辞（公開ラッパーが <see cref="Prefix"/> を渡す）。</param>
    /// <param name="kind">パネルの種別。</param>
    /// <param name="state">状態（<c>表示</c> / <c>終了</c>）。</param>
    [LoggerMessage(LogLevel.Debug, "{Prefix:l} パネル: {Kind:l} {State:l}")]
    private static partial void LogPanelVisibilityCore(this ILogger logger, string prefix, string kind, string state);

    /// <summary>フローティング パネルの表示・終了を記録する。</summary>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="kind">パネルの種別（<c>nameof</c> で渡す）。</param>
    /// <param name="visible">表示した場合は <see langword="true"/>。</param>
    public static void LogPanelVisibility(this ILogger logger, string kind, bool visible)
        => logger.LogPanelVisibilityCore(Prefix, kind, visible ? "表示" : "終了");

    /// <summary>パネルを置いた位置を記録する（生成メソッド）。</summary>
    /// <remarks>LogLevel: Debug<br/>出力書式: <c>[Annotation] パネル: {Kind} 位置=({X}, {Y}) 領域=({AreaX}, {AreaY}, {AreaWidth}, {AreaHeight})</c><br/>
    /// 置ける領域も残す（パネルが切れる・画面の外へ出るといった不具合の切り分けに要る）。</remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="prefix">接頭辞（公開ラッパーが <see cref="Prefix"/> を渡す）。</param>
    /// <param name="kind">パネルの種別。</param>
    /// <param name="x">左上の X（整形済み）。</param>
    /// <param name="y">左上の Y（整形済み）。</param>
    /// <param name="areaX">置ける領域の X（整形済み）。</param>
    /// <param name="areaY">置ける領域の Y（整形済み）。</param>
    /// <param name="areaWidth">置ける領域の幅（整形済み）。</param>
    /// <param name="areaHeight">置ける領域の高さ（整形済み）。</param>
    [LoggerMessage(LogLevel.Debug, "{Prefix:l} パネル: {Kind:l} 位置=({X:l}, {Y:l}) 領域=({AreaX:l}, {AreaY:l}, {AreaWidth:l}, {AreaHeight:l})")]
    private static partial void LogPanelPlacedCore(
        this ILogger logger,
        string prefix,
        string kind,
        string x,
        string y,
        string areaX,
        string areaY,
        string areaWidth,
        string areaHeight);

    /// <summary>パネルを置いた位置を記録する。</summary>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="kind">パネルの種別（<c>nameof</c> で渡す）。</param>
    /// <param name="position">左上の位置（DIP）。</param>
    /// <param name="available">置ける領域（DIP）。</param>
    /// <remarks>
    /// <b>大きさは記録しない</b>: レイアウトの確定前は実寸と違う値が返り、不具合の切り分けを誤らせる。<br/>
    /// <b>整形の前に有効かどうかを判定する</b>: 座標の整形は文字列を確保するため、無効なレベルでは行わない
    /// （規則は <c>.github/コーディング規約.md</c> §7）。
    /// </remarks>
    public static void LogPanelPlaced(this ILogger logger, string kind, EsPoint position, EsRect available)
    {
        if (!logger.IsEnabled(LogLevel.Debug))
        {
            return;
        }

        logger.LogPanelPlacedCore(
            Prefix,
            kind,
            NumberText.Format(position.X),
            NumberText.Format(position.Y),
            NumberText.Format(available.X),
            NumberText.Format(available.Y),
            NumberText.Format(available.Width),
            NumberText.Format(available.Height));
    }

    /// <summary>注釈への押下を記録する（生成メソッド）。</summary>
    /// <remarks>LogLevel: Debug<br/>出力書式: <c>[Annotation] 押下: 位置=({X}, {Y}) クリック回数={ClickCount}</c></remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="prefix">接頭辞（公開ラッパーが <see cref="Prefix"/> を渡す）。</param>
    /// <param name="x">押下した位置の X（元画像ピクセル。 整形済み）。</param>
    /// <param name="y">押下した位置の Y（元画像ピクセル。 整形済み）。</param>
    /// <param name="clickCount">クリック回数。</param>
    [LoggerMessage(LogLevel.Debug, "{Prefix:l} 押下: 位置=({X:l}, {Y:l}) クリック回数={ClickCount}")]
    private static partial void LogPointerPressedCore(this ILogger logger, string prefix, string x, string y, int clickCount);

    /// <summary>注釈への押下を記録する。</summary>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="point">押下した位置（元画像ピクセル）。</param>
    /// <param name="clickCount">クリック回数。</param>
    /// <remarks>
    /// <b>整形の前に有効かどうかを判定する</b>: 座標の整形は文字列を確保するため、無効なレベルでは行わない
    /// （規則は <c>.github/コーディング規約.md</c> §7）。
    /// </remarks>
    public static void LogPointerPressed(this ILogger logger, EsPoint point, int clickCount)
    {
        if (!logger.IsEnabled(LogLevel.Debug))
        {
            return;
        }

        logger.LogPointerPressedCore(Prefix, NumberText.Format(point.X), NumberText.Format(point.Y), clickCount);
    }

    /// <summary>注釈からの解放を記録する（生成メソッド）。</summary>
    /// <remarks>LogLevel: Debug<br/>出力書式: <c>[Annotation] 解放: 位置=({X}, {Y}) 選択あり={HasSelection}</c></remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="prefix">接頭辞（公開ラッパーが <see cref="Prefix"/> を渡す）。</param>
    /// <param name="x">解放した位置の X（元画像ピクセル。 整形済み）。</param>
    /// <param name="y">解放した位置の Y（元画像ピクセル。 整形済み）。</param>
    /// <param name="hasSelection">選択がある場合 <c>true</c>。</param>
    [LoggerMessage(LogLevel.Debug, "{Prefix:l} 解放: 位置=({X:l}, {Y:l}) 選択あり={HasSelection}")]
    private static partial void LogPointerReleasedCore(this ILogger logger, string prefix, string x, string y, bool hasSelection);

    /// <summary>注釈からの解放を記録する。</summary>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="point">解放した位置（元画像ピクセル）。</param>
    /// <param name="hasSelection">選択がある場合 <see langword="true"/>。</param>
    /// <remarks>
    /// <b>整形の前に有効かどうかを判定する</b>: 座標の整形は文字列を確保するため、無効なレベルでは行わない
    /// （規則は <c>.github/コーディング規約.md</c> §7）。
    /// </remarks>
    public static void LogPointerReleased(this ILogger logger, EsPoint point, bool hasSelection)
    {
        if (!logger.IsEnabled(LogLevel.Debug))
        {
            return;
        }

        logger.LogPointerReleasedCore(Prefix, NumberText.Format(point.X), NumberText.Format(point.Y), hasSelection);
    }

    /// <summary>ドラッグ中の文書の変更を記録する（生成メソッド）。</summary>
    /// <remarks>LogLevel: Trace<br/>出力書式: <c>[Annotation] 文書を変更: 注釈={Count} 件</c><br/>
    /// ドラッグの 1 歩ごとに届くため Trace で記録する（既定では出力しない）。</remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="prefix">接頭辞（公開ラッパーが <see cref="Prefix"/> を渡す）。</param>
    /// <param name="count">注釈の件数。</param>
    [LoggerMessage(LogLevel.Trace, "{Prefix:l} 文書を変更: 注釈={Count} 件")]
    private static partial void LogDocumentChangedCore(this ILogger logger, string prefix, int count);

    /// <summary>ドラッグ中の文書の変更を記録する。</summary>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="count">注釈の件数。</param>
    public static void LogDocumentChanged(this ILogger logger, int count)
        => logger.LogDocumentChangedCore(Prefix, count);
}