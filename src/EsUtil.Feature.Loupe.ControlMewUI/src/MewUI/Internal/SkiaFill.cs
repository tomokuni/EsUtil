using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Imaging;
using SkiaSharp;
using DrawingColor = System.Drawing.Color;

// 複製元: EsUtil.SaveEvidence.Platform.Skia（独立パッケージ用の意図的な複製。 .github/依存関係.md §4.1）
namespace EsUtil.Feature.Loupe.MewUI.Internal;

/// <summary>Skia の矩形塗り（単色・不透明前提）を単一所有する。</summary>
/// <remarks>
/// <c>new SKPaint { IsAntialias = false, Style = SKPaintStyle.Fill, Color = ... }</c> という同一の塗りを<br/>
/// 各描画層・ホストが繰り返し書かないよう、塗りの生成と描画を本クラスに集約する。<br/>
/// SkiaSharp は UI フレームワークではなくピクセル・描画の基盤であるため、<see cref="EsReadOnlyBitmap"/> と同じ層（本プロジェクト）が担う。<br/>
/// <b>アンチエイリアス</b>は描画結果へ影響するため無効（矩形は画素境界に沿って塗る）。<br/>
/// 線（ストローク）と文字は各所有者（描画層 / <see cref="SkiaText"/>）が持つ（本クラスは塗りのみ）。
/// </remarks>
internal static class SkiaFill
{
    /// <summary>矩形を塗る。</summary>
    /// <param name="canvas">描画先のキャンバス。</param>
    /// <param name="rect">塗る矩形（コア座標）。</param>
    /// <param name="color">塗り色（不透明度はアルファ）。</param>
    public static void Rect(SKCanvas canvas, EsRect rect, DrawingColor color)
    {
        ArgumentNullException.ThrowIfNull(canvas);

        using var paint = new SKPaint { IsAntialias = false, Style = SKPaintStyle.Fill, Color = color.ToSKColor() };
        canvas.DrawRect(rect.ToSKRect(), paint);
    }

    /// <summary>複数の矩形を同じ色で塗る（ペイントを 1 つだけ生成する）。</summary>
    /// <param name="canvas">描画先のキャンバス。</param>
    /// <param name="rects">塗る矩形の一覧（コア座標）。 空の場合は何もしない。</param>
    /// <param name="color">塗り色（不透明度はアルファ）。</param>
    /// <remarks>辺（4 本）のような同色の矩形を列挙する場合にまとめて塗る（ペイントの生成を 1 回に抑える）。</remarks>
    public static void Rects(SKCanvas canvas, IReadOnlyList<EsRect> rects, DrawingColor color)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        ArgumentNullException.ThrowIfNull(rects);

        if (rects.Count == 0)
        {
            return;
        }

        using var paint = new SKPaint { IsAntialias = false, Style = SKPaintStyle.Fill, Color = color.ToSKColor() };

        foreach (var rect in rects)
        {
            canvas.DrawRect(rect.ToSKRect(), paint);
        }
    }

    /// <summary>角丸矩形を塗る（半径 0 の場合は四角形）。</summary>
    /// <param name="canvas">描画先のキャンバス。</param>
    /// <param name="rect">塗る矩形（コア座標）。</param>
    /// <param name="cornerRadius">角の半径（物理ピクセル）。 0 の場合は四角形として塗る。</param>
    /// <param name="color">塗り色（不透明度はアルファ）。</param>
    /// <remarks>角丸なし（0）は「描かない」ではなく「四角形として描く」指定である（呼び出し側の分岐を不要にする）。</remarks>
    public static void RoundRect(SKCanvas canvas, EsRect rect, double cornerRadius, DrawingColor color)
    {
        ArgumentNullException.ThrowIfNull(canvas);

        using var paint = new SKPaint { IsAntialias = false, Style = SKPaintStyle.Fill, Color = color.ToSKColor() };
        if (cornerRadius > 0)
        {
            canvas.DrawRoundRect(rect.ToSKRect(), (float)cornerRadius, (float)cornerRadius, paint);
            return;
        }

        canvas.DrawRect(rect.ToSKRect(), paint);
    }
}
