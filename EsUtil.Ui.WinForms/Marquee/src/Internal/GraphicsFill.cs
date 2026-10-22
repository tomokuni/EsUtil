using EsUtil.Platform.Core.Geometry;

namespace EsUtil.Ui.WinForms.Marquee.Internal;

/// <summary>GDI+ の矩形塗り（単色）を単一所有する。</summary>
/// <remarks>
/// 同一の塗り（アンチエイリアス無効の単色ブラシ）を各描画層・ホストが繰り返し書かないよう、塗りの生成と描画を本クラスに集約する。<br/>
/// <b>アンチエイリアス</b>は描画結果へ影響するため、塗りには適用しない（矩形は画素境界に沿って塗る）。<br/>
/// 線（ストローク）と文字は各所有者（描画実装 / <see cref="GraphicsText"/>）が持つ（本クラスは塗りのみ）。
/// </remarks>
internal static class GraphicsFill
{
    /// <summary>塗りに使うブラシ（スレッドごとに 1 つを使い回す）。</summary>
    /// <remarks>
    /// 塗りは描画のたびに必要になるため、生成と破棄を繰り返さず<b>色だけを書き換えて使い回す</b>。<br/>
    /// スレッドごとに持つのは、<see cref="SolidBrush"/> の同時書き換えを避けるためである。<br/>
    /// 値を書き換えて使うため、<b>塗りの途中で別の色の塗りを求めない</b>前提とする（各メソッドが先頭で 1 度だけ求める）。
    /// </remarks>
    [ThreadStatic]
    private static SolidBrush? s_fill;

    /// <summary>塗りのブラシを取得する（色だけを書き換える）。</summary>
    /// <param name="color">塗り色（不透明度はアルファ）。</param>
    /// <returns>塗りのブラシ（スレッドごとに共有する 1 つ）。</returns>
    private static SolidBrush ResolveFill(Color color)
    {
        var brush = s_fill ??= new SolidBrush(color);
        brush.Color = color;
        return brush;
    }

    /// <summary>矩形を塗る。</summary>
    /// <param name="graphics">描画先。</param>
    /// <param name="rect">塗る矩形（コア座標）。</param>
    /// <param name="color">塗り色（不透明度はアルファ）。</param>
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

    /// <summary>複数の矩形を同じ色で塗る（ブラシは共有する 1 つを使う）。</summary>
    /// <param name="graphics">描画先。</param>
    /// <param name="rects">塗る矩形の一覧（コア座標）。 空の場合は何もしない。</param>
    /// <param name="color">塗り色（不透明度はアルファ）。</param>
    /// <remarks>辺（4 本）のような同色の矩形を列挙する場合にまとめて塗る（描画の呼び出しだけを列挙する）。</remarks>
    public static void Rects(Graphics graphics, IReadOnlyList<EsRect> rects, Color color)
    {
        ArgumentNullException.ThrowIfNull(graphics);
        ArgumentNullException.ThrowIfNull(rects);

        if (rects.Count == 0)
        {
            return;
        }

        var brush = ResolveFill(color);

        for (var index = 0; index < rects.Count; index++)
        {
            var rect = rects[index];
            if (rect.Width <= 0 || rect.Height <= 0)
            {
                continue;
            }

            graphics.FillRectangle(brush, rect.ToRectangleF());
        }
    }
}
