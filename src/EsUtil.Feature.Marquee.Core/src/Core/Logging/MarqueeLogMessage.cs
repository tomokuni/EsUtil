using System.Globalization;
using EsUtil.Platform.Core.Coord;
using EsUtil.Platform.Core.Geometry;
using Microsoft.Extensions.Logging;

namespace EsUtil.Feature.Marquee.Core.Logging;

/// <summary>Marquee の操作（入力の判断・設定変更）が出力する LoggerMessage 定義（ソース生成ログ）。</summary>
/// <remarks>
/// 入力に伴う判断（ドラッグ開始・確定・キャンセル・クリック・キー処理）はコアの状態管理クラスが担うため、<br/>
/// その経過のログ定義もコアが所有する。<br/>
/// <b>ホイール要求のログは本層が所有しない</b>（ズーム・スクロールはコントロール化のための要求であり、<br/>
/// ホスト（UI 層）が持つ。 本クラスは入力判断のログだけを持つ）。<br/>
/// ログ基盤（ファイル出力・レベル制御）はホストアプリケーションが所有する（本クラスは定義のみ）。<br/>
/// <b>出力の方針</b>: 生成（ロガーの設定時に記録）・低頻度の設定変更（Debug）・低頻度のメソッド呼び出し（Debug）・<br/>
/// マウス移動ごとに変わるハイライト矩形（Trace。 頻度が高いため既定の出力では溢れさせない）とする。<br/>
/// 毎フレーム呼ばれる解決メソッド（<c>CreateRequest</c> / <c>ResolveGeometry</c>）と高頻度の入力通知（<c>Move</c>）は出力しない。<br/>
/// <b>可視性</b>: 本定義を呼ぶのはコアの状態機械とファサードだけであり、UI 層・ホストはロガー（<see cref="MarqueeCore.Log"/>）を渡すだけでよいため internal とする。
/// </remarks>
internal static partial class MarqueeLogMessage
{
    /// <summary>未設定を表す文字列。</summary>
    private const string Null = "null";

    /// <summary>座標変換ポートの設定名（ログの項目名）。</summary>
    private const string CoordTransformName = "CoordTransform";

    /// <summary>ファサードの生成（生成時の設定）を記録する。</summary>
    /// <remarks>
    /// LogLevel: Information<br/>
    /// 出力書式: <c>[Marquee] Facade created: HandleSize={HandleSize}, MinSelectionSize={MinSelectionSize}</c><br/>
    /// <b>コンストラクタでロガーが注入された場合はその時点で出力し</b>、注入されなかった場合はロガーが初めて<br/>
    /// 設定された時点で 1 度だけ出力する（生成時点では出力先が未設定のため、その場では記録できない。 どちらの経路でも<br/>
    /// 生成時の設定を 1 度だけ記録し、どの設定で生成されたファサードからのログかを追えるようにする）。
    /// </remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="handleSize">生成時のハンドルの一辺サイズ（DIP）。</param>
    /// <param name="minSelectionSize">生成時の選択矩形の最小サイズ（対象座標）。</param>
    [LoggerMessage(LogLevel.Information, "[Marquee] Facade created: HandleSize={HandleSize}, MinSelectionSize={MinSelectionSize}")]
    internal static partial void LogFacadeCreated(this ILogger logger, double handleSize, double minSelectionSize);

    /// <summary>出力先（ロガー）の設定を記録する。</summary>
    /// <remarks>
    /// LogLevel: Debug<br/>出力書式: <c>[Marquee] Logger attached</c><br/>
    /// ログの出力を開始した契機を残す（以降の設定変更のログがどの時点から有効かを追えるようにする）。
    /// </remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    [LoggerMessage(LogLevel.Debug, "[Marquee] Logger attached")]
    internal static partial void LogLoggerAttached(this ILogger logger);

    /// <summary>ポインター押下（マウス Down）のログ。</summary>
    /// <remarks>LogLevel: Debug<br/>出力書式: <c>[Marquee] MouseDown: pos=({X}, {Y}) clickCount={ClickCount} hasSelection={HasSelection}</c></remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="x">対象座標 X。</param>
    /// <param name="y">対象座標 Y。</param>
    /// <param name="clickCount">クリック数（2 = ダブルクリック）。</param>
    /// <param name="hasSelection">選択矩形の有無。</param>
    [LoggerMessage(LogLevel.Debug, "[Marquee] MouseDown: pos=({X}, {Y}) clickCount={ClickCount} hasSelection={HasSelection}")]
    internal static partial void LogMouseDown(this ILogger logger, double x, double y, int clickCount, bool hasSelection);

