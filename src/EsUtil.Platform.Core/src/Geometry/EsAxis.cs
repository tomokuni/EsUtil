namespace EsUtil.Platform.Core.Geometry;

/// <summary>2 次元のレイアウト計算で扱う軸。</summary>
/// <remarks>
/// 軸を <see langword="bool"/> で指定すると呼び出し側で意味が読めなくなる（<c>true</c> が横か縦か分からない）ため、<br/>
/// 列挙型で指定する。 「サイズの幅を選ぶか高さを選ぶか」の判断を 1 箇所に閉じ込めるために使用する。
/// </remarks>
public enum EsAxis
{
    /// <summary>水平方向の軸（サイズの幅）。</summary>
    X,

    /// <summary>垂直方向の軸（サイズの高さ）。</summary>
    Y,
}
