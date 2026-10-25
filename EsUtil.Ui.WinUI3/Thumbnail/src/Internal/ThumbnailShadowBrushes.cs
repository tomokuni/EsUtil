using Microsoft.UI.Xaml.Media;
using Windows.Foundation;
using Windows.UI;

namespace EsUtil.Ui.WinUI3.Thumbnail.Internal;

/// <summary>影を「4 辺の線形グラデーション」と「4 隅の放射グラデーション」で塗るブラシ一式。</summary>
/// <remarks>
/// <b>要素を 8 個にするため</b>: 影の計画は 1 画素の帯（<see cref="IThumbnailShadowPlan.BuildDeviceBands"/>）も<br/>
/// 返すが、帯を 1 要素ずつ置くと 1 件あたり数百個になる（実測 2026-10-25: 125% の画面で 532 個）。<br/>
/// 31 件の一覧では 16,000 個を超え、初回の描画に 258 ms かかった。 計画が持つ停止点<br/>
/// （<see cref="IThumbnailShadowPlan.Edges"/> と <see cref="IThumbnailShadowPlan.CornerStops"/>）を<br/>
/// グラデーション ブラシへ写すと、要素 8 個で同じ濃さの変化になる（実測: 36 ms）。<br/>
/// <br/>
/// <b>ブラシは項目間で共有する</b>: 濃さの変化は画像の位置・大きさに依存しないため、同じ計画なら<br/>
/// 一覧のすべての項目で同じブラシを使える（最後に使った 1 組だけを保持する。 UI スレッド専用）。
/// </remarks>
internal sealed class ThumbnailShadowBrushes
{
    /// <summary>最後に生成したブラシ一式。</summary>
    private static ThumbnailShadowBrushes? s_current;

    /// <summary><see cref="s_current"/> を生成した計画。</summary>
    private static IThumbnailShadowPlan? s_currentPlan;

    /// <summary>ブラシ一式を生成する。</summary>
    /// <param name="plan">影の計画。</param>
    /// <remarks>
    /// 停止点の並びは「外側 → 画像の端」である（<see cref="ThumbnailShadowEdge.Stops"/> の契約）。<br/>
    /// そのためグラデーションの始点は必ず<b>外側</b>になる。 辺ごとに始点と終点を入れ替える。
    /// </remarks>
    private ThumbnailShadowBrushes(IThumbnailShadowPlan plan)
    {
        Top = CreateEdge(plan, ThumbnailShadowSide.Top);
        Right = CreateEdge(plan, ThumbnailShadowSide.Right);
        Bottom = CreateEdge(plan, ThumbnailShadowSide.Bottom);
        Left = CreateEdge(plan, ThumbnailShadowSide.Left);

        // 隅の中心は画像の隅の頂点である（隅の正方形のうち画像に接する角）。
        // 半径は正方形の一辺（= ぼかしの広がり）に合わせる（停止点の位置は「距離 ÷ ぼかしの広がり」）
        TopLeft = CreateCorner(plan, 1, 1);
        TopRight = CreateCorner(plan, 0, 1);
        BottomLeft = CreateCorner(plan, 1, 0);
        BottomRight = CreateCorner(plan, 0, 0);
    }

    /// <summary>上辺の影のブラシを取得する。</summary>
    public LinearGradientBrush Top { get; }

    /// <summary>右辺の影のブラシを取得する。</summary>
    public LinearGradientBrush Right { get; }

    /// <summary>下辺の影のブラシを取得する。</summary>
    public LinearGradientBrush Bottom { get; }

    /// <summary>左辺の影のブラシを取得する。</summary>
    public LinearGradientBrush Left { get; }

    /// <summary>左上の隅の影のブラシを取得する。</summary>
    public RadialGradientBrush TopLeft { get; }

    /// <summary>右上の隅の影のブラシを取得する。</summary>
    public RadialGradientBrush TopRight { get; }

