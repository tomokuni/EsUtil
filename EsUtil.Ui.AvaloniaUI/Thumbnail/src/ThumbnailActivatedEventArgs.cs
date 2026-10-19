namespace EsUtil.Ui.AvaloniaUI.Thumbnail;

/// <summary>サムネイルが 2 回押されたときの引数。</summary>
/// <remarks>呼び出し側は <see cref="Tag"/> から対象（ファイルパス等）を解決する。</remarks>
/// <param name="index">項目の添字。</param>
/// <param name="tag">項目へ付けた任意の値。</param>
public sealed class ThumbnailActivatedEventArgs(int index, object? tag) : EventArgs
{
    /// <summary>項目の添字を取得する。</summary>
    public int Index { get; } = index;

    /// <summary>項目へ付けた任意の値を取得する。</summary>
    public object? Tag { get; } = tag;
}
