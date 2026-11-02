using EsUtil.Platform.Core.Common;
using EsUtil.Platform.Core.Coord;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Imaging;
using Microsoft.Extensions.Logging;
using DrawingColor = System.Drawing.Color;

namespace EsUtil.Feature.Loupe.Logging;

/// <summary>ルーペの LoggerMessage 定義（ソース生成ログ。 コアと提示層で共有する）。</summary>
/// <remarks>
/// 設定値の変更・拡大中心の変更・外観の変更は、<b>全 UI で同一のレベル・書式にする必要がある</b>ため<br/>
/// アセンブリに 1 つだけ置く（UI 実装ごとに持つと書式がずれ、片方だけ修正される）。<br/>
/// 値の文字列化（<c>(X, Y)</c>）も本クラスが行い、UI 側に整形処理を置かない。<br/>
/// 「変化したときだけ出力する」判断（同値の再設定）も本クラスが単一所有する（UI 層に同値判定を置かない）。<br/>
/// <b>出力する操作</b>: 生成（ロガーの設定時に記録）・設定の変更（Information）・低頻度のメソッド呼び出し（Debug）・<br/>
/// マウス移動ごとに変わる拡大中心（Trace。 頻度が高いため既定の出力では溢れさせない）。<br/>
/// 毎フレーム呼ばれる解決メソッド（<c>ResolveLayout</c> / <c>ResolveGeometry</c>）は出力しない。<br/>
/// ログ基盤（ファイル出力・レベル制御）はホストアプリケーションが所有する（本クラスは定義のみ）。<br/>
/// 各メソッドは <see cref="ILogger"/> の拡張メソッド（<c>this ILogger</c> 第一引数）として定義し、呼び出し側では logger 引数を省略する。<br/>
/// <b>可視性</b>: 値を所有する層だけが呼ぶため <c>internal</c> とする（ホストはロガーを設定するだけである）。<br/>
/// <b>UI が出力する定義は各 UI が持つ</b>（拡大元画像の変更・描画の例外）。
/// </remarks>
internal static partial class LoupeLogMessage
{
    /// <summary>未設定を表す文字列。</summary>
    private const string Null = "null";

    /// <summary>座標変換ポートの設定名（ログの項目名）。</summary>
    private const string CoordTransformName = "CoordTransform";

    /// <summary>接頭辞の既定値（ホストが設定しない場合に使う）。</summary>
    internal const string DefaultPrefix = "[Loupe]";

    /// <summary>適用する接頭辞（ホストが <c>LogPrefix</c> で設定する）。</summary>
    internal static string Prefix { get; set; } = DefaultPrefix;

    /// <summary>公開ファサードの操作で例外が発生したことを記録する（生成メソッド）。</summary>
    /// <remarks>LogLevel: Error<br/>出力書式: <c>[Loupe] {Operation:l} で例外が発生</c>（例外スタックトレース付き）</remarks>
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
    /// <remarks>公開ファサードが例外を集約して記録し、上位へ伝搬するために使う（[`例外処理規約.md`] §4）。</remarks>
    internal static void LogOperationError(this ILogger logger, Exception exception, string operation)
        => logger.LogOperationErrorCore(exception, Prefix, operation);

    /// <summary>ファサードの生成（生成時の設定）を記録する。</summary>
    /// <remarks>
    /// LogLevel: Information<br/>
    /// 出力書式: <c>[Loupe] Facade created: RequestedSize={RequestedSize}, ZoomLevel={ZoomLevel}</c><br/>
    /// <b>コンストラクタでロガーが注入された場合はその時点で出力し</b>、注入されなかった場合はロガーが初めて<br/>
    /// 設定された時点で 1 度だけ出力する（生成時点では出力先が未設定のため、その場では記録できない。
    /// どちらの経路でも生成時の設定を 1 度だけ記録し、どの設定で生成されたファサードからのログかを追えるようにする）。
    /// </remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="prefix">接頭辞（公開ラッパーが <see cref="Prefix"/> を渡す）。</param>
    /// <param name="requestedSize">生成時の要求サイズ（DIP）。</param>
    /// <param name="zoomLevel">生成時の拡大率。</param>
    [LoggerMessage(LogLevel.Information, "{Prefix:l} Facade created: RequestedSize={RequestedSize}, ZoomLevel={ZoomLevel}")]
    private static partial void LogFacadeCreatedCore(this ILogger logger, string prefix, int requestedSize, int zoomLevel);

