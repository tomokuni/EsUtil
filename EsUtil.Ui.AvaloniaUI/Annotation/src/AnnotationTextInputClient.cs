using Avalonia.Input.TextInput;

namespace EsUtil.Ui.AvaloniaUI.Annotation;

/// <summary>本文の編集で IME（日本語入力）とやり取りするクライアント。</summary>
/// <remarks>
/// <b>入力欄を重ねない代わりに IME の窓口を作る</b>: コントロール自身が本文を描くため、
/// 変換候補の位置・選択範囲・変換中の文字列を IME へ渡す必要がある。<br/>
/// <b>変換中の文字列は文書へ入れない</b>: 確定した文字列だけが <c>OnTextInput</c> から届く
/// （変換中はキャレットの位置へ下線付きで描く）。
/// </remarks>
/// <param name="owner">本文を編集しているコントロール。</param>
internal sealed class AnnotationTextInputClient(AnnotationLayerControl owner) : TextInputMethodClient
{
    /// <summary>IME が変換候補の位置を決めるために使う表示要素を取得する。</summary>
    public override Avalonia.Visual TextViewVisual => owner;

    /// <summary>変換中の文字列を表示できるかどうかを取得する。</summary>
    /// <value>本文の編集中は <see langword="true"/>。</value>
    public override bool SupportsPreedit => owner.IsEditingText;

    /// <summary>周囲の文字列を渡せるかどうかを取得する。</summary>
    /// <value>本文の編集中は <see langword="true"/>。</value>
    public override bool SupportsSurroundingText => owner.IsEditingText;

    /// <summary>本文の文字列を取得する（編集中でなければ空）。</summary>
    public override string SurroundingText => owner.IsEditingText ? owner.TextForInput : string.Empty;

    /// <summary>キャレットの矩形（コントロール座標の DIP）を取得する。</summary>
    public override Avalonia.Rect CursorRectangle => owner.GetCaretRectForInput();

    /// <summary>選択範囲を取得または設定する。</summary>
    public override TextSelection Selection
    {
        get => new(owner.SelectionStart, owner.SelectionEnd);
        set => owner.SetSelectionFromInput(value.Start, value.End);
    }

    /// <summary>変換中の文字列を受け取る。</summary>
    /// <param name="preeditText">変換中の文字列（<see langword="null"/> は変換の終了）。</param>
    public override void SetPreeditText(string? preeditText)
        => owner.SetPreeditTextForInput(preeditText);

    /// <summary>選択範囲が変わったことを IME へ知らせる。</summary>
    /// <remarks>
    /// <b>知らせないと IME の変換候補が古い位置に出る</b>: <see cref="Selection"/> と
    /// <see cref="CursorRectangle"/> は IME が問い合わせる値であり、変わったことを通知しないと
    /// キャレットを動かしても候補の位置と選択範囲が取り残されます。
    /// </remarks>
    internal void NotifySelectionChanged()
    {
        RaiseSelectionChanged();
        RaiseCursorRectangleChanged();
    }

    /// <summary>本文の文字列が変わったことを IME へ知らせる。</summary>
    /// <remarks>周囲の文字列（<see cref="SurroundingText"/>）を渡し直し、変換の基準を最新にします。</remarks>
    internal void NotifyTextChanged()
    {
        RaiseSurroundingTextChanged();
        RaiseSelectionChanged();
        RaiseCursorRectangleChanged();
    }
}
