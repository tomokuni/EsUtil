using Aprillz.MewUI;
using Aprillz.MewUI.Input;

namespace EsUtil.Ui.MewUI.StyledText;

/// <summary>本文コントロールの IME 連携（変換中の文字列の受け取りと変換候補の位置）。</summary>
/// <remarks>
/// <b>MewUI はフォーカス中の要素から IME の位置を求める</b>: ウィンドウのバックエンドは
/// <c>ITextCompositionClient</c> の <c>GetCharRectInWindow</c> で変換中の文字の位置を得て、
/// <c>ImmSetCompositionWindow</c> / <c>ImmSetCandidateWindow</c> を呼びます。 本コントロールが
/// このインターフェイスを実装しないと候補の位置が求まらず、画面の隅（右下）に出ます。<br/>
/// <b>文字と変換は本インターフェイスで受け取る</b>: ウィンドウのプレビュー イベントで
/// <c>Handled</c> を立てると、バックエンドが候補の位置を決める処理を飛ばします。 そのため
/// プレビューでは受け取らず、バックエンドから呼ばれる本インターフェイスで受け取ります。<br/>
/// <b>変換中の文字列は本文へ入れない</b>: 表示にだけ入れ、確定した文字列だけを本文へ入れます。
/// </remarks>
public sealed partial class StyledTextControl : ITextCompositionClient, ITextInputClient
{
    /// <summary>変換中の文字列の先頭の位置（文字数）。</summary>
    private int _compositionStart;

    /// <summary>変換中の文字列の先頭の位置（文字数）を取得する。</summary>
    /// <value>変換中でなければキャレットの位置。</value>
    public int CompositionStartIndex => _compositionStart;

    /// <summary>IME が変換中かどうかを取得する。</summary>
    /// <value>変換中の文字列を持っている場合は <see langword="true"/>。</value>
    public bool IsComposing => PreeditText.Length > 0;

    /// <summary>指定した位置の文字の矩形（ウィンドウ座標）を取得する。</summary>
    /// <param name="charIndex">文字位置（文字数）。</param>
    /// <returns>文字の矩形（DIP。 ウィンドウ座標）。 求められない場合は空の矩形。</returns>
    /// <remarks>
    /// <b>ウィンドウ座標で答える</b>: MewUI の <c>Bounds</c> はウィンドウ基準であるため、
    /// ローカル DIP の左上へ <c>Bounds</c> の左上を足します。 変換候補はこの矩形の下へ出ます。
    /// </remarks>
    public Rect GetCharRectInWindow(int charIndex)
    {
        if (!IsEditing || ResolveLineIndex() is not { } index
            || index.ResolveCaret(charIndex, MeasureText) is not { } caret)
        {
            return default;
        }

        var scale = ResolveScale();
        return new Rect(
            Bounds.X + (caret.X * scale),
            Bounds.Y + (caret.Y * scale),
            Math.Max(1.0, TextEditorAppearance.CaretThicknessDip),
            Math.Max(1.0, caret.Height * scale));
    }

    /// <inheritdoc/>
    public void HandleTextCompositionStart(TextCompositionEventArgs e)
    {
        if (!IsEditing || !IsFocused)
        {
            return;
        }

        // **変換の先頭を覚える**: 候補の位置は「変換中の文字列の先頭の文字の矩形」から決まる
        _compositionStart = SelectionStart;
        PreeditText = e.Text;
        e.Handled = true;
    }

    /// <inheritdoc/>
    public void HandleTextCompositionUpdate(TextCompositionEventArgs e)
    {
        if (!IsEditing || !IsFocused)
        {
            return;
        }

        PreeditText = e.Text;
        e.Handled = true;
    }

    /// <inheritdoc/>
    public void HandleTextCompositionEnd(TextCompositionEventArgs e)
    {
        if (!IsEditing || !IsFocused)
        {
            return;
        }

        PreeditText = string.Empty;
        _compositionStart = CaretIndex;
        if (!string.IsNullOrEmpty(e.Text))
        {
            ReplaceSelection(EsUtil.Feature.StyledText.Core.Text.TextInput.Normalize(e.Text));
        }
        else
        {
            InvalidateVisual();
        }

        e.Handled = true;
    }

    /// <inheritdoc/>
    public void HandleTextInput(TextInputEventArgs e)
    {
        if (!IsEditing || !IsFocused || string.IsNullOrEmpty(e.Text))
        {
            return;
        }

        // **変換中に届いた確定文字列を取りこぼさない**: IME によっては確定した文字列が
        // 変換の終了より先にここへ届く。 変換中だからと捨てると、確定した瞬間に文字が消える。
        PreeditText = string.Empty;
        _compositionStart = CaretIndex;
        ReplaceSelection(EsUtil.Feature.StyledText.Core.Text.TextInput.Normalize(e.Text));
        e.Handled = true;
    }
}
