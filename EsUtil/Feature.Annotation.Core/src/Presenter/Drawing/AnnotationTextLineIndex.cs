using EsUtil.Platform.Core.Geometry;

namespace EsUtil.Feature.Annotation.Presenter.Drawing;

/// <summary>本文の行と文字位置の対応（キャレット・選択範囲・位置から文字の特定）。</summary>
/// <remarks>
/// <b>折り返しは <see cref="AnnotationPresenter"/> が単一所有する</b>: 本クラスは解決済みの行
/// （<see cref="AnnotationTextLinePlan"/>）と本文の文字列から、文字位置と座標の対応だけを求める。<br/>
/// <b>実寸は UI が測る</b>: 文字列の幅は <c>measureText</c>（文字列と書式から実寸を返す関数）で測る
/// （描画に使うテキスト エンジンと測定を一致させるため、UI 層が渡す）。<br/>
/// <b>改行は 1 文字を消費する</b>: 行の区切りである <c>\n</c> は行の文字数に含めないが、
/// 文字位置としては 1 つ進む（キャレットを行末の次へ動かせるようにするため）。
/// </remarks>
public sealed class AnnotationTextLineIndex
{
    /// <summary>本文の行（左から右、上から下）。</summary>
    private readonly IReadOnlyList<AnnotationTextLinePlan> _lines;

    /// <summary>各行の開始位置（文字数）。</summary>
    private readonly int[] _starts;

    /// <summary>行と開始位置を指定して初期化する。</summary>
    /// <param name="lines">本文の行。</param>
    /// <param name="starts">各行の開始位置（文字数）。</param>
    /// <param name="textLength">本文の文字数。</param>
    private AnnotationTextLineIndex(IReadOnlyList<AnnotationTextLinePlan> lines, int[] starts, int textLength)
    {
        _lines = lines;
        _starts = starts;
        TextLength = textLength;
    }

    /// <summary>本文の行を取得する。</summary>
    public IReadOnlyList<AnnotationTextLinePlan> Lines => _lines;

    /// <summary>本文の文字数を取得する。</summary>
    public int TextLength { get; }

    /// <summary>本文の行と開始位置を解決する。</summary>
    /// <param name="shape">吹き出しの描画計画（行を持つもの）。</param>
    /// <param name="plainText">本文の文字列。</param>
    /// <returns>解決した対応。 行が無い場合は <see langword="null"/>。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="shape"/> または <paramref name="plainText"/> が null の場合。</exception>
    public static AnnotationTextLineIndex? Create(AnnotationShapePlan shape, string plainText)
    {
        ArgumentNullException.ThrowIfNull(shape);
        ArgumentNullException.ThrowIfNull(plainText);

        if (shape.Lines.Count == 0)
        {
            return null;
        }

        var starts = new int[shape.Lines.Count];
        var index = 0;
        for (var i = 0; i < shape.Lines.Count; i++)
        {
            starts[i] = index;
            index += LineTextLength(shape.Lines[i]);
            if (index < plainText.Length && plainText[index] == '\n')
            {
                index++;
            }
        }

        return new AnnotationTextLineIndex(shape.Lines, starts, plainText.Length);
    }

    /// <summary>行の文字数を求める（区間の文字数の合計。 改行は含まない）。</summary>
    /// <param name="line">行。</param>
    /// <returns>文字数。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="line"/> が null の場合。</exception>
    public static int LineTextLength(AnnotationTextLinePlan line)
    {
        ArgumentNullException.ThrowIfNull(line);

        var length = 0;
        foreach (var segment in line.Segments)
        {
            length += segment.Text.Length;
        }

        return length;
    }

    /// <summary>行の開始位置（文字数）を求める。</summary>
    /// <param name="lineIndex">行の位置。</param>
    /// <returns>行の開始位置。</returns>
    public int LineStart(int lineIndex) => _starts[Math.Clamp(lineIndex, 0, _starts.Length - 1)];

    /// <summary>指定した文字位置が入る行を求める。</summary>
    /// <param name="index">文字位置。</param>
    /// <returns>行の位置。</returns>
    public int LineIndexOf(int index)
    {
        for (var i = 0; i < _lines.Count; i++)
        {
            if (index <= _starts[i] + LineTextLength(_lines[i]))
            {
                return i;
            }
        }

        return _lines.Count - 1;
    }

