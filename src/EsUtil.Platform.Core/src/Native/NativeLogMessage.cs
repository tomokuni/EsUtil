using Microsoft.Extensions.Logging;

namespace EsUtil.Platform.Core.Native;

/// <summary>プラットフォーム層（<c>Native/</c>）が出力する LoggerMessage 定義（ソース生成ログ）。</summary>
/// <remarks>
/// <b>本ファイルが <c>Native/</c> のログ定義の唯一の置き場所である</b>（コンポーネントごとにファイルを分けず、<br/>
/// 定義の一覧を 1 箇所で確認できるようにする）。<br/>
/// 各メソッドは <see cref="ILogger"/> の拡張メソッド（<c>this ILogger</c> 第一引数）として定義し、呼び出し側では logger 引数を省略する。<br/>
/// 接頭辞（<c>[Platform.Core] Cursor</c> / <c>[Platform.Core] WindowService</c> / <c>[Platform.Core] CaptureService</c> / <c>[Platform.Core] ElementFinder</c> / <c>[Platform.Core] GlobalHotKey</c> / <c>[Platform.Core] CursorGuard</c>）<br/>
/// で出力元を読み分ける。<br/>
/// <b>OS 操作の失敗はすべて記録する</b>: 成否を返す API は結果（<c>success</c>）を含めて出力し、<br/>
/// 失敗した場合に別の結果を返す API は失敗専用の定義（Warning / Error）を出力する。<br/>
/// ログ基盤（ファイル出力・レベル制御・非同期化）はホストアプリケーションが所有する（本クラスは定義のみ）。<br/>
/// <b>高頻度の呼び出しは記録しない</b>（カーソル位置の取得・マウス移動に伴う操作）。 列挙の内部で 1 件ごとに発生しうる<br/>
/// 失敗は Debug とし、既定の出力では溢れないようにする。
/// </remarks>
internal static partial class NativeLogMessage
{
    // ====================================================================
    // カーソル（WinCursorService）
    // ====================================================================

    /// <summary>カーソル表示/非表示の状態変更を記録する。</summary>
    /// <remarks>
    /// LogLevel: Information <br/>
    /// 表示状態が変化したときのみ呼び出される（同じ状態の再設定はスキップされる）。<br/>
    /// 出力書式: <c>[Platform.Core] Cursor: 表示状態変更: show={Show}</c>
    /// </remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="show">変更後の表示状態（true=表示, false=非表示）。</param>
    [LoggerMessage(LogLevel.Information, "[Platform.Core] Cursor: 表示状態変更: show={Show}")]
    internal static partial void LogCursorVisibilityChanged(this ILogger logger, bool show);

    /// <summary>カーソル表示状態の取得（GetCursorInfo）に失敗したことを記録する。</summary>
    /// <remarks>
    /// LogLevel: Warning <br/>
    /// 失敗時は表示中として扱う（フォールバック）。<br/>
    /// 出力書式: <c>[Platform.Core] Cursor: 表示状態の取得に失敗した</c>
    /// </remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    [LoggerMessage(LogLevel.Warning, "[Platform.Core] Cursor: 表示状態の取得に失敗した")]
    internal static partial void LogCursorVisibilityQueryFailed(this ILogger logger);

    /// <summary>カーソル位置の取得（GetCursorPos）に失敗したことを記録する。</summary>
    /// <remarks>
    /// LogLevel: Warning <br/>
    /// 出力書式: <c>[Platform.Core] Cursor: カーソル位置の取得に失敗した</c>
    /// </remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    [LoggerMessage(LogLevel.Warning, "[Platform.Core] Cursor: カーソル位置の取得に失敗した")]
    internal static partial void LogCursorPositionQueryFailed(this ILogger logger);

