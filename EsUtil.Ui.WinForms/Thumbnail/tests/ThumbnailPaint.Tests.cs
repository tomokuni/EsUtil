using System.Reflection;
using Xunit;

namespace EsUtil.Ui.WinForms.Thumbnail.Tests;

/// <summary>一覧の描画（画像・名前・影）を画素で検証するテスト。</summary>
/// <remarks>
/// 画面を持たない環境でも <see cref="ThumbnailControl"/> の描画処理を直接呼べるため、
/// <see cref="Bitmap"/> 上の実寸の画素を検査する（ウィンドウを必要としない）。
/// </remarks>
public class ThumbnailPaintTests
{
    /// <summary>一覧を描画し、指定の矩形の画素を数える。</summary>
    /// <param name="control">対象の一覧。</param>
    /// <param name="width">描画先の幅。</param>
    /// <param name="height">描画先の高さ。</param>
    /// <param name="match">数える画素の条件。</param>
    /// <returns>条件に一致した画素の数。</returns>
    private static int CountPixels(ThumbnailControl control, int width, int height, Func<Color, bool> match)
    {
        using var target = new Bitmap(width, height);
        using (var graphics = Graphics.FromImage(target))
        {
            var args = new PaintEventArgs(graphics, new Rectangle(0, 0, width, height));
            typeof(ThumbnailControl)
                .GetMethod("OnPaint", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(control, [args]);
        }

        var count = 0;
        for (var y = 0; y < target.Height; y++)
        {
            for (var x = 0; x < target.Width; x++)
            {
                if (match(target.GetPixel(x, y)))
                {
                    count++;
                }
            }
        }

        return count;
    }

    /// <summary>画像が項目の画像の矩形へ描かれることを検証する。</summary>
    [Fact]
    internal void 画像を項目の矩形へ描く()
    {
        using var control = new ThumbnailControl { ImageWidth = 96, Width = 400, Height = 300 };
        control.Add(null, "a");
        using var image = new Bitmap(64, 48);
        using (var graphics = Graphics.FromImage(image))
        {
            graphics.Clear(Color.FromArgb(255, 255, 0, 0));
        }

        control.SetImage(0, image);
        control.RefreshLayout();

        var red = CountPixels(control, 400, 300, static pixel => pixel.R > 200 && pixel.G < 60 && pixel.B < 60);

        // 画像は 96xDIP へ引き伸ばして描かれる（幅 96 × 高さ 72 の範囲が赤になる）
        Assert.True(red > 0, "画像が描かれていない");
        Assert.True(red > 96 * 48, $"描かれた範囲が小さすぎる（{red} 画素）");
    }

    /// <summary>画像を差し替えると新しい画像が描かれることを検証する。</summary>
    [Fact]
    internal void 差し替えた画像を描く()
    {
        using var control = new ThumbnailControl { ImageWidth = 96, Width = 400, Height = 300 };
        control.Add(null, "a");
        control.RefreshLayout();

        // 画像の所有権は一覧へ移る（テストから破棄しない）
        var red = new Bitmap(64, 48);
        using (var graphics = Graphics.FromImage(red))
        {
            graphics.Clear(Color.FromArgb(255, 255, 0, 0));
        }

        control.SetImage(0, red);

        using var blue = new Bitmap(64, 48);
        using (var graphics = Graphics.FromImage(blue))
        {
            graphics.Clear(Color.FromArgb(255, 0, 0, 255));
        }

        control.SetImage(0, blue);

        var bluePixels = CountPixels(control, 400, 300, static pixel => pixel.B > 200 && pixel.R < 60);
        Assert.True(bluePixels > 0, "差し替えた画像が描かれていない");
    }
}
