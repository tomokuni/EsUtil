using EsUtil.Platform.Core.Imaging;

namespace EsUtil.Feature.Loupe.MewUI.Sample.Imaging;

/// <summary>ルーペの動作確認用テスト画像を生成する。</summary>
internal static class TestImageFactory
{
    /// <summary>グラデーション＋グリッド＋マーカー付きのテスト画像を生成する。</summary>
    /// <param name="width">画像の幅（ピクセル）。</param>
    /// <param name="height">画像の高さ（ピクセル）。</param>
    /// <returns>生成した画像（読み取りの面。 呼び出し側が保持して破棄する）。</returns>
    /// <remarks>
    /// 左上から右下へのグラデーションを背景とし、<br/>
    /// 10px グリッド（拡大ピクセル検証用）・50px グリッド（倍率計測用）・色付き円マーカー（ズーム確認用）を描画する。<br/>
    /// 画像外の色や十字線・枠線は <c>LoupeControl</c> 側が描画する。<br/>
    /// <b>読み取りの面で返す理由</b>: 生成（ピクセルの描画）が終わった後は書き換えないため、凍結して
    /// 読み取り専用の面へ 0 コピーで渡す（書き換え可能の実体はその場で解放する）。
    /// </remarks>
    public static EsReadOnlyBitmap Create(int width = 800, int height = 600)
    {
        var bitmap = EsBitmap.Create(width, height);

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                // 左上から右下へのグラデーション背景
                var t = (x + y) / (double)(width + height);
                var r = (byte)(t * 200 + 30);
                var g = (byte)((1.0 - ((double)x / width)) * 140 + 50);
                var b = (byte)(((double)y / height) * 180 + 40);

                // 色付きマーカー（円）: ズーム確認用
                if (InCircle(x, y, 120, 110, 45))
                {
                    (r, g, b) = (255, 60, 60);
                }
                else if (InCircle(x, y, 660, 160, 55))
                {
                    (r, g, b) = (80, 255, 80);
                }
                else if (InCircle(x, y, 400, 430, 75))
                {
                    (r, g, b) = (80, 120, 255);
                }

                // 10px グリッド（薄く明るく）: 拡大ピクセル検証用
                if (x % 10 == 0 || y % 10 == 0)
                {
                    r = (byte)Math.Min(255, r + 60);
                    g = (byte)Math.Min(255, g + 60);
                    b = (byte)Math.Min(255, b + 60);
                }

                // 50px グリッド（濃く暗く）: 倍率計測用
                if (x % 50 == 0 || y % 50 == 0)
                {
                    r = (byte)Math.Max(0, r - 100);
                    g = (byte)Math.Max(0, g - 100);
                    b = (byte)Math.Max(0, b - 100);
                }

                bitmap.SetPixel(x, y, b, g, r, 255);
            }
        }

        // 生成（ピクセルの描画）はここで終わり、以降は読むだけであるため、凍結して読み取り専用の面へ渡す
        bitmap.SetReadOnly();
        var read = EsReadOnlyBitmap.FromBitmap(bitmap);
        bitmap.Dispose();
        return read;
    }

    /// <summary>指定円の内側かどうかを判定する。</summary>
    /// <param name="x">X 座標。</param>
    /// <param name="y">Y 座標。</param>
    /// <param name="cx">円の中心 X。</param>
    /// <param name="cy">円の中心 Y。</param>
    /// <param name="radius">円の半径。</param>
    /// <returns>円の内側の場合は true。</returns>
    private static bool InCircle(int x, int y, int cx, int cy, int radius)
    {
        var dx = x - cx;
        var dy = y - cy;
        return (dx * dx) + (dy * dy) <= radius * radius;
    }
}
