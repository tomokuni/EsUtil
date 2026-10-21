namespace EsUtil.Platform.Core.Svg;

/// <summary>SVG の 1 つの図形（部分パス）。</summary>
/// <param name="StartX">開始点の X 座標。</param>
/// <param name="StartY">開始点の Y 座標。</param>
/// <param name="Segments">線分・曲線の列。</param>
/// <param name="IsClosed">閉じているかどうか（<c>Z</c> の有無）。</param>
/// <remarks>
/// <b>UI に依存しない形で持つ理由</b>: 画素への展開（<see cref="SvgPathRasterizer"/>）と WinUI 3 の形状への<br/>
/// 変換（<see cref="SvgIconSource"/> と各 UI の <c>IconLoader</c>）が同じ解析結果を使う。
/// </remarks>
public readonly record struct SvgPathFigure(
    double StartX,
    double StartY,
    IReadOnlyList<SvgPathSegment> Segments,
    bool IsClosed);
