namespace EsUtil.Feature.Thumbnail;

/// <summary>一覧の項目の連続した範囲（両端を含む）。</summary>
/// <remarks>
/// 可視範囲（画面に見えている項目）と生成の優先度の計算に使う。 添字は項目の並び順である。<br/>
/// <see cref="Empty"/> は「該当なし」を表し、<see cref="First"/> が <see cref="Last"/> より大きい。
/// </remarks>
public readonly record struct ThumbnailRange
{
    /// <summary>項目の範囲を生成する。</summary>
    /// <param name="first">先頭の添字（含む）。</param>
    /// <param name="last">末尾の添字（含む）。</param>
    public ThumbnailRange(int first, int last)
    {
        First = first;
        Last = last;
    }

    /// <summary>該当なしの範囲。</summary>
    /// <value><see cref="First"/> が 0、<see cref="Last"/> が -1 の範囲。</value>
    public static ThumbnailRange Empty => new(0, -1);

    /// <summary>先頭の添字（含む）を取得する。</summary>
    public int First { get; }

    /// <summary>末尾の添字（含む）を取得する。</summary>
    public int Last { get; }

    /// <summary>該当が無いかどうかを取得する。</summary>
    /// <value>末尾が先頭より小さい場合は true。</value>
    public bool IsEmpty => Last < First;

    /// <summary>含まれる項目数を取得する。</summary>
    /// <value>項目数。 該当が無い場合は 0。</value>
    public int Count => IsEmpty ? 0 : Last - First + 1;
}
