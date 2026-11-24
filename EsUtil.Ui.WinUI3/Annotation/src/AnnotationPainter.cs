using EsUtil.Platform.Core.Geometry;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using DrawingColor = System.Drawing.Color;
using XamlPoint = Windows.Foundation.Point;
using XamlRect = Windows.Foundation.Rect;
using EsUtil.Feature.StyledText;

namespace EsUtil.Ui.WinUI3.Annotation;

/// <summary>注釈の描画計画を XAML の図形と文字へ写す（WinUI 3 版）。</summary>
/// <remarks>
/// <b>計算を持たない</b>: 角丸・塗り・枠線・しっぽの多角形・行の位置は計画（<see cref="AnnotationShapePlan"/>）が
/// 解決済みである（4 つの UI 版で見た目がずれないよう、判断は Core / Presenter が単一所有する）。<br/>
/// <b>文字は要素として置く</b>: WinUI 3 はキャンバスへ文字を描かないため、区間ごとに <see cref="TextBlock"/> を置く
/// （測るエンジンと描くエンジンが一致する）。<br/>
/// <b>斜体で行の高さを変えない</b>: 各 run は行の高さいっぱいの中央へ置く（行の高さは斜体を外して測る）。
/// </remarks>
internal static class AnnotationPainter
{
    /// <summary>注釈の図形と文字をキャンバスへ並べる。</summary>
    /// <param name="canvas">並べる先（子要素はすべて作り直す）。</param>
    /// <param name="plan">描画計画。</param>
    /// <param name="scale">表示倍率（元画像ピクセル → DIP）。</param>
    /// <param name="offsetX">画像の左上 X 座標（DIP）。</param>
    /// <param name="offsetY">画像の左上 Y 座標（DIP）。</param>
    /// <param name="suppressTextId">本文を描かない注釈の識別子（編集中は入力欄が本文を表示する）。</param>
    /// <param name="measureText">文字列と書式から実寸を測る関数。</param>
    internal static void Draw(
        Canvas canvas,
        AnnotationDrawPlan plan,
        double scale,
        double offsetX,
        double offsetY,
        string? suppressTextId,
        Func<string, TextStyle, EsSize> measureText)
    {
        ArgumentNullException.ThrowIfNull(measureText);
        ArgumentNullException.ThrowIfNull(canvas);
        ArgumentNullException.ThrowIfNull(plan);

        canvas.Children.Clear();
        foreach (var shape in plan.Shapes)
        {
            DrawShape(canvas, shape, scale, offsetX, offsetY, suppressTextId, measureText);
        }
    }

    /// <summary>注釈 1 件を描く。</summary>
    /// <param name="canvas">並べる先。</param>
    /// <param name="shape">描画計画。</param>
    /// <param name="scale">表示倍率。</param>
    /// <param name="offsetX">画像の左上 X 座標（DIP）。</param>
    /// <param name="offsetY">画像の左上 Y 座標（DIP）。</param>
    /// <param name="suppressTextId">本文を描かない注釈の識別子。</param>
    /// <param name="measureText">文字列と書式から実寸を測る関数。</param>
    private static void DrawShape(
        Canvas canvas,
        AnnotationShapePlan shape,
        double scale,
        double offsetX,
        double offsetY,
        string? suppressTextId,
        Func<string, TextStyle, EsSize> measureText)
    {
        ArgumentNullException.ThrowIfNull(measureText);
        var stroke = AnnotationTextMetrics.ToBrush(shape.Border);
        var fill = shape.HasFill ? AnnotationTextMetrics.ToBrush(shape.Fill) : null;
        var thickness = Math.Max(0.5, shape.BorderThickness * scale);

        // しっぽは本体より先に置く（本体の塗りと枠線が付け根を隠す）
        if (shape.HasTail)
        {
            var polygon = new Polygon
            {
                Fill = fill,
                Stroke = stroke,
                StrokeThickness = thickness,
                StrokeLineJoin = PenLineJoin.Round,
            };

            foreach (var point in shape.Tail)
            {
                polygon.Points.Add(new XamlPoint((point.X * scale) + offsetX, (point.Y * scale) + offsetY));
            }

            canvas.Children.Add(polygon);
        }

        var body = new Rectangle
        {
            Width = shape.Bounds.Width * scale,
            Height = shape.Bounds.Height * scale,
            RadiusX = shape.CornerRadius * scale,
            RadiusY = shape.CornerRadius * scale,
            Fill = fill,
            Stroke = stroke,
            StrokeThickness = thickness,
        };

        Canvas.SetLeft(body, (shape.Bounds.X * scale) + offsetX);
        Canvas.SetTop(body, (shape.Bounds.Y * scale) + offsetY);
        canvas.Children.Add(body);

        if (string.Equals(shape.Id, suppressTextId, StringComparison.Ordinal))
        {
            return;
        }

        foreach (var line in shape.Lines)
        {
            foreach (var segment in line.Segments)
            {
                var text = CreateText(segment, scale);
                Canvas.SetLeft(text, (segment.Bounds.X * scale) + offsetX);
                Canvas.SetTop(text, (ResolveTextTop(line, segment, measureText) * scale) + offsetY);
                canvas.Children.Add(text);
            }
        }
    }

