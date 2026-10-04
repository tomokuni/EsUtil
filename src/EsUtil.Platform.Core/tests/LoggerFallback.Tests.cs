using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace EsUtil.Platform.Core.Tests;

/// <summary><see cref="LoggerFallback"/> の null フォールバック規則のテスト。</summary>
/// <remarks>
/// コントロール・サービスが共通で使うロガー取得規則のみを検証するため、UI フレームワークに依存せず<br/>
/// <c>[Fact]</c> で並列実行する。
/// </remarks>
public class LoggerFallbackTests
{
    [Fact]
    internal void OrNullLogger_nullならNullLoggerを返す()
    {
        ILogger? logger = null;

        var result = logger.OrNullLogger();

        Assert.Same(NullLogger.Instance, result);
    }

    [Fact]
    internal void OrNullLogger_非nullなら同じインスタンスを返す()
    {
        // 実装差し替えではない（null 以外はそのまま渡す）ことを、別インスタンスの同一性で検証する
        var logger = NullLogger<LoggerFallbackTests>.Instance;

        var result = logger.OrNullLogger();

        Assert.Same(logger, result);
    }

    [Fact]
    internal void OrNullLogger_NullLoggerを渡しても同じインスタンスを返す()
    {
        var result = NullLogger.Instance.OrNullLogger();

        Assert.Same(NullLogger.Instance, result);
    }

    [Fact]
    internal void OrNullLogger_フォールバック後のログ出力は何も起きない()
    {
        ILogger? logger = null;

        var exception = Record.Exception(() => logger.OrNullLogger().LogInformation("出力されない"));

        Assert.Null(exception);
    }
}
