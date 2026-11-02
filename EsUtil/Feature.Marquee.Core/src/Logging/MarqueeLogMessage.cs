using EsUtil.Platform.Core.Common;
using EsUtil.Platform.Core.Coord;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Imaging;
using Microsoft.Extensions.Logging;
using DrawingColor = System.Drawing.Color;

namespace EsUtil.Feature.Marquee.Logging;

/// <summary>マーキーの LoggerMessage 定義（ソース生成ログ。 コアと提示層で共有する）。</summary>
/// <remarks>
/// 入力に伴う判断（ドラッグ開始・確定・キャンセル・クリック・キー処理）のログ定義も本クラスが持つ。<br/>
/// <b>ホイール要求のログは本層が所有しない</b>（ズーム・スクロールはコントロール化のための要求であり、<br/>
/// ホスト（UI 層）が持つ。 本クラスは入力判断のログだけを持つ）。<br/>
/// ログ基盤（ファイル出力・レベル制御）はホストアプリケーションが所有する（本クラスは定義のみ）。<br/>
/// <b>出力の方針</b>: 生成（ロガーの設定時に記録）・設定の変更（Information）・低頻度の入力の判断（Debug）・<br/>
/// マウス移動ごとに変わるハイライト矩形（Trace。 頻度が高いため既定の出力では溢れさせない）とする。<br/>
/// 毎フレーム呼ばれる解決メソッド（<c>CreateRequest</c> / <c>ResolveGeometry</c>）と高頻度の入力通知（<c>Move</c>）は出力しない。<br/>
/// <b>可視性</b>: 本定義を呼ぶのはコアと提示層だけであり、UI 層・ホストはロガー（<see cref="MarqueeCore.Log"/>）を渡すだけでよいため internal とする。
/// </remarks>
internal static partial class MarqueeLogMessage
{
    /// <summary>未設定を表す文字列。</summary>
    private const string Null = "null";

    /// <summary>座標変換ポートの設定名（ログの項目名）。</summary>
    private const string CoordTransformName = "CoordTransform";

    /// <summary>接頭辞の既定値（ホストが設定しない場合に使う）。</summary>
    internal const string DefaultPrefix = "[Marquee]";

    /// <summary>適用する接頭辞（ホストが <c>LogPrefix</c> で設定する）。</summary>
    internal static string Prefix { get; set; } = DefaultPrefix;

    /// <summary>公開ファサードの操作で例外が発生したことを記録する（生成メソッド）。</summary>
    /// <remarks>LogLevel: Error<br/>出力書式: <c>[Marquee] {Operation:l} で例外が発生</c>（例外スタックトレース付き）</remarks>
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
    /// 出力書式: <c>[Marquee] Facade created: HandleSize={HandleSize}, MinSelectionSize={MinSelectionSize}</c><br/>
    /// <b>コンストラクタでロガーが注入された場合はその時点で出力し</b>、注入されなかった場合はロガーが初めて<br/>
    /// 設定された時点で 1 度だけ出力する（生成時点では出力先が未設定のため、その場では記録できない。 どちらの経路でも<br/>
    /// 生成時の設定を 1 度だけ記録し、どの設定で生成されたファサードからのログかを追えるようにする）。
    /// </remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="prefix">接頭辞（公開ラッパーが <see cref="Prefix"/> を渡す）。</param>
    /// <param name="handleSize">生成時のハンドルの一辺サイズ（DIP）。</param>
    /// <param name="minSelectionSize">生成時の選択矩形の最小サイズ（対象座標）。</param>
    [LoggerMessage(LogLevel.Information, "{Prefix:l} Facade created: HandleSize={HandleSize}, MinSelectionSize={MinSelectionSize}")]
    private static partial void LogFacadeCreatedCore(this ILogger logger, string prefix, string handleSize, string minSelectionSize);

    internal static void LogFacadeCreated(this ILogger logger, double handleSize, double minSelectionSize)
        => logger.LogFacadeCreatedCore(Prefix, NumberText.Format(handleSize), NumberText.Format(minSelectionSize));

