using System.Globalization;
using Microsoft.Extensions.Logging;

namespace EsUtil.Ui.AvaloniaUI.ColumnPacking;

/// <summary>複数列の配置（Avalonia UI 版）が出力する LoggerMessage 定義（ソース生成ログ）。</summary>
/// <remarks>
/// <b>本アセンブリが単一所有する値</b>: パネルのプロパティ（列の上限・行間・列間・配置の方法）と、<br/>
/// レイアウト結果（使用幅・最小の高さ・列数・列の区切り）はコントロール固有の概念であり、変化を知っているのは本層だけである<br/>
/// （記録も同じ層が担う。 <c>.github/ログ規約.md</c> §6）。<br/>
/// <b>頻度でレベルを分ける</b>: プロパティの変更（利用者が設定パネルで変える操作）は <c>Information</c>、<br/>
/// レイアウト結果の変化（設定・サイズの結果として変わる状態）は <c>Debug</c>、項目ごとの座標は <c>Trace</c> とする。<br/>
/// ログ基盤（出力先・レベルの制御）はホストが所有する（本クラスは定義のみ）。
/// </remarks>
internal static partial class ColumnPackingLogMessage
{
    /// <summary>接頭辞の既定値（ホストが設定しない場合に使う）。</summary>
    internal const string DefaultPrefix = "[ColumnPacking]";

    /// <summary>適用する接頭辞（ホストが <c>LogPrefix</c> で設定する）。</summary>
    internal static string Prefix { get; set; } = DefaultPrefix;

    /// <summary>生成（初期設定）を記録する（生成メソッド）。</summary>
    /// <remarks>LogLevel: Information<br/>出力書式: <c>[ColumnPacking] Facade created: ColumnLimit=4, RowSpace=8, ColumnSpace=8, Method=BinarySearch</c></remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="prefix">接頭辞（公開ラッパーが <see cref="Prefix"/> を渡す）。</param>
    /// <param name="columnLimit">列の上限。</param>
    /// <param name="rowSpace">行間（DIP。 整形済み）。</param>
    /// <param name="columnSpace">列間（DIP。 整形済み）。</param>
    /// <param name="method">配置の方法。</param>
    [LoggerMessage(LogLevel.Information, "{Prefix:l} Facade created: ColumnLimit={ColumnLimit}, RowSpace={RowSpace:0.#}, ColumnSpace={ColumnSpace:0.#}, Method={Method:l}")]
    private static partial void LogFacadeCreatedCore(
        this ILogger logger, string prefix, int columnLimit, double rowSpace, double columnSpace, string method);

    /// <summary>生成（初期設定）を記録する。</summary>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="columnLimit">列の上限。</param>
    /// <param name="rowSpace">行間（DIP）。</param>
    /// <param name="columnSpace">列間（DIP）。</param>
    /// <param name="method">配置の方法。</param>
    internal static void LogFacadeCreated(this ILogger logger, int columnLimit, double rowSpace, double columnSpace, string method)
        => logger.LogFacadeCreatedCore(Prefix, columnLimit, rowSpace, columnSpace, method);

    /// <summary>プロパティの変更を記録する（生成メソッド）。</summary>
    /// <remarks>LogLevel: Information<br/>出力書式: <c>[ColumnPacking] Setting: {Name:l} {OldValue:l} → {NewValue:l}</c></remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="prefix">接頭辞（公開ラッパーが <see cref="Prefix"/> を渡す）。</param>
    /// <param name="name">プロパティ名。</param>
    /// <param name="oldValue">変更前の値（整形済み）。</param>
    /// <param name="newValue">変更後の値（整形済み）。</param>
    [LoggerMessage(LogLevel.Information, "{Prefix:l} Setting: {Name:l} {OldValue:l} → {NewValue:l}")]
    private static partial void LogSettingChangedCore(
        this ILogger logger, string prefix, string name, string oldValue, string newValue);

    /// <summary>プロパティの変更を記録する（同値では出力しない）。</summary>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="name">プロパティ名。</param>
    /// <param name="oldValue">変更前の値（整形済み）。</param>
    /// <param name="newValue">変更後の値（整形済み）。</param>
    internal static void LogSettingChanged(this ILogger logger, string name, string oldValue, string newValue)
    {
        if (string.Equals(oldValue, newValue, StringComparison.Ordinal))
        {
            return;
        }

        logger.LogSettingChangedCore(Prefix, name, oldValue, newValue);
    }

    /// <summary>レイアウト結果の変化を記録する（生成メソッド）。</summary>
    /// <remarks>LogLevel: Debug<br/>出力書式: <c>[ColumnPacking] Layout: UsedWidth=620.8 MinHeight=264 Columns=7 Segments=[3,6]</c></remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="prefix">接頭辞（公開ラッパーが <see cref="Prefix"/> を渡す）。</param>
    /// <param name="usedWidth">使用幅。</param>
    /// <param name="minHeight">最小の高さ。</param>
    /// <param name="columns">列数。</param>
    /// <param name="segments">列の区切り。</param>
    [LoggerMessage(LogLevel.Debug, "{Prefix:l} Layout: UsedWidth={UsedWidth:0.#} MinHeight={MinHeight:0.#} Columns={Columns} Segments={Segments:l}")]
    private static partial void LogLayoutChangedCore(
        this ILogger logger, string prefix, double usedWidth, double minHeight, int columns, string segments);

    /// <summary>レイアウト結果の変化を記録する。</summary>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="usedWidth">使用幅。</param>
    /// <param name="minHeight">最小の高さ。</param>
    /// <param name="columns">列数。</param>
    /// <param name="segments">列の区切り。</param>
    internal static void LogLayoutChanged(this ILogger logger, double usedWidth, double minHeight, int columns, string segments)
        => logger.LogLayoutChangedCore(Prefix, usedWidth, minHeight, columns, segments);

    /// <summary>項目ごとの配置を記録する（生成メソッド）。</summary>
    /// <remarks>LogLevel: Trace<br/>出力書式: <c>[ColumnPacking] Layout items: Y=[0, 48, 104]</c></remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="prefix">接頭辞（公開ラッパーが <see cref="Prefix"/> を渡す）。</param>
    /// <param name="y">項目ごとの Y 座標（整形済み）。</param>
    [LoggerMessage(LogLevel.Trace, "{Prefix:l} Layout items: Y={Y:l}")]
    private static partial void LogLayoutItemsCore(this ILogger logger, string prefix, string y);

    /// <summary>項目ごとの配置を記録する（整形は本メソッドが行う）。</summary>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="itemY">項目ごとの Y 座標。</param>
    /// <remarks>
    /// <b>整形（文字列化）は本メソッドが <c>IsEnabled</c> でゲートしてから行う</b>: 引数は呼び出し前に評価されるため、<br/>
    /// 呼び出し側で整形すると Trace が無効でも費用がかかる（呼び出し側に <c>IsEnabled</c> を書かない規則。 ログ規約 §5）。
    /// </remarks>
    internal static void LogLayoutItems(this ILogger logger, IReadOnlyList<double> itemY)
    {
        if (!logger.IsEnabled(LogLevel.Trace))
        {
            return;
        }

        logger.LogLayoutItemsCore(
            Prefix,
            string.Join(", ", itemY.Select(static value => Format(value))));
    }

    /// <summary>数値をログ用の文字列（自然な小数表記）へ整形する。</summary>
    /// <param name="value">整形する値。</param>
    /// <returns>不変カルチャの文字列。</returns>
    internal static string Format(double value) => value.ToString("0.#", CultureInfo.InvariantCulture);
}
