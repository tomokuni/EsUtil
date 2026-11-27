using EsUtil.Feature.StyledText;

namespace EsUtil.Ui.AvaloniaUI.Annotation;

/// <summary>本文の編集の開始・終了と、編集状態の問い合わせ（入力そのものは持ちません）。</summary>
/// <remarks>
/// <b>入力・IME・キャレット・選択は <c>StyledTextControl</c> が単一所有します</b>: 本クラスは「どの注釈を
/// 編集するか」と「編集を始めた・終えた」の通知だけを持ちます。 キー・変換・キャレット・クリップボードの
/// 規則をここへ写すと、コントロールと二重に持ち、片方だけ直したときに食い違います。<br/>
/// <b>編集状態は編集セッションが所有します</b>: コントロールは同じ実体
/// （`AnnotationEditSession.Editor`）を共有するため、本文と状態を 2 つ持ちません。
/// </remarks>
public sealed partial class AnnotationLayerControl
{
    /// <summary>本文の編集中に発生する（開始・終了の両方）。</summary>
    public event EventHandler? EditingChanged;

    /// <summary>本文の選択範囲（キャレットの位置）が変わったときに発生する。</summary>
    public event EventHandler? TextSelectionChanged;

    /// <summary>本文を編集中かどうかを取得する。</summary>
    public bool IsEditing => _session.IsEditing;

    /// <summary>キャレットの位置（文字数）を取得する。</summary>
    public int CaretIndex => _session.CaretIndex;

    /// <summary>本文の文字を選んでいるかどうかを取得する。</summary>
    public bool HasSelection => IsEditing && _session.SelectionLength > 0;

    /// <summary>選択範囲の開始位置（文字数）を取得する。</summary>
    public int SelectionStart => _session.SelectionStart;

    /// <summary>選択範囲の終了位置（文字数）を取得する。</summary>
    public int SelectionEnd => _session.SelectionEnd;

    /// <summary>選択範囲の長さ（文字数）を取得する。</summary>
    public int SelectionLength => _session.SelectionLength;

    /// <summary>選択中の本文を編集する（表示中のレイアウトのまま、その場で入力する）。</summary>
    /// <returns>編集を始めた場合は <see langword="true"/>。 枠線を選んでいる場合は <see langword="false"/>。</returns>
    /// <remarks>
    /// <b>フォーカスは本文コントロールが受け取ります</b>: レイヤーはフォーカスを取りません（入力と IME の
    /// 宛先を 1 つにするため）。 編集セッションを開始するだけです。
    /// </remarks>
    public bool BeginEdit() => _session.BeginEdit();

    /// <summary>本文の編集を終える（内容は既に文書へ入っている）。</summary>
    public void EndEdit() => _session.EndEdit();

    /// <summary>選択範囲の文字がすべて条件を満たすかどうかを取得する（書式ボタンの押下状態に使う）。</summary>
    /// <param name="match">書式が条件を満たすかどうかを返す関数。</param>
    /// <returns>すべて満たす場合は <see langword="true"/>。 吹き出しを選んでいない場合は <see langword="false"/>。</returns>
    /// <remarks>
    /// <b>範囲のすべてで判定する</b>: 一部だけ太字の範囲でボタンが押されて見えないよう、先頭の文字ではなく
    /// 範囲のすべてが同じ属性かで決める。 選択が無い（キャレットだけ）ときは書式バーで選んだ書式で判定する。
    /// </remarks>
    public bool SelectedTextMatches(Func<TextStyle, bool> match) => _session.SelectedTextMatches(match);

    /// <summary><see cref="EditingChanged"/> を発生させる。</summary>
    private void RaiseEditingChanged() => EditingChanged?.Invoke(this, EventArgs.Empty);

    /// <summary><see cref="TextSelectionChanged"/> を発生させる。</summary>
    private void RaiseTextSelectionChanged() => TextSelectionChanged?.Invoke(this, EventArgs.Empty);

    /// <summary>本文の選択範囲が変わったことを購読者へ通知する（本文コントロールの入力から呼ぶ）。</summary>
    /// <remarks>
    /// <b>入力の経路を 1 つにする</b>: 本文の入力は <c>StyledTextControl</c> が行い、状態は編集セッションと共有する。
    /// セッションの通知は公開 API からの操作でしか出ないため、コントロールの変更も同じイベントへ流す。
    /// </remarks>
    internal void NotifyTextSelectionChanged() => _session.NotifyTextSelectionChanged();
}