    /// <summary>ファサードの生成（生成時の設定）を記録する。</summary>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="requestedSize">生成時の要求サイズ（DIP）。</param>
    /// <param name="zoomLevel">生成時の拡大率。</param>
    internal static void LogFacadeCreated(this ILogger logger, int requestedSize, int zoomLevel)
        => logger.LogFacadeCreatedCore(Prefix, requestedSize, zoomLevel);

    /// <summary>提示層の生成を記録する（生成メソッド）。</summary>
    /// <remarks>LogLevel: Information<br/>出力書式: <c>[Loupe] Presenter created</c></remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="prefix">接頭辞（公開ラッパーが <see cref="Prefix"/> を渡す）。</param>
    [LoggerMessage(LogLevel.Information, "{Prefix:l} Presenter created")]
    private static partial void LogPresenterCreatedCore(this ILogger logger, string prefix);

    /// <summary>提示層の生成を記録する。</summary>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    internal static void LogPresenterCreated(this ILogger logger)
        => logger.LogPresenterCreatedCore(Prefix);

    /// <summary>出力先（ロガー）の設定を記録する。</summary>
    /// <remarks>
    /// LogLevel: Debug<br/>出力書式: <c>[Loupe] Logger attached</c><br/>
    /// ログの出力を開始した契機を残す（以降の設定変更のログがどの時点から有効かを追えるようにする）。
    /// </remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="prefix">接頭辞（公開ラッパーが <see cref="Prefix"/> を渡す）。</param>
    [LoggerMessage(LogLevel.Debug, "{Prefix:l} Logger attached")]
    private static partial void LogLoggerAttachedCore(this ILogger logger, string prefix);

    /// <summary>出力先（ロガー）の設定を記録する。</summary>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    internal static void LogLoggerAttached(this ILogger logger) => logger.LogLoggerAttachedCore(Prefix);

    /// <summary>設定値変更のログ（変更前後）。</summary>
    /// <remarks>LogLevel: Information<br/>出力書式: <c>[Loupe] Setting: {Name:l} {OldValue:l} → {NewValue:l}</c></remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="prefix">接頭辞（公開ラッパーが <see cref="Prefix"/> を渡す）。</param>
    /// <param name="name">設定名。</param>
    /// <param name="oldValue">変更前の値（整形済み）。</param>
    /// <param name="newValue">変更後の値（整形済み）。</param>
    [LoggerMessage(LogLevel.Information, "{Prefix:l} Setting: {Name:l} {OldValue:l} → {NewValue:l}")]
    private static partial void LogSettingChangedCore(this ILogger logger, string prefix, string name, string oldValue, string newValue);

    /// <summary>設定値変更のログ（変更前後。 整形済みの値を渡す）。</summary>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="name">設定名。</param>
    /// <param name="oldValue">変更前の値（整形済み）。</param>
    /// <param name="newValue">変更後の値（整形済み）。</param>
    internal static void LogSettingChanged(this ILogger logger, string name, string oldValue, string newValue)
        => logger.LogSettingChangedCore(Prefix, name, oldValue, newValue);

    /// <summary>頻度の高い設定値変更のログ（変更前後。 最も低いレベルで出力する）。</summary>
    /// <remarks>
    /// LogLevel: Trace（書式は <see cref="LogSettingChanged(ILogger, string, string, string)"/> と同一）<br/>
    /// マウス移動ごとに変わる値（拡大中心）が使う（既定の出力では溢れさせない）。
    /// LogLevel: Trace<br/>
    /// 出力書式: <c>[Loupe] Setting: {Name:l} {OldValue:l} → {NewValue:l}</c>
    /// </remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="prefix">接頭辞（公開ラッパーが <see cref="Prefix"/> を渡す）。</param>
    /// <param name="name">設定名。</param>
    /// <param name="oldValue">変更前の値（整形済み）。</param>
    /// <param name="newValue">変更後の値（整形済み）。</param>
    [LoggerMessage(LogLevel.Trace, "{Prefix:l} Setting: {Name:l} {OldValue:l} → {NewValue:l}")]
    private static partial void LogSettingChangedTraceCore(this ILogger logger, string prefix, string name, string oldValue, string newValue);

