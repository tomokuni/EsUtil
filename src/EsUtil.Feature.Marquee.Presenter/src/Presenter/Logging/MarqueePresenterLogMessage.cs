using System.Globalization;
using EsUtil.Platform.Core.Coord;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Imaging;
using Microsoft.Extensions.Logging;
using DrawingColor = System.Drawing.Color;

namespace EsUtil.Feature.Marquee.Presenter.Logging;

/// <summary>選択矩形コントロールの提示層が出力する LoggerMessage 定義（ソース生成ログ）。</summary>
/// <remarks>
/// 本層が単一所有する設定（外観 = <see cref="MarqueeAppearance"/> と表示スケーリング係数）の変更を記録する。<br/>
/// <b>「値を所有する主体が記録する」規則</b>により、コアが所有する設定（<c>MarqueeOptions</c> / 倍率 / ハイライト矩形）は<br/>
/// コアのログ定義（<c>EsUtil.Feature.Marquee.Core</c> の <c>MarqueeLogMessage</c>）が記録し、本層は重複して記録しない<br/>
/// （どの層まで値が伝搬したかをログの接頭辞から追える）。<br/>
/// <b>本層を通過した値は Trace で記録する</b>: コアへ伝搬するだけのプロパティであっても、<br/>
/// ホストの設定が本層を通ったことを確認できるようにする（既定の出力では溢れさせない）。<br/>
/// <b>出力の方針</b>: 生成（ロガーの設定時に記録）・本層が所有する設定の変更（Debug）・コアへの転送（Trace）。<br/>
/// 毎フレーム呼ばれる解決メソッド（<c>ResolveDrawPlan</c>）と高頻度の入力通知（<c>Move</c>）は出力しない。<br/>
/// ログ基盤（ファイル出力・レベル制御）はホストアプリケーションが所有する（本クラスは定義のみ）。<br/>
/// 各メソッドは <see cref="ILogger"/> の拡張メソッド（<c>this ILogger</c> 第一引数）として定義し、呼び出し側では logger 引数を省略する。
/// </remarks>
internal static partial class MarqueePresenterLogMessage
{
    /// <summary>未設定を表す文字列。</summary>
    private const string Null = "null";

    /// <summary>提示層の生成を記録する。</summary>
    /// <remarks>
    /// LogLevel: Information<br/>出力書式: <c>[Marquee] Presenter created</c><br/>
    /// 生成時点では出力先が未設定のため、<b>ロガーが設定された時点で 1 度だけ</b>記録する。
    /// </remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    [LoggerMessage(LogLevel.Information, "[Marquee] Presenter created")]
    internal static partial void LogPresenterCreated(this ILogger logger);

    /// <summary>出力先（ロガー）の設定を記録する。</summary>
    /// <remarks>
    /// LogLevel: Trace<br/>出力書式: <c>[Marquee] Logger attached</c><br/>
    /// 本層はロガーをコアへ伝搬するだけであり、値の変換を行わない（通過の確認を Trace で残す）。
    /// </remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    [LoggerMessage(LogLevel.Trace, "[Marquee] Logger attached")]
    internal static partial void LogLoggerAttached(this ILogger logger);

    /// <summary>コアへ伝搬した値を記録する（本層を通過した確認用）。</summary>
    /// <remarks>
    /// LogLevel: Trace<br/>出力書式: <c>[Marquee] Forwarded: {Name} {Value}</c><br/>
    /// 値の整形は型ごとのオーバーロードが単一所有する（呼び出し側に整形処理を置かない）。<br/>
    /// 本層が所有する設定（値が変化したときにのみ意味を持つもの）は <see cref="LogAppearanceChanged"/> などが Debug で記録する。
    /// </remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="name">値の名前（項目名）。</param>
    /// <param name="value">整形済みの値。</param>
    [LoggerMessage(LogLevel.Trace, "[Marquee] Forwarded: {Name} {Value}")]
    internal static partial void LogForwarded(this ILogger logger, string name, string value);

    /// <summary>設定（<see cref="MarqueeOptions"/>）をコアへ伝搬したことを記録する。</summary>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="name">値の名前（項目名）。</param>
    /// <param name="value">伝搬した設定。</param>
    /// <remarks>項目数が多いため代表値だけを記録する（全項目の差分はコアが Debug で出力する）。</remarks>
    internal static void LogForwarded(this ILogger logger, string name, MarqueeOptions value)
        => logger.LogForwarded(name, $"HandleSize={Format(value.HandleSize)}, MinSelectionSize={Format(value.MinSelectionSize)}");

    /// <summary>実数値をコアへ伝搬したことを記録する。</summary>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="name">値の名前（項目名）。</param>
    /// <param name="value">伝搬した実数値。</param>
    internal static void LogForwarded(this ILogger logger, string name, double value)
        => logger.LogForwarded(name, Format(value));

