using EsUtil.Platform.Core.Imaging;
using Microsoft.Extensions.Logging;

namespace EsUtil.Ui.AvaloniaUI.Loupe.Logging;

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
    /// 「別のインスタンスに差し替わったか」の判断（参照の同一性）を本メソッドが単一所有する。<br/>
    /// 寸法はコアの画像（<see cref="EsReadOnlyBitmap"/>）が破棄済みでも取得できる（幾何は破棄で失われない）。
    /// </remarks>
    internal static void LogSourceChanged(this ILogger logger, EsReadOnlyBitmap? oldValue, EsReadOnlyBitmap? newValue)
    {
        if (ReferenceEquals(oldValue, newValue))
        {
            return;
        }

        logger.LogSettingChanged("Source", Describe(oldValue), Describe(newValue));
    }

    /// <summary>設定変更のログ（生成メソッド）。</summary>
    /// <remarks>LogLevel: Information<br/>出力書式: <c>[Loupe] Setting: {Name:l} {OldValue:l} → {NewValue:l}</c></remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="prefix">接頭辞（公開ラッパーが <see cref="Prefix"/> を渡す）。</param>
    /// <param name="name">設定名。</param>
    /// <param name="oldValue">変更前の値（整形済み）。</param>
    /// <param name="newValue">変更後の値（整形済み）。</param>
    [LoggerMessage(LogLevel.Information, "{Prefix:l} Setting: {Name:l} {OldValue:l} → {NewValue:l}")]
    private static partial void LogSettingChangedCore(this ILogger logger, string prefix, string name, string oldValue, string newValue);

    /// <summary>設定変更のログ（整形済みの値を渡す）。</summary>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="name">設定名。</param>
    /// <param name="oldValue">変更前の値（整形済み）。</param>
    /// <param name="newValue">変更後の値（整形済み）。</param>
    internal static void LogSettingChanged(this ILogger logger, string name, string oldValue, string newValue)
        => logger.LogSettingChangedCore(Prefix, name, oldValue, newValue);

    /// <summary>表示の寸法（割り当て・面・倍率）を記録する。</summary>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="displayWidth">割り当てられた幅（DIP）。</param>
    /// <param name="displayHeight">割り当てられた高さ（DIP）。</param>
    /// <param name="pixelSize">描画に使う一辺（物理ピクセル）。</param>
    /// <param name="scaling">表示 DIP → 物理ピクセルの倍率。</param>
    /// <remarks>
    /// LogLevel: Trace<br/>
    /// 出力書式: <c>[Loupe] Geometry: display={DisplayWidth}x{DisplayHeight} (DIP) / pixel={PixelSize} / scaling={Scaling}</c><br/>
    /// 接頭辞は既定値が <c>[Loupe]</c> であり、<c>Prefix</c> で設定できる。<br/>
    /// <b>割り当て（DIP）と面（物理ピクセル）の対応を 1 行で残す</b>ため、DPI の解釈がずれたときに<br/>
    /// ログだけで原因を特定できる（UI 層が違っても同じ書式で残す。 全 UI で同じ意味の値が出る）。
    /// </remarks>
    internal static void LogGeometry(this ILogger logger, double displayWidth, double displayHeight, int pixelSize, double scaling)
        => logger.LogGeometryCore(Prefix, displayWidth, displayHeight, pixelSize, scaling);

    /// <summary>表示の寸法を記録する（生成メソッド）。</summary>
    /// <remarks>
    /// LogLevel: Trace<br/>
    /// 出力書式: <c>[Loupe] Geometry: display={DisplayWidth}x{DisplayHeight} (DIP) / pixel={PixelSize} / scaling={Scaling}</c><br/>
    /// </remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="prefix">接頭辞（公開ラッパーが <see cref="Prefix"/> を渡す）。</param>
    /// <param name="displayWidth">割り当てられた幅（DIP）。</param>
    /// <param name="displayHeight">割り当てられた高さ（DIP）。</param>
    /// <param name="pixelSize">描画に使う一辺（物理ピクセル）。</param>
    /// <param name="scaling">表示 DIP → 物理ピクセルの倍率。</param>
    [LoggerMessage(LogLevel.Trace, "{Prefix:l} Geometry: display={DisplayWidth}x{DisplayHeight} (DIP) / pixel={PixelSize} / scaling={Scaling}")]
    private static partial void LogGeometryCore(this ILogger logger, string prefix, double displayWidth, double displayHeight, int pixelSize, double scaling);
    /// <summary>ルーペ描画の例外エラーログ（生成メソッド）。</summary>
    /// <remarks>LogLevel: Error<br/>出力書式: <c>[Loupe] 描画の更新で例外が発生</c>（例外スタックトレース付き）</remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="prefix">接頭辞（公開ラッパーが <see cref="Prefix"/> を渡す）。</param>
    /// <param name="exception">発生した例外。</param>
    [LoggerMessage(LogLevel.Error, "{Prefix:l} 描画の更新で例外が発生")]
    private static partial void LogUpdateErrorCore(this ILogger logger, Exception exception, string prefix);

    /// <summary>ルーペ描画の例外エラーログ。</summary>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="exception">発生した例外。</param>
    /// <remarks>例外の記録は描画を実行する UI が行う（コアは描画 API を持たず例外を検知しない）。</remarks>
    internal static void LogUpdateError(this ILogger logger, Exception exception)
        => logger.LogUpdateErrorCore(exception, Prefix);

    /// <summary>ソース画像の寸法をログ用の文字列（<c>幅x高さ</c>）へ整形する。</summary>
    /// <param name="source">対象のソース画像。 null は「画像なし」。</param>
    /// <returns><c>幅x高さ</c> 形式の文字列。 null は <c>null</c>。</returns>
    /// <remarks>
    /// コアの画像（<see cref="EsReadOnlyBitmap"/>）は寸法を<b>破棄後も保持する</b>（幾何は破棄で失われない）ため、<br/>
    /// ログのために破棄の状態を判定しない（表示の可用性は描画の経路が判定する）。
    /// </remarks>
    private static string Describe(EsReadOnlyBitmap? source)
        => source is null ? "null" : $"{source.Width}x{source.Height}";
}
