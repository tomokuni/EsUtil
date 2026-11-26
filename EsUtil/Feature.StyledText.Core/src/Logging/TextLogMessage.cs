using EsUtil.Feature.StyledText.Core.Text;
using EsUtil.Feature.StyledText.Presenter.Formatting;
using EsUtil.Platform.Core.Common;
using Microsoft.Extensions.Logging;

namespace EsUtil.Feature.StyledText.Logging;

/// <summary>書式付き文字列の編集操作を記録するログ定義（アセンブリごとに 1 つ）。</summary>
/// <remarks>
/// <b>本アセンブリが単一所有するもの</b>: 本文の変更・書式の適用・選択・クリップボード操作・コントロールの生成。
/// 定義を UI ごとに複製すると接頭辞と書式が二重所有になるため、4 つの UI 版は本定義を共有する。<br/>
/// <b>値はプレースホルダーで渡す</b>: 構造化ログを活かし、文字列補間で組み立てない。<br/>
/// <b>本文は先頭 100 文字だけを記録する</b>: 自由入力のため、そのまま記録すると 1 行が数百 KB になる。<br/>
/// <b>可視性</b>: UI 層（4 つの UI 版）が呼ぶ定義だけ <c>public</c> とし、コアだけが使う定義は <c>internal</c> とする。
/// </remarks>
public static partial class TextLogMessage
{
    /// <summary>接頭辞の既定値（ホストが設定しない場合に使う）。</summary>
    internal const string DefaultPrefix = "[StyledText]";

    /// <summary>適用する接頭辞（<c>TextEditor.LogPrefix</c> が設定する）。</summary>
    internal static string Prefix { get; set; } = DefaultPrefix;

    /// <summary>コントロールの生成を記録する（生成メソッド）。</summary>
    /// <remarks>
    /// LogLevel: Debug<br/>出力書式:
    /// <c>[StyledText] コントロールを作成: 倍率={Scaling} 行間={LineHeightFactor} 書式バー={Hosting}</c>
    /// </remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="prefix">接頭辞（公開ラッパーが <see cref="Prefix"/> を渡す）。</param>
    /// <param name="scaling">元画像ピクセルから DIP への倍率（整形済み）。</param>
    /// <param name="lineHeightFactor">行間の倍率（整形済み）。</param>
    /// <param name="hosting">書式バーの置き方。</param>
    [LoggerMessage(
        LogLevel.Debug,
        "{Prefix:l} コントロールを作成: 倍率={Scaling:l} 行間={LineHeightFactor:l} 書式バー={Hosting:l}")]
    private static partial void LogControlCreatedCore(
        this ILogger logger, string prefix, string scaling, string lineHeightFactor, string hosting);

    /// <summary>コントロールの生成を記録する。</summary>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="sourceToClientScaling">元画像ピクセルから DIP への倍率。</param>
    /// <param name="lineHeightFactor">行間の倍率。</param>
    /// <param name="hosting">書式バーの置き方。</param>
    public static void LogControlCreated(
        this ILogger logger, double sourceToClientScaling, double lineHeightFactor, TextFormatBarHosting hosting)
    {
        if (!logger.IsEnabled(LogLevel.Debug))
        {
            return;
        }

        logger.LogControlCreatedCore(
            Prefix,
            NumberText.Format(sourceToClientScaling),
            NumberText.Format(lineHeightFactor),
            hosting.ToString());
    }

    /// <summary>設定値の変更を記録する（生成メソッド）。</summary>
    /// <remarks>LogLevel: Information<br/>出力書式: <c>[StyledText] 設定: {Name} {OldValue} → {NewValue}</c></remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="prefix">接頭辞（公開ラッパーが <see cref="Prefix"/> を渡す）。</param>
    /// <param name="name">変更された設定名。</param>
    /// <param name="oldValue">変更前の値（整形済み）。</param>
    /// <param name="newValue">変更後の値（整形済み）。</param>
    [LoggerMessage(LogLevel.Information, "{Prefix:l} 設定: {Name:l} {OldValue:l} → {NewValue:l}")]
    private static partial void LogSettingChangedCore(
        this ILogger logger, string prefix, string name, string oldValue, string newValue);

