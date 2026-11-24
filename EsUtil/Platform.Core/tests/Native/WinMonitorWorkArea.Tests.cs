using EsUtil.Platform.Core.Native.Win;
using Xunit;

namespace EsUtil.Platform.Core.Tests.Native;

/// <summary><see cref="WinMonitorWorkArea"/> の失敗の扱いを検証する。</summary>
/// <remarks>
/// 正しい作業領域は実モニターでしか測れないため、本クラスは<b>失敗の扱い</b>を固定する
/// （照会できないときに例外を投げると、パネルを置くだけで落ちる）。
/// </remarks>
[Collection("Win32")]
public class WinMonitorWorkAreaTests
{
    /// <summary>ハンドルが 0 の場合は照会せずに null を返すことを検証する。</summary>
    [Fact]
    internal void GetWorkArea_ハンドルがゼロなら_null_を返す()
    {
        Assert.Null(WinMonitorWorkArea.GetWorkArea(IntPtr.Zero));
    }
}
