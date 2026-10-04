using Avalonia.Media;
using Microsoft.Extensions.Logging;

namespace EsUtil.Feature.Loupe.AvaloniaUI;

/// <summary>ルーペ（Avalonia 版）が出力する LoggerMessage 定義（ソース生成ログ）。</summary>
/// <remarks>
/// <b>本プロジェクトが単一所有する理由</b>: 拡大元画像（<see cref="LoupeControl.Source"/>）はコントロール固有の概念である<br/>
/// （コアのファサード <c>LoupeCore</c> は画像を保持せず、描画のたびに引数で受け取る）。<br/>
/// したがって「画像が差し替わった」ことを知っているのは画像を所有する UI 層だけであり、記録も同じ層が担う。<br/>
/// MewUI 版は別アセンブリのため定義を共有できない（接頭辞だけを揃える）。<br/>
/// ログ基盤（ファイル出力・レベル制御）はホストアプリケーションが所有する（本クラスは定義のみ）。<br/>
/// 各メソッドは <see cref="ILogger"/> の拡張メソッド（<c>this ILogger</c> 第一引数）として定義し、呼び出し側では logger 引数を省略する。
/// </remarks>
internal static partial class LoupeUiLogMessage
{
    /// <summary>拡大元画像の変更ログ（同値の再設定では出力しない）。</summary>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="oldValue">変更前の拡大元画像（未設定は null）。</param>
    /// <param name="newValue">変更後の拡大元画像（未設定は null）。</param>
    /// <remarks>
    /// LogLevel: Debug<br/>出力書式: <c>[Loupe] Setting: Source {OldValue} → {NewValue}</c><br/>
    /// 「別のインスタンスに差し替わったか」の判断（参照の同一性）を本メソッドが単一所有する<br/>
    /// （寸法が同じ別画像への差し替えは変更として記録する）。
    /// </remarks>
    public static void LogSourceChanged(this ILogger logger, IImage? oldValue, IImage? newValue)
    {
        if (ReferenceEquals(oldValue, newValue))
        {
            return;
        }

        logger.LogSettingChanged("Source", Describe(oldValue), Describe(newValue));
    }

    /// <summary>設定変更のログ。</summary>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="name">設定名。</param>
    /// <param name="oldValue">変更前の値（整形済み）。</param>
    /// <param name="newValue">変更後の値（整形済み）。</param>
    /// LogLevel: Debug<br/>
    /// 出力書式: <c>[Loupe] Setting: {Name} {OldValue} → {NewValue}</c>
    /// <remarks>本文の書式はコアの設定変更ログと揃える（接頭辞だけが本プロジェクトを表す）。</remarks>
    [LoggerMessage(LogLevel.Debug, "[Loupe] Setting: {Name} {OldValue} → {NewValue}")]
    private static partial void LogSettingChanged(this ILogger logger, string name, string oldValue, string newValue);

    /// <summary>ルーペ描画の例外エラーログ。</summary>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="exception">発生した例外。</param>
    /// <remarks>
    /// LogLevel: Error<br/>出力書式: <c>[Loupe] Update exception</c>（例外スタックトレース付き）<br/>
    /// 例外の記録は描画を実行する UI が行う（コアは描画 API を持たず例外を検知しない）。
    /// </remarks>
    [LoggerMessage(LogLevel.Error, "[Loupe] Update exception")]
    public static partial void LogUpdateError(this ILogger logger, Exception exception);

    /// <summary>ソース画像の寸法をログ用の文字列（<c>幅x高さ</c>）へ整形する。</summary>
    /// <param name="source">対象のソース画像。 null は「画像なし」。</param>
    /// <returns><c>幅x高さ</c> 形式の文字列。 null・寸法取得不能（破棄済み）は <c>null</c> または型名。</returns>
    /// <remarks>
    /// <see cref="IImage.Size"/> の参照は破棄済みビットマップで例外になるため、<br/>
    /// 例外時は型名を返す（ログ出力で例外を発生させない）。
    /// </remarks>
    private static string Describe(IImage? source)
    {
        if (source is null)
        {
            return "null";
        }

        try
        {
            var size = source.Size;
            return $"{(int)size.Width}x{(int)size.Height}";
        }
        catch (ObjectDisposedException)
        {
            // 破棄済みビットマップ: 寸法を取得できないため型名で代用する（ログは継続する）
            return source.GetType().Name;
        }
    }
}
