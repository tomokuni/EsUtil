namespace EsUtil.Feature.StyledText.Presenter.Editing;

/// <summary>本文の編集で変わった内容（UI が描画と通知を選ぶための区別）。</summary>
[Flags]
public enum TextEditChange
{
    /// <summary>変わっていない。</summary>
    None = 0,

    /// <summary>本文（文字または書式）が変わった。</summary>
    Text = 1,

    /// <summary>キャレットまたは選択範囲が変わった。</summary>
    Selection = 2,

    /// <summary>編集の開始・終了が変わった。</summary>
    Editing = 4,
}