    /// <summary>カーソル位置の設定（SetCursorPos）に失敗したことを記録する。</summary>
    /// <remarks>
    /// LogLevel: Warning <br/>
    /// 出力書式: <c>[Platform.Core] Cursor: カーソル位置の設定に失敗した: ({X}, {Y})</c>
    /// </remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="x">設定しようとした X 座標（画面物理ピクセル）。</param>
    /// <param name="y">設定しようとした Y 座標（画面物理ピクセル）。</param>
    [LoggerMessage(LogLevel.Warning, "[Platform.Core] Cursor: カーソル位置の設定に失敗した: ({X}, {Y})")]
    internal static partial void LogCursorPositionSetFailed(this ILogger logger, int x, int y);

    // ====================================================================
    // ウィンドウ（WinWindowService）
    // ====================================================================

    /// <summary>ウィンドウをタスクバー / Alt+Tab から除外した結果を記録する。</summary>
    /// <remarks>
    /// LogLevel: Information <br/>
    /// 出力書式: <c>[Platform.Core] WindowService: HideFromTaskSwitcher: hwnd=0x{Hwnd}, success={Success}</c>
    /// </remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="hwnd">対象ウィンドウのハンドル（16進文字列）。</param>
    /// <param name="success">スタイルの書き換えに成功した場合は true。</param>
    [LoggerMessage(LogLevel.Information, "[Platform.Core] WindowService: HideFromTaskSwitcher: hwnd=0x{Hwnd}, success={Success}")]
    internal static partial void LogWindowHiddenFromTaskSwitcher(this ILogger logger, string hwnd, bool success);

    /// <summary>ウィンドウ スタイルの取得（GetWindowLongPtr）に失敗したことを記録する。</summary>
    /// <remarks>
    /// LogLevel: Warning <br/>
    /// 取得できない状態でスタイルを書き換えると既存のビットを失うため、書き換えは行わない。<br/>
    /// 出力書式: <c>[Platform.Core] WindowService: ウィンドウ スタイルの取得に失敗した: hwnd=0x{Hwnd}</c>
    /// </remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="hwnd">対象ウィンドウのハンドル（16進文字列）。</param>
    [LoggerMessage(LogLevel.Warning, "[Platform.Core] WindowService: ウィンドウ スタイルの取得に失敗した: hwnd=0x{Hwnd}")]
    internal static partial void LogWindowStyleQueryFailed(this ILogger logger, string hwnd);

    /// <summary>キャプチャ除外設定の変更結果を記録する。</summary>
    /// <remarks>
    /// LogLevel: Information <br/>
    /// 出力書式: <c>[Platform.Core] WindowService: SetCaptureExclusion: hwnd=0x{Hwnd}, exclude={Exclude}, success={Success}</c>
    /// </remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="hwnd">対象ウィンドウのハンドル（16進文字列）。</param>
    /// <param name="exclude">キャプチャ除外する場合は true。</param>
    /// <param name="success">API 呼び出しの成否。</param>
    [LoggerMessage(LogLevel.Information, "[Platform.Core] WindowService: SetCaptureExclusion: hwnd=0x{Hwnd}, exclude={Exclude}, success={Success}")]
    internal static partial void LogCaptureExclusionChanged(this ILogger logger, string hwnd, bool exclude, bool success);

    /// <summary>ウィンドウの位置・サイズを物理ピクセルで変更した結果を記録する。</summary>
    /// <remarks>
    /// LogLevel: Information <br/>
    /// 出力書式: <c>[Platform.Core] WindowService: SetBounds: hwnd=0x{Hwnd}, bounds=({X}, {Y}, {Width}, {Height}), success={Success}</c>
    /// </remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="hwnd">対象ウィンドウのハンドル（16進文字列）。</param>
    /// <param name="x">矩形の左端（画面物理ピクセル）。</param>
    /// <param name="y">矩形の上端（画面物理ピクセル）。</param>
    /// <param name="width">矩形の幅（画面物理ピクセル）。</param>
    /// <param name="height">矩形の高さ（画面物理ピクセル）。</param>
    /// <param name="success">API 呼び出しの成否。</param>
    [LoggerMessage(LogLevel.Information, "[Platform.Core] WindowService: SetBounds: hwnd=0x{Hwnd}, bounds=({X}, {Y}, {Width}, {Height}), success={Success}")]
    internal static partial void LogWindowBoundsChanged(this ILogger logger, string hwnd, int x, int y, int width, int height, bool success);

