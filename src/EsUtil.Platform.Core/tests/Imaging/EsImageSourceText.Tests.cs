using EsUtil.Platform.Core.Imaging;
using Xunit;

namespace EsUtil.Platform.Core.Tests.Imaging;

/// <summary><see cref="EsImageSourceText"/> のテスト（寸法の文字列化の単一所有）。</summary>
/// <remarks>
/// 書式（<c>幅x高さ</c>）と「未設定は <c>null</c>」の規則を検証する。<br/>
/// 入力は <see cref="IEsImageSource"/> の実装（<see cref="EsBitmap"/>）を使う（UI の表示アダプタも同じ入口を使う）。
/// </remarks>
public class EsImageSourceTextTests
{
    /// <summary>検証用のビットマップを生成する。</summary>
    /// <param name="width">幅（ピクセル）。</param>
    /// <param name="height">高さ（ピクセル）。</param>
    /// <returns>生成したビットマップ。</returns>
    private static EsBitmap Create(int width, int height) => EsBitmap.Create(width, height);

    [Fact]
    internal void ToSizeText_幅x高さ形式で返す()
    {
        using var bmp = Create(1920, 1080);

        Assert.Equal("1920x1080", EsImageSourceText.ToSizeText(bmp));
    }

    [Fact]
    internal void ToSizeText_0サイズでも形式は保つ()
    {
        using var bmp = Create(0, 0);

        Assert.Equal("0x0", EsImageSourceText.ToSizeText(bmp));
    }

    [Fact]
    internal void ToSizeText_未設定はnullを表す文字列を返す()
    {
        Assert.Equal("null", EsImageSourceText.ToSizeText(null));
    }

    [Fact]
    internal void ToSizeText_画像源の面からも同じ寸法を返す()
    {
        using var bmp = Create(640, 480);
        IEsImageSource source = bmp;

        Assert.Equal("640x480", EsImageSourceText.ToSizeText(source));
    }
}
