using EsUtil.Platform.Core.Geometry;
using EsUtil.Ui.WinUI3.Interop.Drawing;

namespace EsUtil.Ui.WinUI3.Loupe.Internal;

/// <summary>描画計画（<see cref="LoupeDrawPlan"/>）から矩形の列を組み立てる。</summary>
/// <remarks>
/// <b>計算を持たない</b>: 矩形は計画に物理ピクセルで解決済みである。 本クラスは「どの層にどの矩形を、どの色で」<br/>
/// 置くかだけを持ち、XAML の要素を作らないため、ウィンドウ無しで検証できる。<br/>
/// <b>戻り値は <see cref="DrawList"/>（変換ブリッジ）</b>である: 「描画する矩形の列」は UI に依存しない型として<br/>
/// ブリッジが単一所有する（同じ型をコントロールごとに複製しない。 `.github/依存関係.md` §4.1）。<br/>
/// <br/>
/// <b>層の順序</b>: 十字線 → 枠線 → ラベル（背面から前面へ）。 拡大画像と下地は矩形ではなく画像・背景で表す。<br/>
/// <b>表示可否</b>: 計画は表示可否で値を消さないため、置くかどうかは本クラスが外観の <c>Show*</c> を見て決める。
/// </remarks>
internal static class LoupeShapePlan
{
    /// <summary>十字線の矩形を組み立てる。</summary>
    /// <param name="plan">描画計画。</param>
    /// <returns>十字線の矩形（表示しない場合は空）。</returns>
    public static DrawList BuildCrosshair(LoupeDrawPlan plan)
    {
        var list = new DrawList();
        if (plan.Appearance.ShowCrosshair)
        {
            AddRects(list, plan.CrosshairPixelRects, plan.Appearance.CrossColor);
        }

        return list;
    }

    /// <summary>枠線の矩形を組み立てる。</summary>
    /// <param name="plan">描画計画。</param>
    /// <returns>枠線の矩形（表示しない場合は空）。</returns>
    public static DrawList BuildBorder(LoupeDrawPlan plan)
    {
        var list = new DrawList();
        if (plan.Appearance.ShowBorder)
        {
            AddRects(list, plan.BorderPixelRects, plan.Appearance.BorderColor);
        }

        return list;
    }

    /// <summary>表示するラベルの配置指示を取り出す。</summary>
    /// <param name="plan">描画計画。</param>
    /// <returns>表示が有効な配置指示（順序は計画のまま）。</returns>
    public static IReadOnlyList<LoupeLabelPlacement> VisibleLabels(LoupeDrawPlan plan)
    {
        var labels = new List<LoupeLabelPlacement>();
        for (var i = 0; i < plan.Labels.Count; i++)
        {
            if (plan.Labels[i].IsVisible)
            {
                labels.Add(plan.Labels[i]);
            }
        }

        return labels;
    }

    /// <summary>矩形の列を画素のまま追加する。</summary>
    /// <param name="list">追加先のリスト。</param>
    /// <param name="rects">追加する矩形（物理ピクセル）。</param>
    /// <param name="color">塗り色。</param>
    private static void AddRects(DrawList list, IReadOnlyList<EsRect> rects, System.Drawing.Color color)
    {
        for (var i = 0; i < rects.Count; i++)
        {
            list.AddRect(rects[i], color);
        }
    }

    /// <summary>色を <c>0xAARRGGBB</c> へ変換する。</summary>
    /// <param name="color">変換する色。</param>
    /// <returns>変換した色。</returns>
    public static uint ToArgb(System.Drawing.Color color)
        => ((uint)color.A << 24) | ((uint)color.R << 16) | ((uint)color.G << 8) | color.B;
}