    /// <summary>UI ハング時にウィンドウを退避（最前面解除・非表示・WM_CLOSE）した結果を記録する。</summary>
    /// <remarks>
    /// LogLevel: Information <br/>
    /// <c>wasVisible</c> は非表示化の前の可視状態（<c>ShowWindow</c> の戻り値であり、失敗を意味しない）。<br/>
    /// 出力書式: <c>[Platform.Core] WindowService: EvacuateWindow: hwnd=0x{Hwnd}, unsetTopmost={UnsetTopmost}, wasVisible={WasVisible}, posted={Posted}</c>
    /// </remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="hwnd">対象ウィンドウのハンドル（16進文字列）。</param>
    /// <param name="unsetTopmost">最前面解除（SetWindowPos）に成功した場合は true。</param>
    /// <param name="wasVisible">非表示化の前に可視だった場合は true。</param>
    /// <param name="posted">WM_CLOSE の送信（PostMessage）に成功した場合は true。</param>
    [LoggerMessage(LogLevel.Information, "[Platform.Core] WindowService: EvacuateWindow: hwnd=0x{Hwnd}, unsetTopmost={UnsetTopmost}, wasVisible={WasVisible}, posted={Posted}")]
    internal static partial void LogWindowEvacuated(this ILogger logger, string hwnd, bool unsetTopmost, bool wasVisible, bool posted);

    // ====================================================================
    // キャプチャ（WinCaptureService）
    // ====================================================================

    /// <summary>仮想スクリーン境界の取得結果を記録する。</summary>
    /// <remarks>
    /// LogLevel: Debug <br/>
    /// 出力書式: <c>[Platform.Core] CaptureService: CaptureVirtualScreen: bounds=({X},{Y},{Width},{Height})</c>
    /// </remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="x">仮想スクリーン原点 X。</param>
    /// <param name="y">仮想スクリーン原点 Y。</param>
    /// <param name="width">仮想スクリーン幅。</param>
    /// <param name="height">仮想スクリーン高さ。</param>
    [LoggerMessage(LogLevel.Debug, "[Platform.Core] CaptureService: CaptureVirtualScreen: bounds=({X},{Y},{Width},{Height})")]
    internal static partial void LogVirtualScreenBounds(this ILogger logger, int x, int y, int width, int height);

    /// <summary>仮想スクリーンの境界が取得できなかった（キャプチャできない）ことを記録する。</summary>
    /// <remarks>
    /// LogLevel: Warning <br/>
    /// 出力書式: <c>[Platform.Core] CaptureService: 仮想スクリーンの境界が不正: bounds=({X},{Y},{Width},{Height})</c>
    /// </remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="x">取得した仮想スクリーン原点 X。</param>
    /// <param name="y">取得した仮想スクリーン原点 Y。</param>
    /// <param name="width">取得した仮想スクリーン幅。</param>
    /// <param name="height">取得した仮想スクリーン高さ。</param>
    [LoggerMessage(LogLevel.Warning, "[Platform.Core] CaptureService: 仮想スクリーンの境界が不正: bounds=({X},{Y},{Width},{Height})")]
    internal static partial void LogVirtualScreenBoundsInvalid(this ILogger logger, int x, int y, int width, int height);