    /// <summary>提示層の生成を記録する（生成メソッド）。</summary>
    /// <remarks>LogLevel: Information<br/>出力書式: <c>[Marquee] Presenter created</c></remarks>
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
    /// LogLevel: Debug<br/>出力書式: <c>[Marquee] Logger attached</c><br/>
    /// ログの出力を開始した契機を残す（以降の設定変更のログがどの時点から有効かを追えるようにする）。
    /// </remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="prefix">接頭辞（公開ラッパーが <see cref="Prefix"/> を渡す）。</param>
    [LoggerMessage(LogLevel.Debug, "{Prefix:l} Logger attached")]
    private static partial void LogLoggerAttachedCore(this ILogger logger, string prefix);

    internal static void LogLoggerAttached(this ILogger logger)
        => logger.LogLoggerAttachedCore(Prefix);

    /// <summary>ポインター押下（マウス Down）のログ。</summary>
    /// <remarks>LogLevel: Debug<br/>出力書式: <c>[Marquee] MouseDown: pos=({X}, {Y}) clickCount={ClickCount} hasSelection={HasSelection}</c></remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="prefix">接頭辞（公開ラッパーが <see cref="Prefix"/> を渡す）。</param>
    /// <param name="x">対象座標 X。</param>
    /// <param name="y">対象座標 Y。</param>
    /// <param name="clickCount">クリック数（2 = ダブルクリック）。</param>
    /// <param name="hasSelection">選択矩形の有無。</param>
    [LoggerMessage(LogLevel.Debug, "{Prefix:l} MouseDown: pos=({X}, {Y}) clickCount={ClickCount} hasSelection={HasSelection}")]
    private static partial void LogMouseDownCore(this ILogger logger, string prefix, string x, string y, int clickCount, bool hasSelection);

    internal static void LogMouseDown(this ILogger logger, double x, double y, int clickCount, bool hasSelection)
        => logger.LogMouseDownCore(Prefix, NumberText.Format(x), NumberText.Format(y), clickCount, hasSelection);

    /// <summary>ポインター解放（マウス Up）のログ。</summary>
    /// <remarks>LogLevel: Debug<br/>出力書式: <c>[Marquee] MouseUp: pos=({X}, {Y}) wasDragging={WasDragging} hasSelection={HasSelection}</c></remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="prefix">接頭辞（公開ラッパーが <see cref="Prefix"/> を渡す）。</param>
    /// <param name="x">対象座標 X。</param>
    /// <param name="y">対象座標 Y。</param>
    /// <param name="wasDragging">ドラッグ中だったかどうか。</param>
    /// <param name="hasSelection">選択矩形の有無。</param>
    /// <param name="rect">マウスを離した時点の選択矩形（対象座標。 未選択は <c>null</c>）。</param>
    [LoggerMessage(LogLevel.Debug, "{Prefix:l} MouseUp: pos=({X}, {Y}) wasDragging={WasDragging} hasSelection={HasSelection} rect={Rect}")]
    private static partial void LogMouseUpCore(this ILogger logger, string prefix, string x, string y, bool wasDragging, bool hasSelection, string rect);

    internal static void LogMouseUp(this ILogger logger, double x, double y, bool wasDragging, bool hasSelection, EsRect? rect)
        => logger.LogMouseUpCore(Prefix, NumberText.Format(x), NumberText.Format(y), wasDragging, hasSelection, FormatRect(rect));

    /// <summary>ダブルクリックのログ。</summary>
    /// <remarks>LogLevel: Debug<br/>出力書式: <c>[Marquee] DoubleClick: pos=({X}, {Y}) inside={Inside}</c></remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="prefix">接頭辞（公開ラッパーが <see cref="Prefix"/> を渡す）。</param>
    /// <param name="x">対象座標 X。</param>
    /// <param name="y">対象座標 Y。</param>
    /// <param name="inside">選択矩形の内側かどうか。</param>
    [LoggerMessage(LogLevel.Debug, "{Prefix:l} DoubleClick: pos=({X}, {Y}) inside={Inside}")]
    private static partial void LogDoubleClickCore(this ILogger logger, string prefix, string x, string y, bool inside);

