namespace EsUtil.Feature.StyledText.Core.Editing;

/// <summary>本文の編集で意味を持つキー（UI のキーの型に依存しない）。</summary>
/// <remarks>
/// <b>本層が扱うキーだけを持つ</b>: 文字の入力は各 UI の文字入力イベントが受け取り、本列挙型は
/// キャレットの移動・削除・書式の切替・終了のように<b>意味を持つキー</b>だけを表す。<br/>
/// <b>UI の型を本層へ持ち込まない</b>: 各 UI が自分のキーの型から本列挙型へ写す。
/// </remarks>
public enum TextEditKey
{
    /// <summary>扱わないキー。</summary>
    None,

    /// <summary>A キー。</summary>
    A,

    /// <summary>B キー。</summary>
    B,

    /// <summary>C キー。</summary>
    C,

    /// <summary>I キー。</summary>
    I,

    /// <summary>U キー。</summary>
    U,

    /// <summary>V キー。</summary>
    V,

    /// <summary>X キー。</summary>
    X,

    /// <summary>数字の 5 キー（取り消し線のショートカット）。</summary>
    Digit5,

    /// <summary>Escape キー。</summary>
    Escape,

    /// <summary>Enter キー。</summary>
    Enter,

    /// <summary>BackSpace キー。</summary>
    Back,

    /// <summary>Delete キー。</summary>
    Delete,

    /// <summary>左矢印キー。</summary>
    Left,

    /// <summary>右矢印キー。</summary>
    Right,

    /// <summary>上矢印キー。</summary>
    Up,

    /// <summary>下矢印キー。</summary>
    Down,

    /// <summary>Home キー。</summary>
    Home,

    /// <summary>End キー。</summary>
    End,
}