    /// <summary>画面領域キャプチャの開始を記録する。</summary>
    /// <remarks>
    /// LogLevel: Debug <br/>
    /// 出力書式: <c>[Platform.Core] CaptureService: CaptureArea: bounds=({X}, {Y}, {Width}, {Height})</c>
    /// </remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="x">矩形の左端（画面物理ピクセル）。</param>
    /// <param name="y">矩形の上端（画面物理ピクセル）。</param>
    /// <param name="width">矩形の幅（画面物理ピクセル）。</param>
    /// <param name="height">矩形の高さ（画面物理ピクセル）。</param>
    [LoggerMessage(LogLevel.Debug, "[Platform.Core] CaptureService: CaptureArea: bounds=({X}, {Y}, {Width}, {Height})")]
    internal static partial void LogCaptureArea(this ILogger logger, int x, int y, int width, int height);

    /// <summary>画面領域キャプチャの完了を記録する。</summary>
    /// <remarks>
    /// LogLevel: Debug <br/>
    /// 出力書式: <c>[Platform.Core] CaptureService: CaptureArea: done, bitmap={Width}x{Height}</c>
    /// </remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="width">Bitmap 幅。</param>
    /// <param name="height">Bitmap 高さ。</param>
    [LoggerMessage(LogLevel.Debug, "[Platform.Core] CaptureService: CaptureArea: done, bitmap={Width}x{Height}")]
    internal static partial void LogCaptureAreaDone(this ILogger logger, int width, int height);

    /// <summary>画面の取得（CopyFromScreen）に失敗したことを記録する。</summary>
    /// <remarks>
    /// LogLevel: Error <br/>
    /// 例外はそのまま呼び出し側へ伝搬する（記録のみを行う）。<br/>
    /// 出力書式: <c>[Platform.Core] CaptureService: 画面の取得に失敗した: bounds=({X}, {Y}, {Width}, {Height})</c>
    /// </remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="x">矩形の左端（画面物理ピクセル）。</param>
    /// <param name="y">矩形の上端（画面物理ピクセル）。</param>
    /// <param name="width">矩形の幅（画面物理ピクセル）。</param>
    /// <param name="height">矩形の高さ（画面物理ピクセル）。</param>
    /// <param name="exception">発生した例外。</param>
    [LoggerMessage(LogLevel.Error, "[Platform.Core] CaptureService: 画面の取得に失敗した: bounds=({X}, {Y}, {Width}, {Height})")]
    internal static partial void LogCaptureFailed(this ILogger logger, int x, int y, int width, int height, Exception exception);

    // ====================================================================
    // 要素検出（WinElementFinder）
    // ====================================================================

    /// <summary>モニターの列挙（EnumDisplayMonitors）に失敗したことを記録する。</summary>
    /// <remarks>
    /// LogLevel: Warning <br/>
    /// 失敗時は空の一覧を返す（フォールバック）。<br/>
    /// 出力書式: <c>[Platform.Core] ElementFinder: モニターの列挙に失敗した</c>
    /// </remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    [LoggerMessage(LogLevel.Warning, "[Platform.Core] ElementFinder: モニターの列挙に失敗した")]
    internal static partial void LogMonitorEnumerationFailed(this ILogger logger);

    /// <summary>ウィンドウの列挙（EnumWindows）に失敗したことを記録する。</summary>
    /// <remarks>
    /// LogLevel: Warning <br/>
    /// 出力書式: <c>[Platform.Core] ElementFinder: ウィンドウの列挙に失敗した: filter={Filter}</c>
    /// </remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="filter">列挙の目的（<c>Find</c> = カーソル下の検出 / <c>List</c> = 一覧）。</param>
    [LoggerMessage(LogLevel.Warning, "[Platform.Core] ElementFinder: ウィンドウの列挙に失敗した: filter={Filter}")]
    internal static partial void LogWindowEnumerationFailed(this ILogger logger, string filter);

