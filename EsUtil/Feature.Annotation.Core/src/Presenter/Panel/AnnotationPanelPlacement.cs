using EsUtil.Platform.Core.Geometry;

namespace EsUtil.Feature.Annotation.Presenter.Panel;

/// <summary>フローティング パネルの位置を決める（純粋計算。 単一所有）。</summary>
/// <remarks>
/// <b>座標はパネルを描く面の左上を原点とする DIP</b>。<br/>
/// <b>見えている範囲の外へは出さない</b>: 切れて見えなくなる（掴めなくなる）ため、置ける領域へ収める
/// （<see cref="ResolveAvailableArea"/> が領域を決める）。<br/>
/// <b>編集対象に被せない</b>: 書式バーを本文の上へ重ねると、編集中の文字が隠れて読めない。
/// 本文の外（上・下・右・左の順）で置ける領域に収まる場所へ置く。
/// </remarks>
public static class AnnotationPanelPlacement
{
    /// <summary>パネルを置ける領域を求める。</summary>
    /// <param name="layer">注釈レイヤーが占める矩形（DIP）。</param>
    /// <param name="visible">パネルを描ける範囲（ウィンドウの内側。 DIP）。</param>
    /// <param name="allowance">レイヤーの外側へ広げる余白（DIP）。</param>
    /// <returns>パネルを置ける領域（DIP）。</returns>
    /// <remarks>
    /// <b>レイヤーの外側へも置ける</b>: レイヤー（画像）の範囲だけで挟むと、端にある注釈の書式バーが内側へ
    /// 押し戻され、本文に重なる。 <paramref name="allowance"/> だけ外側を許す。<br/>
    /// <b>描けない範囲へは置かない</b>: ウィンドウの外は切れて見えなくなるため、<paramref name="visible"/> との
    /// 交わりを返す。 交わりが空になる（レイヤーが画面外にある）場合は <paramref name="visible"/> を返し、
    /// 少なくとも見えている範囲へ収める。
    /// </remarks>
    public static EsRect ResolveAvailableArea(EsRect layer, EsRect visible, double allowance)
    {
        var expanded = layer.Inflate(allowance, allowance);
        if (visible.IsEmpty)
        {
            return expanded;
        }

        var area = expanded.Intersect(visible);
        return area.IsEmpty ? visible : area;
    }

    /// <summary>アンカー（右クリックした位置）をパネルの左上にする。</summary>
    /// <param name="anchor">アンカーの位置（DIP）。</param>
    /// <returns>パネルの左上（DIP）。</returns>
    public static EsPoint ResolveInitial(EsPoint anchor) => anchor;

    /// <summary>対象（編集中の注釈）に被さらない位置を求める。</summary>
    /// <param name="target">被せたくない矩形（DIP）。</param>
    /// <param name="panel">パネルの大きさ（DIP）。</param>
    /// <param name="available">パネルを置ける領域（DIP）。</param>
    /// <param name="gap">対象とパネルの間の余白（DIP）。</param>
    /// <returns>パネルの左上（DIP）。</returns>
    /// <remarks>
    /// <b>上・下・右・左の順に探す</b>: 本文の上に置くのが最も自然で、入りきらない場合は下・右・左へ回る。
    /// どれも入りきらないときは上へ置き、置ける領域へ収める（見えないより重なるほうがよい）。
    /// </remarks>
    public static EsPoint ResolveOutside(EsRect target, EsSize panel, EsRect available, double gap)
    {
        var width = Math.Max(1, panel.Width);
        var height = Math.Max(1, panel.Height);
        var left = target.X;
        var top = target.Y;
        var right = target.X + target.Width;
        var bottom = target.Y + target.Height;

        // 上（対象の左上へ揃える）
        var above = top - gap - height;
        if (above >= available.Y)
        {
            return Clamp(new EsPoint(left, above), width, height, available);
        }

        // 下
        var below = bottom + gap;
        if (below + height <= available.Y + available.Height)
        {
            return Clamp(new EsPoint(left, below), width, height, available);
        }

        // 右
        var toRight = right + gap;
        if (toRight + width <= available.X + available.Width)
        {
            return Clamp(new EsPoint(toRight, top), width, height, available);
        }

        // 左
        return Clamp(new EsPoint(left - gap - width, top), width, height, available);
    }

    /// <summary>ドラッグ中のパネルの左上を求める。</summary>
    /// <param name="origin">ドラッグ開始時のパネルの左上（DIP）。</param>
    /// <param name="cursorOrigin">ドラッグ開始時のポインターの位置（DIP）。</param>
    /// <param name="cursorCurrent">現在のポインターの位置（DIP）。</param>
    /// <returns>移動先のパネルの左上（DIP）。</returns>
    public static EsPoint ResolveDrag(EsPoint origin, EsPoint cursorOrigin, EsPoint cursorCurrent)
        => new(
            origin.X + (cursorCurrent.X - cursorOrigin.X),
            origin.Y + (cursorCurrent.Y - cursorOrigin.Y));

    /// <summary>パネルが置ける領域からはみ出さない位置へ補正する。</summary>
    /// <param name="position">補正する位置（<paramref name="available"/> と同じ座標系）。</param>
    /// <param name="width">パネルの幅。</param>
    /// <param name="height">パネルの高さ。</param>
    /// <param name="available">パネルを置ける領域。</param>
    /// <returns>補正した位置。</returns>
    /// <remarks>
    /// <b>UI のウィンドウを画面の作業領域へ収めるときにも使う</b>: 位置と寸法を同じ座標系で渡せば、
    /// ピクセルでも DIP でも同じ規則になる（UI ごとに <c>Math.Clamp</c> を書き写さない）。
    /// 1 軸分の規則（領域より大きい場合は左上へ寄せる）は <see cref="EsSize.ClampOffset"/> が単一所有する。
    /// </remarks>
    public static EsPoint Clamp(EsPoint position, double width, double height, EsRect available)
    {
        var area = new EsSize(available.Width, available.Height);
        return new EsPoint(
            available.X + area.ClampOffset(position.X - available.X, width, EsAxis.X),
            available.Y + area.ClampOffset(position.Y - available.Y, height, EsAxis.Y));
    }
}
