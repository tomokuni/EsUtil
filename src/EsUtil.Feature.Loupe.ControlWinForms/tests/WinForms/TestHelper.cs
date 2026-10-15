using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Imaging;
using DrawingColor = System.Drawing.Color;

namespace EsUtil.Feature.Loupe.WinForms.Tests;

/// <summary>テストで共用するユーティリティ。</summary>
internal static class TestHelper
{
    /// <summary>指定サイズの画像を生成する（不透明の単色で塗りつぶし）。</summary>
    /// <param name="width">幅（ピクセル）。</param>
    /// <param name="height">高さ（ピクセル）。</param>
    /// <param name="color">塗りつぶす色。</param>
    /// <returns>生成した画像（読み取りの面）。 破棄は呼び出し側。</returns>
    /// <remarks>
    /// 生成した画像は書き込まず読むだけ（ルーペの <c>Source</c> 等へ渡す）であるため、凍結して 0 コピーで<br/>
    /// 読み取り専用の面へ渡す（書き換え可能な実体はその場で解放する）。
    /// </remarks>
    public static EsReadOnlyBitmap CreateBitmap(int width, int height, DrawingColor color)
    {
        var bitmap = EsBitmap.Create(width, height, color);
        bitmap.SetReadOnly();
        var read = EsReadOnlyBitmap.FromBitmap(bitmap);
        bitmap.Dispose();
        return read;
    }

    /// <summary>元画像の中央を区別できるテスト画像を生成する（中心 1 画素のみ別色）。</summary>
    /// <param name="size">一辺のサイズ（ピクセル）。 奇数を推奨。</param>
    /// <param name="background">背景色。</param>
    /// <param name="center">中心画素の色。</param>
    /// <returns>生成した画像（読み取りの面）。 破棄は呼び出し側。</returns>
    public static EsReadOnlyBitmap CreateBitmapWithCenter(int size, DrawingColor background, DrawingColor center)
    {
        var bitmap = EsBitmap.Create(size, size, background);
        bitmap.SetPixel(new EsPointInt(size / 2, size / 2), center);
        bitmap.SetReadOnly();
        var read = EsReadOnlyBitmap.FromBitmap(bitmap);
        bitmap.Dispose();
        return read;
    }

    /// <summary>列と行で値が変わるテスト画像を生成する（位置と行のずれを検出する）。</summary>
    /// <param name="width">幅（ピクセル）。</param>
    /// <param name="height">高さ（ピクセル）。</param>
    /// <returns>生成した画像（読み取りの面）。 破棄は呼び出し側。</returns>
    /// <remarks>
    /// 画素は B = X、G = Y、R = 0、A = 255 であるため、描画先の画素から元画像のどの座標が写ったかを特定できる<br/>
    /// （行幅の渡し方が誤っていると 2 行目以降がずれる）。
    /// </remarks>
    public static EsReadOnlyBitmap CreatePositionMap(int width, int height)
    {
        var bitmap = EsBitmap.Create(width, height);
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                bitmap.SetPixel(x, y, b: (byte)x, g: (byte)y, r: 0, a: 255);
            }
        }

        bitmap.SetReadOnly();
        var read = EsReadOnlyBitmap.FromBitmap(bitmap);
        bitmap.Dispose();
        return read;
    }
}