    /// <summary>ポインター解放（マウス Up）のログ。</summary>
    /// <remarks>LogLevel: Debug<br/>出力書式: <c>[Marquee] MouseUp: pos=({X}, {Y}) wasDragging={WasDragging} hasSelection={HasSelection}</c></remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="x">対象座標 X。</param>
    /// <param name="y">対象座標 Y。</param>
    /// <param name="wasDragging">ドラッグ中だったかどうか。</param>
    /// <param name="hasSelection">選択矩形の有無。</param>
    [LoggerMessage(LogLevel.Debug, "[Marquee] MouseUp: pos=({X}, {Y}) wasDragging={WasDragging} hasSelection={HasSelection}")]
    internal static partial void LogMouseUp(this ILogger logger, double x, double y, bool wasDragging, bool hasSelection);

    /// <summary>ダブルクリックのログ。</summary>
    /// <remarks>LogLevel: Debug<br/>出力書式: <c>[Marquee] DoubleClick: pos=({X}, {Y}) inside={Inside}</c></remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="x">対象座標 X。</param>
    /// <param name="y">対象座標 Y。</param>
    /// <param name="inside">選択矩形の内側かどうか。</param>
    [LoggerMessage(LogLevel.Debug, "[Marquee] DoubleClick: pos=({X}, {Y}) inside={Inside}")]
    internal static partial void LogDoubleClick(this ILogger logger, double x, double y, bool inside);

    /// <summary>マウス押下直前の位置のログ（移動の高頻度ログは行わない）。</summary>
    /// <remarks>LogLevel: Trace<br/>出力書式: <c>[Marquee] Position before MouseDown: pos=({X}, {Y})</c></remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="x">対象座標 X。</param>
    /// <param name="y">対象座標 Y。</param>
    [LoggerMessage(LogLevel.Trace, "[Marquee] Position before MouseDown: pos=({X}, {Y})")]
    internal static partial void LogPositionBeforeDown(this ILogger logger, double x, double y);

    /// <summary>マウス解放直後の位置のログ（移動の高頻度ログは行わない）。</summary>
    /// <remarks>LogLevel: Trace<br/>出力書式: <c>[Marquee] Position after MouseUp: pos=({X}, {Y})</c></remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="x">対象座標 X。</param>
    /// <param name="y">対象座標 Y。</param>
    [LoggerMessage(LogLevel.Trace, "[Marquee] Position after MouseUp: pos=({X}, {Y})")]
    internal static partial void LogPositionAfterUp(this ILogger logger, double x, double y);

    /// <summary>マウスドラッグ開始（新規選択・ハンドル操作）のログ。</summary>
    /// <remarks>LogLevel: Debug<br/>出力書式: <c>[Marquee] DragStart: handle={Handle} pos=({X}, {Y})</c></remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="handle">操作対象のハンドル（None = 新規選択）。</param>
    /// <param name="x">対象座標 X。</param>
    /// <param name="y">対象座標 Y。</param>
    [LoggerMessage(LogLevel.Debug, "[Marquee] DragStart: handle={Handle} pos=({X}, {Y})")]
    internal static partial void LogDragStart(this ILogger logger, string handle, double x, double y);

    /// <summary>キー入力（矢印キー nudge・Enter 確定・Escape キャンセル）のログ。</summary>
    /// <remarks>LogLevel: Debug<br/>出力書式: <c>[Marquee] KeyDown: key={Key} handled={Handled}</c></remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="key">押下されたキー。</param>
    /// <param name="handled">処理されたかどうか。</param>
    [LoggerMessage(LogLevel.Debug, "[Marquee] KeyDown: key={Key} handled={Handled}")]
    internal static partial void LogKeyDown(this ILogger logger, string key, bool handled);

