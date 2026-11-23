namespace EsUtil.Feature.Annotation;

/// <summary>書式バーで選んだ書式（選択が無い間は以降の入力へ効かせる値）を保持する（単一所有）。</summary>
/// <remarks>
/// <b>値の所有者は Presenter</b>: 「いま何を入力しようとしているか」は提示の状態であり、4 つの UI 版が
/// 同じ規則で扱う。 UI は覚えて読むだけで、判定を持たない。<br/>
/// <b>既存の文字には効かせない</b>: 適用先の決定は <see cref="AnnotationTextStyleEdit.ResolveRange"/> が
/// 単一所有する（選択が無ければ適用しない）。 本クラスが持つ値は、次に入力する文字へだけ効く。
/// </remarks>
public sealed class AnnotationTypingStyle
{
    /// <summary>以降の入力へ効かせる書式を取得する。</summary>
    /// <value>まだ書式バーを操作していない場合は <see langword="null"/>（直前の文字の書式を引き継ぐ）。</value>
    public AnnotationTextStyle? Value { get; private set; }

    /// <summary>以降の入力へ効かせる書式を覚える。</summary>
    /// <param name="style">書式。</param>
    /// <exception cref="ArgumentNullException"><paramref name="style"/> が <see langword="null"/> の場合。</exception>
    public void Remember(AnnotationTextStyle style)
    {
        ArgumentNullException.ThrowIfNull(style);
        Value = style;
    }

    /// <summary>書式バーへ表示する値を決める。</summary>
    /// <param name="resolved">選択範囲（選択が無ければキャレットの位置）から解決した書式。</param>
    /// <param name="hasSelection">本文の文字を選んでいる場合は <see langword="true"/>。</param>
    /// <returns>表示する書式。 選択が無い間は覚えた書式。</returns>
    /// <remarks>
    /// <b>選んだ書式が表示に残る</b>: 選択が無いときに書式を変えると本文は変わらないため、そのまま
    /// キャレットの位置の書式を表示すると「操作が効いていない」ように見える（実測 2026-11-22）。
    /// </remarks>
    public AnnotationTextStyle ResolveDisplay(AnnotationTextStyle resolved, bool hasSelection)
        => !hasSelection && Value is { } pending ? pending : resolved;
}
