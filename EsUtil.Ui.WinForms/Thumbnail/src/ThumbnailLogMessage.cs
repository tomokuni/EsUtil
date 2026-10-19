using System.Globalization;
using Microsoft.Extensions.Logging;

namespace EsUtil.Ui.WinForms.Thumbnail;

/// <summary>サムネイル一覧（WinForms 版）が出力する LoggerMessage 定義（ソース生成ログ）。</summary>
/// <remarks>
/// <b>本アセンブリが単一所有する値</b>: 一覧へ渡された画像（<see cref="ThumbnailControl.SetImage"/>）と項目数は<br/>
/// コントロール固有の概念であり、「差し替わった」ことを知っているのは本層だけである（記録も同じ層が担う。 ログ規約 §6）。<br/>
/// 選択の変更はコア（<c>EsUtil.Feature.Thumbnail.Core</c> の <c>ThumbnailSelection</c>）が記録する（本層は重複して記録しない）。<br/>
/// 設定（<c>Options</c>）と外観（<c>Appearance</c>）の変更は提示層が記録する。<br/>
/// ログ基盤（ファイル出力・レベル制御）はホストアプリケーションが所有する（本クラスは定義のみ）。<br/>
/// 各メソッドは <see cref="ILogger"/> の拡張メソッド（<c>this ILogger</c> 第一引数）として定義し、呼び出し側では logger 引数を省略する。
/// </remarks>
internal static partial class ThumbnailLogMessage
{
    /// <summary>接頭辞の既定値（ホストが設定しない場合に使う）。</summary>
    internal const string DefaultPrefix = "[Thumbnail]";

    /// <summary>適用する接頭辞（ホストが <c>LogPrefix</c> で設定する）。</summary>
    internal static string Prefix { get; set; } = DefaultPrefix;

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

    /// <summary>画像の差し替えを記録する（同じインスタンスの再設定では出力しない）。</summary>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="name">設定名（項目の位置を含む名前）。</param>
    /// <param name="oldValue">変更前の画像（整形済み。 未設定は null）。</param>
    /// <param name="newValue">変更後の画像（整形済み。 未設定は null）。</param>
    /// <remarks>
    /// 本コントロールは直前の画像を解放するため、呼び出し側は<b>解放する前に</b>整形した文字列を渡す<br/>
    /// （解放後の <see cref="Bitmap"/> の幅 は例外になる。 整形は <see cref="Describe"/> が単一所有する）。
    /// </remarks>
    internal static void LogImageChanged(this ILogger logger, string name, string? oldValue, string? newValue)
    {
        if (string.Equals(oldValue, newValue, StringComparison.Ordinal))
        {
            return;
        }

        logger.LogSettingChanged(name, oldValue ?? "null", newValue ?? "null");
    }

    /// <summary>項目数の変更を記録する（同数では出力しない）。</summary>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="oldValue">変更前の項目数。</param>
    /// <param name="newValue">変更後の項目数。</param>
    internal static void LogItemCountChanged(this ILogger logger, int oldValue, int newValue)
    {
        if (oldValue == newValue)
        {
            return;
        }

        logger.LogSettingChanged("ItemCount", oldValue.ToString(CultureInfo.InvariantCulture), newValue.ToString(CultureInfo.InvariantCulture));
    }

    /// <summary>画像をログ用の文字列（<c>幅x高さ</c>）へ整形する。</summary>
    /// <param name="image">対象の画像。 null は「画像なし」。</param>
    /// <returns><c>幅x高さ</c> 形式の文字列。 null は <c>null</c>。 破棄済みの画像は <c>disposed</c>。</returns>
    /// <remarks>
    /// <b>解放する前に呼ぶ</b>（解放後の <see cref="Bitmap"/> の幅は例外になる）。<br/>
    /// 呼び出し側が所有権を移さずに破棄した画像を渡した場合でも、記録のために例外を投げない。
    /// </remarks>
    internal static string? Describe(Bitmap? image)
    {
        if (image is null)
        {
            return null;
        }

        try
        {
            return $"{image.Width}x{image.Height}";
        }
        catch (ArgumentException)
        {
            // 破棄済みの GDI+ ビットマップ（寸法を取得できない）
            return "disposed";
        }
    }
}
