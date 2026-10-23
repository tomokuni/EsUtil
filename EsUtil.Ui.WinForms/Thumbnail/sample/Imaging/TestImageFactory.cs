using EsUtil.Platform.Core.Imaging;
using DrawingColor = System.Drawing.Color;

namespace EsUtil.Ui.WinForms.Thumbnail.Sample.Imaging;

/// <summary>動作確認用の画像を生成する（外部ファイルを必要としない）。</summary>
/// <remarks>
/// 画像は <b>コアの型</b>（<see cref="EsBitmap"/>）で作る。 コントロールは自 UI の画像型を受け取るため、<br/>
/// ホストが変換する（このサンプルは変換ブリッジの <c>EsBitmapAdapter</c> を使う）。
/// </remarks>
internal static class TestImageFactory
{
    /// <summary>識別できる模様を持つ画像を生成する。</summary>
    /// <param name="width">幅（ピクセル）。</param>
    /// <param name="height">高さ（ピクセル）。</param>
    /// <param name="index">模様と色を決める番号。</param>
    /// <returns>生成した画像。 呼び出し側が所有する。</returns>
    public static EsBitmap Create(int width, int height, int index)
    {
        var color = DrawingColor.FromArgb(255, (byte)(60 + (index * 37 % 180)), (byte)(90 + (index * 53 % 150)), (byte)(120 + (index * 71 % 120)));
        var image = EsBitmap.Create(width, height, color);

        // 左上へ番号の位置が分かる帯を置く（並び順と選択の確認に使う）
        var band = Math.Max(2, height / 20);
        var bandWidth = Math.Min(width, band * (index + 2));
        for (var y = 0; y < band; y++)
        {
            for (var x = 0; x < bandWidth; x++)
            {
                image.SetPixel(x, y, 255, 255, 255, 255);
            }
        }

        return image;
    }
}