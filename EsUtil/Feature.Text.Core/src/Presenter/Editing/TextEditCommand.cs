namespace EsUtil.Feature.Text.Presenter.Editing;

/// <summary>本文の編集で実行する操作（キーの割り当てから解決する）。</summary>
/// <remarks>
/// <b>キーの割り当てと操作を分ける</b>: どのキーがどの操作かを <see cref="TextEditKeyMap"/> が単一所有し、
/// 操作の実行は UI（と編集セッション）が行う。<br/>
/// <b>クリップボードと終了は UI が実行する</b>: <see cref="Copy"/> / <see cref="Cut"/> / <see cref="Paste"/> /
/// <see cref="EndEditing"/> は UI の仕組み（クリップボード・フォーカス）を要するため、本列挙型は要求だけを表す。
/// </remarks>
public enum TextEditCommand
{
    /// <summary>実行する操作はない。</summary>
    None,

    /// <summary>すべて選択する。</summary>
    SelectAll,

    /// <summary>選択範囲をコピーする。</summary>
    Copy,

    /// <summary>選択範囲を切り取る。</summary>
    Cut,

    /// <summary>貼り付ける。</summary>
    Paste,

    /// <summary>編集を終える。</summary>
    EndEditing,

    /// <summary>改行を挿入する。</summary>
    InsertLineBreak,

    /// <summary>キャレットの前の 1 クラスタを削除する。</summary>
    DeleteBackward,

    /// <summary>キャレットの次の 1 クラスタを削除する。</summary>
    DeleteForward,

    /// <summary>キャレットを 1 クラスタ左へ移す。</summary>
    CaretLeft,

    /// <summary>キャレットを 1 クラスタ右へ移す。</summary>
    CaretRight,

    /// <summary>キャレットを 1 行上へ移す。</summary>
    CaretUp,

    /// <summary>キャレットを 1 行下へ移す。</summary>
    CaretDown,

    /// <summary>キャレットを行の先頭へ移す。</summary>
    CaretLineStart,

    /// <summary>キャレットを行の末尾へ移す。</summary>
    CaretLineEnd,

    /// <summary>太字を切り替える。</summary>
    ToggleBold,

    /// <summary>斜体を切り替える。</summary>
    ToggleItalic,

    /// <summary>下線を切り替える。</summary>
    ToggleUnderline,

    /// <summary>取り消し線を切り替える。</summary>
    ToggleStrike,
}
