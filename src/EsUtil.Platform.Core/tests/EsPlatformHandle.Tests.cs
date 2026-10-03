using Xunit;

namespace EsUtil.Platform.Core.Tests;

/// <summary><see cref="EsPlatformHandle"/> の純粋計算テスト（UI 非依存・並列実行）。</summary>
/// <remarks>
/// 値型の等価性・有効判定のみを検証するため、UI フレームワークの初期化に依存せず <c>[Fact]</c> で並列実行する。
/// </remarks>
public class EsPlatformHandleTests
{
    [Fact]
    public void IsValid_ハンドル値が0以外なら真()
    {
        var handle = new EsPlatformHandle(0x1234, "HWND");

        Assert.True(handle.IsValid);
    }

    [Fact]
    public void IsValid_ハンドル値が0なら偽()
    {
        var handle = new EsPlatformHandle(0, "HWND");

        Assert.False(handle.IsValid);
    }

    [Fact]
    public void 既定値はハンドル値0と記述子null()
    {
        var handle = default(EsPlatformHandle);

        Assert.Equal(0, handle.Handle);
        Assert.Null(handle.Descriptor);
        Assert.False(handle.IsValid);
    }

    [Fact]
    public void レコード等価性で同一の値は一致する()
    {
        var a = new EsPlatformHandle(0x1234, "HWND");
        var b = new EsPlatformHandle(0x1234, "HWND");

        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void 記述子の違いは等価でない()
    {
        var a = new EsPlatformHandle(0x1234, "HWND");
        var b = new EsPlatformHandle(0x1234, "XID");

        Assert.NotEqual(a, b);
    }
}
