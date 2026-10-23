using EsUtil.Platform.Core.Window;
using Xunit;

namespace EsUtil.Platform.Core.Tests;

/// <summary>ウィンドウの窓口（<see cref="EsWindow"/>）の単体テスト。</summary>
/// <remarks>
/// 窓口は実装を持たず機能の型へ委譲するため、ここでは<b>委譲の結果</b>だけを検証する。<br/>
/// 解釈の規則そのものは各型のテスト（`Common.EnumCanonical` / `Window.EsWindowState`）が検証する。
/// </remarks>
public class EsWindowTests
{
    [Fact]
    internal void FromHandle_ハンドル値を保持する()
    {
        var handle = EsWindow.FromHandle(0x1234, "HWND");

        Assert.Equal(0x1234, handle.Handle);
        Assert.Equal("HWND", handle.Descriptor);
        Assert.True(handle.IsValid);
    }

    [Fact]
    internal void FromHandle_記述子を省略できる()
    {
        var handle = EsWindow.FromHandle(0x1234);

        Assert.Equal(0x1234, handle.Handle);
        Assert.Null(handle.Descriptor);
    }

    [Fact]
    internal void TryParseState_名前を解釈する()
    {
        Assert.True(EsWindow.TryParseState("Minimized", out var state));
        Assert.Equal(EsWindowState.Minimized, state);
    }

    [Fact]
    internal void TryParseState_数値文字列は受理しない()
    {
        Assert.False(EsWindow.TryParseState("999", out var state));
        Assert.Equal(EsWindowState.Normal, state);
    }
}