using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Imaging;
using Xunit;
using DrawingColor = System.Drawing.Color;
using EsUtil.Feature.Annotation.Core.Defaults;
using EsUtil.Feature.Annotation.Presenter.Editor;

namespace EsUtil.Ui.MewUI.Annotation.Tests;

/// <summary><see cref="AnnotationLayerControl"/> のテスト。</summary>
/// <remarks>
/// 追加・削除・元に戻す・やり直しの規則はコア（<see cref="AnnotationDocument"/> と <c>AnnotationHistory</c>）が
/// 単一所有するため、ここでは<b>コントロールがコアへ正しく繋いでいること</b>と、
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
        var layer = CreateLayer(200, 120);

        var id = layer.AddFrame(new EsRect(20, 10, 80, 60));

        Assert.Single(layer.Document.Items);
        Assert.Equal(id, layer.SelectedId);
        Assert.IsType<FrameAnnotation>(layer.Selected);
    }

    /// <summary>追加した枠線を元に戻すと消え、やり直すと戻る。</summary>
    [Fact]
    internal void 元に戻すとやり直しで文書が復元される()
    {
        var layer = CreateLayer(200, 120);
        layer.AddFrame(new EsRect(20, 10, 80, 60));

        Assert.True(layer.CanUndo);
        Assert.True(layer.Undo());
        Assert.Empty(layer.Document.Items);

        Assert.True(layer.CanRedo);
        Assert.True(layer.Redo());
        Assert.Single(layer.Document.Items);
    }

    /// <summary>吹き出しを追加すると本文が入る。</summary>
    [Fact]
    internal void 吹き出しを追加すると本文が入る()
    {
        var layer = CreateLayer(200, 120);

        layer.AddBubble(new EsRect(10, 10, 100, 60), "本文");

        var bubble = Assert.IsType<BubbleAnnotation>(Assert.Single(layer.Document.Items));
        Assert.Equal("本文", bubble.Text.PlainText);
    }

    /// <summary>文書を差し替えると変更が通知される（ホストが保存の要否を決める）。</summary>
    [Fact]
    internal void 注釈を追加すると変更が通知される()
    {
        var layer = CreateLayer(200, 120);
        var raised = 0;
        layer.DocumentChanged += (_, _) => raised++;

        layer.AddFrame(new EsRect(20, 10, 80, 60));

        Assert.Equal(1, raised);
    }

    /// <summary>合成画像は元画像と同じ寸法で、枠線の色を含む。</summary>
    [Fact]
    internal void 合成画像は元画像と同じ寸法で枠線を含む()
    {
        var layer = CreateLayer(200, 120);
        layer.AddFrame(new EsRect(20, 10, 80, 60));

        using var composed = layer.RenderComposedImage();

        Assert.NotNull(composed);
        Assert.Equal(200, composed.Width);
        Assert.Equal(120, composed.Height);
        Assert.True(CountPixels(composed, s_borderColor) > 100, "枠線の画素が見つかりません。");
    }

    /// <summary>注釈が無いときの合成画像は元画像の寸法で作られる。</summary>
    [Fact]
    internal void 注釈が無い合成画像は元画像の寸法になる()
    {
        var layer = CreateLayer(64, 32);

        using var composed = layer.RenderComposedImage();

        Assert.NotNull(composed);
        Assert.Equal(64, composed.Width);
        Assert.Equal(32, composed.Height);
    }

    /// <summary>選択中の注釈を削除でき、元に戻すと復元される。</summary>
    [Fact]
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
    [Fact]
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
    [Fact]
    internal void 外観は安全域へ補正される()
    {
        var layer = CreateLayer(200, 120);
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
        var layer = CreateLayer(200, 120);
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
        var layer = CreateLayer(200, 120);
        layer.AddBubble(new EsRect(10, 10, 200, 80), "abcd");

        layer.TextSelectionRange = (1, 2);
        Assert.True(layer.ApplyTextStyle(AnnotationDefaults.TextStyle with { IsBold = true }));

        var text = Assert.IsType<BubbleAnnotation>(layer.Selected).Text;
        Assert.False(text.StyleAt(0).IsBold);
        Assert.True(text.StyleAt(1).IsBold);
        Assert.True(text.StyleAt(2).IsBold);
        Assert.False(text.StyleAt(3).IsBold);
    }

    /// <summary>書式バー用のタブは「代表色」の 1 つ（ピッカーは別のフラグで出す）。</summary>
    [Fact]
    internal void 書式バーのタブは代表色の1つ()
    {
        var tab = AnnotationPalette.CreatePaletteTab();

        Assert.Equal(AnnotationPalette.PaletteTabTitle, tab.Title);
        Assert.Equal(AnnotationPaletteCatalog.RepresentativeColors.Count, tab.Colors.Count);
    }

    /// <summary>指定サイズの画像を持つレイヤーを作る（枠線の色は <see cref="s_borderColor"/>）。</summary>
    /// <param name="width">幅（ピクセル）。</param>
    /// <param name="height">高さ（ピクセル）。</param>
    /// <returns>生成したレイヤー。</returns>
    private static AnnotationLayerControl CreateLayer(int width, int height)
    {
        _ = TestHelper.Factory;

        var layer = new AnnotationLayerControl
        {
            Source = TestHelper.CreateBitmap(width, height, DrawingColor.White),
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