    /// <summary>確定要求（Enter・ダブルクリック・API 呼び出し）のログ。</summary>
    /// <remarks>LogLevel: Debug<br/>出力書式: <c>[Marquee] ConfirmRequested: rect={Rect}</c></remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="rect">確定する選択矩形（整形済み）。</param>
    [LoggerMessage(LogLevel.Debug, "[Marquee] ConfirmRequested: rect={Rect}")]
    internal static partial void LogConfirmRequested(this ILogger logger, string rect);

    /// <summary>キャンセル要求（Escape・枠外ダブルクリック・API 呼び出し）のログ。</summary>
    /// <remarks>LogLevel: Debug<br/>出力書式: <c>[Marquee] CancelRequested</c></remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    [LoggerMessage(LogLevel.Debug, "[Marquee] CancelRequested")]
    internal static partial void LogCancelRequested(this ILogger logger);

    /// <summary>設定値変更のログ（変更前後）。</summary>
    /// <remarks>LogLevel: Debug<br/>出力書式: <c>[Marquee] Setting: {Name} {OldValue} → {NewValue}</c></remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="name">設定名。</param>
    /// <param name="oldValue">変更前の値。</param>
    /// <param name="newValue">変更後の値。</param>
    [LoggerMessage(LogLevel.Debug, "[Marquee] Setting: {Name} {OldValue} → {NewValue}")]
    internal static partial void LogSettingChanged(this ILogger logger, string name, string oldValue, string newValue);

    /// <summary>頻度の高い設定値変更のログ（変更前後。 最も低いレベルで出力する）。</summary>
    /// <remarks>
    /// LogLevel: Trace（書式は <see cref="LogSettingChanged(ILogger, string, string, string)"/> と同一）<br/>
    /// マウス移動（ホバー検出）のたびに変わるハイライト矩形が使う（既定の出力では溢れさせない）。
    /// LogLevel: Trace<br/>
    /// 出力書式: <c>[Marquee] Setting: {Name} {OldValue} → {NewValue}</c>
    /// </remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="name">設定名。</param>
    /// <param name="oldValue">変更前の値（整形済み）。</param>
    /// <param name="newValue">変更後の値（整形済み）。</param>
    [LoggerMessage(LogLevel.Trace, "[Marquee] Setting: {Name} {OldValue} → {NewValue}")]
    internal static partial void LogSettingChangedTrace(this ILogger logger, string name, string oldValue, string newValue);

    /// <summary>ハイライト矩形（頻度の高い設定値）の変更ログ（同値の再設定では出力しない）。</summary>
    /// <remarks>
    /// LogLevel: Trace<br/>出力書式: <c>[Marquee] Setting: HighlightSourceRect {OldValue} → {NewValue}</c><br/>
    /// ハイライト矩形はホストがカーソル下の要素の検出結果として与える値であり、<b>最も頻度が高い</b>（ホバーのたびに変わりうる）ため<br/>
    /// 最も低いレベル（Trace）で記録する（既定の出力では溢れさせない。 必要時にレベルを下げて追う）。<br/>
    /// 「変化したときだけ出力する」判断を本メソッドが単一所有する（出力をコアが行うため、UI 層は同値判定を持たない）。
    /// </remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="oldValue">変更前のハイライト矩形（未指定は null）。</param>
    /// <param name="newValue">変更後のハイライト矩形（未指定は null）。</param>
    internal static void LogHighlightSourceRectChanged(this ILogger logger, EsRect? oldValue, EsRect? newValue)
    {
        if (oldValue == newValue)
        {
            return;
        }

        logger.LogSettingChangedTrace(nameof(MarqueeCore.HighlightSourceRect), FormatRect(oldValue), FormatRect(newValue));
    }

    /// <summary>座標変換ポートの設定変更を記録する（同値の再設定では出力しない）。</summary>
    /// <remarks>
    /// LogLevel: Debug<br/>出力書式: <c>[Marquee] Setting: CoordTransform {OldValue} → {NewValue}</c><br/>
    /// 値は未設定（<c>null</c> = 等価変換）またはポートの実装型名で表す（ポートは値として比較・整形できないため）。<br/>
    /// 低頻度のメソッド呼び出しのため Debug で記録する。
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

    /// <summary>整数設定値の変更ログ（同値の再設定では出力しない）。</summary>
    /// <remarks>
    /// <see cref="LogSettingChanged(ILogger, string, string, string)"/> と同じ書式で出力する。<br/>
    /// 「変化したときだけ出力する」判断と数値の文字列化は <see cref="LogSettingChangedIfChanged"/> が単一所有する。
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

