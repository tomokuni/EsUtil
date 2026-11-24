using System.Globalization;

namespace EsUtil.Feature.Annotation.Core.Text;

/// <summary>本文の文字位置を書記素クラスタ（利用者が 1 文字として見る単位）で扱う規則（単一所有）。</summary>
/// <remarks>
/// <b>位置は UTF-16 の char ではなくクラスタで数える</b>: サロゲート ペア（絵文字）と結合文字（基底文字 + 結合記号）を
/// 半分に割ると文字列が壊れ（孤立したサロゲート）、描画 API が例外を投げる。 キャレット・選択・削除・折り返し・
/// 書式の範囲は、すべて本クラスを通して境界を決める。<br/>
/// <b>境界の判定は <see cref="StringInfo"/> に委ねる</b>（Unicode の拡張書記素クラスタの規則）。 絵文字の ZWJ 連結の
/// 一部は .NET が 1 つのクラスタとして扱わないため、その範囲では要素ごとに分かれる（文字列は壊れない）。<br/>
/// 位置は文字数（<see cref="string.Length"/> と同じ数え方）であり、行・書式の範囲も同じ単位で数える。
/// </remarks>
public static class AnnotationTextCursor
{
    /// <summary>指定した位置がクラスタの境界かを判定する。</summary>
    /// <param name="text">本文。</param>
    /// <param name="index">位置（文字数）。</param>
    /// <returns>境界（先頭・末尾を含む）の場合は <see langword="true"/>。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> が null の場合。</exception>
    public static bool IsClusterBoundary(string text, int index)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (index <= 0 || index >= text.Length)
        {
            return true;
        }

        return Array.BinarySearch(Boundaries(text), index) >= 0;
    }

    /// <summary>位置を、その位置を含むクラスタの先頭へ寄せる。</summary>
    /// <param name="text">本文。</param>
    /// <param name="index">位置（文字数）。 範囲外は本文の範囲へ丸める。</param>
    /// <returns>クラスタの先頭の位置。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> が null の場合。</exception>
    /// <remarks>キャレットを後方へ動かすとき・範囲の開始を決めるときに使う（クラスタを分割しない）。</remarks>
    public static int SnapBackward(string text, int index)
    {
        ArgumentNullException.ThrowIfNull(text);

        var position = Math.Clamp(index, 0, text.Length);
        if (position <= 0)
        {
            return 0;
        }

        if (position >= text.Length)
        {
            return text.Length;
        }

        var starts = Boundaries(text);
        var found = Array.BinarySearch(starts, position);
        if (found >= 0)
        {
            return starts[found];
        }

        var insertion = ~found;
        return insertion == 0 ? 0 : starts[insertion - 1];
    }

    /// <summary>位置を、その位置を含むクラスタの末尾（次のクラスタの先頭）へ寄せる。</summary>
    /// <param name="text">本文。</param>
    /// <param name="index">位置（文字数）。 範囲外は本文の範囲へ丸める。</param>
    /// <returns>クラスタの末尾の位置。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> が null の場合。</exception>
    /// <remarks>キャレットを前方へ動かすとき・範囲の終了を決めるときに使う（クラスタを分割しない）。</remarks>
    public static int SnapForward(string text, int index)
    {
        ArgumentNullException.ThrowIfNull(text);

        var position = Math.Clamp(index, 0, text.Length);
        var starts = Boundaries(text);
        var found = Array.BinarySearch(starts, position);
        if (found >= 0)
        {
            return starts[found];
        }

        var insertion = ~found;
        return insertion >= starts.Length ? text.Length : starts[insertion];
    }

    /// <summary>指定した位置の次（後ろ）のクラスタの先頭を求める。</summary>
    /// <param name="text">本文。</param>
    /// <param name="index">位置（文字数）。</param>
    /// <returns>次のクラスタの先頭。 末尾より後ろの場合は本文の文字数。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> が null の場合。</exception>
    public static int Next(string text, int index)
    {
        ArgumentNullException.ThrowIfNull(text);

        var position = Math.Clamp(index, 0, text.Length);
        var starts = Boundaries(text);
        var found = Array.BinarySearch(starts, position);
        var next = found >= 0 ? found + 1 : ~found;
        return next < starts.Length ? starts[next] : text.Length;
    }

    /// <summary>指定した位置の前（手前）のクラスタの先頭を求める。</summary>
    /// <param name="text">本文。</param>
    /// <param name="index">位置（文字数）。</param>
    /// <returns>前のクラスタの先頭。 先頭より手前の場合は 0。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> が null の場合。</exception>
    public static int Previous(string text, int index)
    {
        ArgumentNullException.ThrowIfNull(text);

        var position = Math.Clamp(index, 0, text.Length);
        var starts = Boundaries(text);
        var found = Array.BinarySearch(starts, position);
        var previous = found >= 0 ? found - 1 : ~found - 1;
        return previous >= 0 ? starts[previous] : 0;
    }

    /// <summary>削除する範囲（1 クラスタ分）を求める。</summary>
    /// <param name="text">本文。</param>
    /// <param name="caret">キャレットの位置（文字数）。</param>
    /// <param name="forward">キャレットの次のクラスタを削除する場合は <see langword="true"/>。</param>
    /// <returns>削除する範囲（開始位置と長さ）。 削除する文字が無い場合は長さ 0。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> が null の場合。</exception>
    /// <remarks>
    /// <b>サロゲート ペア・結合文字を半分に割らない</b>: 削除は常にクラスタ単位で行う。
    /// キャレットがクラスタの内側にある場合は、そのクラスタを削除する。
    /// </remarks>
    public static (int Start, int Length) ResolveDelete(string text, int caret, bool forward)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (text.Length == 0)
        {
            return (0, 0);
        }

        var position = Math.Clamp(caret, 0, text.Length);
        if (forward)
        {
            var start = SnapBackward(text, position);
            return start >= text.Length ? (0, 0) : (start, Next(text, start) - start);
        }

        if (position <= 0)
        {
            return (0, 0);
        }

        var boundary = SnapBackward(text, position);
        var from = boundary < position ? boundary : Previous(text, boundary);
        return (from, Next(text, from) - from);
    }

    /// <summary>本文のクラスタの開始位置（文字数）の一覧を求める。</summary>
    /// <param name="text">本文。</param>
    /// <returns>クラスタの開始位置（昇順。 空の本文は空）。</returns>
    /// <remarks>折り返し・書式の範囲・実寸の測定が同じ境界を使うための入口である（毎回作り直さない）。</remarks>
    internal static int[] Boundaries(string text)
        => string.IsNullOrEmpty(text) ? [] : StringInfo.ParseCombiningCharacters(text);

    /// <summary>本文の末尾のクラスタを求める。</summary>
    /// <param name="text">本文。</param>
    /// <returns>末尾のクラスタ（空の本文は空文字列）。</returns>
    internal static string LastCluster(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        var starts = Boundaries(text);
        return text[starts[^1]..];
    }

    /// <summary>本文のクラスタの数を求める。</summary>
    /// <param name="text">本文。</param>
    /// <returns>クラスタの数（空の本文は 0）。</returns>
    internal static int CountClusters(string text) => Boundaries(text).Length;
}