    /// <summary>左下の隅の影のブラシを取得する。</summary>
    public RadialGradientBrush BottomLeft { get; }

    /// <summary>右下の隅の影のブラシを取得する。</summary>
    public RadialGradientBrush BottomRight { get; }

    /// <summary>計画に対応するブラシ一式を取得する（同じ計画なら作り直さない）。</summary>
    /// <param name="plan">影の計画。</param>
    /// <returns>ブラシ一式。</returns>
    public static ThumbnailShadowBrushes Resolve(IThumbnailShadowPlan plan)
    {
        if (s_current is { } current && ReferenceEquals(s_currentPlan, plan))
        {
            return current;
        }

        s_current = new ThumbnailShadowBrushes(plan);
        s_currentPlan = plan;
        return s_current;
    }

    /// <summary>1 辺分のブラシを生成する。</summary>
    /// <param name="plan">影の計画。</param>
    /// <param name="side">辺。</param>
    /// <returns>辺のブラシ（始点が外側）。</returns>
    private static LinearGradientBrush CreateEdge(IThumbnailShadowPlan plan, ThumbnailShadowSide side)
    {
        var brush = new LinearGradientBrush
        {
            MappingMode = BrushMappingMode.RelativeToBoundingBox,

            // 外側 → 画像の端（停止点の並びに合わせる）
            StartPoint = side switch
            {
                ThumbnailShadowSide.Top => new Point(0, 0),
                ThumbnailShadowSide.Right => new Point(1, 0),
                ThumbnailShadowSide.Bottom => new Point(0, 1),
                _ => new Point(0, 0),
            },
            EndPoint = side switch
            {
                ThumbnailShadowSide.Top => new Point(0, 1),
                ThumbnailShadowSide.Right => new Point(0, 0),
                ThumbnailShadowSide.Bottom => new Point(0, 0),
                _ => new Point(1, 0),
            },
        };

        foreach (var edge in plan.Edges)
        {
            if (edge.Side == side)
            {
                AddStops(brush.GradientStops, edge.Stops);
                break;
            }
        }

        return brush;
    }

    /// <summary>1 隅分のブラシを生成する。</summary>
    /// <param name="plan">影の計画。</param>
    /// <param name="centerX">中心の X（隅の正方形に対する 0〜1 の比率。 画像に接する角）。</param>
    /// <param name="centerY">中心の Y（隅の正方形に対する 0〜1 の比率。 画像に接する角）。</param>
    /// <returns>隅のブラシ。</returns>
    private static RadialGradientBrush CreateCorner(IThumbnailShadowPlan plan, double centerX, double centerY)
    {
        // 半径 1（= 正方形の一辺）で、対角の距離は停止点の範囲を超える。
        // 超えた部分は最後の停止点（ぼかしの広がりでの濃さ = ほぼ透明）で塗られる
        var brush = new RadialGradientBrush
        {
            Center = new Point(centerX, centerY),
            GradientOrigin = new Point(centerX, centerY),
            RadiusX = 1,
            RadiusY = 1,
        };

        AddStops(brush.GradientStops, plan.CornerStops);
        return brush;
    }

    /// <summary>停止点をグラデーションへ追加する。</summary>
    /// <param name="target">追加先。</param>
    /// <param name="stops">停止点。</param>
    private static void AddStops(IList<GradientStop> target, IReadOnlyList<ThumbnailShadowStop> stops)
    {
        foreach (var stop in stops)
        {
            target.Add(new GradientStop { Offset = stop.Offset, Color = ToColor(stop.Argb) });
        }
    }

    /// <summary>色（<c>0xAARRGGBB</c>）を XAML の色へ変換する。</summary>
    /// <param name="argb">色。</param>
    /// <returns>変換した色。</returns>
    private static Color ToColor(uint argb)
        => Color.FromArgb((byte)(argb >> 24), (byte)(argb >> 16), (byte)(argb >> 8), (byte)argb);
}
