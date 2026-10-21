namespace EsUtil.Platform.Core.Svg;

/// <summary>SVG の線分または 3 次ベジェ。</summary>
/// <param name="IsCubic">3 次ベジェかどうか（false の場合は線分）。</param>
/// <param name="C1X">第 1 制御点の X 座標（線分では未使用）。</param>
/// <param name="C1Y">第 1 制御点の Y 座標（線分では未使用）。</param>
/// <param name="C2X">第 2 制御点の X 座標（線分では未使用）。</param>
/// <param name="C2Y">第 2 制御点の Y 座標（線分では未使用）。</param>
/// <param name="X">終点の X 座標。</param>
/// <param name="Y">終点の Y 座標。</param>
/// <remarks>生成は <see cref="Line"/> / <see cref="Cubic"/> を使う（使わない制御点を書かせないため）。</remarks>
public readonly record struct SvgPathSegment(
    bool IsCubic,
    double C1X,
    double C1Y,
    double C2X,
    double C2Y,
    double X,
    double Y)
{
    /// <summary>線分を生成する。</summary>
    /// <param name="x">終点の X 座標。</param>
    /// <param name="y">終点の Y 座標。</param>
    /// <returns>生成した線分。</returns>
    public static SvgPathSegment Line(double x, double y) => new(false, 0, 0, 0, 0, x, y);

    /// <summary>3 次ベジェを生成する。</summary>
    /// <param name="c1x">第 1 制御点の X 座標。</param>
    /// <param name="c1y">第 1 制御点の Y 座標。</param>
    /// <param name="c2x">第 2 制御点の X 座標。</param>
    /// <param name="c2y">第 2 制御点の Y 座標。</param>
    /// <param name="x">終点の X 座標。</param>
    /// <param name="y">終点の Y 座標。</param>
    /// <returns>生成した曲線。</returns>
    public static SvgPathSegment Cubic(double c1x, double c1y, double c2x, double c2y, double x, double y)
        => new(true, c1x, c1y, c2x, c2y, x, y);
}