    /// <summary>行の先頭から指定した文字数までの幅を求める。</summary>
    /// <param name="line">行。</param>
    /// <param name="count">文字数（0 以下なら 0）。</param>
    /// <param name="measureText">文字列と書式から実寸を測る関数。</param>
    /// <returns>幅（元画像ピクセル）。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="line"/> または <paramref name="measureText"/> が null の場合。</exception>
    /// <remarks>
    /// <b>区間の原点から測る</b>: 位置の決め方は <see cref="AnnotationTextLineLayout"/> が単一所有する
    /// （行頭から幅を積み上げると、区間の境界・末尾の空白・丸めの分だけ描画とずれる）。
    /// </remarks>
    public static double MeasurePrefix(
        AnnotationTextLinePlan line,
        int count,
        Func<string, AnnotationTextStyle, EsSize> measureText)
    {
        ArgumentNullException.ThrowIfNull(line);
        ArgumentNullException.ThrowIfNull(measureText);

        var x = AnnotationTextLineLayout.ResolveX(line, count, MakePrefixMeasure(measureText));
        return x - line.Bounds.X;
    }

    /// <summary>区間の先頭から指定した文字数までの幅を測る関数を作る。</summary>
    /// <param name="measureText">文字列と書式から実寸を測る関数。</param>
    /// <returns>区間と文字数から幅を返す関数。</returns>
    private static AnnotationTextLineLayout.MeasurePrefix MakePrefixMeasure(
        Func<string, AnnotationTextStyle, EsSize> measureText)
        => (segment, count) => count <= 0
            ? 0
            : measureText(count >= segment.Text.Length ? segment.Text : segment.Text[..count], segment.Style).Width;

    /// <summary>点に最も近い文字位置を求める。</summary>
    /// <param name="point">位置（元画像ピクセル）。</param>
    /// <param name="measureText">文字列と書式から実寸を測る関数。</param>
    /// <returns>文字位置（0 起点）。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="measureText"/> が null の場合。</exception>
    public int IndexAt(EsPoint point, Func<string, AnnotationTextStyle, EsSize> measureText)
    {
        ArgumentNullException.ThrowIfNull(measureText);

        var lineIndex = 0;
        for (var i = 0; i < _lines.Count; i++)
        {
            var bounds = _lines[i].Bounds;
            if (point.Y < bounds.Y + bounds.Height || i == _lines.Count - 1)
            {
                lineIndex = i;
                break;
            }
        }

        var line = _lines[lineIndex];
        return _starts[lineIndex]
            + AnnotationTextLineLayout.ResolveOffset(line, point.X, MakePrefixMeasure(measureText));
    }

    /// <summary>キャレットの位置（元画像ピクセル）を求める。</summary>
    /// <param name="index">文字位置。</param>
    /// <param name="measureText">文字列と書式から実寸を測る関数。</param>
    /// <returns>キャレットの矩形（幅は 0）。 行が無い場合は <see langword="null"/>。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="measureText"/> が null の場合。</exception>
    public AnnotationCaretRect? ResolveCaret(int index, Func<string, AnnotationTextStyle, EsSize> measureText)
    {
        ArgumentNullException.ThrowIfNull(measureText);

        if (_lines.Count == 0)
        {
            return null;
        }

        var lineIndex = LineIndexOf(index);
        var line = _lines[lineIndex];
        var offset = Math.Clamp(index - _starts[lineIndex], 0, LineTextLength(line));
        var x = AnnotationTextLineLayout.ResolveX(line, offset, MakePrefixMeasure(measureText));
        return new AnnotationCaretRect(x, line.Bounds.Y, line.Bounds.Height);
    }

    /// <summary>選択範囲を行ごとの矩形へ分解する。</summary>
    /// <param name="start">選択の開始位置（文字数）。</param>
    /// <param name="end">選択の終了位置（文字数）。</param>
    /// <param name="measureText">文字列と書式から実寸を測る関数。</param>
    /// <returns>行ごとの矩形（元画像ピクセル。 上から下の順）。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="measureText"/> が null の場合。</exception>
    public IReadOnlyList<EsRect> ResolveSelectionRects(
        int start,
        int end,
        Func<string, AnnotationTextStyle, EsSize> measureText)
    {
        ArgumentNullException.ThrowIfNull(measureText);

        if (end <= start || _lines.Count == 0)
        {
            return [];
        }

        var rects = new List<EsRect>(_lines.Count);
        var measurePrefix = MakePrefixMeasure(measureText);
        for (var i = 0; i < _lines.Count; i++)
        {
            var line = _lines[i];
            var lineStart = _starts[i];
            var from = Math.Max(start, lineStart);
            var to = Math.Min(end, lineStart + LineTextLength(line));
            if (to <= from)
            {
                continue;
            }

            var x1 = AnnotationTextLineLayout.ResolveX(line, from - lineStart, measurePrefix);
            var x2 = AnnotationTextLineLayout.ResolveX(line, to - lineStart, measurePrefix);
            rects.Add(new EsRect(x1, line.Bounds.Y, x2 - x1, line.Bounds.Height));
        }

        return rects;
    }
}
