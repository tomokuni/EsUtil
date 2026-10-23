using Xunit;

namespace EsUtil.Platform.Core.Tests;

/// <summary>例外の扱いの窓口（<see cref="EsErrors"/>）の単体テスト。</summary>
/// <remarks>
/// 判断そのものは `Common.BoundaryCatch` が単一所有するため、ここでは<b>窓口が委譲していること</b>
/// （境界の呼び出し側が同じ結果を受け取ること）だけを検証する。
/// </remarks>
public class EsErrorsTests
{
    [Fact]
    internal void IsRecoverable_例外は継続できる()
        => Assert.True(EsErrors.IsRecoverable(new InvalidOperationException("test")));

    [Fact]
    internal void IsRecoverable_nullは継続できない()
        => Assert.False(EsErrors.IsRecoverable(null!));
}