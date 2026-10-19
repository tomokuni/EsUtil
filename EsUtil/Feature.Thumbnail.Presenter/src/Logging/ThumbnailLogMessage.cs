using System.Globalization;
using Microsoft.Extensions.Logging;

namespace EsUtil.Feature.Thumbnail.Logging;

/// <summary>サムネイル一覧の提示層が出力する LoggerMessage 定義（ソース生成ログ）。</summary>
/// <remarks>
/// <b>本層が単一所有する設定</b>: 計算に必要な設定（<see cref="ThumbnailOptions"/>）と外観（<see cref="ThumbnailAppearance"/>）は<br/>
/// 本層が保持するため、変更の記録も本層が行う（UI 層に同値判定・差分検出を置かない。 ログ規約 §6）。<br/>
/// <b>出力の方針</b>: 生成（ロガーの設定時に 1 度だけ Information）・本層が所有する設定の変更（Debug）。<br/>
/// 毎フレーム呼ばれる解決メソッド（<see cref="ThumbnailPresenter.ResolveLayout"/> / <see cref="ThumbnailPresenter.ResolveDrawPlan"/>）は出力しない。<br/>
/// ログ基盤（ファイル出力・レベル制御）はホストアプリケーションが所有する（本クラスは定義のみ）。<br/>
/// 各メソッドは <see cref="ILogger"/> の拡張メソッド（<c>this ILogger</c> 第一引数）として定義し、呼び出し側では logger 引数を省略する。
/// </remarks>
internal static partial class ThumbnailLogMessage
{
    /// <summary>接頭辞の既定値（ホストが設定しない場合に使う）。</summary>
    internal const string DefaultPrefix = "[Thumbnail]";

    /// <summary>適用する接頭辞（ホストが <c>LogPrefix</c> で設定する）。</summary>
    internal static string Prefix { get; set; } = DefaultPrefix;

    /// <summary>提示層の生成を記録する（生成メソッド）。</summary>
    /// <remarks>LogLevel: Information<br/>出力書式: <c>[Thumbnail] Presenter created</c></remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="prefix">接頭辞（公開ラッパーが <see cref="Prefix"/> を渡す）。</param>
    [LoggerMessage(LogLevel.Information, "{Prefix} Presenter created")]
    private static partial void LogPresenterCreatedCore(this ILogger logger, string prefix);

    /// <summary>提示層の生成を記録する。</summary>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <remarks>生成時点では出力先が未設定のため、ロガーが設定された時点で 1 度だけ記録する。</remarks>
    internal static void LogPresenterCreated(this ILogger logger)
        => logger.LogPresenterCreatedCore(Prefix);

    /// <summary>出力先（ロガー）の設定を記録する（生成メソッド）。</summary>
    /// <remarks>LogLevel: Trace<br/>出力書式: <c>[Thumbnail] Logger attached</c></remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="prefix">接頭辞（公開ラッパーが <see cref="Prefix"/> を渡す）。</param>
    [LoggerMessage(LogLevel.Trace, "{Prefix} Logger attached")]
    private static partial void LogLoggerAttachedCore(this ILogger logger, string prefix);

    /// <summary>出力先（ロガー）の設定を記録する。</summary>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <remarks>本層はロガーをコアへ伝搬しない（コアのログ定義は選択を所有する UI 側が設定する）。 通過の確認だけを残す。</remarks>
    internal static void LogLoggerAttached(this ILogger logger)
        => logger.LogLoggerAttachedCore(Prefix);

    /// <summary>設定変更のログ（生成メソッド）。</summary>
    /// <remarks>LogLevel: Debug<br/>出力書式: <c>[Thumbnail] Setting: {Name} {OldValue} → {NewValue}</c></remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="prefix">接頭辞（公開ラッパーが <see cref="Prefix"/> を渡す）。</param>
    /// <param name="name">設定名。</param>
    /// <param name="oldValue">変更前の値（整形済み）。</param>
    /// <param name="newValue">変更後の値（整形済み）。</param>
    [LoggerMessage(LogLevel.Debug, "{Prefix} Setting: {Name} {OldValue} → {NewValue}")]
    private static partial void LogSettingChangedCore(
        this ILogger logger, string prefix, string name, string oldValue, string newValue);