    /// <summary>設定値の変更を記録する（整形済みの値を渡す）。</summary>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="name">変更された設定名。</param>
    /// <param name="oldValue">変更前の値（整形済み）。</param>
    /// <param name="newValue">変更後の値（整形済み）。</param>
    public static void LogSettingChanged(this ILogger logger, string name, string oldValue, string newValue)
        => logger.LogSettingChangedCore(Prefix, name, oldValue, newValue);

    /// <summary>設定値の変更（真偽値）を記録する。</summary>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="name">変更された設定名。</param>
    /// <param name="oldValue">変更前の値。</param>
    /// <param name="newValue">変更後の値。</param>
    public static void LogSettingChanged(this ILogger logger, string name, bool oldValue, bool newValue)
        => logger.LogSettingChangedCore(Prefix, name, oldValue.ToString(), newValue.ToString());

    /// <summary>設定値（書式バーに出す項目）の変更を記録する。</summary>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="name">変更された設定名。</param>
    /// <param name="oldValue">変更前の項目。</param>
    /// <param name="newValue">変更後の項目。</param>
    /// <remarks>出す項目の名前を並べて記録する（型名では何が出るか読めない）。</remarks>
    public static void LogSettingChanged(
        this ILogger logger, string name, TextFormatBarItemVisibility oldValue, TextFormatBarItemVisibility newValue)
    {
        if (!logger.IsEnabled(LogLevel.Information))
        {
            return;
        }

        logger.LogSettingChangedCore(Prefix, name, DescribeItems(oldValue), DescribeItems(newValue));
    }

    /// <summary>出さない項目を含めた並びを記録用の文字列にする。</summary>
    /// <param name="items">書式バーに出す項目の組み合わせ。</param>
    /// <returns>項目名をカンマで並べた文字列（すべて出さない場合は <c>なし</c>）。</returns>
    private static string DescribeItems(TextFormatBarItemVisibility items)
        => items.VisibleItems.IsDefaultOrEmpty ? "なし" : string.Join(",", items.VisibleItems.ToArray());

    /// <summary>本文の編集を開始したことを記録する（生成メソッド）。</summary>
    /// <remarks>LogLevel: Debug<br/>出力書式: <c>[StyledText] 編集を開始: キャレット={Caret} 本文={Preview}</c></remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="prefix">接頭辞（公開ラッパーが <see cref="Prefix"/> を渡す）。</param>
    /// <param name="caret">開始位置（文字数）。</param>
    /// <param name="preview">本文の先頭（整形済み）。</param>
    [LoggerMessage(LogLevel.Debug, "{Prefix:l} 編集を開始: キャレット={Caret} 本文={Preview:l}")]
    private static partial void LogEditingStartedCore(this ILogger logger, string prefix, int caret, string preview);

    /// <summary>本文の編集を開始したことを記録する。</summary>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="caret">開始位置（文字数）。</param>
    /// <param name="text">本文（先頭 100 文字だけを記録する）。</param>
    public static void LogEditingStarted(this ILogger logger, int caret, string text)
    {
        if (!logger.IsEnabled(LogLevel.Debug))
        {
            return;
        }

        logger.LogEditingStartedCore(Prefix, caret, ToPreview(text));
    }

    /// <summary>本文の編集を終了したことを記録する（生成メソッド）。</summary>
    /// <remarks>
    /// LogLevel: Debug<br/>出力書式:
    /// <c>[StyledText] 編集を終了: キャレット={Caret} 選択=({SelectionStart}, {SelectionLength})</c>
    /// </remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="prefix">接頭辞（公開ラッパーが <see cref="Prefix"/> を渡す）。</param>
    /// <param name="caret">終了時のキャレットの位置（文字数）。</param>
    /// <param name="selectionStart">終了時の選択範囲の開始位置（文字数）。</param>
    /// <param name="selectionLength">終了時の選択範囲の長さ（文字数）。</param>
    [LoggerMessage(
        LogLevel.Debug,
        "{Prefix:l} 編集を終了: キャレット={Caret} 選択=({SelectionStart}, {SelectionLength})")]
    private static partial void LogEditingEndedCore(
        this ILogger logger, string prefix, int caret, int selectionStart, int selectionLength);