    internal static void LogDoubleClick(this ILogger logger, double x, double y, bool inside)
        => logger.LogDoubleClickCore(Prefix, NumberText.Format(x), NumberText.Format(y), inside);

    /// <summary>マウス押下直前の位置のログ（移動の高頻度ログは行わない）。</summary>
    /// <remarks>LogLevel: Trace<br/>出力書式: <c>[Marquee] Position before MouseDown: pos=({X}, {Y})</c></remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="prefix">接頭辞（公開ラッパーが <see cref="Prefix"/> を渡す）。</param>
    /// <param name="x">対象座標 X。</param>
    /// <param name="y">対象座標 Y。</param>
    [LoggerMessage(LogLevel.Trace, "{Prefix:l} Position before MouseDown: pos=({X}, {Y})")]
    private static partial void LogPositionBeforeDownCore(this ILogger logger, string prefix, string x, string y);

    internal static void LogPositionBeforeDown(this ILogger logger, double x, double y)
        => logger.LogPositionBeforeDownCore(Prefix, NumberText.Format(x), NumberText.Format(y));

    /// <summary>マウス解放直後の位置のログ（移動の高頻度ログは行わない）。</summary>
    /// <remarks>LogLevel: Trace<br/>出力書式: <c>[Marquee] Position after MouseUp: pos=({X}, {Y})</c></remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="prefix">接頭辞（公開ラッパーが <see cref="Prefix"/> を渡す）。</param>
    /// <param name="x">対象座標 X。</param>
    /// <param name="y">対象座標 Y。</param>
    [LoggerMessage(LogLevel.Trace, "{Prefix:l} Position after MouseUp: pos=({X}, {Y})")]
    private static partial void LogPositionAfterUpCore(this ILogger logger, string prefix, string x, string y);

    internal static void LogPositionAfterUp(this ILogger logger, double x, double y)
        => logger.LogPositionAfterUpCore(Prefix, NumberText.Format(x), NumberText.Format(y));

    /// <summary>マウスドラッグ開始（新規選択・ハンドル操作）のログ。</summary>
    /// <remarks>LogLevel: Debug<br/>出力書式: <c>[Marquee] DragStart: handle={Handle} pos=({X}, {Y})</c></remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="prefix">接頭辞（公開ラッパーが <see cref="Prefix"/> を渡す）。</param>
    /// <param name="handle">操作対象のハンドル（None = 新規選択）。</param>
    /// <param name="x">対象座標 X。</param>
    /// <param name="y">対象座標 Y。</param>
    [LoggerMessage(LogLevel.Debug, "{Prefix:l} DragStart: handle={Handle} pos=({X}, {Y})")]
    private static partial void LogDragStartCore(this ILogger logger, string prefix, string handle, string x, string y);

    internal static void LogDragStart(this ILogger logger, string handle, double x, double y)
        => logger.LogDragStartCore(Prefix, handle, NumberText.Format(x), NumberText.Format(y));

    /// <summary>キー入力（矢印キー nudge・Enter 確定・Escape キャンセル）のログ。</summary>
    /// <remarks>LogLevel: Debug<br/>出力書式: <c>[Marquee] KeyDown: key={Key} handled={Handled}</c></remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="prefix">接頭辞（公開ラッパーが <see cref="Prefix"/> を渡す）。</param>
    /// <param name="key">押下されたキー。</param>
    /// <param name="handled">処理されたかどうか。</param>
    [LoggerMessage(LogLevel.Debug, "{Prefix:l} KeyDown: key={Key} handled={Handled}")]
    private static partial void LogKeyDownCore(this ILogger logger, string prefix, string key, bool handled);

