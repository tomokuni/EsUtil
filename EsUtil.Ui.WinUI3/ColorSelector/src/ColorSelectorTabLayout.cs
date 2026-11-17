namespace EsUtil.Ui.WinUI3.ColorSelector;

/// <summary>カスタム タブの一覧の並べ方。</summary>
/// <remarks>
/// <b>用途で選ぶ</b>: 名前で探すなら <see cref="List"/>、数が少なく色で選ぶなら <see cref="Grid"/>（■ だけを
/// 6 列に並べるため、スクロールせずに選べる）。
/// </remarks>
public enum ColorSelectorTabLayout
{
    /// <summary>色の四角 + 色名の一覧（スクロールする）。</summary>
    List,

    /// <summary>色の四角だけを 6 列に並べる（名前はツールチップで示す）。</summary>
    Grid,
}