    /// <summary>本文の編集を終了したことを記録する。</summary>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="caret">終了時のキャレットの位置（文字数）。</param>
    /// <param name="selectionStart">終了時の選択範囲の開始位置（文字数）。</param>
    /// <param name="selectionLength">終了時の選択範囲の長さ（文字数）。</param>
    public static void LogEditingEnded(this ILogger logger, int caret, int selectionStart, int selectionLength)
        => logger.LogEditingEndedCore(Prefix, caret, selectionStart, selectionLength);

    /// <summary>本文の選択範囲の変更を記録する（生成メソッド）。</summary>
    /// <remarks>
    /// LogLevel: Trace<br/>出力書式:
    /// <c>[StyledText] 本文の選択: キャレット={Caret} 選択=({SelectionStart}, {SelectionLength})</c>
    /// </remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="prefix">接頭辞（公開ラッパーが <see cref="Prefix"/> を渡す）。</param>
    /// <param name="caret">キャレットの位置（文字数）。</param>
    /// <param name="selectionStart">選択範囲の開始位置（文字数）。</param>
    /// <param name="selectionLength">選択範囲の長さ（文字数）。</param>
    [LoggerMessage(
        LogLevel.Trace,
        "{Prefix:l} 本文の選択: キャレット={Caret} 選択=({SelectionStart}, {SelectionLength})")]
    private static partial void LogTextSelectionChangedCore(
        this ILogger logger, string prefix, int caret, int selectionStart, int selectionLength);

    /// <summary>本文の選択範囲の変更を記録する（キャレット移動・選択のたびに届くため Trace）。</summary>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="caret">キャレットの位置（文字数）。</param>
    /// <param name="selectionStart">選択範囲の開始位置（文字数）。</param>
    /// <param name="selectionLength">選択範囲の長さ（文字数）。</param>
    public static void LogTextSelectionChanged(
        this ILogger logger, int caret, int selectionStart, int selectionLength)
        => logger.LogTextSelectionChangedCore(Prefix, caret, selectionStart, selectionLength);

    /// <summary>本文の変更を記録する（生成メソッド）。</summary>
    /// <remarks>LogLevel: Debug<br/>出力書式: <c>[StyledText] 本文を変更: 文字数={Length} 本文={Preview}</c></remarks>
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

    /// <summary>本文の書式の適用を記録する（生成メソッド）。</summary>
    /// <remarks>
    /// LogLevel: Debug<br/>出力書式:
    /// <c>[StyledText] 書式を適用: 開始={Start} 長さ={Length} 本文={Preview}</c>
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

    /// <summary>クリップボード操作（コピー・切り取り・貼り付け）を記録する（生成メソッド）。</summary>
    /// <remarks>
    /// LogLevel: Debug<br/>出力書式:
    /// <c>[StyledText] 操作: {Operation} キャレット={Caret} 選択=({SelectionStart}, {SelectionLength}) 本文={Preview}</c>
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
        this ILogger logger,
        string prefix,
        string operation,
        int caret,
        int selectionStart,
        int selectionLength,
        string preview);

    /// <summary>クリップボード操作（コピー・切り取り・貼り付け）を記録する。</summary>
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

    /// <summary>書式を適用した範囲の文字を切り出す（本文全体を記録しないため）。</summary>
    /// <param name="text">適用後の本文全体。</param>
    /// <param name="start">適用する範囲の開始位置（文字数）。</param>
    /// <param name="length">適用する範囲の長さ（文字数。 0 以下は本文全体）。</param>
    /// <returns>範囲の文字。 範囲が無い場合は本文全体。</returns>
    private static string ResolveRangeText(string text, int start, int length)
    {
        if (TextStyleEdit.ResolveRange(start, length) is not { } range)
        {
            return text;
        }

        var from = Math.Clamp(range.Start, 0, text.Length);
        var to = Math.Clamp(from + range.Length, from, text.Length);
        return text[from..to];
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
}
