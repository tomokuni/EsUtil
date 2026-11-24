using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Imaging;
using Xunit;
using DrawingColor = System.Drawing.Color;
using EsUtil.Feature.Annotation.Core.Defaults;
using EsUtil.Feature.StyledText.Core.Defaults;

namespace EsUtil.Ui.WinForms.Annotation.Tests;

/// <summary><see cref="AnnotationLayerControl"/> のテスト（WinForms 版）。</summary>
/// <remarks>
/// 追加・削除・元に戻すの規則はコアが単一所有するため、ここでは<b>コントロールがコアへ正しく繋いでいること</b>と、
/// <b>合成画像が元画像と同じ寸法で注釈を含むこと</b>を確かめる。
/// </remarks>
public class AnnotationLayerControlTests
{
    /// <summary>枠線の色（合成画像の画素で識別するための目印）。</summary>
    private static readonly DrawingColor s_borderColor = DrawingColor.FromArgb(255, 220, 20, 60);

    /// <summary>枠線を追加すると文書へ入り、選択される。</summary>
    [Fact]
    internal void 枠線を追加すると文書へ入り選択される()
    {
        using var layer = CreateLayer(200, 120);

        var id = layer.AddFrame(new EsRect(20, 10, 80, 60));

        Assert.Single(layer.Document.Items);
        Assert.Equal(id, layer.SelectedId);
        Assert.IsType<FrameAnnotation>(layer.Selected);
    }

    /// <summary>追加した枠線を元に戻すと消え、やり直すと戻る。</summary>
    [Fact]
    internal void 元に戻すとやり直しで文書が復元される()
    {
        using var layer = CreateLayer(200, 120);
        layer.AddFrame(new EsRect(20, 10, 80, 60));

        Assert.True(layer.Undo());
        Assert.Empty(layer.Document.Items);
        Assert.True(layer.Redo());
        Assert.Single(layer.Document.Items);
    }

    /// <summary>吹き出しを追加すると本文が入る。</summary>
    [Fact]
    internal void 吹き出しを追加すると本文が入る()
    {
        using var layer = CreateLayer(200, 120);

        layer.AddBubble(new EsRect(10, 10, 100, 60), "本文");

        var bubble = Assert.IsType<BubbleAnnotation>(Assert.Single(layer.Document.Items));
        Assert.Equal("本文", bubble.Text.PlainText);
    }

    /// <summary>合成画像は元画像と同じ寸法で、枠線の色を含む。</summary>
    [Fact]
    internal void 合成画像は元画像と同じ寸法で枠線を含む()
    {
        using var layer = CreateLayer(200, 120);
        layer.AddFrame(new EsRect(20, 10, 80, 60));

        using var composed = layer.RenderComposedImage();

        Assert.NotNull(composed);
        Assert.Equal(200, composed.Width);
        Assert.Equal(120, composed.Height);
        Assert.True(CountPixels(composed, s_borderColor) > 100, "枠線の画素が見つかりません。");
    }

    /// <summary>本文の実寸は文字数と文字サイズに比例する。</summary>
    [Fact]
    internal void 本文の実寸は文字数に比例する()
    {
        var style = TextDefaults.DefaultStyle with { FontSize = 20 };

        var shortSize = AnnotationTextMetrics.Measure("あ", style);
        var longSize = AnnotationTextMetrics.Measure("ああああ", style);

        Assert.True(longSize.Width > shortSize.Width);
        Assert.True(shortSize.Height >= style.FontSize);
    }

    /// <summary>選択中の注釈を削除でき、元に戻すと復元される。</summary>
    [Fact]
    internal void 選択中の注釈を削除できる()
    {
        using var layer = CreateLayer(200, 120);
        layer.AddFrame(new EsRect(20, 10, 80, 60));

        Assert.True(layer.DeleteSelected());
        Assert.Empty(layer.Document.Items);
        Assert.Null(layer.SelectedId);

        Assert.True(layer.Undo());
        Assert.Single(layer.Document.Items);
    }

    /// <summary>本文の余白は安全域へ補正される。</summary>
    [Fact]
    internal void 本文の余白は安全域へ補正される()
    {
        using var layer = CreateLayer(200, 120);
        layer.AddBubble(new EsRect(10, 10, 100, 60), "本文");

        Assert.True(layer.SetSelectedBodyPadding(AnnotationLimits.BodyPaddingMax + 1000));

        Assert.Equal(
            AnnotationLimits.BodyPaddingMax,
            Assert.IsType<BubbleAnnotation>(layer.Selected).BodyPadding);
    }

