using EsUtil.Ui.WinUI3.Loupe.Internal;
using EsUtil.Platform.Core.Imaging;
using SkiaSharp;

namespace EsUtil.Ui.WinUI3.Loupe.Tests;

/// <summary>テストで共用するユーティリティ。</summary>
/// <remarks>
/// 拡大元の画像はコアの読み取りの面（<see cref="EsReadOnlyBitmap"/>）として用意する<br/>
/// （ルーペの <c>Source</c> が受け取る型はコアの画像だけである）。
/// </remarks>
internal static class TestHelper
{
    /// <summary>指定サイズの画像を生成する（不透明の単色で塗りつぶし）。</summary>
    /// <param name="size">一辺のサイズ（ピクセル）。</param>
    /// <param name="color">塗りつぶす色。</param>
    /// <returns>生成した画像（読み取りの面）。 破棄は呼び出し側。</returns>
    /// <remarks>
    /// 生成した画像は書き込まず読むだけ（ルーペの <c>Source</c> 等へ渡す）であるため、凍結して 0 コピーで<br/>
    /// 読み取り専用の面へ渡す。
    /// </remarks>
    public static EsReadOnlyBitmap CreateBitmap(int size, SKColor color)
    {
        // 変換は複製を返すため、元の SKBitmap は破棄する
        using var bitmap = new SKBitmap(size, size);
        bitmap.Erase(color);

        using var created = bitmap.ToEsBitmap();
        created.SetReadOnly();
        return EsReadOnlyBitmap.FromBitmap(created);
    }

    /// <summary>元画像の中央を区別できるテスト画像を生成する（中心 1 画素のみ別色）。</summary>
    /// <param name="size">一辺のサイズ（ピクセル）。 奇数を推奨。</param>
    /// <param name="background">背景色。</param>
    /// <param name="center">中心画素の色。</param>
    /// <returns>生成した画像（読み取りの面）。 破棄は呼び出し側。</returns>
    public static EsReadOnlyBitmap CreateBitmapWithCenter(int size, SKColor background, SKColor center)
    {
        // 変換は複製を返すため、元の SKBitmap は破棄する
        using var bitmap = new SKBitmap(size, size);
        bitmap.Erase(background);
        bitmap.SetPixel(size / 2, size / 2, center);

        using var created = bitmap.ToEsBitmap();
        created.SetReadOnly();
        return EsReadOnlyBitmap.FromBitmap(created);
    }
}
