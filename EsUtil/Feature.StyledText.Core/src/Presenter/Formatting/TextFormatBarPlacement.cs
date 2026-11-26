using EsUtil.Platform.Core.Geometry;

namespace EsUtil.Feature.StyledText.Presenter.Formatting;

/// <summary>ポップアップで出す書式バーの位置を決める（純粋計算。 単一所有）。</summary>
/// <remarks>
/// <b>座標は本文コントロールの左上を原点とする DIP</b>（ポップアップの位置も同じ座標系で指定する）。<br/>
/// <b>選択範囲に被せない</b>: 選択の上に出し、入りきらない場合は下へ回る（編集中の文字が隠れると
/// 何を書式しているのか分からない）。<br/>
/// <b>見えている範囲の外へは出さない</b>: はみ出すと切れて押せなくなるため、置ける領域へ収める。<br/>
/// <b>位置の規則を UI ごとに書き写さない</b>: 4 つの UI 版が同じ結果になるよう、本クラスが単一所有する。
/// </remarks>
public static class TextFormatBarPlacement
{
    /// <summary>選択範囲とバーの間の余白（DIP）を取得する。</summary>
    /// <value>6 DIP。</value>
    public const double Gap = 6;

    /// <summary>ポップアップで出す書式バーの左上（DIP）を求める。</summary>
    /// <param name="anchor">選択範囲の矩形（本文コントロールの座標の DIP）。</param>
    /// <param name="bar">バーの実寸（DIP）。</param>
    /// <param name="available">バーを置ける領域（本文コントロールの座標の DIP）。</param>
    /// <param name="gap">選択範囲とバーの間の余白（DIP）。</param>
    /// <returns>バーの左上（DIP）。</returns>
    /// <remarks>
    /// <b>上・下の順に探す</b>: 選択の上に出すのが最も自然で、入りきらない場合は下へ回る。 どちらにも
    /// 入りきらないときは上へ置き、置ける領域へ収める（見えないより重なるほうがよい）。<br/>
    /// <b>横は選択範囲の左端へ揃える</b>: センタリングすると、選択の長さで位置が動いて視線が定まらない。
    /// </remarks>
    public static EsPoint ResolvePopupPosition(EsRect anchor, EsSize bar, EsRect available, double gap)
    {
        var width = Math.Max(1, bar.Width);
        var height = Math.Max(1, bar.Height);
        var left = anchor.X;
        var top = anchor.Y;
        var bottom = anchor.Y + anchor.Height;

        var above = top - gap - height;
        var below = bottom + gap;

        double y;
        if (available.Height <= 0)
        {
            y = above;
        }
        else if (above >= available.Y)
        {
            y = above;
        }
        else if (below + height <= available.Y + available.Height)
        {
            y = below;
        }
        else
        {
            y = above;
        }

        return new EsPoint(
            ClampAxis(left, width, available.X, available.Width),
            ClampAxis(y, height, available.Y, available.Height));
    }

    /// <summary>1 軸分の位置を置ける領域へ収める。</summary>
    /// <param name="position">収める前の位置。</param>
    /// <param name="length">収める対象の長さ。</param>
    /// <param name="areaStart">置ける領域の始点。</param>
    /// <param name="areaLength">置ける領域の長さ。</param>
    /// <returns>収めた後の位置。 領域が未確定（0 以下）の場合はそのまま返す。</returns>
    /// <remarks>対象が領域より大きい場合は領域の始点へ寄せる（左上寄せ）。</remarks>
    private static double ClampAxis(double position, double length, double areaStart, double areaLength)
    {
        if (areaLength <= 0)
        {
            return position;
        }

        var max = Math.Max(areaStart, areaStart + areaLength - length);
        return Math.Clamp(position, areaStart, max);
    }
}
