namespace EsUtil.Feature.Thumbnail.WinForms;

/// <summary>サムネイルが 2 回押されたときの引数。</summary>
/// <param name="index">項目の添字。</param>
/// <param name="tag">項目へ付けた任意の値。</param>
public sealed class ThumbnailActivatedEventArgs(int index, object? tag) : EventArgs
{
    /// <summary>項目の添字を取得する。</summary>
    public int Index { get; } = index;

    /// <summary>項目へ付けた任意の値を取得する。</summary>
    public object? Tag { get; } = tag;
}
