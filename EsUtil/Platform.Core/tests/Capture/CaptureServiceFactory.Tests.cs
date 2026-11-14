using EsUtil.Platform.Core.Capture;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Native.Win;
using Xunit;

namespace EsUtil.Platform.Core.Tests.Capture;

/// <summary><see cref="CaptureServiceFactory"/> の方式の選択を検証するテスト。</summary>
/// <remarks>
/// <b>実行機に依存する値を固定しない</b>: WGC が使えるかは環境で変わるため、
/// 「設定と実際の方式が矛盾しないこと」を固定する（使える環境では WGC、使えなければ GDI）。
/// </remarks>
public class CaptureServiceFactoryTests
{
    [Fact]
    internal void GDIを指定すると_GDIの実装を返す()
        => Assert.IsType<WinCaptureService>(CaptureServiceFactory.Create(CaptureMethod.Gdi));

    [Fact]
    internal void WGCを指定すると_使える場合だけWGCの実装を返す()
    {
        var service = CaptureServiceFactory.Create(CaptureMethod.WindowsGraphicsCapture);

        if (WindowsGraphicsCaptureSupport.IsSupported)
        {
            Assert.IsType<WinGraphicsCaptureService>(service);
        }
        else
        {
            // 使えない環境では GDI へ落とす（撮影そのものを失敗させない）
            Assert.IsType<WinCaptureService>(service);
        }
    }

    [Fact]
    internal void 解決した方式は_設定と使えるかの積()
    {
        Assert.Equal(CaptureMethod.Gdi, CaptureServiceFactory.Resolve(CaptureMethod.Gdi));
        Assert.Equal(
            WindowsGraphicsCaptureSupport.IsSupported ? CaptureMethod.WindowsGraphicsCapture : CaptureMethod.Gdi,
            CaptureServiceFactory.Resolve(CaptureMethod.WindowsGraphicsCapture));
    }

    [Fact]
    internal void WGCの実装は_撮影できない領域をGDIへ退避する()
    {
        // モニターの外へはみ出す領域は WGC では撮れない（モニター単位のため）。 例外にせず撮れる
        var service = new WinGraphicsCaptureService();
        var image = service.CaptureArea(new EsRectInt(-1, -1, 8, 8));

        Assert.Equal(8, image.Width);
        Assert.Equal(8, image.Height);
    }
}
