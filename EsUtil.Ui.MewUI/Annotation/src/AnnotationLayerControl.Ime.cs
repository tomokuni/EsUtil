using Aprillz.MewUI;
using Aprillz.MewUI.Input;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Feature.Annotation.Presenter.Drawing;

namespace EsUtil.Ui.MewUI.Annotation;

/// <summary>注釈レイヤーの IME 連携（変換候補の位置と文字の受け取り）。</summary>
/// <remarks>
/// <b>MewUI はフォーカス中の要素から IME の位置を求める</b>: ウィンドウのバックエンドは
/// <c>ITextCompositionClient</c> の <c>GetCharRectInWindow</c> で変換中の文字の位置を得て、
/// <c>ImmSetCompositionWindow</c> / <c>ImmSetCandidateWindow</c> を呼びます。 本レイヤーが
/// このインターフェイスを実装しないと候補の位置が求まらず、画面の隅（右下）に出ます
/// （実測 2026-11-23: MewUI 版だけで発生）。<br/>
/// <b>文字と変換は本インターフェイスで受け取る</b>: ウィンドウのプレビュー イベントで
/// <c>Handled</c> を立てると、バックエンドが候補の位置を決める処理を飛ばします。 そのため
/// プレビューでは受け取らず、バックエンドから呼ばれる本インターフェイスで受け取ります。
/// </remarks>
public sealed partial class AnnotationLayerControl : ITextCompositionClient, ITextInputClient
{
    /// <summary>変換中の文字列の先頭の位置（文字数）を取得する。</summary>
    /// <value>変換中でなければキャレットの位置。</value>
    public int CompositionStartIndex => _compositionStart;

    /// <summary>IME が変換中かどうかを取得する。</summary>
    /// <value>変換中の文字列を持っている場合は <see langword="true"/>。</value>
    public bool IsComposing => IsEditingText && _preeditText.Length > 0;

    /// <summary>指定した位置の文字の矩形（ウィンドウ座標）を取得する。</summary>
    /// <param name="charIndex">文字位置（文字数）。</param>
    /// <returns>文字の矩形（DIP。 ウィンドウ座標）。 求められない場合は空の矩形。</returns>
    /// <remarks>
    /// <b>ウィンドウ座標で答える</b>: MewUI の <c>Element.Bounds</c> はウィンドウ基準であるため、
    /// ローカル DIP の左上へ <c>Bounds</c> の左上を足します。 変換候補はこの矩形の下へ出ます。
    /// </remarks>
    public Rect GetCharRectInWindow(int charIndex)
    {
        if (!IsEditingText || !TryResolveCaretSource(charIndex, DefaultLayouts, out var caret))
        {
            return default;
        }

        var dip = ToClientDip(new EsPoint(caret.X, caret.Y));
        var (scale, _) = ResolveImageTransform();
        return new Rect(
            Bounds.X + dip.X,
            Bounds.Y + dip.Y,
            Math.Max(1.0, AnnotationEditorAppearance.CaretThicknessDip),
            Math.Max(1.0, caret.Height * scale));
    }

    /// <inheritdoc/>
    public void HandleTextCompositionStart(TextCompositionEventArgs e)
    {
        ApplyCompositionStart(e);
    }

    /// <inheritdoc/>
    public void HandleTextCompositionUpdate(TextCompositionEventArgs e)
    {
        ApplyCompositionUpdate(e);
    }

    /// <inheritdoc/>
    public void HandleTextCompositionEnd(TextCompositionEventArgs e)
    {
        ApplyCompositionEnd(e);
    }

    /// <inheritdoc/>
    public void HandleTextInput(TextInputEventArgs e)
    {
        ApplyTextInput(e);
    }
}
