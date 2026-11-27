using Avalonia.Headless.XUnit;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Imaging;
using Xunit;
using EsUtil.Feature.Annotation.Core.Defaults;
using EsUtil.Feature.Annotation.Core.Geometry;
using EsUtil.Feature.StyledText.Core.Defaults;

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

        // 白い画像（コアの画像）を用意する
        using var bitmap = EsBitmap.Create(width, height, System.Drawing.Color.White);
        using var source = EsReadOnlyBitmap.FromBitmap(bitmap);

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

        Assert.Equal(width, composed.Width);
        Assert.Equal(height, composed.Height);

        // 枠線が描かれている（背景の白だけではない）
        var hasAnnotationPixel = false;
        for (var y = 0; y < height && !hasAnnotationPixel; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var (b, g, r, _) = composed.GetPixel(x, y);
                if (r < 200 || g < 200 || b < 200)
                {
                    hasAnnotationPixel = true;
                    break;
                }
            }
        }

        Assert.True(hasAnnotationPixel, "注釈が合成画像へ描かれていない");
    }

    /// <summary>追加した注釈を元に戻すと消え、やり直すと戻る。</summary>
    [AvaloniaFact]
    internal void 元に戻すとやり直しで文書が復元される()
    {
        var layer = CreateLayer(200, 120);
        layer.AddFrame(new EsRect(20, 10, 80, 60));

        Assert.True(layer.Undo());
        Assert.Empty(layer.Document.Items);

        Assert.True(layer.Redo());
        Assert.Single(layer.Document.Items);
    }

    /// <summary>選択中の注釈を削除でき、元に戻すと復元される。</summary>
    [AvaloniaFact]
    internal void 選択中の注釈を削除できる()
    {
        var layer = CreateLayer(200, 120);
        layer.AddFrame(new EsRect(20, 10, 80, 60));

        Assert.True(layer.DeleteSelected());
        Assert.Empty(layer.Document.Items);
        Assert.Null(layer.SelectedId);

        Assert.True(layer.Undo());
        Assert.Single(layer.Document.Items);
    }

    /// <summary>本文の余白は安全域へ補正される。</summary>
    [AvaloniaFact]
    internal void 本文の余白は安全域へ補正される()
    {
        var layer = CreateLayer(200, 120);
        layer.AddBubble(new EsRect(10, 10, 100, 60), "本文");

        Assert.True(layer.SetSelectedBodyPadding(AnnotationLimits.BodyPaddingMax + 1000));

        Assert.Equal(
            AnnotationLimits.BodyPaddingMax,
            Assert.IsType<BubbleAnnotation>(layer.Selected).BodyPadding);
    }

    /// <summary>外観は安全域へ補正される（外枠と角丸）。</summary>
    [AvaloniaFact]
    internal void 外観は安全域へ補正される()
    {
        var layer = CreateLayer(200, 120);
        layer.AddFrame(new EsRect(20, 10, 80, 60));

        Assert.True(layer.TransformSelectedAppearance(
            appearance => appearance with { BorderThickness = 0, CornerRadius = double.NaN }));

        var item = layer.Selected;
        Assert.NotNull(item);
        Assert.Equal(AnnotationLimits.BorderThicknessMin, item.Appearance.BorderThickness);
        Assert.Equal(0, item.Appearance.CornerRadius);
    }

    /// <summary>しっぽの先端だけが動き、幅は安全域へ補正される。</summary>
    [AvaloniaFact]
    internal void しっぽの先端だけが動く()
    {
        var layer = CreateLayer(200, 120);
        layer.AddBubble(new EsRect(10, 10, 100, 60), "本文");
        var before = Assert.IsType<BubbleAnnotation>(layer.Selected).Bounds;

        Assert.True(layer.TransformSelectedTail(tail => tail with { Tip = new EsPoint(90, 20), Width = 0 }));

        var after = Assert.IsType<BubbleAnnotation>(layer.Selected);
        Assert.Equal(before, after.Bounds);
        Assert.Equal(new EsPoint(90, 20), after.Tail.Tip);
        Assert.Equal(AnnotationLimits.TailWidthMin, after.Tail.Width);
    }

    /// <summary>書式は選択した範囲だけに効く（キャレットだけの状態では変えない）。</summary>
    [AvaloniaFact]
    internal void 書式は選択した範囲だけに効く()
    {
        var layer = CreateLayer(200, 120);
        layer.AddBubble(new EsRect(10, 10, 200, 80), "abcd");

        layer.Select(1, 2);
        Assert.True(layer.ApplyStyle(TextDefaults.DefaultStyle with { IsBold = true }));

        var text = Assert.IsType<BubbleAnnotation>(layer.Selected).Text;
        Assert.False(text.StyleAt(0).IsBold);
        Assert.True(text.StyleAt(1).IsBold);
        Assert.True(text.StyleAt(2).IsBold);
        Assert.False(text.StyleAt(3).IsBold);
    }

    /// <summary>指定サイズの画像を持つレイヤーを作る。</summary>
    /// <param name="width">幅（ピクセル）。</param>
    /// <param name="height">高さ（ピクセル）。</param>
    /// <returns>生成したレイヤー。</returns>
    private static AnnotationLayerControl CreateLayer(int width, int height) => new()
    {
        SourcePixelWidth = width,
        SourcePixelHeight = height,
        Document = new AnnotationDocument(width, height, "shot.png"),
    };
}
