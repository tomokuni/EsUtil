namespace EsUtil.Feature.ColumnPacking.Internal;

/// <summary><b>行間スペースと列間スペースを表すレコード構造体</b></summary>
/// <remarks>
/// 【制約】<br/>
/// Row と Column は非負値。<br/>
/// デフォルト値は (0.0, 0.0)。<br/>
/// </remarks>
internal readonly record struct Space(double Row, double Column);