    /// <summary>設定変更のログ（整形済みの値を渡す）。</summary>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="name">設定名。</param>
    /// <param name="oldValue">変更前の値（整形済み）。</param>
    /// <param name="newValue">変更後の値（整形済み）。</param>
    internal static void LogSettingChanged(this ILogger logger, string name, string oldValue, string newValue)
        => logger.LogSettingChangedCore(Prefix, name, oldValue, newValue);

    /// <summary>整数設定値の変更ログ（同値の再設定では出力しない）。</summary>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="name">設定名。</param>
    /// <param name="oldValue">変更前の値。</param>
    /// <param name="newValue">変更後の値。</param>
    internal static void LogSettingChanged(this ILogger logger, string name, int oldValue, int newValue)
        => logger.LogSettingChangedIfChanged(name, oldValue, newValue);

    /// <summary>実数設定値の変更ログ（同値の再設定では出力しない）。</summary>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="name">設定名。</param>
    /// <param name="oldValue">変更前の値。</param>
    /// <param name="newValue">変更後の値。</param>
    internal static void LogSettingChanged(this ILogger logger, string name, double oldValue, double newValue)
        => logger.LogSettingChangedIfChanged(name, oldValue, newValue);

    /// <summary>真偽設定値の変更ログ（同値の再設定では出力しない）。</summary>
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

    /// <summary>色設定値（<c>0xAARRGGBB</c>）の変更ログ（同値の再設定では出力しない）。</summary>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="name">設定名。</param>
    /// <param name="oldValue">変更前の色。</param>
    /// <param name="newValue">変更後の色。</param>
    internal static void LogSettingChanged(this ILogger logger, string name, uint oldValue, uint newValue)
    {
        if (oldValue == newValue)
        {
            return;
        }

        logger.LogSettingChanged(name, FormatColor(oldValue), FormatColor(newValue));
    }

    /// <summary>設定（<see cref="ThumbnailOptions"/>）の変更ログ（変化した項目だけを出力する）。</summary>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="oldValue">変更前の設定。</param>
    /// <param name="newValue">変更後の設定。</param>
    /// <remarks>
    /// 設定を 1 つの値へ統合しているため、変更された項目の判定（差分検出）を本メソッドが単一所有する<br/>
    /// （ホストは前後の値を渡さず、値を保持する提示層が変化を知るだけでよい）。
    /// </remarks>
    internal static void LogOptionsChanged(this ILogger logger, ThumbnailOptions oldValue, ThumbnailOptions newValue)
    {
        ArgumentNullException.ThrowIfNull(oldValue);
        ArgumentNullException.ThrowIfNull(newValue);

        logger.LogSettingChanged(nameof(ThumbnailOptions.ItemMargin), oldValue.ItemMargin, newValue.ItemMargin);
        logger.LogSettingChanged(nameof(ThumbnailOptions.ItemPadding), oldValue.ItemPadding, newValue.ItemPadding);
        logger.LogSettingChanged(nameof(ThumbnailOptions.ItemSpacing), oldValue.ItemSpacing, newValue.ItemSpacing);
        logger.LogSettingChanged(nameof(ThumbnailOptions.LabelHeight), oldValue.LabelHeight, newValue.LabelHeight);
        logger.LogSettingChanged(
            nameof(ThumbnailOptions.LabelHorizontalPadding), oldValue.LabelHorizontalPadding, newValue.LabelHorizontalPadding);
        logger.LogSettingChanged(nameof(ThumbnailOptions.SurroundExtent), oldValue.SurroundExtent, newValue.SurroundExtent);
        logger.LogSettingChanged(nameof(ThumbnailOptions.ContentPadding), oldValue.ContentPadding, newValue.ContentPadding);
        logger.LogSettingChanged(
            nameof(ThumbnailOptions.MinimumImageWidth), oldValue.MinimumImageWidth, newValue.MinimumImageWidth);
        logger.LogSettingChanged(
            nameof(ThumbnailOptions.MaximumColumns), oldValue.MaximumColumns, newValue.MaximumColumns);
    }