    internal static void LogKeyDown(this ILogger logger, string key, bool handled)
        => logger.LogKeyDownCore(Prefix, key, handled);

    /// <summary>確定要求（Enter・ダブルクリック・API 呼び出し）のログ。</summary>
    /// <remarks>LogLevel: Debug<br/>出力書式: <c>[Marquee] ConfirmRequested: rect={Rect}</c></remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="prefix">接頭辞（公開ラッパーが <see cref="Prefix"/> を渡す）。</param>
    /// <param name="rect">確定する選択矩形（整形済み）。</param>
    [LoggerMessage(LogLevel.Debug, "{Prefix:l} ConfirmRequested: rect={Rect}")]
    private static partial void LogConfirmRequestedCore(this ILogger logger, string prefix, string rect);

    internal static void LogConfirmRequested(this ILogger logger, string rect)
        => logger.LogConfirmRequestedCore(Prefix, rect);

    /// <summary>キャンセル要求（Escape・枠外ダブルクリック・API 呼び出し）のログ。</summary>
    /// <remarks>LogLevel: Debug<br/>出力書式: <c>[Marquee] CancelRequested</c></remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="prefix">接頭辞（公開ラッパーが <see cref="Prefix"/> を渡す）。</param>
    [LoggerMessage(LogLevel.Debug, "{Prefix:l} CancelRequested")]
    private static partial void LogCancelRequestedCore(this ILogger logger, string prefix);

    internal static void LogCancelRequested(this ILogger logger)
        => logger.LogCancelRequestedCore(Prefix);

    /// <summary>設定値変更のログ（変更前後）。</summary>
    /// <remarks>LogLevel: Information<br/>出力書式: <c>[Marquee] Setting: {Name:l} {OldValue:l} → {NewValue:l}</c></remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="prefix">接頭辞（公開ラッパーが <see cref="Prefix"/> を渡す）。</param>
    /// <param name="name">設定名。</param>
    /// <param name="oldValue">変更前の値。</param>
    /// <param name="newValue">変更後の値。</param>
    [LoggerMessage(LogLevel.Information, "{Prefix:l} Setting: {Name:l} {OldValue:l} → {NewValue:l}")]
    private static partial void LogSettingChangedCore(this ILogger logger, string prefix, string name, string oldValue, string newValue);

    internal static void LogSettingChanged(this ILogger logger, string name, string oldValue, string newValue)
        => logger.LogSettingChangedCore(Prefix, name, oldValue, newValue);

    /// <summary>頻度の高い設定値変更のログ（変更前後。 最も低いレベルで出力する）。</summary>
    /// <remarks>
    /// LogLevel: Trace（書式は <see cref="LogSettingChanged(ILogger, string, string, string)"/> と同一）<br/>
    /// マウス移動（ホバー検出）のたびに変わるハイライト矩形が使う（既定の出力では溢れさせない）。
    /// LogLevel: Trace<br/>
    /// 出力書式: <c>[Marquee] Setting: {Name:l} {OldValue:l} → {NewValue:l}</c>
    /// </remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="prefix">接頭辞（公開ラッパーが <see cref="Prefix"/> を渡す）。</param>
    /// <param name="name">設定名。</param>
    /// <param name="oldValue">変更前の値（整形済み）。</param>
    /// <param name="newValue">変更後の値（整形済み）。</param>
    [LoggerMessage(LogLevel.Trace, "{Prefix:l} Setting: {Name:l} {OldValue:l} → {NewValue:l}")]
    private static partial void LogSettingChangedTraceCore(this ILogger logger, string prefix, string name, string oldValue, string newValue);

    internal static void LogSettingChangedTrace(this ILogger logger, string name, string oldValue, string newValue)
        => logger.LogSettingChangedTraceCore(Prefix, name, oldValue, newValue);

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
    /// LogLevel: Information<br/>出力書式: <c>[Marquee] Setting: CoordTransform {OldValue} → {NewValue}</c><br/>
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

