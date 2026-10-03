using EsUtil.Feature.Marquee.Core.Interaction;
using EsUtil.Platform.Core.Geometry;

namespace EsUtil.Feature.Marquee.Core.Selection;

/// <summary>選択矩形（対象座標）と、その矩形だけから決まる純粋な幾何計算。</summary>
/// <param name="Rect">選択矩形（対象座標）。 未選択は幅・高さが 0。</param>
/// <remarks>
/// <b>適用パターン</b>: Value Object（不変の矩形と、その値だけから決まる計算を 1 つの型へ閉じる）。<br/>
/// <br/>
/// ハンドル位置・対角・ドラッグ変形・境界クリップは「その矩形」に対する計算であるため、独立した静的クラスではなく<br/>
/// 値の振る舞いとして持つ（利用者に矩形を引数で渡させない。 矩形を差し替える操作は新しい値を返す）。<br/>
/// 状態機械（<see cref="MarqueeState"/>）とコアの幾何の解決（<c>MarqueeCore.ResolveGeometry</c>）が共通利用する（DRY 原則）。<br/>
/// コア型（<see cref="EsRect"/> / <see cref="EsPoint"/>）のみに依存し、UI フレームワークへ依存しない（UI 型との相互変換は各 UI の Interop 層が担う）。<br/>
/// <b>可視性</b>: 状態機械の内部手順とコアの幾何の解決だけが使う（描画層は解決済みの矩形を受け取る）ため internal とする。
/// </remarks>
internal readonly record struct MarqueeSelection(EsRect Rect)
{
    /// <summary>始点と終点から正規化された選択矩形を生成する。</summary>
    /// <param name="start">始点（対象座標）。</param>
    /// <param name="end">終点（対象座標）。</param>
    /// <returns>幅・高さが常に 0 以上の選択矩形（区間は <see cref="EsRectInterval.Closed"/>）。</returns>
    /// <remarks>
    /// 始点が終点より右下でも同じ矩形になる（ドラッグ方向に依存しない）。<br/>
    /// ドラッグで作る選択は「始点のピクセルから終点のピクセルまで」を意図するため、区間の流儀を <see cref="EsRectInterval.Closed"/> で与える<br/>
    /// （幅は <c>|終点 − 始点|</c> のまま。 <c>+1</c> の判断は受け取った側が <see cref="EsRect.Interval"/> を読んで行う）。
    /// </remarks>
    internal static MarqueeSelection FromPoints(EsPoint start, EsPoint end)
        => new(new EsRect(
            Math.Min(start.X, end.X),
            Math.Min(start.Y, end.Y),
            Math.Abs(end.X - start.X),
            Math.Abs(end.Y - start.Y),
            EsRectInterval.Closed));

    /// <summary>指定したハンドルの位置（矩形上の点）を取得する。</summary>
    /// <param name="handle">ハンドル種別。</param>
    /// <returns>ハンドルの位置。 未知のハンドル（<see cref="MarqueeHitTarget.None"/> を含む）の場合は右下隅。</returns>
    /// <remarks>
    /// ハンドル位置はヒットテスト・nudge の追従先・描画（ハンドル矩形の中心）で共通利用する（DRY 原則）。<br/>
    /// 移動（<see cref="MarqueeHitTarget.Move"/>）は矩形の中心を返す（掴む位置の基準）。
    /// </remarks>
    internal EsPoint Handle(MarqueeHitTarget handle) => handle switch
    {
        MarqueeHitTarget.TopLeft => Rect.TopLeft,
        MarqueeHitTarget.TopCenter => new EsPoint(Rect.Center.X, Rect.Y),
        MarqueeHitTarget.TopRight => Rect.TopRight,
        MarqueeHitTarget.MiddleLeft => new EsPoint(Rect.X, Rect.Center.Y),
        MarqueeHitTarget.MiddleRight => new EsPoint(Rect.Right, Rect.Center.Y),
        MarqueeHitTarget.BottomLeft => Rect.BottomLeft,
        MarqueeHitTarget.BottomCenter => new EsPoint(Rect.Center.X, Rect.Bottom),
        MarqueeHitTarget.BottomRight => Rect.BottomRight,
        MarqueeHitTarget.Move => Rect.Center,
        _ => Rect.BottomRight,
    };

    /// <summary>矩形の「始点でない方の隅」（終点）を取得する。</summary>
    /// <param name="startPoint">選択の始点（対象座標）。</param>
    /// <returns>始点と反対側の隅の座標。</returns>
    /// <remarks>
    /// 矩形の左端が始点の X と一致する場合は右辺（<see cref="EsRect.Right"/>）、それ以外は左端を終点とする<br/>
    /// （Y も同様に下端または上端）。 nudge の終点移動と基点更新で共通利用する。<br/>
    /// 浮動小数の誤差を許容するため、一致判定には微小なしきい値を使う。
    /// </remarks>
    internal EsPoint OppositeCorner(EsPoint startPoint)
        => new(
            Math.Abs(startPoint.X - Rect.X) < 0.001 ? Rect.Right : Rect.X,
            Math.Abs(startPoint.Y - Rect.Y) < 0.001 ? Rect.Bottom : Rect.Y);

    /// <summary>矩形を境界内にクリップする（ハンドル種別に応じて固定角を維持）。</summary>
    /// <param name="bounds">クリップ範囲（対象座標）。</param>
    /// <param name="handle">アクティブなハンドル。</param>
    /// <returns>境界内に収めた選択矩形。</returns>
    /// <remarks>
    /// 移動（<see cref="MarqueeHitTarget.Move"/>）の場合は矩形全体が境界内に収まるようにシフトする（サイズは維持）。<br/>
    /// それ以外のハンドル変形は固定角（ハンドルと反対側の隅）を動かさず、動く辺のみ境界内に制限する。
    /// </remarks>
    /// <remarks>寸法は変わらないため、区間の流儀（<see cref="EsRect.Interval"/>）はそのまま引き継ぐ。</remarks>
    internal MarqueeSelection ClampToBounds(EsRect bounds, MarqueeHitTarget handle)
    {
        double l = Rect.X;
        double t = Rect.Y;
        double r = Rect.Right;
        double b = Rect.Bottom;

        if (handle == MarqueeHitTarget.Move)
        {
            double shiftX = l < bounds.X ? bounds.X - l : (r > bounds.Right ? bounds.Right - r : 0);
            double shiftY = t < bounds.Y ? bounds.Y - t : (b > bounds.Bottom ? bounds.Bottom - b : 0);
            l += shiftX;
            r += shiftX;
            t += shiftY;
            b += shiftY;
        }
        else
        {
            if (l < bounds.X) l = bounds.X;
            if (t < bounds.Y) t = bounds.Y;
            if (r > bounds.Right) r = bounds.Right;
            if (b > bounds.Bottom) b = bounds.Bottom;
        }

        return new MarqueeSelection(new EsRect(l, t, r - l, b - t, Rect.Interval));
    }

    /// <summary>ドラッグハンドル操作による変形を計算する。</summary>
    /// <param name="dx">X 方向の移動量（対象座標）。</param>
    /// <param name="dy">Y 方向の移動量（対象座標）。</param>
    /// <param name="handle">アクティブなハンドル。</param>
    /// <param name="minSize">選択矩形の最小サイズ（補正済みの値を渡す。 安全域は <c>MarqueeCore.Limits</c> が単一所有する）。</param>
    /// <returns>変形後の選択矩形（反転を正規化し、最小サイズを保証する）。</returns>
    /// <remarks>
    /// ハンドルを反対側までドラッグした場合（左端が右端を超える等）は矩形を反転（正規化）する。<br/>
    /// 最小サイズは幅・高さが <paramref name="minSize"/> 未満にならないよう保証する。
    /// </remarks>
    internal MarqueeSelection Drag(double dx, double dy, MarqueeHitTarget handle, double minSize)
    {
        double l = Rect.X;
        double t = Rect.Y;
        double r = Rect.Right;
        double b = Rect.Bottom;

        switch (handle)
        {
            case MarqueeHitTarget.TopLeft: l += dx; t += dy; break;
            case MarqueeHitTarget.TopCenter: t += dy; break;
            case MarqueeHitTarget.TopRight: r += dx; t += dy; break;
            case MarqueeHitTarget.MiddleLeft: l += dx; break;
            case MarqueeHitTarget.MiddleRight: r += dx; break;
            case MarqueeHitTarget.BottomLeft: l += dx; b += dy; break;
            case MarqueeHitTarget.BottomCenter: b += dy; break;
            case MarqueeHitTarget.BottomRight: r += dx; b += dy; break;
            case MarqueeHitTarget.Move: l += dx; t += dy; r += dx; b += dy; break;
        }

        double x1 = Math.Min(l, r);
        double x2 = Math.Max(l, r);
        double y1 = Math.Min(t, b);
        double y2 = Math.Max(t, b);

        var m = minSize;
        if (x2 - x1 < m) x2 = x1 + m;
        if (y2 - y1 < m) y2 = y1 + m;

        return new MarqueeSelection(new EsRect(x1, y1, x2 - x1, y2 - y1, Rect.Interval));
    }
}
