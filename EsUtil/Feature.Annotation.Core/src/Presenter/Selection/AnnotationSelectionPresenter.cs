using EsUtil.Feature.Annotation.Core.Defaults;
using EsUtil.Feature.Annotation.Core.Text;

namespace EsUtil.Feature.Annotation.Presenter.Selection;

/// <summary>選択中の注釈の「編集の対象」を読み書きする入口（単一所有）。</summary>
/// <remarks>
/// <b>値の所有者は提示層</b>: 選択範囲の文字書式・注釈の外観は「いま何を編集しているか」という
/// 提示の状態であり、UI が自前で組み立てない。 4 つの UI 版は本クラスを呼ぶだけにして、
/// 「選択が無ければ適用しない」という規則を 1 か所に置く。<br/>
/// <b>文書は Core が持つ</b>: 本クラスは文書の変更入口（<see cref="AnnotationDocument"/> の <c>With...</c>）を
/// 呼ぶだけで、値の検証・画像内への収め方は Core が単一所有する。<br/>
/// <b>変更が無い場合は <see langword="null"/> を返す</b>（呼び出し側は履歴へ積まない）。
/// </remarks>
public static class AnnotationSelectionPresenter
{
    /// <summary>選択中の注釈の文字書式を取得する。</summary>
    /// <param name="document">文書。</param>
    /// <param name="selectedId">選択中の注釈の識別子（無い場合は <see langword="null"/>）。</param>
    /// <param name="range">選択範囲。</param>
    /// <returns>選択範囲の先頭の書式。 吹き出しを選んでいない場合は既定の文字書式。</returns>
    public static AnnotationTextStyle ResolveTextStyle(
        AnnotationDocument document, string? selectedId, (int Start, int Length) range)
    {
        ArgumentNullException.ThrowIfNull(document);

        if (FindBubble(document, selectedId) is not { } bubble)
        {
            return AnnotationDefaults.TextStyle;
        }

        var start = Math.Clamp(range.Start, 0, bubble.Text.PlainText.Length);
        return bubble.Text.StyleAt(start);
    }

    /// <summary>選択中の注釈の外観を取得する。</summary>
    /// <param name="document">文書。</param>
    /// <param name="selectedId">選択中の注釈の識別子（無い場合は <see langword="null"/>）。</param>
    /// <returns>外観。 何も選んでいない場合は <see langword="null"/>。</returns>
    public static AnnotationAppearance? ResolveAppearance(AnnotationDocument document, string? selectedId)
    {
        ArgumentNullException.ThrowIfNull(document);

        return document.Find(selectedId ?? string.Empty)?.Appearance;
    }

    /// <summary>選択中の吹き出しを取得する。</summary>
    /// <param name="document">文書。</param>
    /// <param name="selectedId">選択中の注釈の識別子（無い場合は <see langword="null"/>）。</param>
    /// <returns>吹き出し。 選んでいない・吹き出しでない場合は <see langword="null"/>。</returns>
    /// <remarks>
    /// <b>吹き出しだけが持つ値の入口</b>である（本文の余白・しっぽの幅・しっぽの辺）。
    /// UI が <c>Selected as BubbleAnnotation</c> を書くと、同じ判定が 4 つの UI 版へ散らばる。
    /// </remarks>
    public static BubbleAnnotation? ResolveBubble(AnnotationDocument document, string? selectedId)
    {
        ArgumentNullException.ThrowIfNull(document);

        return FindBubble(document, selectedId);
    }

    /// <summary>選択範囲に文字書式を適用する。</summary>
    /// <param name="document">文書。</param>
    /// <param name="selectedId">選択中の注釈の識別子。</param>
    /// <param name="range">選択範囲。</param>
    /// <param name="style">書式。</param>
    /// <returns>変更後の文書。 適用しない場合（選択が無い・吹き出しでない・同じ値）は <see langword="null"/>。</returns>
    /// <remarks>
    /// <b>選択が無ければ適用しない</b>（規則は <see cref="AnnotationTextStyleEdit"/> が単一所有する）。
    /// キャレットだけの状態で書式を変えると、キャレット以降の文字が意図せず変わる。
    /// </remarks>
    public static AnnotationDocument? ApplyTextStyle(
        AnnotationDocument document, string? selectedId, (int Start, int Length) range, AnnotationTextStyle style)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(style);

        if (FindBubble(document, selectedId) is not { } bubble
            || AnnotationTextStyleEdit.ResolveRange(range.Start, range.Length) is not { } selection)
        {
            return null;
        }

        return NullIfUnchanged(document, document.WithText(
            bubble.Id, bubble.Text.ApplyStyle(selection.Start, selection.Length, style)));
    }

    /// <summary>選択中の注釈の外観を差し替える。</summary>
    /// <param name="document">文書。</param>
    /// <param name="selectedId">選択中の注釈の識別子。</param>
    /// <param name="appearance">新しい外観。</param>
    /// <returns>変更後の文書。 変更しない場合（選択が無い・同じ値）は <see langword="null"/>。</returns>
    public static AnnotationDocument? ApplyAppearance(
        AnnotationDocument document, string? selectedId, AnnotationAppearance appearance)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(appearance);

        return string.IsNullOrEmpty(selectedId)
            ? null
            : NullIfUnchanged(document, document.WithAppearance(selectedId, appearance));
    }

    /// <summary>選択中の吹き出しの本文の余白を差し替える。</summary>
    /// <param name="document">文書。</param>
    /// <param name="selectedId">選択中の注釈の識別子。</param>
    /// <param name="padding">新しい余白（元画像ピクセル。 安全域へ補正する）。</param>
    /// <returns>変更後の文書。 変更しない場合（選択が無い・吹き出しでない・同じ値）は <see langword="null"/>。</returns>
    public static AnnotationDocument? SetBodyPadding(
        AnnotationDocument document, string? selectedId, double padding)
    {
        ArgumentNullException.ThrowIfNull(document);

        return selectedId is null || FindBubble(document, selectedId) is null
            ? null
            : NullIfUnchanged(document, document.WithBodyPadding(selectedId, padding));
    }

    /// <summary>選択中の吹き出しを取得する。</summary>
    /// <param name="document">文書。</param>
    /// <param name="selectedId">選択中の注釈の識別子。</param>
    /// <returns>吹き出し。 選んでいない・吹き出しでない場合は <see langword="null"/>。</returns>
    private static BubbleAnnotation? FindBubble(AnnotationDocument document, string? selectedId)
        => string.IsNullOrEmpty(selectedId) ? null : document.Find(selectedId) as BubbleAnnotation;

    /// <summary>変更が無い（同じ実体の）場合は <see langword="null"/> を返す。</summary>
    /// <param name="before">変更前の文書。</param>
    /// <param name="after">変更後の文書。</param>
    /// <returns>変更後の文書。 変更が無い場合は <see langword="null"/>。</returns>
    private static AnnotationDocument? NullIfUnchanged(AnnotationDocument before, AnnotationDocument after)
        => ReferenceEquals(before, after) ? null : after;
}
