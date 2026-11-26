using EsUtil.Platform.Core.Common;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Palette;
using Microsoft.Extensions.Logging;

namespace EsUtil.Feature.Annotation.Logging;

/// <summary>注釈の LoggerMessage 定義（ソース生成ログ。 コア・提示層・UI で共有する）。</summary>
/// <remarks>
/// <b>レベルは頻度で決める</b>（規則の所有者は <c>.github/ログ規約.md</c> §2）。<br/>
/// - Information: 利用者の明示的な操作（注釈ファイルの読み込み・保存）・プロセスで 1 度だけの生成・設定（行間）の変更<br/>
/// - Debug: 状態の変化（選択・本文の編集・本文の変更・本文の操作（コピー・切り取り・貼り付け）・書式の適用・注釈の削除・重なり順・履歴の移動・パネルの表示・押下と解放）と提示層の生成<br/>
/// - Trace: 高頻度の値（ドラッグ中の文書の変更・位置と大きさ・しっぽ・外観・本文の余白の変更）<br/>
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
    /// <remarks>
    /// LogLevel: Debug<br/>出力書式: <c>[Annotation] ファサードを作成しました: 行間={LineHeightFactor}</c>
    /// </remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="prefix">接頭辞（公開ラッパーが <see cref="Prefix"/> を渡す）。</param>
    /// <param name="lineHeightFactor">行間の倍率（整形済み）。</param>
    [LoggerMessage(LogLevel.Debug, "{Prefix:l} ファサードを作成しました: 行間={LineHeightFactor:l}")]
    private static partial void LogFacadeCreatedCore(
        this ILogger logger, string prefix, string lineHeightFactor);

    /// <summary>ファサードの生成を記録する。</summary>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="lineHeightFactor">生成時の行間の倍率。</param>
    internal static void LogFacadeCreated(this ILogger logger, double lineHeightFactor)
    {
        if (!logger.IsEnabled(LogLevel.Debug))
        {
            return;
        }

        logger.LogFacadeCreatedCore(Prefix, NumberText.Format(lineHeightFactor));
    }

    /// <summary>提示層の生成を記録する（生成メソッド）。</summary>
    /// <remarks>
    /// LogLevel: Debug<br/>出力書式: <c>[Annotation] 提示層を作成しました: 行間={LineHeightFactor}</c>
    /// </remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="prefix">接頭辞（公開ラッパーが <see cref="Prefix"/> を渡す）。</param>
    /// <param name="lineHeightFactor">行間の倍率（整形済み）。</param>
    [LoggerMessage(LogLevel.Debug, "{Prefix:l} 提示層を作成しました: 行間={LineHeightFactor:l}")]
    private static partial void LogPresenterCreatedCore(
        this ILogger logger, string prefix, string lineHeightFactor);

    /// <summary>提示層の生成を記録する。</summary>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="lineHeightFactor">生成時の行間の倍率。</param>
    internal static void LogPresenterCreated(this ILogger logger, double lineHeightFactor)
    {
        if (!logger.IsEnabled(LogLevel.Debug))
        {
            return;
        }

        logger.LogPresenterCreatedCore(Prefix, NumberText.Format(lineHeightFactor));
    }

    /// <summary>出力先（ロガー）の設定を記録する（生成メソッド）。</summary>
    /// <remarks>
    /// LogLevel: Debug<br/>出力書式: <c>[Annotation] ロガーを接続しました: 接頭辞={AttachedPrefix}</c>
    /// </remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="prefix">接頭辞（公開ラッパーが <see cref="Prefix"/> を渡す）。</param>
    /// <param name="attachedPrefix">接続時点の接頭辞（整形済み）。</param>
    [LoggerMessage(LogLevel.Debug, "{Prefix:l} ロガーを接続しました: 接頭辞={AttachedPrefix:l}")]
    private static partial void LogLoggerAttachedCore(
        this ILogger logger, string prefix, string attachedPrefix);

    /// <summary>出力先（ロガー）の設定を記録する。</summary>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    internal static void LogLoggerAttached(this ILogger logger)
        => logger.LogLoggerAttachedCore(Prefix, Prefix);

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
    /// <remarks>
    /// LogLevel: Debug<br/>出力書式:
    /// <c>[Annotation] 注釈を作成: 種別={Kind} 識別子={Id} 位置=({X}, {Y}) 大きさ=({Width}, {Height})</c>
    /// </remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="prefix">接頭辞（公開ラッパーが <see cref="Prefix"/> を渡す）。</param>
    /// <param name="kind">注釈の種別。</param>
    /// <param name="id">生成した注釈の識別子。</param>
    /// <param name="x">左上の X（整形済み）。</param>
    /// <param name="y">左上の Y（整形済み）。</param>
    /// <param name="width">幅（整形済み）。</param>
    /// <param name="height">高さ（整形済み）。</param>
    [LoggerMessage(
        LogLevel.Debug,
        "{Prefix:l} 注釈を作成: 種別={Kind:l} 識別子={Id:l} 位置=({X:l}, {Y:l}) 大きさ=({Width:l}, {Height:l})")]
    private static partial void LogAnnotationCreatedCore(
        this ILogger logger, string prefix, string kind, string id, string x, string y, string width, string height);

    /// <summary>注釈の生成を記録する。</summary>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="kind">注釈の種別。</param>
    /// <param name="id">生成した注釈の識別子。</param>
    /// <param name="bounds">生成した注釈の矩形（元画像ピクセル）。</param>
    internal static void LogAnnotationCreated(this ILogger logger, AnnotationKind kind, string id, EsRect bounds)
    {
        if (!logger.IsEnabled(LogLevel.Debug))
        {
            return;
        }

        logger.LogAnnotationCreatedCore(
            Prefix,
            kind.ToString(),
            id,
            NumberText.Format(bounds.X),
            NumberText.Format(bounds.Y),
            NumberText.Format(bounds.Width),
            NumberText.Format(bounds.Height));
    }

    /// <summary>注釈の削除を記録する（生成メソッド）。</summary>
    /// <remarks>
    /// LogLevel: Debug<br/>出力書式:
    /// <c>[Annotation] 注釈を削除: 種別={Kind} 識別子={Id} 位置=({X}, {Y}) 大きさ=({Width}, {Height}) 本文={Preview}</c>
    /// </remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="prefix">接頭辞（公開ラッパーが <see cref="Prefix"/> を渡す）。</param>
    /// <param name="kind">注釈の種別。</param>
    /// <param name="id">削除した注釈の識別子。</param>
    /// <param name="x">左上の X（整形済み）。</param>
    /// <param name="y">左上の Y（整形済み）。</param>
    /// <param name="width">幅（整形済み）。</param>
    /// <param name="height">高さ（整形済み）。</param>
    /// <param name="preview">本文の先頭（整形済み。 枠線は空）。</param>
    [LoggerMessage(
        LogLevel.Debug,
        "{Prefix:l} 注釈を削除: 種別={Kind:l} 識別子={Id:l} 位置=({X:l}, {Y:l}) 大きさ=({Width:l}, {Height:l}) 本文={Preview:l}")]
    private static partial void LogAnnotationRemovedCore(
        this ILogger logger, string prefix, string kind, string id, string x, string y, string width, string height, string preview);

    /// <summary>注釈の削除を記録する（削除後は値が引けなくなるため、位置・大きさ・本文を削除前に渡す）。</summary>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="kind">注釈の種別。</param>
    /// <param name="id">削除した注釈の識別子。</param>
    /// <param name="bounds">削除した注釈の矩形（元画像ピクセル）。</param>
    /// <param name="text">削除した吹き出しの本文（枠線は空）。 先頭 100 文字だけを記録する。</param>
    internal static void LogAnnotationRemoved(
        this ILogger logger, AnnotationKind kind, string id, EsRect bounds, string text)
    {
        if (!logger.IsEnabled(LogLevel.Debug))
        {
            return;
        }

        logger.LogAnnotationRemovedCore(
            Prefix,
            kind.ToString(),
            id,
            NumberText.Format(bounds.X),
            NumberText.Format(bounds.Y),
            NumberText.Format(bounds.Width),
            NumberText.Format(bounds.Height),
            ToPreview(text));
    }

    /// <summary>重なり順の変更を記録する（生成メソッド）。</summary>
    /// <remarks>LogLevel: Debug<br/>出力書式: <c>[Annotation] 重なり順: {Action} 識別子={Id}</c></remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="prefix">接頭辞（公開ラッパーが <see cref="Prefix"/> を渡す）。</param>
    /// <param name="action">移動の向き（<c>最前面へ</c> / <c>最背面へ</c>）。</param>
    /// <param name="id">移動した注釈の識別子。</param>
    [LoggerMessage(LogLevel.Debug, "{Prefix:l} 重なり順: {Action:l} 識別子={Id:l}")]
    private static partial void LogOrderChangedCore(this ILogger logger, string prefix, string action, string id);

    /// <summary>重なり順の変更を記録する。</summary>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="toFront">最前面へ移した場合は <see langword="true"/>。</param>
    /// <param name="id">移動した注釈の識別子。</param>
    internal static void LogOrderChanged(this ILogger logger, bool toFront, string id)
        => logger.LogOrderChangedCore(Prefix, toFront ? "最前面へ" : "最背面へ", id);

    /// <summary>注釈の位置と大きさの変更を記録する（生成メソッド）。</summary>
    /// <remarks>LogLevel: Trace<br/>出力書式: <c>[Annotation] 位置と大きさ: 位置=({X}, {Y}) 大きさ=({Width}, {Height})</c></remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="prefix">接頭辞（公開ラッパーが <see cref="Prefix"/> を渡す）。</param>
    /// <param name="x">左上の X（元画像ピクセル。 整形済み）。</param>
    /// <param name="y">左上の Y（元画像ピクセル。 整形済み）。</param>
    /// <param name="width">幅（元画像ピクセル。 整形済み）。</param>
    /// <param name="height">高さ（元画像ピクセル。 整形済み）。</param>
    [LoggerMessage(LogLevel.Trace, "{Prefix:l} 位置と大きさ: 位置=({X:l}, {Y:l}) 大きさ=({Width:l}, {Height:l})")]
    private static partial void LogAnnotationBoundsChangedCore(
        this ILogger logger, string prefix, string x, string y, string width, string height);

    /// <summary>注釈の位置と大きさの変更を記録する（ドラッグの 1 歩ごとに届くため Trace とする）。</summary>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="bounds">変更後の矩形（元画像ピクセル）。</param>
    internal static void LogAnnotationBoundsChanged(this ILogger logger, EsRect bounds)
    {
        if (!logger.IsEnabled(LogLevel.Trace))
        {
            return;
        }

        logger.LogAnnotationBoundsChangedCore(
            Prefix,
            NumberText.Format(bounds.X),
            NumberText.Format(bounds.Y),
            NumberText.Format(bounds.Width),
            NumberText.Format(bounds.Height));
    }

    /// <summary>しっぽの変更を記録する（生成メソッド）。</summary>
    /// <remarks>
    /// LogLevel: Trace<br/>出力書式:
    /// <c>[Annotation] しっぽ: 先端=({X}, {Y}) 辺={Edge} 付け根={Offset} 幅={Width}</c>
    /// </remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="prefix">接頭辞（公開ラッパーが <see cref="Prefix"/> を渡す）。</param>
    /// <param name="x">先端の X（元画像ピクセル。 整形済み）。</param>
    /// <param name="y">先端の Y（元画像ピクセル。 整形済み）。</param>
    /// <param name="edge">付け根の辺。</param>
    /// <param name="offset">辺上の位置（整形済み）。</param>
    /// <param name="width">付け根の幅（整形済み）。</param>
    [LoggerMessage(
        LogLevel.Trace,
        "{Prefix:l} しっぽ: 先端=({X:l}, {Y:l}) 辺={Edge:l} 付け根={Offset:l} 幅={Width:l}")]
    private static partial void LogAnnotationTailChangedCore(
        this ILogger logger, string prefix, string x, string y, string edge, string offset, string width);

    /// <summary>しっぽの変更を記録する（ドラッグの 1 歩ごとに届くため Trace とする）。</summary>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="tail">変更後のしっぽ。</param>
    internal static void LogAnnotationTailChanged(this ILogger logger, BubbleTail tail)
    {
        if (!logger.IsEnabled(LogLevel.Trace))
        {
            return;
        }

        logger.LogAnnotationTailChangedCore(
            Prefix,
            NumberText.Format(tail.Tip.X),
            NumberText.Format(tail.Tip.Y),
            tail.BaseEdge.ToString(),
            NumberText.Format(tail.BaseOffset),
            NumberText.Format(tail.Width));
    }

    /// <summary>外観の変更を記録する（生成メソッド）。</summary>
    /// <remarks>
    /// LogLevel: Trace<br/>出力書式:
    /// <c>[Annotation] 外観: 枠線={Border} 太さ={Thickness} 内側={Fill} 角丸={Corner}</c>
    /// </remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="prefix">接頭辞（公開ラッパーが <see cref="Prefix"/> を渡す）。</param>
    /// <param name="border">枠線の色（<c>#AARRGGBB</c>）。</param>
    /// <param name="thickness">枠線の太さ（整形済み）。</param>
    /// <param name="fill">内側の色（<c>#AARRGGBB</c>）。</param>
    /// <param name="corner">角丸の半径（整形済み）。</param>
    [LoggerMessage(
        LogLevel.Trace,
        "{Prefix:l} 外観: 枠線={Border:l} 太さ={Thickness:l} 内側={Fill:l} 角丸={Corner:l}")]
    private static partial void LogAnnotationAppearanceChangedCore(
        this ILogger logger, string prefix, string border, string thickness, string fill, string corner);

    /// <summary>外観の変更を記録する（スライダーの操作中は 1 歩ごとに届くため Trace とする）。</summary>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="appearance">変更後の外観。</param>
    internal static void LogAnnotationAppearanceChanged(this ILogger logger, AnnotationAppearance appearance)
    {
        if (!logger.IsEnabled(LogLevel.Trace))
        {
            return;
        }

        logger.LogAnnotationAppearanceChangedCore(
            Prefix,
            ColorString.ToArgbHex(appearance.Border),
            NumberText.Format(appearance.BorderThickness),
            ColorString.ToArgbHex(appearance.Fill),
            NumberText.Format(appearance.CornerRadius));
    }

    /// <summary>本文の余白の変更を記録する（生成メソッド）。</summary>
    /// <remarks>LogLevel: Trace<br/>出力書式: <c>[Annotation] 本文の余白: {Padding}</c></remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="prefix">接頭辞（公開ラッパーが <see cref="Prefix"/> を渡す）。</param>
    /// <param name="padding">余白（元画像ピクセル。 整形済み）。</param>
    [LoggerMessage(LogLevel.Trace, "{Prefix:l} 本文の余白: {Padding:l}")]
    private static partial void LogAnnotationBodyPaddingChangedCore(
        this ILogger logger, string prefix, string padding);

    /// <summary>本文の余白の変更を記録する（スライダーの操作中は 1 歩ごとに届くため Trace とする）。</summary>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="padding">余白（元画像ピクセル）。</param>
    internal static void LogAnnotationBodyPaddingChanged(this ILogger logger, double padding)
    {
        if (!logger.IsEnabled(LogLevel.Trace))
        {
            return;
        }

        logger.LogAnnotationBodyPaddingChangedCore(Prefix, NumberText.Format(padding));
    }

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

    /// <summary>設定値の変更を記録する（整形済みの文字列を渡す）。</summary>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="name">変更された設定名。</param>
    /// <param name="oldValue">変更前の値（整形済み）。</param>
    /// <param name="newValue">変更後の値（整形済み）。</param>
    /// <remarks>列挙や組み合わせなど、数値・真偽で表せない設定値を記録するために UI 層からも使う。</remarks>
    public static void LogSettingChanged(this ILogger logger, string name, string oldValue, string newValue)
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

    /// <summary>真偽の設定値の変更を記録する（同値の再設定では出力しない）。</summary>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="name">変更された設定名。</param>
    /// <param name="oldValue">変更前の値。</param>
    /// <param name="newValue">変更後の値。</param>
    public static void LogSettingChanged(this ILogger logger, string name, bool oldValue, bool newValue)
    {
        if (oldValue == newValue || !logger.IsEnabled(LogLevel.Information))
        {
            return;
        }

        logger.LogSettingChangedCore(Prefix, name, oldValue.ToString(), newValue.ToString());
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
    public static void LogOperationError(this ILogger logger, Exception exception, string operation)
        => logger.LogOperationErrorCore(exception, Prefix, operation);
    /// <summary>注釈の選択の変更を記録する（生成メソッド）。</summary>
    /// <remarks>LogLevel: Debug<br/>出力書式: <c>[Annotation] 選択: 種別={Kind} 識別子={Id}</c></remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="prefix">接頭辞（公開ラッパーが <see cref="Prefix"/> を渡す）。</param>
    /// <param name="kind">選択した注釈の種別（解除は <c>none</c>）。</param>
    /// <param name="id">選択した注釈の識別子（解除は <c>none</c>）。</param>
    [LoggerMessage(LogLevel.Debug, "{Prefix:l} 選択: 種別={Kind:l} 識別子={Id:l}")]
    private static partial void LogSelectionChangedCore(this ILogger logger, string prefix, string kind, string id);

    /// <summary>注釈の選択の変更を記録する。</summary>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="kind">選択した注釈の種別。 解除は <see langword="null"/>。</param>
    /// <param name="id">選択した注釈の識別子。 解除は <see langword="null"/>。</param>
    public static void LogSelectionChanged(this ILogger logger, AnnotationKind? kind, string? id)
        => logger.LogSelectionChangedCore(Prefix, kind?.ToString() ?? "none", id ?? "none");

    /// <summary>本文の編集の開始・終了を記録する（生成メソッド）。</summary>
    /// <remarks>
    /// LogLevel: Debug<br/>出力書式:
    /// <c>[Annotation] 本文の編集: {State} キャレット={Caret} 選択=({SelectionStart}, {SelectionLength}) 本文={Preview}</c>
    /// </remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="prefix">接頭辞（公開ラッパーが <see cref="Prefix"/> を渡す）。</param>
    /// <param name="state">状態（<c>開始</c> / <c>終了</c>）。</param>
    /// <param name="caret">キャレットの位置（文字数）。</param>
    /// <param name="selectionStart">選択範囲の開始位置（文字数）。</param>
    /// <param name="selectionLength">選択範囲の長さ（文字数）。</param>
    /// <param name="preview">本文の先頭（整形済み）。</param>
    [LoggerMessage(
        LogLevel.Debug,
        "{Prefix:l} 本文の編集: {State:l} キャレット={Caret} 選択=({SelectionStart}, {SelectionLength}) 本文={Preview:l}")]
    private static partial void LogEditingChangedCore(
        this ILogger logger, string prefix, string state, int caret, int selectionStart, int selectionLength, string preview);

    /// <summary>本文の編集の開始・終了を記録する。</summary>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="editing">編集中の場合は <see langword="true"/>。</param>
    /// <param name="caret">キャレットの位置（文字数）。</param>
    /// <param name="selectionStart">選択範囲の開始位置（文字数）。</param>
    /// <param name="selectionLength">選択範囲の長さ（文字数）。</param>
    /// <param name="text">本文（先頭 100 文字だけを記録する）。</param>
    public static void LogEditingChanged(
        this ILogger logger, bool editing, int caret, int selectionStart, int selectionLength, string text)
    {
        if (!logger.IsEnabled(LogLevel.Debug))
        {
            return;
        }

        logger.LogEditingChangedCore(
            Prefix, editing ? "開始" : "終了", caret, selectionStart, selectionLength, ToPreview(text));
    }

    /// <summary>履歴の移動（元に戻す・やり直す）を記録する（生成メソッド）。</summary>
    /// <remarks>LogLevel: Debug<br/>出力書式: <c>[Annotation] 履歴: {Action} 戻せる={UndoCount} やり直せる={RedoCount}</c></remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="prefix">接頭辞（公開ラッパーが <see cref="Prefix"/> を渡す）。</param>
    /// <param name="action">移動の向き（<c>元に戻す</c> / <c>やり直す</c>）。</param>
    /// <param name="undoCount">移動後に戻せる回数。</param>
    /// <param name="redoCount">移動後にやり直せる回数。</param>
    [LoggerMessage(LogLevel.Debug, "{Prefix:l} 履歴: {Action:l} 戻せる={UndoCount} やり直せる={RedoCount}")]
    private static partial void LogHistoryMovedCore(
        this ILogger logger, string prefix, string action, int undoCount, int redoCount);

    /// <summary>履歴の移動（元に戻す・やり直す）を記録する。</summary>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="redo">やり直した場合は <see langword="true"/>。</param>
    /// <param name="undoCount">移動後に戻せる回数。</param>
    /// <param name="redoCount">移動後にやり直せる回数。</param>
    internal static void LogHistoryMoved(this ILogger logger, bool redo, int undoCount, int redoCount)
        => logger.LogHistoryMovedCore(Prefix, redo ? "やり直す" : "元に戻す", undoCount, redoCount);

    /// <summary>本文の書式の適用を記録する（生成メソッド）。</summary>
    /// <remarks>
    /// LogLevel: Debug<br/>出力書式:
    /// <c>[Annotation] 書式を適用: 開始={Start} 長さ={Length} 本文={Preview}</c>
    /// </remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="prefix">接頭辞（公開ラッパーが <see cref="Prefix"/> を渡す）。</param>
    /// <param name="start">適用する範囲の開始位置（文字数）。</param>
    /// <param name="length">適用する範囲の長さ（文字数）。</param>
    /// <param name="preview">適用した範囲の文字（整形済み）。</param>
    [LoggerMessage(LogLevel.Debug, "{Prefix:l} 書式を適用: 開始={Start} 長さ={Length} 本文={Preview:l}")]
    private static partial void LogTextStyleAppliedCore(
        this ILogger logger, string prefix, int start, int length, string preview);

    /// <summary>本文の書式の適用を記録する（適用した範囲の文字は本メソッドが切り出す）。</summary>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="text">適用後の本文全体。</param>
    /// <param name="start">適用する範囲の開始位置（文字数）。</param>
    /// <param name="length">適用する範囲の長さ（文字数。 0 以下は本文全体）。</param>
    public static void LogTextStyleApplied(this ILogger logger, string text, int start, int length)
    {
        if (!logger.IsEnabled(LogLevel.Debug))
        {
            return;
        }

        logger.LogTextStyleAppliedCore(Prefix, start, length, ToPreview(ResolveRangeText(text, start, length)));
    }

    /// <summary>書式を適用した範囲の文字を切り出す（本文全体を記録しないため）。</summary>
    /// <param name="text">適用後の本文全体。</param>
    /// <param name="start">適用する範囲の開始位置（文字数）。</param>
    /// <param name="length">適用する範囲の長さ（文字数。 0 以下は本文全体）。</param>
    /// <returns>範囲の文字。 範囲が無い場合は本文全体。</returns>
    private static string ResolveRangeText(string text, int start, int length)
    {
        if (EsUtil.Feature.StyledText.Core.Text.TextStyleEdit.ResolveRange(start, length) is not { } range)
        {
            return text;
        }

        var from = Math.Clamp(range.Start, 0, text.Length);
        var to = Math.Clamp(from + range.Length, from, text.Length);
        return text[from..to];
    }

    /// <summary>本文の変更を記録する（生成メソッド）。</summary>
    /// <remarks>
    /// LogLevel: Debug<br/>出力書式: <c>[Annotation] 本文を変更: 文字数={Length} 本文={Preview}</c>
    /// </remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="prefix">接頭辞（公開ラッパーが <see cref="Prefix"/> を渡す）。</param>
    /// <param name="length">変更後の本文の文字数。</param>
    /// <param name="preview">変更後の本文の先頭（整形済み）。</param>
    [LoggerMessage(LogLevel.Debug, "{Prefix:l} 本文を変更: 文字数={Length} 本文={Preview:l}")]
    private static partial void LogTextChangedCore(this ILogger logger, string prefix, int length, string preview);

    /// <summary>本文の変更を記録する（入力・削除・貼り付けの結果）。</summary>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="length">変更後の本文の文字数。</param>
    /// <param name="text">変更後の本文（先頭 100 文字だけを記録する）。</param>
    public static void LogTextChanged(this ILogger logger, int length, string text)
    {
        if (!logger.IsEnabled(LogLevel.Debug))
        {
            return;
        }

        logger.LogTextChangedCore(Prefix, length, ToPreview(text));
    }

    /// <summary>本文の操作（コピー・切り取り・貼り付け）を記録する（生成メソッド）。</summary>
    /// <remarks>
    /// LogLevel: Debug<br/>出力書式:
    /// <c>[Annotation] 操作: {Operation} キャレット={Caret} 選択=({SelectionStart}, {SelectionLength}) 本文={Preview}</c>
    /// </remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="prefix">接頭辞（公開ラッパーが <see cref="Prefix"/> を渡す）。</param>
    /// <param name="operation">操作の名前（<c>コピー</c> / <c>切り取り</c> / <c>貼り付け</c>）。</param>
    /// <param name="caret">操作時のキャレットの位置（文字数）。</param>
    /// <param name="selectionStart">操作時の選択範囲の開始位置（文字数）。</param>
    /// <param name="selectionLength">操作時の選択範囲の長さ（文字数）。</param>
    /// <param name="preview">操作の対象または結果の文字列（整形済み）。</param>
    [LoggerMessage(
        LogLevel.Debug,
        "{Prefix:l} 操作: {Operation:l} キャレット={Caret} 選択=({SelectionStart}, {SelectionLength}) 本文={Preview:l}")]
    private static partial void LogTextOperationCore(
        this ILogger logger, string prefix, string operation, int caret, int selectionStart, int selectionLength, string preview);

    /// <summary>本文の操作（コピー・切り取り・貼り付け）を記録する。</summary>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="operation">操作の名前（<c>コピー</c> / <c>切り取り</c> / <c>貼り付け</c>）。</param>
    /// <param name="caret">操作時のキャレットの位置（文字数）。</param>
    /// <param name="selectionStart">操作時の選択範囲の開始位置（文字数）。</param>
    /// <param name="selectionLength">操作時の選択範囲の長さ（文字数）。</param>
    /// <param name="text">操作の対象または結果の文字列（先頭 100 文字だけを記録する）。</param>
    public static void LogTextOperation(
        this ILogger logger, string operation, int caret, int selectionStart, int selectionLength, string text)
    {
        if (!logger.IsEnabled(LogLevel.Debug))
        {
            return;
        }

        logger.LogTextOperationCore(Prefix, operation, caret, selectionStart, selectionLength, ToPreview(text));
    }

    /// <summary>本文の選択範囲の変更を記録する（生成メソッド）。</summary>
    /// <remarks>
    /// LogLevel: Trace<br/>出力書式:
    /// <c>[Annotation] 本文の選択: キャレット={Caret} 選択=({SelectionStart}, {SelectionLength}) 本文={Preview}</c>
    /// </remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="prefix">接頭辞（公開ラッパーが <see cref="Prefix"/> を渡す）。</param>
    /// <param name="caret">キャレットの位置（文字数）。</param>
    /// <param name="selectionStart">選択範囲の開始位置（文字数）。</param>
    /// <param name="selectionLength">選択範囲の長さ（文字数）。</param>
    /// <param name="preview">本文の先頭（整形済み）。</param>
    [LoggerMessage(
        LogLevel.Trace,
        "{Prefix:l} 本文の選択: キャレット={Caret} 選択=({SelectionStart}, {SelectionLength}) 本文={Preview:l}")]
    private static partial void LogTextSelectionChangedCore(
        this ILogger logger, string prefix, int caret, int selectionStart, int selectionLength, string preview);

    /// <summary>本文の選択範囲の変更を記録する。</summary>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="caret">キャレットの位置（文字数）。</param>
    /// <param name="selectionStart">選択範囲の開始位置（文字数）。</param>
    /// <param name="selectionLength">選択範囲の長さ（文字数）。</param>
    /// <param name="text">本文（先頭 100 文字だけを記録する）。</param>
    public static void LogTextSelectionChanged(
        this ILogger logger, int caret, int selectionStart, int selectionLength, string text)
    {
        if (!logger.IsEnabled(LogLevel.Trace))
        {
            return;
        }

        logger.LogTextSelectionChangedCore(Prefix, caret, selectionStart, selectionLength, ToPreview(text));
    }

    /// <summary>ログに載せる文字列を整形する（改行を 1 行にし、先頭 100 文字へ切り詰める）。</summary>
    /// <param name="text">対象の文字列。</param>
    /// <returns>整形した文字列。</returns>
    /// <remarks>
    /// <b>値は大きくなりうる</b>: 本文は自由入力であり、そのまま記録すると 1 行が数百 KB になる。
    /// 障害の切り分けに要るのは先頭の内容だけであるため、100 文字で切って末尾に <c>…</c> を付ける。
    /// </remarks>
    internal static string ToPreview(string text)
    {
        const int MaxLength = 100;

        var singleLine = text.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Replace("\n", "\\n", StringComparison.Ordinal);
        return singleLine.Length <= MaxLength ? singleLine : string.Concat(singleLine.AsSpan(0, MaxLength), "…");
    }

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

    /// <summary>パネルを移動した位置を記録する（生成メソッド）。</summary>
    /// <remarks>LogLevel: Trace<br/>出力書式: <c>[Annotation] パネル移動: {Kind} 位置=({X}, {Y})</c></remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="prefix">接頭辞（公開ラッパーが <see cref="Prefix"/> を渡す）。</param>
    /// <param name="kind">パネルの種別。</param>
    /// <param name="x">左上の X（整形済み）。</param>
    /// <param name="y">左上の Y（整形済み）。</param>
    [LoggerMessage(LogLevel.Trace, "{Prefix:l} パネル移動: {Kind:l} 位置=({X:l}, {Y:l})")]
    private static partial void LogPanelMovedCore(this ILogger logger, string prefix, string kind, string x, string y);

    /// <summary>パネルを移動した位置を記録する（選択のたびに届くため Trace とする）。</summary>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="kind">パネルの種別（<c>nameof</c> で渡す）。</param>
    /// <param name="position">左上の位置（DIP）。</param>
    internal static void LogPanelMoved(this ILogger logger, string kind, EsPoint position)
    {
        if (!logger.IsEnabled(LogLevel.Trace))
        {
            return;
        }

        logger.LogPanelMovedCore(Prefix, kind, NumberText.Format(position.X), NumberText.Format(position.Y));
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
