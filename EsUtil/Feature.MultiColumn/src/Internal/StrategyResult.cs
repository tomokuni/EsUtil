namespace EsUtil.Feature.MultiColumn.Internal;

/// <summary><b>レイアウト計算の結果を格納するレコード構造体</b></summary>
/// <remarks>
/// 【制約】<br/>
/// UsedWidth と MinHeight は非負の有限値であることを想定。<br/>
/// ColumnSegments は null でない配列。<br/>
/// <br/>
/// Empty インスタンスはデフォルト値として使用可能。<br/>
/// </remarks>
internal readonly record struct StrategyResult(double UsedWidth, double MinHeight, ColumnSegment[] ColumnSegments)
{
    /// <summary><b>空のレイアウト結果</b></summary>
    public static readonly StrategyResult Empty = new(0.0, 0.0, []);
}
