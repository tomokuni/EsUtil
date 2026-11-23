using EsUtil.Platform.Core.Geometry;

namespace EsUtil.Feature.Annotation;

/// <summary>注釈の当たり判定（選択・しっぽのハンドル）。</summary>
/// <remarks>
/// <b>判断は Core が単一所有する</b>: どの注釈を選ぶかは UI 版で変えない（重なり順・しっぽの優先順が
/// UI ごとにずれると、同じ操作で違う注釈が選ばれる）。 UI は座標を渡して結果を使うだけにする。<br/>
/// 座標は元画像ピクセルである。
/// </remarks>
public static class AnnotationHitTest
{
    /// <summary>しっぽの先端を掴むための既定の半径を取得する。</summary>
    /// <remarks>
    /// <b>呼び出し側の座標系で解釈する</b>: UI は画面上で一定の大きさ（DIP）で掴めるよう、
    /// 表示倍率で割った値を対象座標の半径として渡す（<see cref="HitTestTail"/> の <c>radius</c>）。
    /// </remarks>
    public const double TailHandleRadiusDefault = 10.0;

    /// <summary>指定された位置にある最も前面の注釈を取得する。</summary>
    /// <param name="document">対象の文書。</param>
    /// <param name="point">位置（元画像ピクセル）。</param>
    /// <returns>見つかった注釈。 無い場合は <see langword="null"/>。</returns>
    /// <remarks><b>最前面から探す</b>（重なっている場合は手前の注釈が選ばれる）。</remarks>
    public static AnnotationItem? HitTest(AnnotationDocument document, EsPoint point)
    {
        ArgumentNullException.ThrowIfNull(document);

        for (var i = document.Items.Length - 1; i >= 0; i--)
        {
            var item = document.Items[i];
            if (Contains(item.Bounds, point))
            {
                return item;
            }
        }

        return null;
    }

    /// <summary>指定された位置にあるしっぽの先端を持つ吹き出しを取得する。</summary>
    /// <param name="document">対象の文書。</param>
    /// <param name="point">位置（元画像ピクセル）。</param>
    /// <param name="radius">掴む半径（<paramref name="point"/> と同じ座標系）。 0 以下は <see cref="TailHandleRadiusDefault"/>。</param>
    /// <returns>見つかった吹き出し。 無い場合は <see langword="null"/>。</returns>
    /// <remarks>
    /// <b>先端は本体より優先する</b>（本体の内側にある先端も掴めるようにするため、本体の判定より先に使う）。
    /// 最前面の吹き出しから探す。
    /// </remarks>
    public static BubbleAnnotation? HitTestTail(AnnotationDocument document, EsPoint point, double radius = TailHandleRadiusDefault)
    {
        ArgumentNullException.ThrowIfNull(document);
        var effectiveRadius = AnnotationLimits.CoercePositive(radius, TailHandleRadiusDefault);
        var squared = effectiveRadius * effectiveRadius;

        for (var i = document.Items.Length - 1; i >= 0; i--)
        {
            if (document.Items[i] is not BubbleAnnotation bubble)
            {
                continue;
            }

            var dx = bubble.Tail.Tip.X - point.X;
            var dy = bubble.Tail.Tip.Y - point.Y;
            if ((dx * dx) + (dy * dy) <= squared)
            {
                return bubble;
            }
        }

        return null;
    }

    /// <summary>矩形の内側に点があるかどうかを判定する（境界を含む）。</summary>
    /// <param name="rect">矩形。</param>
    /// <param name="point">点。</param>
    /// <returns>内側（境界を含む）の場合は <see langword="true"/>。</returns>
    private static bool Contains(EsRect rect, EsPoint point)
        => point.X >= rect.X && point.X <= rect.Right && point.Y >= rect.Y && point.Y <= rect.Bottom;
}
