using Aprillz.MewUI.Rendering;
using DrawingColor = System.Drawing.Color;

namespace EsUtil.Ui.MewUI.Thumbnail.Internal;

/// <summary>矩形の塗り（単色・不透明前提）を単一所有する。</summary>
/// <remarks>
/// 同じ塗りを各描画層・ホストが繰り返し書かないよう、塗りの生成と描画を本クラスに集約する。<br/>
/// 角丸あり（半径 &gt; 0）と角丸なしを 1 つの入口で扱う（呼び出し側に分岐を持たせない）。
/// </remarks>
internal static class MewFill
{
    /// <summary>矩形を塗る。</summary>
    /// <param name="context">描画先のコンテキスト。</param>
    /// <param name="rect">塗る矩形（コア座標）。</param>
    /// <param name="color">塗り色（不透明度はアルファ）。</param>
    public static void Rect(IGraphicsContext context, EsRect rect, DrawingColor color)
    {
        ArgumentNullException.ThrowIfNull(context);

        context.FillRectangle(rect.ToMewUIRect(), color.ToMewUIColor());
    }

    /// <summary>複数の矩形を同じ色で塗る。</summary>
    /// <param name="context">描画先のコンテキスト。</param>
    /// <param name="rects">塗る矩形の一覧（コア座標）。 空の場合は何もしない。</param>
    /// <param name="color">塗り色（不透明度はアルファ）。</param>
    /// <remarks>辺（4 本）のような同色の矩形を列挙する場合にまとめて塗る（描画の呼び出しだけを列挙する）。</remarks>
    public static void Rects(IGraphicsContext context, IReadOnlyList<EsRect> rects, DrawingColor color)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(rects);

        if (rects.Count == 0)
        {
            return;
        }

        var mewColor = color.ToMewUIColor();
        foreach (var rect in rects)
        {
            context.FillRectangle(rect.ToMewUIRect(), mewColor);
        }
    }

    /// <summary>角丸矩形を塗る（半径 0 の場合は四角形）。</summary>
    /// <param name="context">描画先のコンテキスト。</param>
    /// <param name="rect">塗る矩形（コア座標）。</param>
    /// <param name="cornerRadius">角の半径（物理ピクセル）。 0 の場合は四角形として塗る。</param>
    /// <param name="color">塗り色（不透明度はアルファ）。</param>
    /// <remarks>角丸なし（0）は「描かない」ではなく「四角形として描く」指定である（呼び出し側の分岐を不要にする）。</remarks>
    public static void RoundRect(IGraphicsContext context, EsRect rect, double cornerRadius, DrawingColor color)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (cornerRadius > 0)
        {
            context.FillRoundedRectangle(rect.ToMewUIRect(), cornerRadius, cornerRadius, color.ToMewUIColor());
            return;
        }

        context.FillRectangle(rect.ToMewUIRect(), color.ToMewUIColor());
    }
}
