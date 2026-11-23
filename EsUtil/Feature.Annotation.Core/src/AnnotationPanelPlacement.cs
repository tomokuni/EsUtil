using EsUtil.Platform.Core.Geometry;

namespace EsUtil.Feature.Annotation;

/// <summary>フローティング パネルの位置を決める（純粋計算。 単一所有）。</summary>
/// <remarks>
/// <b>座標はアプリの画面の左上を原点とする DIP</b>。<br/>
/// <b>アプリの画面の外へは出さない</b>: 見えなくなる（掴めなくなる）ため、置ける領域へ収める。<br/>
/// <b>編集対象に被せない</b>: 書式バーを本文の上へ重ねると、編集中の文字が隠れて読めない（実測 2026-11-20）。
/// 本文の外（上・下・右・左の順）で置ける領域に収まる場所へ置く。
/// </remarks>
public static class AnnotationPanelPlacement
{
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
    /// <param name="position">補正する位置。</param>
    /// <param name="width">パネルの幅。</param>
    /// <param name="height">パネルの高さ。</param>
    /// <param name="available">パネルを置ける領域。</param>
    /// <returns>補正した位置。</returns>
    internal static EsPoint Clamp(EsPoint position, double width, double height, EsRect available)
    {
        var minX = available.X;
        var minY = available.Y;
        var maxX = Math.Max(minX, available.X + available.Width - width);
        var maxY = Math.Max(minY, available.Y + available.Height - height);
        return new EsPoint(
            Math.Clamp(position.X, minX, maxX),
            Math.Clamp(position.Y, minY, maxY));
    }
}
