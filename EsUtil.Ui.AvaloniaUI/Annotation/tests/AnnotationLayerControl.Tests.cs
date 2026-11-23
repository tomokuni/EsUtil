using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using EsUtil.Platform.Core.Geometry;
using Xunit;

namespace EsUtil.Ui.AvaloniaUI.Annotation.Tests;

/// <summary><see cref="AnnotationLayerControl"/> が保存用の合成画像を作れることを検証する。</summary>
/// <remarks>
/// 保存（<c>*.annotated.*</c>）は表示倍率に依らず元画像ピクセルの 1:1 で描く必要がある。<br/>
/// 画像の表示（Fit）と注釈の合成が同じ寸法・同じ座標系で行われることをヘッドレスで確かめる。
/// </remarks>
[Collection("AvaloniaHeadless")]
public class AnnotationLayerControlTests
{
    /// <summary>画像と注釈を元画像ピクセルの 1:1 で合成できることを検証する。</summary>
    /// <remarks>
    /// 注釈は画像の内側へ描かれ、元画像の外へは広がらない（合成画像の寸法は元画像と同じ）。
    /// 枠線を 1 つ描いたとき、背景と異なる画素ができることも確かめる（注釈が焼き込まれる）。
    /// </remarks>
    [AvaloniaFact]
    internal void 画像と注釈を元画像ピクセルで合成できる()
    {
        const int width = 64;
        const int height = 48;

        // 白い画像（IImage）を用意する
        using var source = new RenderTargetBitmap(new PixelSize(width, height), new Vector(96, 96));
        using (var context = source.CreateDrawingContext())
        {
            context.FillRectangle(Brushes.White, new Rect(0, 0, width, height));
        }

        var layer = new AnnotationLayerControl
        {
            Source = source,
            SourcePixelWidth = width,
            SourcePixelHeight = height,
            Document = new AnnotationDocument(width, height)
                .Add(AnnotationGeometry.CreateFrame(
                    new EsRect(8, 8, 24, 16),
                    new AnnotationAppearance(
                        System.Drawing.Color.FromArgb(255, 255, 0, 0),
                        4,
                        System.Drawing.Color.Transparent,
                        0),
                    new EsSize(width, height))),
        };

        using var composed = layer.RenderComposedImage();

        Assert.Equal(new PixelSize(width, height), composed.PixelSize);

        // 枠線が描かれている（背景の白だけではない）
        using var target = new WriteableBitmap(composed.PixelSize, new Vector(96, 96));
        using var buffer = target.Lock();
        composed.CopyPixels(buffer);
        var stride = buffer.RowBytes;
        var row = new byte[stride];
        var hasAnnotationPixel = false;
        for (var y = 0; y < height && !hasAnnotationPixel; y++)
        {
            System.Runtime.InteropServices.Marshal.Copy(buffer.Address + (y * stride), row, 0, stride);
            for (var x = 0; x < width; x++)
            {
                var offset = x * 4;
                if (row[offset] < 200 || row[offset + 1] < 200 || row[offset + 2] < 200)
                {
                    hasAnnotationPixel = true;
                    break;
                }
            }
        }

        Assert.True(hasAnnotationPixel, "注釈が合成画像へ描かれていない");
    }
}