    /// <summary>頻度の高い設定値変更のログ（変更前後。 最も低いレベルで出力する）。</summary>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="name">設定名。</param>
    /// <param name="oldValue">変更前の値（整形済み）。</param>
    /// <param name="newValue">変更後の値（整形済み）。</param>
    internal static void LogSettingChangedTrace(this ILogger logger, string name, string oldValue, string newValue)
        => logger.LogSettingChangedTraceCore(Prefix, name, oldValue, newValue);

    /// <summary>整数設定値の変更ログ（同値の再設定では出力しない）。</summary>
    /// <remarks>
    /// <see cref="LogSettingChanged(ILogger, string, string, string)"/> と同じ書式で出力する。<br/>
    /// 「変化したときだけ出力する」判断と数値の文字列化は <see cref="LogSettingChangedIfChanged"/> が単一所有する<br/>
    /// （外観設定の再適用でログが溢れるのを防ぐ規則）。
    /// </remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="name">設定名。</param>
    /// <param name="oldValue">変更前の値。</param>
    /// <param name="newValue">変更後の値。</param>
    internal static void LogSettingChanged(this ILogger logger, string name, int oldValue, int newValue)
        => logger.LogSettingChangedIfChanged(name, oldValue, newValue);

    /// <summary>実数設定値の変更ログ（同値の再設定では出力しない）。</summary>
    /// <remarks><see cref="LogSettingChanged(ILogger, string, int, int)"/> の実数版（書式・規則とも同一）。</remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="name">設定名。</param>
    /// <param name="oldValue">変更前の値。</param>
    /// <param name="newValue">変更後の値。</param>
    internal static void LogSettingChanged(this ILogger logger, string name, double oldValue, double newValue)
        => logger.LogSettingChangedIfChanged(name, oldValue, newValue);

    /// <summary>真偽設定値の変更ログ（同値の再設定では出力しない）。</summary>
    /// <remarks>
    /// <see cref="LogSettingChanged(ILogger, string, int, int)"/> の真偽版。<br/>
    /// <c>bool</c> は <see cref="IFormattable"/> を実装しないため本体の汎用版へは委譲できない（書式は型の標準表記）。
    /// </remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="name">設定名。</param>
    /// <param name="oldValue">変更前の値。</param>
    /// <param name="newValue">変更後の値。</param>
    internal static void LogSettingChanged(this ILogger logger, string name, bool oldValue, bool newValue)
    {
        if (oldValue == newValue)
        {
            return;
        }

        logger.LogSettingChanged(name, oldValue.ToString(), newValue.ToString());
    }

    /// <summary>色設定値の変更ログ（同値の再設定では出力しない）。</summary>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="name">設定名。</param>
    /// <param name="oldValue">変更前の色。</param>
    /// <param name="newValue">変更後の色。</param>
    /// <remarks>
    /// 同値の判定は成分（<see cref="DrawingColor.ToArgb"/>）で行う。 色の <c>==</c> は生成経路（名前付き色か<br/>
    /// ARGB 生成か）の違いでも不一致になるため、同じ色に「変更なし」のログが出てしまうのを防ぐ。
    /// </remarks>
    internal static void LogSettingChanged(this ILogger logger, string name, DrawingColor oldValue, DrawingColor newValue)
    {
        if (oldValue.ToArgb() == newValue.ToArgb())
        {
            return;
        }

        logger.LogSettingChanged(name, oldValue.ToHexText(), newValue.ToHexText());
    }

