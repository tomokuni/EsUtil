namespace EsUtil.Feature.StyledText.Presenter.Editing;

/// <summary>本文の編集で押される修飾キー（UI の修飾キーの型に依存しない）。</summary>
/// <remarks>
/// <b>UI の型を本層へ持ち込まない</b>: 修飾キーは UI フレームワークごとに型が違うため、
/// 各 UI が自分の型から本列挙型へ写す（写すのは 3 つのビットだけである）。
/// </remarks>
[Flags]
public enum TextEditModifiers
{
    /// <summary>修飾キーなし。</summary>
    None = 0,

    /// <summary>Control キー。</summary>
    Control = 1,

    /// <summary>Shift キー。</summary>
    Shift = 2,

    /// <summary>Alt キー。</summary>
    Alt = 4,
}
