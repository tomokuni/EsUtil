using System.Drawing.Imaging;
using EsUtil.Platform.Core.Geometry;

namespace EsUtil.Ui.WinForms.Marquee.Tests;

/// <summary>描画の検証で共有するユーティリティ。</summary>
/// <remarks>
/// 描画実装は <see cref="Graphics"/> だけを要求するため、ウィンドウを持たないテストでも<br/>
/// <see cref="Bitmap"/> 上の実寸の画素を検査できる（これが本コントロールの描画を検証可能にしている設計判断である）。
/// </remarks>
internal static class TestHelper
{
    /// <summary>白で塗った検証用の描画面（32bpp の <see cref="Bitmap"/>）を作る。</summary>
    /// <param name="width">幅（画素）。</param>
    /// <param name="height">高さ（画素）。</param>
    /// <returns>白で塗られた描画面。</returns>
    public static Bitmap CreateCanvas(int width, int height)
    {
        var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.White);
        return bitmap;
    }

    /// <summary>描画計画を新しい描画面（白）へ描く。</summary>
    /// <param name="plan">描く計画。</param>
    /// <param name="width">幅（画素）。</param>
    /// <param name="height">高さ（画素）。</param>
    /// <returns>描画済みの描画面。</returns>
    public static Bitmap Paint(MarqueeDrawPlan plan, int width, int height)
    {
        var bitmap = CreateCanvas(width, height);
        using var graphics = Graphics.FromImage(bitmap);
        MarqueeGraphicsPainter.Paint(graphics, in plan);
        return bitmap;
    }

    /// <summary>検証用の描画計画を組み立てる（指定しない項目は空として扱う）。</summary>
    /// <param name="appearance">外観（表示可否と色の所有者）。</param>
    /// <param name="size">描画面のサイズ（DIP）。</param>
    /// <param name="layers">描く層とその順序（提示層が所有する順序をそのまま渡す）。</param>
    /// <param name="focus">注目矩形（DIP）。 null の場合は注目矩形なし。</param>
    /// <param name="overlay">暗転して塗る領域。</param>
    /// <param name="outerBorder">外枠の矩形。</param>
    /// <param name="innerFill">内側の塗り。</param>
    /// <param name="innerBorder">内側の枠線の矩形。</param>
    /// <param name="handles">ドラッグハンドルの幾何。 null の場合はハンドルなし。</param>
    /// <param name="marker">始点マーカー。 null の場合はマーカーなし。</param>
    /// <param name="labels">ラベルの配置指示。 null の場合はラベルなし。</param>
    /// <returns>描画計画。</returns>
    public static MarqueeDrawPlan CreatePlan(
        MarqueeAppearance appearance,
        EsSize size,
        IReadOnlyList<MarqueeDrawLayer> layers,
        EsRect? focus = null,
        IReadOnlyList<EsRect>? overlay = null,
        IReadOnlyList<EsRect>? outerBorder = null,
        EsRect? innerFill = null,
        IReadOnlyList<EsRect>? innerBorder = null,
        MarqueeHandles? handles = null,
        MarqueeMarker? marker = null,
        IReadOnlyList<MarqueeLabelPlacement>? labels = null)
        => new(
            appearance,
            new EsRect(0, 0, size.Width, size.Height),
            focus,
            focus is { } anchor ? new EsPoint(anchor.X, anchor.Y) : null,
            overlay ?? [],
            outerBorder ?? [],
            innerFill,
            innerBorder ?? [],
            handles,
            marker,
            layers,
            labels ?? []);

    /// <summary>描画面の 1 画素を取得する。</summary>
    /// <param name="bitmap">描画面。</param>
    /// <param name="x">X 座標（画素）。</param>
    /// <param name="y">Y 座標（画素）。</param>
    /// <returns>指定位置の色。</returns>
    public static Color Pixel(Bitmap bitmap, int x, int y) => bitmap.GetPixel(x, y);

    /// <summary>描画面のどこかに指定の色があるかどうかを調べる。</summary>
    /// <param name="bitmap">描画面。</param>
    /// <param name="color">探す色（不透明の塗りを想定する）。</param>
    /// <returns>一致する画素が 1 つ以上ある場合は true。</returns>
    public static bool ContainsColor(Bitmap bitmap, Color color)
    {
        for (var y = 0; y < bitmap.Height; y++)
        {
            for (var x = 0; x < bitmap.Width; x++)
            {
                if (bitmap.GetPixel(x, y).ToArgb() == color.ToArgb())
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>指定の領域に白以外の画素があるかどうかを調べる。</summary>
    /// <param name="bitmap">描画面。</param>
    /// <returns>白以外の画素が 1 つ以上ある場合は true。</returns>
    public static bool HasInk(Bitmap bitmap)
    {
        for (var y = 0; y < bitmap.Height; y++)
        {
            for (var x = 0; x < bitmap.Width; x++)
            {
                if (bitmap.GetPixel(x, y).ToArgb() != Color.White.ToArgb())
                {
                    return true;
                }
            }
        }

        return false;
    }
}