    /// <summary>外観（<see cref="ThumbnailAppearance"/>）の変更ログ（変化した項目だけを出力する）。</summary>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="oldValue">変更前の外観。</param>
    /// <param name="newValue">変更後の外観。</param>
    /// <remarks>外観を 1 つの値へ統合しているため、変更された項目の判定（差分検出）を本メソッドが単一所有する。</remarks>
    internal static void LogAppearanceChanged(this ILogger logger, ThumbnailAppearance oldValue, ThumbnailAppearance newValue)
    {
        ArgumentNullException.ThrowIfNull(oldValue);
        ArgumentNullException.ThrowIfNull(newValue);

        logger.LogSettingChanged(nameof(ThumbnailAppearance.LabelFontSize), oldValue.LabelFontSize, newValue.LabelFontSize);
        logger.LogSettingChanged(
            nameof(ThumbnailAppearance.LabelVerticalPadding), oldValue.LabelVerticalPadding, newValue.LabelVerticalPadding);
        logger.LogSettingChanged(
            nameof(ThumbnailAppearance.BoldCurrentLabel), oldValue.BoldCurrentLabel, newValue.BoldCurrentLabel);
        logger.LogSettingChanged(
            nameof(ThumbnailAppearance.HoverBackgroundLightArgb),
            oldValue.HoverBackgroundLightArgb,
            newValue.HoverBackgroundLightArgb);
        logger.LogSettingChanged(
            nameof(ThumbnailAppearance.HoverBackgroundDarkArgb),
            oldValue.HoverBackgroundDarkArgb,
            newValue.HoverBackgroundDarkArgb);
        logger.LogSettingChanged(
            nameof(ThumbnailAppearance.SelectionBackgroundLightArgb),
            oldValue.SelectionBackgroundLightArgb,
            newValue.SelectionBackgroundLightArgb);
        logger.LogSettingChanged(
            nameof(ThumbnailAppearance.SelectionBackgroundDarkArgb),
            oldValue.SelectionBackgroundDarkArgb,
            newValue.SelectionBackgroundDarkArgb);
        logger.LogSettingChanged(
            nameof(ThumbnailAppearance.HighlightedLabelArgb), oldValue.HighlightedLabelArgb, newValue.HighlightedLabelArgb);
        logger.LogSettingChanged(
            nameof(ThumbnailAppearance.HideShadowWhenHighlighted),
            oldValue.HideShadowWhenHighlighted,
            newValue.HideShadowWhenHighlighted);
        logger.LogSettingChanged(
            nameof(ThumbnailAppearance.ShowHoverBackground), oldValue.ShowHoverBackground, newValue.ShowHoverBackground);
        logger.LogSettingChanged(
            nameof(ThumbnailAppearance.ShowSelectionBackground),
            oldValue.ShowSelectionBackground,
            newValue.ShowSelectionBackground);
        var oldShadow = Describe(oldValue.Shadow);
        var newShadow = Describe(newValue.Shadow);
        if (!string.Equals(oldShadow, newShadow, StringComparison.Ordinal))
        {
            logger.LogSettingChanged(nameof(ThumbnailAppearance.Shadow), oldShadow, newShadow);
        }
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

    /// <summary>色（<c>0xAARRGGBB</c>）をログ用の文字列へ整形する。</summary>
    /// <param name="value">色。</param>
    /// <returns><c>0xAARRGGBB</c> 形式の文字列。</returns>
    private static string FormatColor(uint value) => "0x" + value.ToString("X8", CultureInfo.InvariantCulture);

    /// <summary>影の計画をログ用の文字列へ整形する。</summary>
    /// <param name="value">影の計画（null は「影なし」）。</param>
    /// <returns>実装の型名。 null は <c>null</c>。</returns>
    private static string Describe(IThumbnailShadowPlan? value) => value is null ? "null" : value.GetType().Name;
}
