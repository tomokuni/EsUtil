using EsUtil.Platform.Core.Geometry;
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
        var stroke = StyledText.RichTextMetrics.ToBrush(shape.Border);
        var fill = shape.HasFill ? StyledText.RichTextMetrics.ToBrush(shape.Fill) : null;
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

        // **文字の描画は本文コントロールと同じ実装を使う**: 注釈に写しを持つと、行の位置や
        // 下線の位置が片方だけ直したときに食い違う。
        foreach (var line in shape.Lines)
        {
            foreach (var segment in line.Segments)
            {
                var text = StyledText.RichTextMetrics.CreateText(segment, scale);
                Canvas.SetLeft(text, (segment.Bounds.X * scale) + offsetX);
                Canvas.SetTop(text, (StyledText.RichTextMetrics.ResolveTextTop(line, segment) * scale) + offsetY);
                canvas.Children.Add(text);
            }
        }
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

        var border = StyledText.RichTextMetrics.ToBrush(borderColor);
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

        var fill = StyledText.RichTextMetrics.ToBrush(handleColor);
        var stroke = StyledText.RichTextMetrics.ToBrush(handleBorderColor);
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
