using System.Runtime.Versioning;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Coord;
using EsUtil.Platform.Core.Native.Win;
using Xunit;

namespace EsUtil.Platform.Core.Tests.Native;

/// <summary><see cref="WinCaptureService"/> の実機テスト（GDI の DIB と <c>BitBlt</c> で画面を取得する）。</summary>
/// <remarks>
/// <b>実機でしか検証できない理由</b>: 画面のキャプチャは OS のデスクトップへ問い合わせるため、
/// ヘッドレス環境では動かない。 Win32 の宣言を CsWin32 の生成へ移した後も
/// <b>DIB の作成・ビットマップの選択・BitBlt・後始末が成立する</b>ことを実測で確かめる。<br/>
/// <b>画素の内容は検証しない</b>: 画面の内容は環境によって変わる（ロック画面・黒画面もあり得る）。
/// 検証するのは<b>要求した寸法の画像が返り、例外にならないこと</b>、および<b>資源が解放されること</b>
/// （同じ呼び出しを繰り返せること）。<br/>
/// Win32 の資源を扱うため <see cref="Win32Group"/> で直列化する。
/// </remarks>
[SupportedOSPlatform("windows10.0.17763")]
[Collection("Win32")]
public class WinCaptureServiceTests
{
    /// <summary>指定した領域が要求どおりの寸法で取得できることを検証する。</summary>
    /// <param name="width">幅（ピクセル）。</param>
    /// <param name="height">高さ（ピクセル）。</param>
    [Theory]
    [InlineData(1, 1)]
    [InlineData(16, 16)]
    [InlineData(64, 32)]
    internal void CaptureArea_要求した寸法の画像を返す(int width, int height)
    {
        var service = new WinCaptureService();

        using var image = service.CaptureArea(new EsRectInt(0, 0, width, height));

        Assert.Equal(width, image.Width);
        Assert.Equal(height, image.Height);
    }

    /// <summary>負の座標（仮想スクリーンの左端）でも取得できることを検証する。</summary>
    /// <remarks>プライマリ モニタが (0,0) にない環境では、画面の一部が負の座標にある。</remarks>
    [Fact]
    internal void CaptureArea_負の座標でも取得できる()
    {
        var service = new WinCaptureService();

        using var image = service.CaptureArea(new EsRectInt(-8, -8, 16, 16));

        Assert.Equal(16, image.Width);
        Assert.Equal(16, image.Height);
    }

    /// <summary>取得した画素が不透明（アルファ 255）であることを検証する。</summary>
    /// <remarks>画面のピクセルはアルファを持たないため、コピー後に不透明へ揃える。</remarks>
    [Fact]
    internal void CaptureArea_アルファを不透明へ揃える()
    {
        var service = new WinCaptureService();

        using var image = service.CaptureArea(new EsRectInt(0, 0, 4, 4));

        var (_, _, _, alpha) = image.GetPixel(0, 0);
        Assert.Equal(255, alpha);
    }

    /// <summary>同じ呼び出しを繰り返せることを検証する（GDI の資源が解放されている）。</summary>
    /// <remarks>
    /// 解放しないとデバイス コンテキストとビットマップが残り、繰り返すうちに GDI の上限へ達して失敗する。
    /// 3 回の繰り返しでは上限に達しないため、<b>解放漏れは検出できない</b>が、
    /// <b>選択の戻し忘れ（選択中のビットマップは削除できない）で 2 回目が失敗する</b>ことは検出できる。
    /// </remarks>
    [Fact]
    internal void CaptureArea_繰り返し呼べる()
    {
        var service = new WinCaptureService();

        for (var i = 0; i < 3; i++)
        {
            using var image = service.CaptureArea(new EsRectInt(0, 0, 8, 8));
            Assert.Equal(8, image.Width);
        }
    }

    /// <summary>仮想スクリーン全体が取得できることを検証する。</summary>
    [Fact]
    internal void CaptureVirtualScreen_仮想スクリーンを取得する()
    {
        var service = new WinCaptureService();

        using var image = service.CaptureVirtualScreen(out var origin);

        Assert.NotNull(image);
        Assert.True(image!.Width > 0);
        Assert.True(image.Height > 0);

        // 原点は仮想スクリーンの左上（プライマリ モニタが (0,0) なら 0、左に置いていれば負値）
        var bounds = EsWindowPlacementMath.GetVirtualScreenBounds();
        Assert.Equal(bounds.X, origin.X);
        Assert.Equal(bounds.Y, origin.Y);
    }

    /// <summary>幅が 0 の要求は引数の例外になることを検証する。</summary>
    [Fact]
    internal void CaptureArea_幅が0なら例外()
    {
        var service = new WinCaptureService();

        Assert.Throws<ArgumentOutOfRangeException>(() => service.CaptureArea(new EsRectInt(0, 0, 0, 8)));
    }

    /// <summary>高さが 0 の要求は引数の例外になることを検証する。</summary>
    [Fact]
    internal void CaptureArea_高さが0なら例外()
    {
        var service = new WinCaptureService();

        Assert.Throws<ArgumentOutOfRangeException>(() => service.CaptureArea(new EsRectInt(0, 0, 8, 0)));
    }
}
