using EsUtil.Feature.Annotation.Core.Text;

namespace EsUtil.Feature.Annotation.Presenter.Drawing;

/// <summary>行の中の文字位置と座標の対応（単一所有。 4 つの UI 版が同じ結果になる）。</summary>
/// <remarks>
/// <b>描画と同じ区間の原点を使う</b>: 本文は書式ごとの区間を <see cref="AnnotationTextSegmentPlan.Bounds"/> の
/// 位置へ描く。 キャレット・選択範囲・当たり判定も<b>同じ区間の原点からの距離</b>で求めないと、
/// 行頭から幅を積み上げた値との差（区間の境界・末尾の空白・丸め）だけ文字からずれる。<br/>
/// <b>文字の実寸だけを UI に委ねる</b>: 実寸は描画 API に依存するため関数で受け取る
/// （折り返しの <c>measureText</c> と同じ形）。 位置の決め方は本クラスが単一所有する。
/// </remarks>
public static class AnnotationTextLineLayout
{
    /// <summary>区間の先頭から指定した文字数までの幅を測る関数。</summary>
    /// <param name="segment">区間。</param>
    /// <param name="count">文字数（0 以上、区間の文字数以下）。</param>
    /// <returns>幅（元画像ピクセル）。</returns>
    public delegate double MeasurePrefix(AnnotationTextSegmentPlan segment, int count);

    /// <summary>行に含まれる文字数を求める。</summary>
    /// <param name="line">行。</param>
    /// <returns>文字数（区間の数ではない）。</returns>
    public static int Length(AnnotationTextLinePlan line)
    {
        ArgumentNullException.ThrowIfNull(line);

        var length = 0;
        foreach (var segment in line.Segments)
        {
            length += segment.Text.Length;
        }

        return length;
    }

    /// <summary>行の先頭から数えた文字位置が入る区間と、その区間の先頭からの文字数を求める。</summary>
    /// <param name="line">行。</param>
    /// <param name="offsetInLine">行の先頭からの文字位置。</param>
    /// <returns>区間の位置と区間の先頭からの文字数。</returns>
    /// <remarks>
    /// <b>区間の境界は次の区間へ属させる</b>: 境界の文字位置のキャレットは、次に描かれる文字の左端
    /// （＝次の区間の <see cref="AnnotationTextSegmentPlan.Bounds"/> の左端）に置くのが正しい。
    /// </remarks>
    public static (int SegmentIndex, int OffsetInSegment) Locate(
        AnnotationTextLinePlan line, int offsetInLine)
    {
        ArgumentNullException.ThrowIfNull(line);

        var offset = Math.Clamp(offsetInLine, 0, Length(line));
        var consumed = 0;
        for (var i = 0; i < line.Segments.Count; i++)
        {
            var length = line.Segments[i].Text.Length;
            if (offset <= consumed + length)
            {
                return (i, offset - consumed);
            }

            consumed += length;
        }

        return line.Segments.Count == 0 ? (0, 0) : (line.Segments.Count - 1, line.Segments[^1].Text.Length);
    }

    /// <summary>行の先頭から数えた文字位置の X 座標を求める。</summary>
    /// <param name="line">行。</param>
    /// <param name="offsetInLine">行の先頭からの文字位置。</param>
    /// <param name="measurePrefix">区間の先頭から指定した文字数までの幅を測る関数。</param>
    /// <returns>X 座標（元画像ピクセル）。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="measurePrefix"/> が null の場合。</exception>
    public static double ResolveX(
        AnnotationTextLinePlan line, int offsetInLine, MeasurePrefix measurePrefix)
    {
        ArgumentNullException.ThrowIfNull(measurePrefix);

        if (line.Segments.Count == 0)
        {
            return line.Bounds.X;
        }

        var (index, offset) = Locate(line, offsetInLine);

        // 区間の境界（区間の末尾＝次の区間の先頭）は、次の区間の原点を使う（次の文字の左端）
        if (offset == 0 && index > 0)
        {
            return line.Segments[index].Bounds.X;
        }

        var segment = line.Segments[index];
        var offsetInSegment = AnnotationTextCursor.SnapBackward(
            segment.Text, Math.Clamp(offset, 0, segment.Text.Length));
        return segment.Bounds.X + measurePrefix(segment, offsetInSegment);
    }

    /// <summary>X 座標に最も近い文字位置（行の先頭から数えた文字数）を求める。</summary>
    /// <param name="line">行。</param>
    /// <param name="x">X 座標（元画像ピクセル）。</param>
    /// <param name="measurePrefix">区間の先頭から指定した文字数までの幅を測る関数。</param>
    /// <returns>文字位置（行の先頭から数えた文字数）。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="measurePrefix"/> が null の場合。</exception>
    public static int ResolveOffset(AnnotationTextLinePlan line, double x, MeasurePrefix measurePrefix)
    {
        ArgumentNullException.ThrowIfNull(measurePrefix);

        var consumed = 0;
        for (var i = 0; i < line.Segments.Count; i++)
        {
            var segment = line.Segments[i];
            var right = segment.Bounds.X + segment.Bounds.Width;
            if (x > right && i < line.Segments.Count - 1)
            {
                consumed += segment.Text.Length;
                continue;
            }

            // **この区間の中**で最も近い文字位置を選ぶ（区間の外の幅を混ぜない）
            var best = 0;
            var bestDistance = double.MaxValue;

            void Consider(int count)
            {
                var distance = Math.Abs(segment.Bounds.X + measurePrefix(segment, count) - x);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = count;
                }
            }

            var boundaries = AnnotationTextCursor.Boundaries(segment.Text);
            foreach (var count in boundaries)
            {
                Consider(count);
            }

            // **末尾も候補にする**: クラスタの開始位置だけでは、行の末尾（最後のクラスタの次）へ
            // キャレットを置けない。
            if (boundaries.Length == 0 || boundaries[^1] != segment.Text.Length)
            {
                Consider(segment.Text.Length);
            }

            return consumed + best;
        }

        return Length(line);
    }

    /// <summary>行のベースライン（元画像ピクセル）を求める。</summary>
    /// <param name="line">行。</param>
    /// <param name="measureMetrics">書式から行の実寸（高さとベースライン）を測る関数。</param>
    /// <returns>ベースラインの Y 座標。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="measureMetrics"/> が null の場合。</exception>
    /// <remarks>
    /// <b>行の中で最も大きい文字を基準にする</b>: 区間ごとに縦位置を決めると、書式が混ざった行で
    /// 文字が上下へずれる（斜体で実寸が変わったときも同じ）。 描画もキャレットも同じベースラインを使う。
    /// </remarks>
    public static double ResolveBaseline(
        AnnotationTextLinePlan line, Func<AnnotationTextStyle, TextLineMetrics> measureMetrics)
    {
        ArgumentNullException.ThrowIfNull(measureMetrics);

        var reference = default(TextLineMetrics);
        foreach (var segment in line.Segments)
        {
            var metrics = measureMetrics(segment.Style);
            if (metrics.Height > reference.Height)
            {
                reference = metrics;
            }
        }

        if (reference.Height <= 0)
        {
            return line.Bounds.Y;
        }

        return line.Bounds.Y + ((line.Bounds.Height - reference.Height) / 2) + reference.Baseline;
    }
}
