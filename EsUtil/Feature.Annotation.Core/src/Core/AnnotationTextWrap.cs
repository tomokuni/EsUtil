using System.Collections.Immutable;
using EsUtil.Platform.Core.Geometry;

namespace EsUtil.Feature.Annotation.Core;

/// <summary>本文の折り返し（Core が単一所有する）。</summary>
/// <remarks>
/// <b>実寸の計測だけを UI に委ねる</b>: 文字の実寸は描画 API に依存するため関数で受け取る
/// （ルーペの <c>measureText</c> と同じ形）。 折り返しの規則（禁則・明示改行）は本クラスが単一所有するので、
/// 4 つの UI 版で同じ位置に折り返す。<br/>
/// <b>行頭禁則</b>: 句読点・閉じ括弧を行の先頭へ置かない（直前の 1 文字を次の行へ送る）。
/// </remarks>
internal static class AnnotationTextWrap
{
    /// <summary>行頭に置かない文字（句読点・閉じ括弧・中黒）。</summary>
    private static readonly char[] s_lineStartProhibited =
    [
        '、', '。', '，', '．', ',', '.', '）', ')', '」', '』', '】', '〉', '》', '・', '：', ':', '；', ';', '！', '!', '？', '?',
    ];

    /// <summary>折り返した行を返す。</summary>
    /// <param name="text">本文。</param>
    /// <param name="maxWidth">1 行の最大幅（元画像ピクセル）。</param>
    /// <param name="measureText">文字列と書式から実寸を測る関数。</param>
    /// <returns>行の並び（各行は書式ごとの区間）。</returns>
    /// <remarks>
    /// 1. 明示改行（<c>\n</c>）で必ず行を分ける。<br/>
    /// 2. 幅を超える位置で折り返す（文字単位。 欧文の単語分割の最適化は行わない）。<br/>
    /// 3. 行頭に置かない文字が来たら、直前の 1 文字を次の行へ送る。
    /// </remarks>
    internal static ImmutableArray<ImmutableArray<TextSegment>> Wrap(
        RichText text, double maxWidth, Func<string, AnnotationTextStyle, EsSize> measureText)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(measureText);

        var lines = ImmutableArray.CreateBuilder<ImmutableArray<TextSegment>>();
        var line = ImmutableArray<TextSegment>.Empty;
        var widthLimit = AnnotationLimits.CoercePositive(maxWidth, 1.0);

        foreach (var run in text.Runs)
        {
            if (string.IsNullOrEmpty(run.Text))
            {
                continue;
            }

            var style = run.Style.Normalized();
            foreach (var character in run.Text)
            {
                if (character == '\n')
                {
                    lines.Add(line);
                    line = [];
                    continue;
                }

                var candidate = Append(line, character, style, measureText);
                if (Width(candidate) <= widthLimit || line.Length == 0)
                {
                    line = candidate;
                    continue;
                }

                if (IsLineStartProhibited(character) && CharacterCount(line) > 1)
                {
                    // 句読点を行頭へ置かない（直前の 1 文字を次の行へ送る）。
                    // 区間は文字をまとめて持つため、「行の文字数」で判定する（区間の数ではない）。
                    var last = line[^1];
                    var carried = last.Text[^1];
                    var remaining = last.Text[..^1];
                    var kept = remaining.Length == 0
                        ? line.RemoveAt(line.Length - 1)
                        : line.SetItem(
                            line.Length - 1,
                            new TextSegment(remaining, last.Style, measureText(remaining, last.Style).Width));
                    lines.Add(kept);

                    line = Append([], carried, last.Style, measureText);
                    line = Append(line, character, style, measureText);
                    continue;
                }

                lines.Add(line);
                line = Append([], character, style, measureText);
            }
        }

        lines.Add(line);
        return lines.ToImmutable();
    }

    /// <summary>行の幅の合計を求める。</summary>
    /// <param name="line">行。</param>
    /// <returns>幅（元画像ピクセル）。</returns>
    private static double Width(ImmutableArray<TextSegment> line)
    {
        var total = 0.0;
        foreach (var segment in line)
        {
            total += segment.Width;
        }

        return total;
    }

    /// <summary>行の末尾へ 1 文字を加えた行を返す。</summary>
    /// <param name="line">元の行（変更しない）。</param>
    /// <param name="character">加える文字。</param>
    /// <param name="style">書式。</param>
    /// <param name="measureText">実寸を測る関数。</param>
    /// <returns>加えた行（同じ書式が続く場合は末尾の区間を伸ばす）。</returns>
    private static ImmutableArray<TextSegment> Append(
        ImmutableArray<TextSegment> line,
        char character,
        AnnotationTextStyle style,
        Func<string, AnnotationTextStyle, EsSize> measureText)
    {
        if (line.Length > 0 && Equals(line[^1].Style, style))
        {
            var last = line[^1];
            var merged = last.Text + character;
            return line.SetItem(line.Length - 1, new TextSegment(merged, style, measureText(merged, style).Width));
        }

        var segmentText = character.ToString();
        return line.Add(new TextSegment(segmentText, style, measureText(segmentText, style).Width));
    }

    /// <summary>行に含まれる文字数を求める。</summary>
    /// <param name="line">行。</param>
    /// <returns>文字数（区間の数ではない）。</returns>
    private static int CharacterCount(ImmutableArray<TextSegment> line)
    {
        var count = 0;
        foreach (var segment in line)
        {
            count += segment.Text.Length;
        }

        return count;
    }

    /// <summary>行頭に置かない文字かどうかを判定する。</summary>
    /// <param name="character">判定する文字。</param>
    /// <returns>行頭に置かない場合は <see langword="true"/>。</returns>
    private static bool IsLineStartProhibited(char character) => Array.IndexOf(s_lineStartProhibited, character) >= 0;

    /// <summary>折り返した行の 1 区間（文字列・書式・幅）。</summary>
    /// <param name="Text">区間の文字列。</param>
    /// <param name="Style">区間の書式。</param>
    /// <param name="Width">区間の幅（元画像ピクセル）。</param>
    internal readonly record struct TextSegment(string Text, AnnotationTextStyle Style, double Width);
}
