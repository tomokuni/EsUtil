namespace EsUtil.Feature.StyledText.Core.Text;

/// <summary>IME の変換中に「本文へ入れたらどう見えるか」を求める（単一所有）。</summary>
/// <remarks>
/// <b>変換中の文字列も本文の一部として描く</b>: 変換中の文字列をキャレットの位置へ重ねて描くと、
/// 既存の文字と重なって読めず、キャレットも動かないため「入力が反映されていない」ように見える。<br/>
/// <b>本文は変えない</b>: 返すのは描画用の文字列である。 確定したときにだけ本文へ入れる
/// （変換を取り消しても本文は変わらない）。
/// </remarks>
public static class TextCompositionPreview
{
    /// <summary>変換中の文字列を本文へ入れた描画用の文字列を返す。</summary>
    /// <param name="text">本文。</param>
    /// <param name="caret">キャレットの位置（文字数）。</param>
    /// <param name="preedit">変換中の文字列（空なら <paramref name="text"/> をそのまま返す）。</param>
    /// <returns>変換中の文字列を入れた文字列（描画だけに使う）。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> が <see langword="null"/> の場合。</exception>
    public static RichText Apply(RichText text, int caret, string? preedit)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (string.IsNullOrEmpty(preedit))
        {
            return text;
        }

        // **変換中の文字列には下線を引く**: 未確定であることを示す（一般的な入力欄と同じ）。
        // 書式はキャレットの位置のものを引き継ぎ、下線だけを足す（色・大きさは本文のまま）。
        var at = Math.Clamp(caret, 0, text.PlainText.Length);
        var style = text.StyleAt(at > 0 ? at - 1 : 0) with { IsUnderline = true };
        return text.Insert(at, preedit).ApplyStyle(at, preedit.Length, style);
    }

    /// <summary>変換中の文字列を入れた後のキャレットの位置を求める。</summary>
    /// <param name="caret">キャレットの位置（文字数）。</param>
    /// <param name="preedit">変換中の文字列（空なら <paramref name="caret"/> をそのまま返す）。</param>
    /// <returns>変換中の文字列の末尾の位置。</returns>
    public static int ResolveCaret(int caret, string? preedit)
        => string.IsNullOrEmpty(preedit) ? caret : caret + preedit.Length;
}