    /// <summary>矩形（未指定は null）をコアへ伝搬したことを記録する。</summary>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="name">値の名前（項目名）。</param>
    /// <param name="value">伝搬した矩形（未指定は null）。</param>
    internal static void LogForwarded(this ILogger logger, string name, EsRect? value)
        => logger.LogForwarded(name, value is { } rect
            ? $"({Format(rect.X)}, {Format(rect.Y)}, {Format(rect.Width)}, {Format(rect.Height)})"
            : Null);

    /// <summary>座標変換ポートをコアへ伝搬したことを記録する。</summary>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="name">値の名前（項目名）。</param>
    /// <param name="value">伝搬した座標変換ポート（未設定は null）。</param>
    /// <remarks>ポートは値として比較・整形できないため、設定された実装の種別のみを記録する。</remarks>
    internal static void LogForwarded(this ILogger logger, string name, ICoordTransform? value)
        => logger.LogForwarded(name, value?.GetType().Name ?? Null);

    /// <summary>設定値変更のログ（変更前後）。</summary>
    /// <remarks>LogLevel: Debug<br/>出力書式: <c>[Marquee] Setting: {Name} {OldValue} → {NewValue}</c></remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="name">設定名。</param>
    /// <param name="oldValue">変更前の値（整形済み）。</param>
    /// <param name="newValue">変更後の値（整形済み）。</param>
    [LoggerMessage(LogLevel.Debug, "[Marquee] Setting: {Name} {OldValue} → {NewValue}")]
    internal static partial void LogSettingChanged(this ILogger logger, string name, string oldValue, string newValue);

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
    /// LogLevel: Debug（書式は個別の変更ログと同一）<br/>
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
        logger.LogSettingChanged(nameof(MarqueeAppearance.OverlayColor), oldValue.OverlayColor, newValue.OverlayColor);
        logger.LogSettingChanged(nameof(MarqueeAppearance.OuterBorderColor), oldValue.OuterBorderColor, newValue.OuterBorderColor);
        logger.LogSettingChanged(nameof(MarqueeAppearance.InnerFillColor), oldValue.InnerFillColor, newValue.InnerFillColor);
        logger.LogSettingChanged(nameof(MarqueeAppearance.InnerBorderColor), oldValue.InnerBorderColor, newValue.InnerBorderColor);
        logger.LogSettingChanged(nameof(MarqueeAppearance.SizeLabelFontSize), oldValue.SizeLabelFontSize, newValue.SizeLabelFontSize);
        logger.LogSettingChanged(nameof(MarqueeAppearance.CoordLabelFontSize), oldValue.CoordLabelFontSize, newValue.CoordLabelFontSize);
        logger.LogSettingChanged(nameof(MarqueeAppearance.LabelPadding), oldValue.LabelPadding, newValue.LabelPadding);
        logger.LogSettingChanged(nameof(MarqueeAppearance.ShowHandles), oldValue.ShowHandles, newValue.ShowHandles);
        logger.LogSettingChanged(nameof(MarqueeAppearance.ShowSizeLabel), oldValue.ShowSizeLabel, newValue.ShowSizeLabel);
        logger.LogSettingChanged(nameof(MarqueeAppearance.ShowCoordLabel), oldValue.ShowCoordLabel, newValue.ShowCoordLabel);
        logger.LogSettingChanged(nameof(MarqueeAppearance.ShowOverlay), oldValue.ShowOverlay, newValue.ShowOverlay);
        logger.LogSettingChanged(nameof(MarqueeAppearance.ShowOuterBorder), oldValue.ShowOuterBorder, newValue.ShowOuterBorder);
        logger.LogSettingChanged(nameof(MarqueeAppearance.ShowFullOverlay), oldValue.ShowFullOverlay, newValue.ShowFullOverlay);
        logger.LogSettingChanged(nameof(MarqueeAppearance.ShowInnerFill), oldValue.ShowInnerFill, newValue.ShowInnerFill);
        logger.LogSettingChanged(nameof(MarqueeAppearance.ShowInnerBorder), oldValue.ShowInnerBorder, newValue.ShowInnerBorder);
        logger.LogSettingChanged(
            nameof(MarqueeAppearance.OuterBorderThickness), oldValue.OuterBorderThickness, newValue.OuterBorderThickness);
        logger.LogSettingChanged(
            nameof(MarqueeAppearance.InnerBorderThickness), oldValue.InnerBorderThickness, newValue.InnerBorderThickness);
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

    /// <summary>実数値をカルチャに依存しない文字列へ整形する。</summary>
    /// <param name="value">整形する値。</param>
    /// <returns>整形した文字列。</returns>
    /// <remarks>ログの書式を環境非依存にする（小数点の記号がロケールで変わらないようにする）。</remarks>
    private static string Format(double value) => value.ToString(format: null, CultureInfo.InvariantCulture);
}
