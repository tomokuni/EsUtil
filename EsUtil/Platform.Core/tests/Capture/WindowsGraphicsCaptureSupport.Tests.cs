using EsUtil.Platform.Core.Capture;
using Xunit;

namespace EsUtil.Platform.Core.Tests.Capture;

/// <summary><see cref="WindowsGraphicsCaptureSupport"/> の判定の整合を検証するテスト。</summary>
/// <remarks>
/// <b>実行機に依存する値を固定しない</b>: 判定は OS の版で変わるため、
/// 「同じ判定を別々に問い合わせた結果が矛盾しないこと」を固定する
/// （実行機ごとの値そのものはテストにしない）。
/// </remarks>
public class WindowsGraphicsCaptureSupportTests
{
    [Fact]
    internal void 対応は_画面撮影の判定と同じ()
        => Assert.Equal(WindowsGraphicsCaptureSupport.IsDisplayCaptureSupported, WindowsGraphicsCaptureSupport.IsSupported);

    [Fact]
    internal void 画面撮影が使えないなら_ウィンドウ撮影も使えない()
    {
        // ウィンドウ撮影は 1809 で追加され、画面撮影（1803）より後の版を要求する（版の単調性）
        if (!WindowsGraphicsCaptureSupport.IsDisplayCaptureSupported)
        {
            Assert.False(WindowsGraphicsCaptureSupport.IsWindowCaptureSupported);
        }
    }

    [Fact]
    internal void 使える場合は_理由を返さない()
    {
        var reason = WindowsGraphicsCaptureSupport.DescribeUnsupportedReason();
        var usable = WindowsGraphicsCaptureSupport.IsDisplayCaptureSupported
            && WindowsGraphicsCaptureSupport.IsWindowCaptureSupported;

        Assert.Equal(usable, reason.Length == 0);
    }

    [Fact]
    internal void 理由は_利用者へ見せる日本語()
    {
        var reason = WindowsGraphicsCaptureSupport.DescribeUnsupportedReason();
        if (reason.Length == 0)
        {
            return;
        }

        Assert.Contains("Windows.Graphics.Capture", reason, StringComparison.Ordinal);
        Assert.Contains("以降", reason, StringComparison.Ordinal);
    }
}