    /// <summary>矩形を取得できないウィンドウを対象から除外したことを記録する。</summary>
    /// <remarks>
    /// LogLevel: Debug <br/>
    /// 列挙の内部で 1 件ごとに起こりうるため Debug とする（既定の出力では溢れさせない）。<br/>
    /// 出力書式: <c>[Platform.Core] ElementFinder: 矩形を取得できないため除外した: hwnd=0x{Hwnd}</c>
    /// </remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="hwnd">対象ウィンドウのハンドル（16進文字列）。</param>
    [LoggerMessage(LogLevel.Debug, "[Platform.Core] ElementFinder: 矩形を取得できないため除外した: hwnd=0x{Hwnd}")]
    internal static partial void LogWindowRectUnavailable(this ILogger logger, string hwnd);

    // ====================================================================
    // グローバルホットキー（WinGlobalHotKeyService）
    // ====================================================================

    /// <summary>ホットキーの登録（RegisterHotKey）に失敗したことを記録する。</summary>
    /// <remarks>
    /// LogLevel: Warning <br/>
    /// 他のアプリが同じ組み合わせを登録済みの場合にも発生する（失敗したキーは機能しない）。<br/>
    /// 出力書式: <c>[Platform.Core] GlobalHotKey: ホットキーの登録に失敗した: id={Id}, modifiers={Modifiers}, key={Key}</c>
    /// </remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="id">ホットキーの識別子。</param>
    /// <param name="modifiers">修飾キーのフラグ。</param>
    /// <param name="key">仮想キーコード。</param>
    [LoggerMessage(LogLevel.Warning, "[Platform.Core] GlobalHotKey: ホットキーの登録に失敗した: id={Id}, modifiers={Modifiers}, key={Key}")]
    internal static partial void LogHotKeyRegisterFailed(this ILogger logger, int id, int modifiers, int key);

    /// <summary>ホットキーの解除（UnregisterHotKey）に失敗したことを記録する。</summary>
    /// <remarks>
    /// LogLevel: Warning <br/>
    /// 出力書式: <c>[Platform.Core] GlobalHotKey: ホットキーの解除に失敗した: id={Id}</c>
    /// </remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="id">ホットキーの識別子。</param>
    [LoggerMessage(LogLevel.Warning, "[Platform.Core] GlobalHotKey: ホットキーの解除に失敗した: id={Id}")]
    internal static partial void LogHotKeyUnregisterFailed(this ILogger logger, int id);

    // ====================================================================
    // ウィンドウ プロシージャの差し替え（ホットキー・カーソル上書き抑止）
    // ====================================================================

    /// <summary>ホットキー配信のためのウィンドウ プロシージャ差し替えに失敗したことを記録する。</summary>
    /// <remarks>
    /// LogLevel: Warning <br/>
    /// 差し替えに失敗した場合、ホットキーは登録できても押下を検出できない。<br/>
    /// 出力書式: <c>[Platform.Core] GlobalHotKey: ウィンドウ プロシージャの差し替えに失敗した: hwnd=0x{Hwnd}</c>
    /// </remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="hwnd">対象ウィンドウのハンドル（16進文字列）。</param>
    [LoggerMessage(LogLevel.Warning, "[Platform.Core] GlobalHotKey: ウィンドウ プロシージャの差し替えに失敗した: hwnd=0x{Hwnd}")]
    internal static partial void LogHotKeySubclassFailed(this ILogger logger, string hwnd);

    /// <summary>カーソル上書き抑止のためのウィンドウ プロシージャ差し替えに失敗したことを記録する。</summary>
    /// <remarks>
    /// LogLevel: Warning <br/>
    /// 差し替えに失敗した場合、選択操作中も OS がカーソル形状を上書きする。<br/>
    /// 出力書式: <c>[Platform.Core] CursorGuard: ウィンドウ プロシージャの差し替えに失敗した: hwnd=0x{Hwnd}</c>
    /// </remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="hwnd">対象ウィンドウのハンドル（16進文字列）。</param>
    [LoggerMessage(LogLevel.Warning, "[Platform.Core] CursorGuard: ウィンドウ プロシージャの差し替えに失敗した: hwnd=0x{Hwnd}")]
    internal static partial void LogCursorGuardSubclassFailed(this ILogger logger, string hwnd);
}
