using EsUtil.Platform.Core.Geometry;

namespace EsUtil.Ui.WinForms.Interop.Imaging;

/// <summary>GDI+ の矩形塗り（単色）を単一所有する。</summary>
/// <remarks>
/// 同一の塗り（アンチエイリアス無効の単色ブラシ）を各描画層・ホストが繰り返し書かないよう、<br/>
/// 塗りの生成と描画を本クラスに集約する。<br/>
/// <b>アンチエイリアス</b>は描画結果へ影響するため、塗りには適用しない（矩形は画素境界に沿って塗る）。<br/>
/// 線（ストローク）と文字は各所有者が持つ（本クラスは塗りのみ）。
/// </remarks>
public static class GdiFill
{
    /// <summary>塗りに使うブラシ（スレッドごとに 1 つを使い回す）。</summary>
    /// <remarks>
    /// 塗りは描画のたびに必要になるため、生成と破棄を繰り返さず<b>色だけを書き換えて使い回す</b>。<br/>
    /// スレッドごとに持つのは、<see cref="SolidBrush"/> の同時書き換えを避けるためである。<br/>
    /// 値を書き換えて使うため、<b>塗りの途中で別の色の塗りを求めない</b>前提とする（各メソッドが先頭で 1 度だけ求める）。
    /// </remarks>
    [ThreadStatic]
    private static SolidBrush? s_fill;

    /// <summary>矩形を塗る。</summary>
    /// <param name="graphics">描画先。 null 不可。</param>
    /// <param name="rect">塗る矩形（コア座標）。 幅・高さが 0 以下の場合は塗らない。</param>
    /// <param name="color">塗り色（不透明度はアルファ）。</param>
    /// <exception cref="ArgumentNullException"><paramref name="graphics"/> が null の場合。</exception>
    /// <remarks>幅・高さが 0 以下の矩形は塗らない（GDI+ は負のサイズの矩形を描画しない）。</remarks>
    public static void Rect(Graphics graphics, EsRect rect, Color color)
    {
        ArgumentNullException.ThrowIfNull(graphics);

        if (rect.Width <= 0 || rect.Height <= 0)
        {
            return;
        }

        graphics.FillRectangle(ResolveFill(color), rect.ToRectangleF());
    }

    /// <summary>複数の矩形を同じ色で塗る。</summary>
    /// <param name="graphics">描画先。 null 不可。</param>
    /// <param name="rects">塗る矩形の一覧（コア座標）。 空の場合は何もしない。</param>
    /// <param name="color">塗り色（不透明度はアルファ）。</param>
    /// <exception cref="ArgumentNullException"><paramref name="graphics"/> または <paramref name="rects"/> が null の場合。</exception>
    /// <remarks>
    /// 辺（4 本）のような同色の矩形を列挙する場合にまとめて塗る（ブラシの解決を 1 回に保つ）。<br/>
    /// 幅・高さが 0 以下の矩形は塗らない（<see cref="Rect"/> と同じ規則）。
    /// </remarks>
    public static void Rects(Graphics graphics, IReadOnlyList<EsRect> rects, Color color)
    {
        ArgumentNullException.ThrowIfNull(graphics);
        ArgumentNullException.ThrowIfNull(rects);

        if (rects.Count == 0)
        {
            return;
        }

        var brush = ResolveFill(color);
        foreach (var rect in rects)
        {
            if (rect.Width <= 0 || rect.Height <= 0)
            {
                continue;
            }

            graphics.FillRectangle(brush, rect.ToRectangleF());
        }
    }
    /// <summary>角丸の矩形を塗る（角丸半径 0 以下は四角形）。</summary>
    /// <param name="graphics">描画先。 null 不可。</param>
    /// <param name="rect">塗る矩形（コア座標）。 幅・高さが 0 以下の場合は塗らない。</param>
    /// <param name="cornerRadius">角丸の半径（DIP）。 0 以下の場合は四角形として塗る。</param>
    /// <param name="color">塗り色（不透明度はアルファ）。</param>
    /// <exception cref="ArgumentNullException"><paramref name="graphics"/> が null の場合。</exception>
    /// <remarks>角丸の半径は矩形の短辺の半分へクランプする（半径が大きすぎると角が崩れる）。</remarks>
    public static void RoundRect(Graphics graphics, EsRect rect, double cornerRadius, Color color)
    {
        if (cornerRadius <= 0)
        {
            Rect(graphics, rect, color);
            return;
        }

        ArgumentNullException.ThrowIfNull(graphics);

        if (rect.Width <= 0 || rect.Height <= 0)
        {
            return;
        }

        var radius = (float)Math.Min(cornerRadius, Math.Min(rect.Width, rect.Height) / 2);
        var bounds = rect.ToRectangleF();
        using var path = new System.Drawing.Drawing2D.GraphicsPath();
        path.AddArc(bounds.Left, bounds.Top, radius * 2, radius * 2, 180, 90);
        path.AddArc(bounds.Right - (radius * 2), bounds.Top, radius * 2, radius * 2, 270, 90);
        path.AddArc(bounds.Right - (radius * 2), bounds.Bottom - (radius * 2), radius * 2, radius * 2, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - (radius * 2), radius * 2, radius * 2, 90, 90);
        path.CloseFigure();
        graphics.FillPath(ResolveFill(color), path);
    }

    /// <summary>塗りのブラシを取得する（色だけを書き換える）。</summary>
    /// <param name="color">塗り色（不透明度はアルファ）。</param>
    /// <returns>塗りのブラシ（スレッドごとに共有する 1 つ）。</returns>
    private static SolidBrush ResolveFill(Color color)
    {
        var brush = s_fill ??= new SolidBrush(color);
        brush.Color = color;
        return brush;
    }
}
