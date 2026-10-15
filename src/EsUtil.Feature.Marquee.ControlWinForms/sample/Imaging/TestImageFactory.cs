using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace EsUtil.Feature.Marquee.WinForms.Sample.Imaging;

/// <summary>サンプルで下地として表示する検証用の画像を作る。</summary>
/// <remarks>
/// 選択オーバーレイを重ねる対象（画像）を用意するために使う。<br/>
/// 単色ではなく格子と目印を描くのは、選択矩形の位置ずれ（DPI の換算誤り）を目で見つけられるようにするためである。
/// </remarks>
internal static class TestImageFactory
{
    /// <summary>格子線の間隔（画素）。</summary>
    private const int GridStep = 100;

    /// <summary>検証用の画像を作る。</summary>
    /// <param name="width">幅（画素）。</param>
    /// <param name="height">高さ（画素）。</param>
    /// <returns>下地として使う画像。</returns>
    public static Bitmap Create(int width, int height)
    {
        var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(bitmap);

        using (var background = new LinearGradientBrush(
            new Rectangle(0, 0, width, height),
            Color.FromArgb(255, 24, 40, 72),
            Color.FromArgb(255, 16, 96, 96),
            35.0f))
        {
            graphics.FillRectangle(background, 0, 0, width, height);
        }

        using (var grid = new Pen(Color.FromArgb(56, 255, 255, 255)))
        {
            for (var x = GridStep; x < width; x += GridStep)
            {
                graphics.DrawLine(grid, x, 0, x, height);
            }

            for (var y = GridStep; y < height; y += GridStep)
            {
                graphics.DrawLine(grid, 0, y, width, y);
            }
        }

        // 目印（位置合わせの基準になる同心円）
        using (var marker = new Pen(Color.FromArgb(160, 255, 210, 96), 3.0f))
        {
            for (var radius = 60; radius <= 180; radius += 40)
            {
                graphics.DrawEllipse(marker, (width / 2) - radius, (height / 2) - radius, radius * 2, radius * 2);
            }
        }

        using (var label = new Font(SystemFonts.MessageBoxFont?.FontFamily ?? Control.DefaultFont.FontFamily, 20.0f))
        using (var foreground = new SolidBrush(Color.FromArgb(200, 255, 255, 255)))
        {
            graphics.DrawString("Marquee (WinForms) sample image", label, foreground, 24.0f, 24.0f);
        }

        return bitmap;
    }
}
