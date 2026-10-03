using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace EsUtil.Platform.Core;

/// <summary>ロガーの null を <see cref="NullLogger"/> へ吸収する共通ヘルパー。</summary>
/// <remarks>
/// 「ロガー未設定（null）は出力なしとして扱う」規則を単一所有する。<br/>
/// コントロール・サービスは <c>logger ?? NullLogger.Instance</c> を自前で書かず本メソッドへ委譲する。<br/>
/// 拡張メソッド（<c>this ILogger?</c> 第一引数）として提供し、呼び出し側は <c>logger.OrNullLogger()</c> と記述する。
/// </remarks>
public static class LoggerFallback
{
    /// <summary>ロガーを取得する。</summary>
    /// <param name="logger">ロガー。 null 可。</param>
    /// <returns>指定されたロガー。 null の場合は <see cref="NullLogger.Instance"/>。</returns>
    public static ILogger OrNullLogger(this ILogger? logger) => logger ?? NullLogger.Instance;
}
