using EsUtil.Platform.Core.Imaging;

namespace EsUtil.Ui.AvaloniaUI.Annotation.Sample.Imaging;

/// <summary>動作確認用の画像を生成する（外部ファイルを必要としない）。</summary>
/// <remarks>
/// <b>4 つの UI 版で同じ画像を使う</b>（同じ画素・同じマーカー・同じグリッド）。<br/>
/// 注釈の座標が元画像ピクセルであることを確かめられるよう、マス目と四隅のマーカーを入れる。
/// </remarks>
internal static class TestImageFactory
{
    /// <summary>動作確認用の画像を生成する。</summary>
    /// <returns>生成した画像。 呼び出し側が所有する。</returns>
    public static EsBitmap Create()
    {
        const int width = 960;
        const int height = 640;
        const int cell = 80;

        var image = EsBitmap.CreateFilled(width, height, span =>
        {
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var checker = ((x / cell) + (y / cell)) % 2 == 0;
                    var offset = ((y * width) + x) * 4;
                    span[offset] = (byte)(checker ? 200 : 120);
                    span[offset + 1] = (byte)(checker ? 190 : 140);
                    span[offset + 2] = (byte)(checker ? 170 : 160);
                    span[offset + 3] = 255;
                }
            }
        });

        // 四隅のマーカー（座標の向きを確かめる）
        for (var y = 0; y < 24; y++)
        {
            for (var x = 0; x < 24; x++)
            {
                image.SetPixel(x, y, 220, 40, 40, 255);
                image.SetPixel(width - 1 - x, y, 40, 160, 60, 255);
                image.SetPixel(x, height - 1 - y, 40, 80, 220, 255);
                image.SetPixel(width - 1 - x, height - 1 - y, 220, 160, 40, 255);
            }
        }

        return image;
    }
}
