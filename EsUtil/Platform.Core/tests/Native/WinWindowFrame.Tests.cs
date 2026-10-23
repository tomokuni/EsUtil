using System.ComponentModel;
using System.Runtime.Versioning;
using EsUtil.Platform.Core.Native.Win;
using Xunit;

namespace EsUtil.Platform.Core.Tests.Native;

/// <summary><see cref="WinWindowFrame"/> の実機テスト（枠の太さの計測）。</summary>
/// <remarks>
/// 正しい値は実ウィンドウでしか測れないため、本クラスは<b>失敗の扱い</b>を固定する
/// （測れないときに 0 を返すと、枠を無視した寸法でウィンドウが作られ、最小サイズが破られる）。
/// 実ウィンドウでの値は <see cref="WinWindowServiceTests"/> と同じ方法で作れるが、
/// 枠の太さはテーマ・DPI で変わるため値そのものは検証しない。
/// </remarks>
[SupportedOSPlatform("windows10.0.17763")]
[Collection("Win32")]
public class WinWindowFrameTests
{
    /// <summary>ハンドルが 0 の場合は例外になることを検証する（使用できない状態を黙って通さない）。</summary>
    [Fact]
    internal void GetSize_ハンドルがゼロなら_Win32Exception_を投げる()
    {
        var ex = Assert.Throws<Win32Exception>(() => WinWindowFrame.GetSize(EsWindow.FromHandle(IntPtr.Zero)));

        Assert.False(string.IsNullOrWhiteSpace(ex.Message));
    }
}