    /// <summary>外観（<see cref="MarqueeAppearance"/>）の変更ログ（変化した項目だけを出力する）。</summary>
    /// <remarks>
    /// LogLevel: Information（書式は個別の変更ログと同一）<br/>
    /// 外観を 1 つの値へ統合しているため、変更された項目の判定（差分検出）を本メソッドが単一所有する<br/>
    /// （ホストは前後の値を渡さず、値を保持する提示層が変化を知るだけでよい）。<br/>
    /// 外観の再適用で項目数ぶんのログが溢れないよう、各項目のログが同値判定を行う（変化した項目だけが出力される）。
    /// </remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="oldValue">変更前の外観。</param>
    /// <param name="newValue">変更後の外観。</param>
    internal static void LogAppearanceChanged(this ILogger logger, MarqueeAppearance oldValue, MarqueeAppearance newValue)
    {
        logger.LogSettingChanged(nameof(MarqueeAppearance.HandleColor), oldValue.HandleColor, newValue.HandleColor);
        logger.LogSettingChanged(nameof(MarqueeAppearance.HandleBorderColor), oldValue.HandleBorderColor, newValue.HandleBorderColor);
        logger.LogSettingChanged(nameof(MarqueeAppearance.MarkerColor), oldValue.MarkerColor, newValue.MarkerColor);
        logger.LogSettingChanged(nameof(MarqueeAppearance.MarkerThickness), oldValue.MarkerThickness, newValue.MarkerThickness);
        logger.LogSettingChanged(nameof(MarqueeAppearance.CoordLabelOffset), oldValue.CoordLabelOffset, newValue.CoordLabelOffset);
        logger.LogSettingChanged(nameof(MarqueeAppearance.SizeLabelColor), oldValue.SizeLabelColor, newValue.SizeLabelColor);
        logger.LogSettingChanged(
            nameof(MarqueeAppearance.SizeLabelBackgroundColor), oldValue.SizeLabelBackgroundColor, newValue.SizeLabelBackgroundColor);
        logger.LogSettingChanged(nameof(MarqueeAppearance.CoordLabelColor), oldValue.CoordLabelColor, newValue.CoordLabelColor);
        logger.LogSettingChanged(
            nameof(MarqueeAppearance.CoordLabelBackgroundColor), oldValue.CoordLabelBackgroundColor, newValue.CoordLabelBackgroundColor);
        logger.LogSettingChanged(nameof(MarqueeAppearance.OuterMaskColor), oldValue.OuterMaskColor, newValue.OuterMaskColor);
        logger.LogSettingChanged(nameof(MarqueeAppearance.BorderColor), oldValue.BorderColor, newValue.BorderColor);
        logger.LogSettingChanged(nameof(MarqueeAppearance.InnerMaskColor), oldValue.InnerMaskColor, newValue.InnerMaskColor);
        logger.LogSettingChanged(nameof(MarqueeAppearance.SizeLabelFontSize), oldValue.SizeLabelFontSize, newValue.SizeLabelFontSize);
        logger.LogSettingChanged(nameof(MarqueeAppearance.CoordLabelFontSize), oldValue.CoordLabelFontSize, newValue.CoordLabelFontSize);
        logger.LogSettingChanged(nameof(MarqueeAppearance.LabelPadding), oldValue.LabelPadding, newValue.LabelPadding);
        logger.LogSettingChanged(nameof(MarqueeAppearance.ShowHandles), oldValue.ShowHandles, newValue.ShowHandles);
        logger.LogSettingChanged(nameof(MarqueeAppearance.ShowMarker), oldValue.ShowMarker, newValue.ShowMarker);
        logger.LogSettingChanged(nameof(MarqueeAppearance.ShowSizeLabel), oldValue.ShowSizeLabel, newValue.ShowSizeLabel);
        logger.LogSettingChanged(nameof(MarqueeAppearance.ShowCoordLabel), oldValue.ShowCoordLabel, newValue.ShowCoordLabel);
        logger.LogSettingChanged(nameof(MarqueeAppearance.ShowOuterMask), oldValue.ShowOuterMask, newValue.ShowOuterMask);
        logger.LogSettingChanged(nameof(MarqueeAppearance.ShowBorder), oldValue.ShowBorder, newValue.ShowBorder);
        logger.LogSettingChanged(nameof(MarqueeAppearance.ShowInnerMask), oldValue.ShowInnerMask, newValue.ShowInnerMask);
        logger.LogSettingChanged(
            nameof(MarqueeAppearance.BorderThickness), oldValue.BorderThickness, newValue.BorderThickness);
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
    /// LogLevel: Information（項目ごとの書式は個別の変更ログと同一）<br/>
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

    /// <summary>カーソルの移動に失敗したログ（表示上の追従のみが失敗した状態）。</summary>
    /// <remarks>
    /// LogLevel: Warning<br/>
    /// 出力書式: <c>[Marquee] Cursor move failed: source=({X}, {Y})</c><br/>
    /// <b>値で返された失敗を記録する唯一の場所</b>: カーソル移動は表示上の追従であり、
    /// 失敗しても選択操作は続行する（コアは失敗を記録せず、値として呼び出し側へ返す）。
    /// </remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="sourcePosition">移動先（対象座標）。</param>
    internal static void LogCursorMoveFailed(this ILogger logger, EsPoint sourcePosition)
        => logger.LogCursorMoveFailedCore(Prefix, NumberText.Format(sourcePosition.X), NumberText.Format(sourcePosition.Y));

    /// <summary>カーソルの移動に失敗したログ（接頭辞を引数で受ける）。</summary>
    /// <remarks>LogLevel: Warning<br/>出力書式: <c>[Marquee] Cursor move failed: source=({X}, {Y})</c></remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="prefix">接頭辞（公開ラッパーが <see cref="Prefix"/> を渡す）。</param>
    /// <param name="x">移動先の X 座標（対象座標）。</param>
    /// <param name="y">移動先の Y 座標（対象座標）。</param>
    [LoggerMessage(LogLevel.Warning, "{Prefix:l} Cursor move failed: source=({X}, {Y})")]
    private static partial void LogCursorMoveFailedCore(this ILogger logger, string prefix, string x, string y);

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
        => $"({NumberText.Format(rect.X)}, {NumberText.Format(rect.Y)}, {NumberText.Format(rect.Width)}, {NumberText.Format(rect.Height)})";

    /// <summary>座標変換ポートを文字列へ整形する。</summary>
    /// <param name="port">整形する座標変換ポート。 未設定は null。</param>
    /// <returns>ポートの実装型名。 未設定は <c>null</c>。</returns>
    /// <remarks>ポートは値として比較・整形できないため、設定された実装の種別のみを記録する。</remarks>
    private static string FormatPort(ICoordTransform? port) => port?.GetType().Name ?? Null;
    /// <summary>ハイライト中で入力を無視したことを記録する（生成メソッド）。</summary>
    /// <remarks>LogLevel: Trace<br/>出力書式: <c>[Marquee] Input ignored: {Operation} (highlight active)</c></remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="prefix">接頭辞（公開ラッパーが <see cref="Prefix"/> を渡す）。</param>
    /// <param name="operation">無視した操作の名前。</param>
    [LoggerMessage(LogLevel.Trace, "{Prefix:l} Input ignored: {Operation:l} (highlight active)")]
    private static partial void LogInputIgnoredCore(this ILogger logger, string prefix, string operation);

    /// <summary>ハイライト中で入力を無視したことを記録する。</summary>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="operation">無視した操作の名前。</param>
    /// <remarks>
    /// ハイライト中は選択を編集しないため、ポインターの移動ごとに出力される（高頻度。 Trace）。<br/>
    /// 入力が届かないことを確かめるのに役立つ（不具合の分別に使う）。
    /// </remarks>
    internal static void LogInputIgnored(this ILogger logger, string operation)
        => logger.LogInputIgnoredCore(Prefix, operation);
}
