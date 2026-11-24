using System.Collections.Immutable;
using EsUtil.Feature.StyledText.Core.Defaults;
using EsUtil.Feature.StyledText.Core.Internal;
using EsUtil.Platform.Core.Geometry;

namespace EsUtil.Feature.StyledText.Core.Text;

/// <summary>本文を折り返し、行の矩形と書式ごとの区間へ配置する（単一所有）。</summary>
/// <remarks>
/// <b>折り返しと配置を 1 か所で決める</b>: 折り返しの位置（<see cref="TextWrap"/>）と行の縦位置は対であり、
/// 別々に実装すると行の高さとキャレットの高さが食い違う。 4 つの UI 版は本クラスの結果を描くだけにする。<br/>
/// <b>実寸の計測だけを UI に委ねる</b>: 文字の実寸は描画 API に依存するため関数で受け取る。
/// </remarks>
public static class TextLayout
{
    /// <summary>本文を本文の矩形へ折り返して配置する。</summary>
    /// <param name="text">本文。</param>
    /// <param name="body">本文を置く矩形（元画像ピクセル）。</param>
    /// <param name="measureText">文字列と書式から実寸を測る関数。</param>
    /// <param name="lineHeightFactor">行間の倍率（1 未満・非有限は 1）。</param>
    /// <returns>行の並び（左揃え・縦は中央揃え）。 本文が空の場合は空。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> または <paramref name="measureText"/> が null の場合。</exception>
    public static ImmutableArray<TextLinePlan> ResolveLines(
        RichText text,
        EsRect body,
        Func<string, TextStyle, EsSize> measureText,
        double lineHeightFactor)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(measureText);

        var wrapped = TextWrap.Wrap(text, body.Width, measureText);
        if (wrapped.Length == 0)
        {
            return [];
        }

        var factor = double.IsFinite(lineHeightFactor) && lineHeightFactor > 0 ? lineHeightFactor : 1.0;

        // 行の高さは行内で最も大きい文字に合わせる（書式が混ざっても行の間隔が揃う）
        var heights = new double[wrapped.Length];
        for (var i = 0; i < wrapped.Length; i++)
        {
            var height = 0.0;
            foreach (var segment in wrapped[i])
            {
                height = Math.Max(height, measureText("Ag", segment.Style).Height * factor);
            }

            heights[i] = height;
        }

        FillEmptyLineHeights(heights, measureText, factor);

        var totalHeight = 0.0;
        foreach (var height in heights)
        {
            totalHeight += height;
        }

        // 本文の矩形より縦に大きい場合は上へ詰める（はみ出しは描画側のクリップに任せる）
        var offsetY = Math.Max(0.0, (body.Height - totalHeight) / 2);
        var lines = ImmutableArray.CreateBuilder<TextLinePlan>(wrapped.Length);
        var y = body.Y + offsetY;
        for (var i = 0; i < wrapped.Length; i++)
        {
            var segments = ImmutableArray.CreateBuilder<TextSegmentPlan>(wrapped[i].Length);
            var x = body.X;
            foreach (var segment in wrapped[i])
            {
                segments.Add(new TextSegmentPlan(
                    segment.Text, segment.Style, new EsRect(x, y, segment.Width, heights[i])));
                x += segment.Width;
            }

            lines.Add(new TextLinePlan(new EsRect(body.X, y, body.Width, heights[i]), segments.ToImmutable()));
            y += heights[i];
        }

        return lines.ToImmutable();
    }

    /// <summary>文字を持たない行（空行）にも高さを与える。</summary>
    /// <param name="heights">行の高さ（空行は 0 で入っている）。 本メソッドが書き換える。</param>
    /// <param name="measureText">文字列と書式から実寸を測る関数。</param>
    /// <param name="lineHeightFactor">行間の倍率。</param>
    /// <remarks>
    /// <b>空行を 0 にしない</b>: 0 のままだと、明示改行で空けた行が詰まって見え（空行を入れられない）、
    /// キャレットも高さを持たない。 近い行の高さへ合わせ、前後に行が無ければ既定の文字の高さを使う。
    /// </remarks>
    private static void FillEmptyLineHeights(
        double[] heights, Func<string, TextStyle, EsSize> measureText, double lineHeightFactor)
    {
        var fallback = measureText("Ag", TextDefaults.DefaultStyle).Height * lineHeightFactor;
        for (var i = 0; i < heights.Length; i++)
        {
            if (heights[i] > 0)
            {
                continue;
            }

            // 直前の行 → 直後の行 → 既定の高さ の順に使う（書式が大きい本文でも行の間隔が揃う）
            var height = 0.0;
            for (var previous = i - 1; previous >= 0 && height <= 0; previous--)
            {
                height = heights[previous];
            }

            for (var next = i + 1; next < heights.Length && height <= 0; next++)
            {
                height = heights[next];
            }

            heights[i] = height > 0 ? height : fallback;
        }
    }
}