    /// <summary>確定要求（Enter・ダブルクリック・API 呼び出し）のログ（矩形の整形を含む）。</summary>
    /// <remarks>
    /// <see cref="LogConfirmRequested(ILogger, string)"/> と同じ書式で出力する。<br/>
    /// 矩形の整形（<c>(X, Y, W, H)</c>）は <see cref="FormatRect(EsRect)"/> が単一所有する
    /// （整形を呼び出し側へ置くと、同じ矩形が経路によって別の書式で出力される）。
    /// </remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="rect">確定する選択矩形。</param>
    internal static void LogConfirmRequested(this ILogger logger, EsRect rect)
        => logger.LogConfirmRequested(FormatRect(rect));

    /// <summary>設定（<see cref="MarqueeOptions"/>）の変更ログ（変化した項目だけを出力する）。</summary>
    /// <remarks>
    /// LogLevel: Debug（項目ごとの書式は個別の変更ログと同一）<br/>
    /// 設定を 1 つの値へ統合したため、変更された項目の判定（差分検出）を本メソッドが単一所有する<br/>
    /// （UI 層は前後の値を渡さず、コアが保持する値の変化を知るだけでよい）。<br/>
    /// 外観の再適用で設定数ぶんのログが溢れないよう、各項目のログが同値判定を行う（変化した項目だけが出力される）。
    /// </remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="oldValue">変更前の設定。</param>
    /// <param name="newValue">変更後の設定。</param>
    internal static void LogOptionsChanged(this ILogger logger, MarqueeOptions oldValue, MarqueeOptions newValue)
    {
        logger.LogSettingChanged(nameof(MarqueeOptions.HandleSize), oldValue.HandleSize, newValue.HandleSize);
        logger.LogSettingChanged(
            nameof(MarqueeOptions.HandleBorderThickness), oldValue.HandleBorderThickness, newValue.HandleBorderThickness);
        logger.LogSettingChanged(nameof(MarqueeOptions.MarkerLength), oldValue.MarkerLength, newValue.MarkerLength);
        logger.LogSettingChanged(nameof(MarqueeOptions.HandleHitRadius), oldValue.HandleHitRadius, newValue.HandleHitRadius);
        logger.LogSettingChanged(nameof(MarqueeOptions.MinSelectionSize), oldValue.MinSelectionSize, newValue.MinSelectionSize);
        logger.LogSettingChanged(nameof(MarqueeOptions.ClampToSourceBounds), oldValue.ClampToSourceBounds, newValue.ClampToSourceBounds);
    }

    /// <summary>矩形を <c>(X, Y, W, H)</c> 形式の文字列へ整形する。</summary>
    /// <param name="rect">整形する矩形。 未指定は null。</param>
    /// <returns><c>(X, Y, W, H)</c> 形式の文字列。 未指定は <c>null</c>。</returns>
    /// <remarks>数値の書式は現在のカルチャに依存しない（ログを環境非依存にする）。</remarks>
    private static string FormatRect(EsRect? rect)
        => rect is { } value ? FormatRect(value) : Null;

    /// <summary>矩形を <c>(X, Y, W, H)</c> 形式の文字列へ整形する。</summary>
    /// <param name="rect">整形する矩形。</param>
    /// <returns><c>(X, Y, W, H)</c> 形式の文字列。</returns>
    /// <remarks>数値の書式は現在のカルチャに依存しない（ログを環境非依存にする）。</remarks>
    private static string FormatRect(EsRect rect)
        => $"({rect.X.ToString(CultureInfo.InvariantCulture)}, {rect.Y.ToString(CultureInfo.InvariantCulture)}, {rect.Width.ToString(CultureInfo.InvariantCulture)}, {rect.Height.ToString(CultureInfo.InvariantCulture)})";

    /// <summary>座標変換ポートを文字列へ整形する。</summary>
    /// <param name="port">整形する座標変換ポート。 未設定は null。</param>
    /// <returns>ポートの実装型名。 未設定は <c>null</c>。</returns>
    /// <remarks>ポートは値として比較・整形できないため、設定された実装の種別のみを記録する。</remarks>
    private static string FormatPort(ICoordTransform? port) => port?.GetType().Name ?? Null;
}
