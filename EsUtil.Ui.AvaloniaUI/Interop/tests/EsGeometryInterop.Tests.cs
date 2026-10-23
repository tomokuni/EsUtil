using Avalonia;
using Avalonia.Platform;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core;
using Xunit;

namespace EsUtil.Ui.AvaloniaUI.Interop.Tests;

/// <summary><see cref="EsGeometryInterop"/> / <see cref="EsPlatformHandleInterop"/> の変換テスト。</summary>
/// <remarks>
/// コア幾何型と Avalonia 型の相互変換のみを検証するため、コントロール生成（ヘッドレス）に依存せず<br/>
/// <c>[Fact]</c> で並列実行する。
/// </remarks>
public class EsGeometryInteropTests
{
    // ====================================================================
    // DIP 座標（EsPoint / EsSize / EsVector / EsRect）
    // ====================================================================

    [Fact]
    internal void EsPoint_往復変換で値が一致する()
    {
        var es = new EsPoint(1.5, -2.5);

        Assert.Equal(new Point(1.5, -2.5), es.ToAvaloniaPoint());
        Assert.Equal(es, new Point(1.5, -2.5).ToEsPoint());
    }

    [Fact]
    internal void EsSize_往復変換で値が一致する()
    {
        var es = new EsSize(100, 50);

        Assert.Equal(new Size(100, 50), es.ToAvaloniaSize());
        Assert.Equal(es, new Size(100, 50).ToEsSize());
    }

    [Fact]
    internal void EsVector_往復変換で値が一致する()
    {
        var es = new EsVector(-3, 4);

        Assert.Equal(new Vector(-3, 4), es.ToAvaloniaVector());
        Assert.Equal(es, new Vector(-3, 4).ToEsVector());
    }

    [Fact]
    internal void EsRect_往復変換で値が一致する()
    {
        var es = new EsRect(10, 20, 100, 50);

        Assert.Equal(new Rect(10, 20, 100, 50), es.ToAvaloniaRect());
        Assert.Equal(es, new Rect(10, 20, 100, 50).ToEsRect());
    }

    // ====================================================================
    // 画面物理座標（EsPointInt / EsRectInt）
    // ====================================================================

    [Fact]
    internal void EsPointInt_往復変換で値が一致する()
    {
        var es = new EsPointInt(123, 456);

        Assert.Equal(new PixelPoint(123, 456), es.ToAvaloniaPixelPoint());
        Assert.Equal(es, new PixelPoint(123, 456).ToEsPointInt());
    }

    [Fact]
    internal void EsPointInt_負の座標も変換できる()
    {
        // プライマリモニタが (0,0) にないマルチモニタ構成を模擬する
        var es = new EsPointInt(-1920, -100);

        Assert.Equal(new PixelPoint(-1920, -100), es.ToAvaloniaPixelPoint());
        Assert.Equal(es, new PixelPoint(-1920, -100).ToEsPointInt());
    }

    [Fact]
    internal void EsRectInt_往復変換で値が一致する()
    {
        var es = new EsRectInt(10, 20, 1920, 1080);

        Assert.Equal(new PixelRect(10, 20, 1920, 1080), es.ToAvaloniaPixelRect());
        Assert.Equal(es, new PixelRect(10, 20, 1920, 1080).ToEsRectInt());
    }

    // ====================================================================
    // ネイティブハンドル（EsPlatformHandle）
    // ====================================================================

    [Fact]
    internal void EsPlatformHandle_nullはハンドル値0に変換する()
    {
        IPlatformHandle? handle = null;

        var result = handle.ToEsPlatformHandle();

        Assert.Equal(0, result.Handle);
        Assert.Null(result.Descriptor);
        Assert.False(result.IsValid);
    }

    [Fact]
    internal void EsPlatformHandle_Avaloniaハンドルから変換する()
    {
        IPlatformHandle handle = new PlatformHandle(new IntPtr(0x1234), "HWND");

        var result = handle.ToEsPlatformHandle();

        Assert.Equal(0x1234, (int)result.Handle);
        Assert.Equal("HWND", result.Descriptor);
        Assert.True(result.IsValid);
    }

    [Fact]
    internal void EsPlatformHandle_Avaloniaハンドルへ変換する()
    {
        var es = EsWindow.FromHandle(0x1234, "HWND");

        var result = es.ToPlatformHandle();

        Assert.Equal(new IntPtr(0x1234), result.Handle);
        Assert.Equal("HWND", result.HandleDescriptor);
    }

    [Fact]
    internal void EsPlatformHandle_記述子nullは空文字として変換する()
    {
        var es = EsWindow.FromHandle(0x1234);

        var result = es.ToPlatformHandle();

        Assert.Equal("", result.HandleDescriptor);
    }
}
