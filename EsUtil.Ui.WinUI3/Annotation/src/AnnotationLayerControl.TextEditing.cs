using EsUtil.Feature.StyledText;

namespace EsUtil.Ui.WinUI3.Annotation;

/// <summary>本文の編集の開始・終了と、編集状態の問い合わせ（入力そのものは持ちません）。</summary>
/// <remarks>
/// <b>入力・IME・キャレット・選択は <c>StyledTextControl</c> が単一所有します</b>: 本クラスは「どの注釈を
/// 編集するか」と「編集を始めた・終えた」の通知だけを持ちます。 キー・変換・キャレット・クリップボードの
/// 規則をここへ写すと、コントロールと二重に持ち、片方だけ直したときに食い違います。
/// </remarks>
public sealed partial class AnnotationLayerControl
{
    /// <summary>本文の編集中に発生する（開始・終了の両方）。</summary>
    public event EventHandler? EditingChanged;

    /// <summary>本文の選択範囲（キャレットの位置）が変わったときに発生する。</summary>
    public event EventHandler? TextSelectionChanged;

    /// <summary>本文を編集中かどうかを取得する。</summary>
    public bool IsEditingText => _session.IsEditingText;

    /// <summary>キャレットの位置（文字数）を取得する。</summary>
    public int CaretIndex => _session.CaretIndex;

    /// <summary>本文の文字を選んでいるかどうかを取得する。</summary>
    public bool HasTextSelection => IsEditingText && TextSelectionLength > 0;

    /// <summary>選択範囲の開始位置（文字数）を取得する。</summary>
    public int TextSelectionStart => _session.SelectionStart;

    /// <summary>選択範囲の終了位置（文字数）を取得する。</summary>
    public int TextSelectionEnd => _session.SelectionEnd;

    /// <summary>選択範囲の長さ（文字数）を取得する。</summary>
    public int TextSelectionLength => TextSelectionEnd - TextSelectionStart;

    /// <summary>選択範囲（開始位置と長さ）を取得または設定する。</summary>
    /// <value>開始位置（文字数）と長さ（文字数）。 範囲は本文の範囲へ丸める。</value>
    public (int Start, int Length) TextSelectionRange
    {
        get => (TextSelectionStart, TextSelectionLength);
        set => _session.TextSelectionRange = (value.Start, value.Length);
    }

    /// <summary>本文の編集を始める（吹き出しを選んでいるときだけ）。</summary>
    /// <returns>編集を始めた場合は <see langword="true"/>。 枠線を選んでいる場合は <see langword="false"/>。</returns>
    /// <remarks>
    /// <b>フォーカスは本文コントロールが受け取ります</b>: レイヤーはフォーカスを取りません（入力と IME の
    /// 宛先を 1 つにするため）。 編集セッションを開始するだけです。
    /// </remarks>
    public bool BeginTextEditing() => _session.BeginTextEditing();

    /// <summary>本文の編集を終える（内容は既に文書へ入っている）。</summary>
    public void EndTextEditing() => _session.EndTextEditing();

    /// <summary>選択範囲の文字がすべて条件を満たすかどうかを取得する（書式ボタンの押下状態に使う）。</summary>
    /// <param name="match">書式が条件を満たすかどうかを返す関数。</param>
    /// <returns>すべて満たす場合は <see langword="true"/>。 吹き出しを選んでいない場合は <see langword="false"/>。</returns>
    public bool SelectedTextMatches(Func<TextStyle, bool> match) => _session.SelectedTextMatches(match);

    /// <summary>選択範囲（選択が無ければ全体）の書式を変換する。</summary>
    /// <param name="transform">現在の書式から新しい書式を作る関数。</param>
    internal void TransformSelectionStyle(Func<TextStyle, TextStyle> transform)
        => _session.TransformTextStyle(transform);

    /// <summary>本文全体の書式を差し替える。</summary>
    /// <param name="style">新しい書式。</param>
    internal void ApplyFullStyle(TextStyle style) => ApplyTextStyle(style);
}