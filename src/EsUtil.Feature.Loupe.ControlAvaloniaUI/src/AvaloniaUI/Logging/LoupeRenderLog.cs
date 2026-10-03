using Microsoft.Extensions.Logging;

namespace EsUtil.Feature.Loupe.AvaloniaUI.Logging;

/// <summary>ルーペの描画パイプライン（中間ビットマップの生成と世代管理）に固有のログ定義。</summary>
/// <remarks>
/// 非同期に中間ビットマップを生成して表示へ反映する描画方式（Avalonia 版）に固有のログである。<br/>
/// 画面へ直接描画する MewUI 版には対応する処理が無いため、汎用コア（<c>EsUtil.Feature.Loupe.Core</c>）ではなく<br/>
/// 本 UI プロジェクトが所有する（汎用コアに UI 固有のログを持たせない）。<br/>
/// 各メソッドは <see cref="ILogger"/> の拡張メソッド（<c>this ILogger</c> 第一引数）として定義し、呼び出し側では logger 引数を省略する。
/// </remarks>
internal static partial class LoupeRenderLog
{
    /// <summary>再描画コアの実行ログ。</summary>
    /// <remarks>LogLevel: Trace<br/>出力書式: <c>[Loupe.ControlAvaloniaUI] RenderCore: generation={Generation}</c></remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="generation">描画世代番号。</param>
    [LoggerMessage(LogLevel.Trace, "[Loupe.ControlAvaloniaUI] RenderCore: generation={Generation}")]
    public static partial void LogRenderCore(this ILogger logger, long generation);

    /// <summary>非同期描画の完了・破棄ログ。</summary>
    /// <remarks>
    /// LogLevel: Trace<br/>
    /// 出力書式: <c>[Loupe.ControlAvaloniaUI] GenerateAsync done: set={Set} disposed={Disposed} visible={Visible} gen={Generation} current={Current}</c>
    /// </remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="set">生成したビットマップを表示へ反映したかどうか。</param>
    /// <param name="disposed">古い世代のビットマップとして破棄したかどうか。</param>
    /// <param name="visible">表示状態。</param>
    /// <param name="generation">生成したビットマップの世代番号。</param>
    /// <param name="current">判定時点の最新世代番号。</param>
    [LoggerMessage(LogLevel.Trace, "[Loupe.ControlAvaloniaUI] GenerateAsync done: set={Set} disposed={Disposed} visible={Visible} gen={Generation} current={Current}")]
    public static partial void LogGenerateAsyncDone(this ILogger logger, bool set, bool disposed, bool visible, long generation, long current);
}
