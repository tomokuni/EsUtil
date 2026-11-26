namespace EsUtil.Ui.WinForms.StyledText;

/// <summary><see cref="StyledTextControl"/> を書式バー（<see cref="IStyledTextFormatBar"/>）の相手にする接続。</summary>
/// <remarks>
/// <b>ホストが接続を書かない</b>: バーは「選択範囲の書式を読み書きする相手」だけを必要とする。
/// 本クラスが <see cref="ITextFormatBarContext"/> を実装し、コントロールの公開 API へ転送する
/// （ホストごとに同じ接続を書き写すと、適用の規則がずれる）。<br/>
/// <b>適用の規則はコントロールが単一所有する</b>: 選択が無いときは以降の入力へ回す等の判断は
/// <see cref="StyledTextControl.ApplyTextStyle(TextStyle)"/> と <see cref="StyledTextControl.TransformTextStyle(Func{TextStyle, TextStyle})"/> が持つ。
/// </remarks>
/// <param name="control">書式を読み書きする本文コントロール。</param>
public sealed class StyledTextFormatBarContext(StyledTextControl control) : ITextFormatBarContext
{
    /// <inheritdoc/>
    public TextStyle SelectedTextStyle => control.SelectedTextStyle;

    /// <inheritdoc/>
    public bool HasTextSelection => control.HasSelection;

    /// <inheritdoc/>
    public bool SelectedTextMatches(Func<TextStyle, bool> match) => control.SelectedTextMatches(match);

    /// <inheritdoc/>
    public bool Transform(Func<TextStyle, TextStyle> transform) => control.TransformTextStyle(transform);

    /// <inheritdoc/>
    public bool Apply(TextStyle style) => control.ApplyTextStyle(style);
}