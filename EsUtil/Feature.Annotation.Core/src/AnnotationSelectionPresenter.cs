using DrawingColor = System.Drawing.Color;

namespace EsUtil.Feature.Annotation;

/// <summary>選択中の注釈の「編集の対象」を読み書きする入口（単一所有）。</summary>
/// <remarks>
/// <b>値の所有者は Presenter</b>: 選択範囲の文字書式・注釈の外観は「いま何を編集しているか」という
/// 提示の状態であり、UI が自前で組み立てない。 4 つの UI 版は本クラスを呼ぶだけにして、
/// 「選択が無ければ適用しない」「選択が無ければ <see langword="null"/>」という規則を 1 か所に置く。<br/>
/// <b>文書は Core が持つ</b>: 本クラスは文書を書き換える入口（<see cref="AnnotationDocument"/> の
/// メソッド）を呼ぶだけで、値の検証・画像内への収め方は Core が単一所有する。
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
    /// <returns>変更後の文書。 適用しない場合（選択が無い・吹き出しでない）は <see langword="null"/>。</returns>
    /// <remarks>
    /// <b>選択が無ければ適用しない</b>（規則は <see cref="AnnotationTextStyleEdit"/> が単一所有する）。
    /// キャレットだけの状態で書式を変えると、キャレット以降の文字が意図せず変わる。
    /// </remarks>
    public static AnnotationDocument? ApplyTextStyle(
        AnnotationDocument document, string? selectedId, (int Start, int Length) range, AnnotationTextStyle style)
    {
        ArgumentNullException.ThrowIfNull(document);

        if (FindBubble(document, selectedId) is not { } bubble
            || AnnotationTextStyleEdit.ResolveRange(range.Start, range.Length) is not { } selection)
        {
            return null;
        }

        var text = bubble.Text.ApplyStyle(selection.Start, selection.Length, style);
        return text == bubble.Text ? null : document.WithText(bubble, text);
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

        if (document.Find(selectedId ?? string.Empty) is not { } item || item.Appearance == appearance)
        {
            return null;
        }

        item.Appearance = appearance;
        return document;
    }

    /// <summary>外観の色を差し替えた外観を作る。</summary>
    /// <param name="appearance">元の外観。</param>
    /// <param name="border">枠線の色（変更しない場合は <see langword="null"/>）。</param>
    /// <param name="fill">内側の色（変更しない場合は <see langword="null"/>）。</param>
    /// <returns>差し替えた外観。</returns>
    /// <remarks>右クリックの外観フライアウトの項目を UI が組み立てないように、写像をここへ置く。</remarks>
    public static AnnotationAppearance WithColors(
        AnnotationAppearance appearance, DrawingColor? border, DrawingColor? fill)
        => appearance with
        {
            Border = border ?? appearance.Border,
            Fill = fill ?? appearance.Fill,
        };

    /// <summary>選択中の吹き出しを取得する。</summary>
    /// <param name="document">文書。</param>
    /// <param name="selectedId">選択中の注釈の識別子。</param>
    /// <returns>吹き出し。 選んでいない・吹き出しでない場合は <see langword="null"/>。</returns>
    private static BubbleAnnotation? FindBubble(AnnotationDocument document, string? selectedId)
        => string.IsNullOrEmpty(selectedId) ? null : document.Find(selectedId) as BubbleAnnotation;
}
