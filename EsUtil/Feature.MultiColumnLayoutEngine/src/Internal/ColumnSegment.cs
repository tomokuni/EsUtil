namespace EsUtil.Feature.MultiColumn.Internal;

/// <summary><b>各列のアイテム範囲を表すレコード構造体</b></summary>
/// <remarks>
/// 【制約】<br/>
/// StartIdx ≤ EndIdx で有効な範囲を表す。<br/>
/// インデックスは 0-based。<br/>
/// </remarks>
internal readonly record struct ColumnSegment(int StartIdx, int EndIdx);