    /// <summary>外観は安全域へ補正される（外枠と角丸）。</summary>
    [Fact]
    internal void 外観は安全域へ補正される()
    {
        using var layer = CreateLayer(200, 120);
        layer.AddFrame(new EsRect(20, 10, 80, 60));

        Assert.True(layer.UpdateSelectedAppearance(
            appearance => appearance with { BorderThickness = 0, CornerRadius = double.NaN }));

        var item = layer.Selected;
        Assert.NotNull(item);
        Assert.Equal(AnnotationLimits.BorderThicknessMin, item.Appearance.BorderThickness);
        Assert.Equal(0, item.Appearance.CornerRadius);
    }

    /// <summary>しっぽの先端だけが動き、幅は安全域へ補正される。</summary>
    [Fact]
    internal void しっぽの先端だけが動く()
    {
        using var layer = CreateLayer(200, 120);
        layer.AddBubble(new EsRect(10, 10, 100, 60), "本文");
        var before = Assert.IsType<BubbleAnnotation>(layer.Selected).Bounds;

        Assert.True(layer.UpdateSelectedTail(tail => tail with { Tip = new EsPoint(90, 20), Width = 0 }));

        var after = Assert.IsType<BubbleAnnotation>(layer.Selected);
        Assert.Equal(before, after.Bounds);
        Assert.Equal(new EsPoint(90, 20), after.Tail.Tip);
        Assert.Equal(AnnotationLimits.TailWidthMin, after.Tail.Width);
    }

    /// <summary>書式は選択した範囲だけに効く（キャレットだけの状態では変えない）。</summary>
    [Fact]
    internal void 書式は選択した範囲だけに効く()
    {
        using var layer = CreateLayer(200, 120);
        layer.AddBubble(new EsRect(10, 10, 200, 80), "abcd");

        layer.TextSelectionRange = (1, 2);
        Assert.True(layer.ApplyTextStyle(TextDefaults.DefaultStyle with { IsBold = true }));

        var text = Assert.IsType<BubbleAnnotation>(layer.Selected).Text;
        Assert.False(text.StyleAt(0).IsBold);
        Assert.True(text.StyleAt(1).IsBold);
        Assert.True(text.StyleAt(2).IsBold);
        Assert.False(text.StyleAt(3).IsBold);
    }

    /// <summary>本文の編集中も本文を合成画像へ描く（文字が消えない）。</summary>
    /// <remarks>
    /// 本 UI は本文を別の入力欄で表示しない。 編集中に本文の描画を止めると、文字が消えて入力内容を
    /// 確認できなくなる。
    /// </remarks>
    [Fact]
    internal void 本文の編集中も本文を合成画像へ描く()
    {
        var textColor = DrawingColor.FromArgb(255, 0, 0, 255);
        using var layer = CreateLayer(200, 120);
        layer.DraftDefaults = layer.DraftDefaults with
        {
            TextStyle = layer.DraftDefaults.TextStyle with { Color = textColor },
        };
        layer.AddBubble(new EsRect(10, 10, 160, 90), "本文");

        Assert.True(layer.BeginTextEditing());
        using var composed = layer.RenderComposedImage();

        Assert.NotNull(composed);
        Assert.True(CountPixels(composed, textColor) > 0, "編集中に本文が描かれていません。");
    }

    /// <summary>代表色は 40 色ある。</summary>
    [Fact]
    internal void 代表色は40色ある()
    {
        Assert.Equal(40, AnnotationPalette.RepresentativeColors.Count);
    }

    /// <summary>指定サイズの画像を持つレイヤーを作る。</summary>
    /// <param name="width">幅（ピクセル）。</param>
    /// <param name="height">高さ（ピクセル）。</param>
    /// <returns>生成したレイヤー。 破棄は呼び出し側。</returns>
    private static AnnotationLayerControl CreateLayer(int width, int height)
    {
        using var bitmap = EsBitmap.Create(width, height, DrawingColor.White);
        bitmap.SetReadOnly();

        var layer = new AnnotationLayerControl
        {
            Source = EsReadOnlyBitmap.FromBitmap(bitmap),
            SourcePixelWidth = width,
            SourcePixelHeight = height,
            Document = new AnnotationDocument(width, height, "shot.png"),
        };

        layer.DraftDefaults = layer.DraftDefaults with
        {
            FrameAppearance = new AnnotationAppearance(s_borderColor, 6, DrawingColor.Transparent, 0),
        };

        return layer;
    }

    /// <summary>画像の中の指定色の画素数を数える。</summary>
    /// <param name="bitmap">対象の画像。</param>
    /// <param name="color">数える色。</param>
    /// <returns>一致する画素数。</returns>
    private static int CountPixels(EsBitmap bitmap, DrawingColor color)
    {
        var count = 0;
        for (var y = 0; y < bitmap.Height; y++)
        {
            for (var x = 0; x < bitmap.Width; x++)
            {
                var pixel = bitmap.GetPixel(x, y);
                if (pixel.R == color.R && pixel.G == color.G && pixel.B == color.B)
                {
                    count++;
                }
            }
        }

        return count;
    }
}
