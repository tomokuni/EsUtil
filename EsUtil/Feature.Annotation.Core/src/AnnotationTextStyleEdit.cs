namespace EsUtil.Feature.Annotation;

/// <summary>本文の書式を適用する範囲の規則（単一所有）。</summary>
/// <remarks>
/// <b>適用先は選択した文字だけ</b>: キャレットだけの状態で書式を変えても、既存の文字は変えない
/// （実測 2026-11-21: 4 つの UI 版で「全体」または「キャレット以降」に適用されていた）。<br/>
/// <b>選択が無いときの変更は以降の入力へ回す</b>: 書式バーは常に操作でき、選んだ書式は
/// <see cref="Insert"/> で入力する文字へ適用する（保留の保持は Presenter の <c>AnnotationTypingStyle</c>）。
/// 本文の書式は「選択した文字」へだけ適用する。 選択の有無は UI が持ち、範囲の決定は本クラスが持つ。
/// </remarks>
public static class AnnotationTextStyleEdit
{
    /// <summary>書式を適用する範囲を決める。</summary>
    /// <param name="selectionStart">選択の開始位置（文字数）。</param>
    /// <param name="selectionLength">選択の長さ（0 は選択なし）。</param>
    /// <returns>適用する範囲。 選択が無い場合は <see langword="null"/>。</returns>
    public static (int Start, int Length)? ResolveRange(int selectionStart, int selectionLength)
        => selectionLength <= 0 ? null : (Math.Max(0, selectionStart), selectionLength);

    /// <summary>入力した文字列を本文へ挿入する（以降の入力へ効かせる書式も同時に反映する）。</summary>
    /// <param name="text">本文。</param>
    /// <param name="start">置き換える範囲の開始位置（文字数）。</param>
    /// <param name="length">置き換える範囲の長さ（選択が無ければ 0）。</param>
    /// <param name="inserted">入力する文字列。</param>
    /// <param name="typingStyle">以降の入力へ効かせる書式（無ければ直前の文字の書式を引き継ぐ）。</param>
    /// <returns>挿入後の本文。</returns>
    /// <remarks>
    /// <b>選択が無いときの書式変更は「以降の入力」へ効かせる</b>: 既存の文字は変えず、ここで入力する文字へだけ
    /// 適用する。 挿入する文字が直前の文字の書式を引き継ぐ規則（<see cref="RichText.Insert"/>）はそのまま使い、
    /// 保留した書式があるときだけ上書きする。
    /// </remarks>
    public static RichText Insert(
        RichText text, int start, int length, string inserted, AnnotationTextStyle? typingStyle)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(inserted);

        var from = Math.Max(0, start);
        var result = text.Remove(from, Math.Max(0, length)).Insert(from, inserted);
        return typingStyle is { } style && inserted.Length > 0
            ? result.ApplyStyle(from, inserted.Length, style)
            : result;
    }
}
