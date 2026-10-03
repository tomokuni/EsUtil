using System.Globalization;
using EsUtil.Platform.Core.Coord;
using EsUtil.Platform.Core.Geometry;
using Microsoft.Extensions.Logging;

namespace EsUtil.Feature.Loupe.Core.Logging;

/// <summary>ルーペのコアが出力する LoggerMessage 定義（ソース生成ログ）。</summary>
/// <remarks>
/// 設定値の変更・拡大中心の変更は、<b>両 UI で同一のレベル・書式にする必要がある</b>ため<br/>
/// コアが単一所有する（UI 実装ごとに持つと書式がずれ、片方だけ修正される）。<br/>
/// 値の文字列化（<c>(X, Y)</c>）も本クラスが行い、UI 側に整形処理を置かない。<br/>
/// 「変化したときだけ出力する」判断（同値の再設定）も本クラスが単一所有する（UI 層に同値判定を置かない）。<br/>
/// <b>出力する操作</b>: 生成（ロガーの設定時に記録）・低頻度の設定変更（Debug）・低頻度のメソッド呼び出し（Debug）・<br/>
/// マウス移動ごとに変わる拡大中心（Trace。 頻度が高いため既定の出力では溢れさせない）。<br/>
/// 毎フレーム呼ばれる解決メソッド（<c>ResolveLayout</c> / <c>ResolveGeometry</c>）は出力しない。<br/>
/// ログ基盤（ファイル出力・レベル制御）はホストアプリケーションが所有する（本クラスは定義のみ）。<br/>
/// 各メソッドは <see cref="ILogger"/> の拡張メソッド（<c>this ILogger</c> 第一引数）として定義し、呼び出し側では logger 引数を省略する。<br/>
/// <b>可視性</b>: コアが出力する定義だけを持つため <c>internal</c> とする<br/>
/// （設定・環境値・拡大中心の変更はコアが検出して出力するため、UI から呼ぶ必要がない）。<br/>
/// <b>UI が出力する定義はコントロール化の層が持つ</b>（拡大元画像の変更・描画の例外）。
/// </remarks>
internal static partial class LoupeLogMessage
{
    /// <summary>未設定を表す文字列。</summary>
    private const string Null = "null";

    /// <summary>座標変換ポートの設定名（ログの項目名）。</summary>
    private const string CoordTransformName = "CoordTransform";

    /// <summary>ファサードの生成（生成時の設定）を記録する。</summary>
    /// <remarks>
    /// LogLevel: Information<br/>
    /// 出力書式: <c>[Loupe] Facade created: RequestedSize={RequestedSize}, ZoomLevel={ZoomLevel}</c><br/>
    /// <b>コンストラクタでロガーが注入された場合はその時点で出力し</b>、注入されなかった場合はロガーが初めて<br/>
    /// 設定された時点で 1 度だけ出力する（生成時点では出力先が未設定のため、その場では記録できない。
    /// どちらの経路でも生成時の設定を 1 度だけ記録し、どの設定で生成されたファサードからのログかを追えるようにする）。
    /// </remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="requestedSize">生成時の要求サイズ（DIP）。</param>
    /// <param name="zoomLevel">生成時の拡大率。</param>
    [LoggerMessage(LogLevel.Information, "[Loupe] Facade created: RequestedSize={RequestedSize}, ZoomLevel={ZoomLevel}")]
    internal static partial void LogFacadeCreated(this ILogger logger, int requestedSize, int zoomLevel);

    /// <summary>出力先（ロガー）の設定を記録する。</summary>
    /// <remarks>
    /// LogLevel: Debug<br/>出力書式: <c>[Loupe] Logger attached</c><br/>
    /// ログの出力を開始した契機を残す（以降の設定変更のログがどの時点から有効かを追えるようにする）。
    /// </remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    [LoggerMessage(LogLevel.Debug, "[Loupe] Logger attached")]
    internal static partial void LogLoggerAttached(this ILogger logger);

    /// <summary>設定値変更のログ（変更前後）。</summary>
    /// <remarks>LogLevel: Debug<br/>出力書式: <c>[Loupe] Setting: {Name} {OldValue} → {NewValue}</c></remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="name">設定名。</param>
    /// <param name="oldValue">変更前の値（整形済み）。</param>
    /// <param name="newValue">変更後の値（整形済み）。</param>
    [LoggerMessage(LogLevel.Debug, "[Loupe] Setting: {Name} {OldValue} → {NewValue}")]
    internal static partial void LogSettingChanged(this ILogger logger, string name, string oldValue, string newValue);

    /// <summary>頻度の高い設定値変更のログ（変更前後。 最も低いレベルで出力する）。</summary>
    /// <remarks>
    /// LogLevel: Trace（書式は <see cref="LogSettingChanged(ILogger, string, string, string)"/> と同一）<br/>
    /// マウス移動ごとに変わる値（拡大中心）が使う（既定の出力では溢れさせない）。
    /// </remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="name">設定名。</param>
    /// <param name="oldValue">変更前の値（整形済み）。</param>
    /// <param name="newValue">変更後の値（整形済み）。</param>
    [LoggerMessage(LogLevel.Trace, "[Loupe] Setting: {Name} {OldValue} → {NewValue}")]
    internal static partial void LogSettingChangedTrace(this ILogger logger, string name, string oldValue, string newValue);

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
            oldValue.ToString(format: null, CultureInfo.InvariantCulture),
            newValue.ToString(format: null, CultureInfo.InvariantCulture));
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
    /// LogLevel: Debug<br/>出力書式: <c>[Loupe] Setting: CoordTransform {OldValue} → {NewValue}</c><br/>
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
    /// LogLevel: Debug（書式は個別の変更ログと同一）<br/>
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
            ? $"({p.X.ToString(CultureInfo.InvariantCulture)}, {p.Y.ToString(CultureInfo.InvariantCulture)})"
            : Null;

    /// <summary>座標変換ポートを文字列化する。</summary>
    /// <param name="port">座標変換ポート。 未設定（null = 等価変換）は null。</param>
    /// <returns>未設定は <c>null</c>、設定済みはポートの実装型名。</returns>
    /// <remarks>ポートは値として比較・整形できないため、設定された実装の種別のみを記録する。</remarks>
    private static string FormatPort(ICoordTransform? port) => port?.GetType().Name ?? Null;
}