    /// <summary>外観（<see cref="LoupeAppearance"/>）の変更ログ（変化した項目だけを出力する）。</summary>
    /// <remarks>
    /// LogLevel: Information（書式は個別の変更ログと同一）<br/>
    /// 外観を 1 つの値へ統合しているため、変更された項目の判定（差分検出）を本メソッドが単一所有する<br/>
    /// （ホストは前後の値を渡さず、値を保持する提示層が変化を知るだけでよい）。<br/>
    /// 外観の再適用で項目数ぶんのログが溢れないよう、各項目のログが同値判定を行う（変化した項目だけが出力される）。
    /// </remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="oldValue">変更前の外観。</param>
    /// <param name="newValue">変更後の外観。</param>
    internal static void LogAppearanceChanged(this ILogger logger, LoupeAppearance oldValue, LoupeAppearance newValue)
    {
        logger.LogSettingChanged(nameof(LoupeAppearance.PasteboardColor), oldValue.PasteboardColor, newValue.PasteboardColor);
        logger.LogSettingChanged(nameof(LoupeAppearance.BorderColor), oldValue.BorderColor, newValue.BorderColor);
        logger.LogSettingChanged(nameof(LoupeAppearance.CrossColor), oldValue.CrossColor, newValue.CrossColor);
        logger.LogSettingChanged(nameof(LoupeAppearance.CoordLabelColor), oldValue.CoordLabelColor, newValue.CoordLabelColor);
        logger.LogSettingChanged(
            nameof(LoupeAppearance.CoordLabelBackgroundColor), oldValue.CoordLabelBackgroundColor, newValue.CoordLabelBackgroundColor);
        logger.LogSettingChanged(nameof(LoupeAppearance.CoordLabelFontSize), oldValue.CoordLabelFontSize, newValue.CoordLabelFontSize);
        logger.LogSettingChanged(
            nameof(LoupeAppearance.CoordLabelCornerRadius), oldValue.CoordLabelCornerRadius, newValue.CoordLabelCornerRadius);
        logger.LogSettingChanged(nameof(LoupeAppearance.CoordLabelOffset), oldValue.CoordLabelOffset, newValue.CoordLabelOffset);
        logger.LogSettingChanged(nameof(LoupeAppearance.ShowCrosshair), oldValue.ShowCrosshair, newValue.ShowCrosshair);
        logger.LogSettingChanged(nameof(LoupeAppearance.ShowBorder), oldValue.ShowBorder, newValue.ShowBorder);
        logger.LogSettingChanged(nameof(LoupeAppearance.BorderThickness), oldValue.BorderThickness, newValue.BorderThickness);
        logger.LogSettingChanged(nameof(LoupeAppearance.ShowCoordLabel), oldValue.ShowCoordLabel, newValue.ShowCoordLabel);
    }
    /// <summary>数値設定値の変更ログ（同値の再設定では出力しない）の本体。</summary>
    /// <typeparam name="T">数値の型（整数・実数）。</typeparam>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="name">設定名。</param>
    /// <param name="oldValue">変更前の値。</param>
    /// <param name="newValue">変更後の値。</param>
    /// <remarks>
    /// 「同値なら出力しない」判定と、カルチャに依存しない数値の文字列化を単一所有する<br/>
    /// （整数版・実数版が同じ規則を別々に持つと、片方だけ非カルチャ非依存のまま残る）。
    /// </remarks>
    private static void LogSettingChangedIfChanged<T>(this ILogger logger, string name, T oldValue, T newValue)
        where T : struct, IEquatable<T>, IFormattable
    {
        if (oldValue.Equals(newValue))
        {
            return;
        }

        logger.LogSettingChanged(
            name,
            NumberText.Format(oldValue),
            NumberText.Format(newValue));
    }

