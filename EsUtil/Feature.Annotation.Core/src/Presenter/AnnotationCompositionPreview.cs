namespace EsUtil.Feature.Annotation.Presenter;

/// <summary>IME の変換中に「本文へ入れたらどう見えるか」を求める（単一所有）。</summary>
/// <remarks>
/// <b>変換中の文字列も本文の一部として描く</b>: 変換中の文字列をキャレットの位置へ重ねて描くと、
/// <b>既存の文字と重なって読めず</b>、キャレットも動かないため「入力が反映されていない」ように見えます
/// （実測 2026-11-22: 4 つの UI 版すべてで同じ見え方）。<br/>
/// <b>文書は変えない</b>: 返すのは描画用の複製です。 確定したときにだけ本文へ入れます（変換を取り消しても
/// 本文は変わりません）。
/// </remarks>
public static class AnnotationCompositionPreview
{
    /// <summary>変換中の文字列を本文へ入れた描画用の文書を返す。</summary>
    /// <param name="document">本文の文書。</param>
    /// <param name="bubbleId">編集している吹き出しの識別子（無い場合は <see langword="null"/>）。</param>
    /// <param name="caret">キャレットの位置（文字数）。</param>
    /// <param name="preedit">変換中の文字列（空なら <paramref name="document"/> をそのまま返す）。</param>
    /// <returns>変換中の文字列を入れた文書（描画だけに使う）。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="document"/> が <see langword="null"/> の場合。</exception>
    public static AnnotationDocument Apply(
        AnnotationDocument document, string? bubbleId, int caret, string? preedit)
    {
        ArgumentNullException.ThrowIfNull(document);

        if (string.IsNullOrEmpty(preedit) || bubbleId is null)
        {
            return document;
        }

        if (document.Find(bubbleId) is not BubbleAnnotation bubble)
        {
            return document;
        }

        // **変換中の文字列には下線を引く**: 未確定であることを示す（一般的な入力欄と同じ）。
        // 書式はキャレットの位置のものを引き継ぎ、下線だけを足す（色・大きさは本文のまま）。
        var at = Math.Clamp(caret, 0, bubble.Text.PlainText.Length);
        var style = bubble.Text.StyleAt(at > 0 ? at - 1 : 0) with { IsUnderline = true };
        var inserted = bubble.Text.Insert(at, preedit).ApplyStyle(at, preedit.Length, style);
        return document.WithText(bubble, inserted);
    }

    /// <summary>変換中の文字列を入れた後のキャレットの位置を求める。</summary>
    /// <param name="caret">キャレットの位置（文字数）。</param>
    /// <param name="preedit">変換中の文字列（空なら <paramref name="caret"/> をそのまま返す）。</param>
    /// <returns>変換中の文字列の末尾の位置。</returns>
    public static int ResolveCaret(int caret, string? preedit)
        => string.IsNullOrEmpty(preedit) ? caret : caret + preedit.Length;
}
