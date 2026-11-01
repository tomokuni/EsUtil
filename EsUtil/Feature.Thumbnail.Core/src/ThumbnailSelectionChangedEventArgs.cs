namespace EsUtil.Feature.Thumbnail;

/// <summary>選択が変わったことを通知するイベント引数（変更前後の添字を持つ）。</summary>
/// <param name="previousIndex">変更前の添字（選択が無い場合は -1）。</param>
/// <param name="index">変更後の添字（選択が無い場合は -1）。</param>
/// <remarks>
/// <b>変更前の添字を渡す理由</b>: 選択の見た目は「選択中の項目」にだけ付くため、UI は<b>変更前と変更後の両方</b>を<br/>
/// 描き直す必要がある。 変更前を渡さないと、UI は直前の選択を自前で覚えることになり、<br/>
/// 一覧の組み直し・項目の削除と組み合わさったときに取りこぼす（実際に「前の項目が選択中のまま残る」不具合になった。 実測 2026-10-31）。
/// </remarks>
public sealed class ThumbnailSelectionChangedEventArgs(int previousIndex, int index) : EventArgs
{
    /// <summary>変更前の添字を取得する。</summary>
    /// <value>項目の添字。 選択が無かった場合は -1。</value>
    public int PreviousIndex { get; } = previousIndex;

    /// <summary>変更後の添字を取得する。</summary>
    /// <value>項目の添字。 選択が無い場合は -1。</value>
    public int Index { get; } = index;
}