    /// <summary>拡大中心（対象座標）の変更ログ（同値の再設定では出力しない）。</summary>
    /// <remarks>
    /// LogLevel: Trace<br/>出力書式: <c>[Loupe] Setting: CenterPoint {OldValue} → {NewValue}</c><br/>
    /// 中心はマウス移動のたびに設定される（最も頻度が高い）ため、<b>最も低いレベル（Trace）</b>で記録する<br/>
    /// （既定の出力では溢れさせない。 必要時にレベルを下げて追う）。<br/>
    /// 「変化したときだけ出力する」判断を本メソッドが単一所有する（出力をコアが行うため、UI 層は同値判定を持たない）。
    /// </remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="oldValue">変更前の拡大中心（未設定は null）。</param>
    /// <param name="newValue">変更後の拡大中心（未設定は null）。</param>
    internal static void LogCenterPointChanged(this ILogger logger, EsPoint? oldValue, EsPoint? newValue)
    {
        if (oldValue == newValue)
        {
            return;
        }

        logger.LogSettingChangedTrace("CenterPoint", FormatPoint(oldValue), FormatPoint(newValue));
    }

    /// <summary>座標変換ポートの設定変更を記録する（同値の再設定では出力しない）。</summary>
    /// <remarks>
    /// LogLevel: Information<br/>出力書式: <c>[Loupe] Setting: CoordTransform {OldValue} → {NewValue}</c><br/>
    /// 値は未設定（<c>null</c> = 等価変換）またはポートの実装型名で表す（ポートは値として比較・整形できないため）。
    /// </remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="oldValue">変更前の座標変換ポート（未設定は null）。</param>
    /// <param name="newValue">変更後の座標変換ポート（未設定は null）。</param>
    internal static void LogCoordTransformChanged(this ILogger logger, ICoordTransform? oldValue, ICoordTransform? newValue)
    {
        if (ReferenceEquals(oldValue, newValue))
        {
            return;
        }

        logger.LogSettingChanged(CoordTransformName, FormatPort(oldValue), FormatPort(newValue));
    }

    /// <summary>設定（<see cref="LoupeOptions"/>）の変更ログ（変化した項目だけを出力する）。</summary>
    /// <remarks>
    /// LogLevel: Information（書式は個別の変更ログと同一）<br/>
    /// 設定を 1 つの値へ統合したため、変更された項目の判定（差分検出）を本メソッドが単一所有する<br/>
    /// （UI 層は前後の値を渡さず、コアが保持する値の変化を知るだけでよい）。<br/>
    /// 外観の再適用で設定数ぶんのログが溢れないよう、各項目のログが同値判定を行う（変化した項目だけが出力される）。
    /// </remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="oldValue">変更前の設定。</param>
    /// <param name="newValue">変更後の設定。</param>
    internal static void LogOptionsChanged(this ILogger logger, LoupeOptions oldValue, LoupeOptions newValue)
    {
        logger.LogSettingChanged(nameof(LoupeOptions.RequestedSize), oldValue.RequestedSize, newValue.RequestedSize);
        logger.LogSettingChanged(nameof(LoupeOptions.ZoomLevel), oldValue.ZoomLevel, newValue.ZoomLevel);
    }

    /// <summary>拡大中心（対象座標）を文字列化する。</summary>
    /// <param name="point">拡大中心。 未設定は null。</param>
    /// <returns><c>(X, Y)</c> 形式の文字列。 未設定は <c>null</c>。</returns>
    /// <remarks>
    /// 座標は対象座標（元画像ピクセル）のため、そのままの値を表示する（丸めない）。<br/>
    /// 数値の書式は現在のカルチャに依存しない（ログを環境非依存にする）。
    /// </remarks>
    private static string FormatPoint(EsPoint? point)
        => point is { } p
            ? $"({NumberText.Format(p.X)}, {NumberText.Format(p.Y)})"
            : Null;

    /// <summary>座標変換ポートを文字列化する。</summary>
    /// <param name="port">座標変換ポート。 未設定（null = 等価変換）は null。</param>
    /// <returns>未設定は <c>null</c>、設定済みはポートの実装型名。</returns>
    /// <remarks>ポートは値として比較・整形できないため、設定された実装の種別のみを記録する。</remarks>
    private static string FormatPort(ICoordTransform? port) => port?.GetType().Name ?? Null;
}
