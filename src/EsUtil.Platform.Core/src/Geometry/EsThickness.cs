namespace EsUtil.Platform.Core.Geometry;

/// <summary>UI フレームワーク非依存の 4 辺の厚さ（左・上・右・下）を表す値オブジェクト。</summary>
/// <remarks>
/// Avalonia / MewUI の <c>Thickness</c> に対応する。<see cref="EsRect.Inflate(EsThickness)"/> 等の引数に使う。<br/>
/// 4 辺を個別に持つため、片側だけ太い枠も 1 つの値で表せる。<br/>
/// 各 UI 実装の厚さ型との変換は Interop 層が提供する。
/// </remarks>
/// <param name="Left">左辺の厚さ（負値も許容。 縮小を表す）。</param>
/// <param name="Top">上辺の厚さ（負値も許容）。</param>
/// <param name="Right">右辺の厚さ（負値も許容）。</param>
/// <param name="Bottom">下辺の厚さ（負値も許容）。</param>
public readonly record struct EsThickness(double Left, double Top, double Right, double Bottom)
{
    /// <summary>4 辺とも 0 の厚さを取得する。</summary>
    /// <value>左・上・右・下とも 0 の厚さ。</value>
    public static readonly EsThickness Zero;

    /// <summary>4 辺に同じ厚さを持つ枠を生成する。</summary>
    /// <param name="uniform">全辺に適用する厚さ。</param>
    public EsThickness(double uniform)
        : this(uniform, uniform, uniform, uniform)
    {
    }

    /// <summary>左右の厚さの合計を取得する。</summary>
    /// <value>左辺 + 右辺の厚さ。</value>
    public double Horizontal => Left + Right;

    /// <summary>上下の厚さの合計を取得する。</summary>
    /// <value>上辺 + 下辺の厚さ。</value>
    public double Vertical => Top + Bottom;

    /// <summary>タプルから生成する。</summary>
    /// <param name="value">(Left, Top, Right, Bottom) 形式のタプル。</param>
    /// <returns>タプルの成分を引き継いだ厚さ。</returns>
    public static EsThickness FromTuple((double Left, double Top, double Right, double Bottom) value)
        => new(value.Left, value.Top, value.Right, value.Bottom);

    /// <summary>タプルへ変換する。</summary>
    /// <returns>(Left, Top, Right, Bottom) 形式のタプル。</returns>
    public (double Left, double Top, double Right, double Bottom) ToTuple() => (Left, Top, Right, Bottom);
}
