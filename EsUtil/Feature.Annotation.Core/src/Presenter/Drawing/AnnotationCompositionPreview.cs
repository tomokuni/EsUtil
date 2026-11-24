using EsUtil.Feature.StyledText.Core.Text;

namespace EsUtil.Feature.Annotation.Presenter.Drawing;

/// <summary>IME の変換中に「本文へ入れたらどう見えるか」を求める（注釈の文書への適用）。</summary>
/// <remarks>
/// <b>変換中の文字列も本文の一部として描く</b>: 変換中の文字列をキャレットの位置へ重ねて描くと、<br/>
/// <b>既存の文字と重なって読めず</b>、キャレットも動かないため「入力が反映されていない」ように見える。<br/>
/// <b>文書は変えない</b>: 返すのは描画用の文書である（不変の文書を差し替えるだけ）。 確定したときにだけ<br/>
/// 本文へ入れる（変換を取り消しても本文は変わらない）。<br/>
/// <b>文字列の組み立ては <see cref="TextCompositionPreview"/> が単一所有する</b>（本クラスは本文を取り出して書き戻すだけ）。
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

        return document.WithText(bubbleId, TextCompositionPreview.Apply(bubble.Text, caret, preedit));
    }

    /// <summary>変換中の文字列を入れた後のキャレットの位置を求める。</summary>
    /// <param name="caret">キャレットの位置（文字数）。</param>
    /// <param name="preedit">変換中の文字列（空なら <paramref name="caret"/> をそのまま返す）。</param>
    /// <returns>変換中の文字列の末尾の位置。</returns>
    public static int ResolveCaret(int caret, string? preedit)
        => TextCompositionPreview.ResolveCaret(caret, preedit);
}