    /// <summary>区間の文字を置く要素を作る。</summary>
    /// <param name="segment">区間。</param>
    /// <param name="scale">表示倍率。</param>
    /// <returns>文字の要素。</returns>
    private static TextBlock CreateText(TextSegmentPlan segment, double scale)
    {
        var style = segment.Style;
        var text = new TextBlock
        {
            Text = segment.Text,
            FontFamily = new FontFamily(style.FontFamily),
            FontSize = style.FontSize * scale,
            FontWeight = style.IsBold ? FontWeights.Bold : FontWeights.Normal,
            FontStyle = style.IsItalic ? Windows.UI.Text.FontStyle.Italic : Windows.UI.Text.FontStyle.Normal,
            Foreground = AnnotationTextMetrics.ToBrush(style.Color),
            TextWrapping = TextWrapping.NoWrap,
            IsHitTestVisible = false,
        };

        var decorations = Windows.UI.Text.TextDecorations.None;
        if (style.IsUnderline)
        {
            decorations |= Windows.UI.Text.TextDecorations.Underline;
        }

        if (style.IsStrike)
        {
            decorations |= Windows.UI.Text.TextDecorations.Strikethrough;
        }

        if (decorations != Windows.UI.Text.TextDecorations.None)
        {
            text.TextDecorations = decorations;
        }

        return text;
    }

    /// <summary>行の中で区間の文字を置く上端（元画像ピクセル）を求める。</summary>
    /// <param name="line">行。</param>
    /// <param name="segment">区間。</param>
    /// <param name="measureText">文字列と書式から実寸を測る関数。</param>
    /// <returns>上端の Y 座標（行の上端からの中央寄せ）。</returns>
    /// <remarks>
    /// 区間ごとの実寸で縦位置を決めず、行の高さいっぱいの中央へ置く（斜体にしても行の間隔が変わらない）。
    /// </remarks>
    private static double ResolveTextTop(
        TextLinePlan line,
        TextSegmentPlan segment,
        Func<string, TextStyle, EsSize> measureText)
    {
        var runHeight = measureText("Ag", segment.Style with { IsItalic = false }).Height;
        return line.Bounds.Y + ((line.Bounds.Height - runHeight) / 2);
    }

    /// <summary>選択枠（枠とハンドル）を重ねる層へ並べる。</summary>
    /// <param name="canvas">並べる先（子要素はすべて作り直す）。</param>
    /// <param name="borderRects">枠の矩形（DIP）。</param>
    /// <param name="borderColor">枠の色。</param>
    /// <param name="handleRects">ハンドルの矩形（DIP）。</param>
    /// <param name="handleColor">ハンドルの塗り。</param>
    /// <param name="handleBorderColor">ハンドルの枠線の色。</param>
    internal static void DrawSelection(
        Canvas canvas,
        IReadOnlyList<EsRect> borderRects,
        DrawingColor borderColor,
        IReadOnlyList<EsRect>? handleRects,
        DrawingColor handleColor,
        DrawingColor handleBorderColor)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        ArgumentNullException.ThrowIfNull(borderRects);

        canvas.Children.Clear();

        var border = AnnotationTextMetrics.ToBrush(borderColor);
        foreach (var rect in borderRects)
        {
            var line = new Rectangle
            {
                Width = rect.Width,
                Height = rect.Height,
                Fill = border,
            };
            Canvas.SetLeft(line, rect.X);
            Canvas.SetTop(line, rect.Y);
            canvas.Children.Add(line);
        }

        if (handleRects is null)
        {
            return;
        }

        var fill = AnnotationTextMetrics.ToBrush(handleColor);
        var stroke = AnnotationTextMetrics.ToBrush(handleBorderColor);
        foreach (var rect in handleRects)
        {
            var handle = new Rectangle
            {
                Width = rect.Width,
                Height = rect.Height,
                Fill = fill,
                Stroke = stroke,
                StrokeThickness = 1,
            };

            Canvas.SetLeft(handle, rect.X);
            Canvas.SetTop(handle, rect.Y);
            canvas.Children.Add(handle);
        }
    }

    /// <summary>矩形を XAML の矩形へ変換する。</summary>
    /// <param name="rect">コアの矩形。</param>
    /// <returns>XAML の矩形。</returns>
    internal static XamlRect ToXamlRect(EsRect rect) => new(rect.X, rect.Y, rect.Width, rect.Height);
}
