namespace EsUtil.Platform.Core.Svg;

/// <summary>SVG の図形を指定サイズの画素へ展開する。</summary>
/// <remarks>
/// <b>用途</b>: ウィンドウ（タスク バー）アイコンの ICO は<b>画素</b>を要求するため、図形を画素へ落とす必要がある。<br/>
/// XAML の要素を画面外で描画して読み戻す方法は、要素が表示ツリーに載っていることを要求し、<br/>
/// 起動直後のタイミングに依存する。 本クラスは<b>解析済みの図形だけから決定的に画素を作る</b>ため、<br/>
/// 表示状態に依存せず、単体テストで画素を検証できる。<br/>
/// <br/>
/// <b>塗り</b>: SVG の既定（nonzero）で塗る（交差の向きで穴を表す）。<br/>
/// <b>輪郭</b>: 1 画素あたり <c>4x4</c> の副標本で被覆率を求め、覆われた割合をアルファにする<br/>
/// （アイコンの大きさでは十分な滑らかさになり、費用も起動時の 1 回に収まる）。<br/>
/// <b>曲線</b>: 3 次ベジェは折れ線へ分割する（<c>12</c> 分割。 24x24 の座標系では分割の誤差は 1 画素未満）。
/// </remarks>
public static class SvgPathRasterizer
{
    /// <summary>1 画素あたりの副標本の数（辺の分割数）。</summary>
    private const int SubSamples = 4;

    /// <summary>3 次ベジェ 1 本を折れ線へ分割する本数。</summary>
    private const int CurveSegments = 12;

    /// <summary>図形を指定サイズの画素へ展開する。</summary>
    /// <param name="figures">展開する図形（SVG の viewBox 座標）。</param>
    /// <param name="size">一辺（ピクセル）。 1 以上。</param>
    /// <param name="argb">塗りの色（<c>0xAARRGGBB</c>。 アルファは被覆率に掛ける）。</param>
    /// <returns>BGRA8888・非乗算の画素（<c>size × size × 4</c> バイト）。 背景は透明。</returns>
    /// <remarks>図形の外接矩形を一辺いっぱいへ等方に引き伸ばす（他の UI 版のアイコンの見た目と揃える）。</remarks>
    /// <exception cref="ArgumentNullException"><paramref name="figures"/> が null の場合。</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="size"/> が 1 未満の場合。</exception>
    public static byte[] Rasterize(IReadOnlyList<SvgPathFigure> figures, int size, uint argb)
    {
        ArgumentNullException.ThrowIfNull(figures);
        ArgumentOutOfRangeException.ThrowIfLessThan(size, 1);

        var pixels = new byte[size * size * 4];
        var polygons = Flatten(figures);
        if (polygons.Count == 0)
        {
            return pixels;
        }

        var bounds = ComputeBounds(polygons);
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return pixels;
        }

        // 外接矩形を一辺いっぱいへ等方に広げ、中央へ置く
        var scale = Math.Min(size / bounds.Width, size / bounds.Height);
        var offsetX = ((size - (bounds.Width * scale)) / 2) - (bounds.X * scale);
        var offsetY = ((size - (bounds.Height * scale)) / 2) - (bounds.Y * scale);
        foreach (var polygon in polygons)
        {
            for (var i = 0; i < polygon.Length; i++)
            {
                polygon[i] = new Point(
                    (polygon[i].X * scale) + offsetX,
                    (polygon[i].Y * scale) + offsetY);
            }
        }

        var coverage = ComputeCoverage(polygons, size);

        var colorA = (byte)((argb >> 24) & 0xFF);
        var colorR = (byte)((argb >> 16) & 0xFF);
        var colorG = (byte)((argb >> 8) & 0xFF);
        var colorB = (byte)(argb & 0xFF);
        for (var i = 0; i < coverage.Length; i++)
        {
            var value = coverage[i];
            if (value == 0)
            {
                continue;
            }

            var offset = i * 4;
            pixels[offset] = colorB;
            pixels[offset + 1] = colorG;
            pixels[offset + 2] = colorR;
            pixels[offset + 3] = (byte)(colorA * value / (SubSamples * SubSamples));
        }

        return pixels;
    }

    /// <summary>図形の外接矩形を求める。</summary>
    /// <param name="polygons">折れ線の一覧。</param>
    /// <returns>外接矩形。</returns>
    private static (double X, double Y, double Width, double Height) ComputeBounds(IReadOnlyList<Point[]> polygons)
    {
        double minX = double.MaxValue, minY = double.MaxValue;
        double maxX = double.MinValue, maxY = double.MinValue;

        foreach (var polygon in polygons)
        {
            foreach (var point in polygon)
            {
                minX = Math.Min(minX, point.X);
                minY = Math.Min(minY, point.Y);
                maxX = Math.Max(maxX, point.X);
                maxY = Math.Max(maxY, point.Y);
            }
        }

        return minX > maxX ? (0, 0, 0, 0) : (minX, minY, maxX - minX, maxY - minY);
    }

    /// <summary>副標本ごとの被覆数を数える。</summary>
    /// <param name="polygons">折れ線の一覧（画素座標へ変換済み）。</param>
    /// <param name="size">一辺（ピクセル）。</param>
    /// <returns>画素ごとの被覆数（<c>size × size</c>）。</returns>
    /// <remarks>
    /// 副標本の走査線ごとに辺との交点を求め、交差の向きを数えて内側の区間を塗る<br/>
    /// （1 点ずつ内外を判定する方法では費用が辺の数だけ増えるため、走査線でまとめて求める）。
    /// </remarks>
    private static int[] ComputeCoverage(IReadOnlyList<Point[]> polygons, int size)
    {
        var coverage = new int[size * size];
        var rowCoverage = new bool[size * SubSamples];
        var crossings = new List<(double X, int Direction)>();

        for (var subRow = 0; subRow < size * SubSamples; subRow++)
        {
            var y = (subRow + 0.5) / SubSamples;
            crossings.Clear();

            foreach (var polygon in polygons)
            {
                for (var i = 0; i < polygon.Length; i++)
                {
                    var a = polygon[i];
                    var b = polygon[(i + 1) % polygon.Length];
                    if (a.Y == b.Y)
                    {
                        continue;
                    }

                    // 半開区間 [min, max) で数える（頂点の二重計上を避ける）
                    var min = Math.Min(a.Y, b.Y);
                    var max = Math.Max(a.Y, b.Y);
                    if (y < min || y >= max)
                    {
                        continue;
                    }

                    var t = (y - a.Y) / (b.Y - a.Y);
                    crossings.Add((a.X + ((b.X - a.X) * t), b.Y > a.Y ? 1 : -1));
                }
            }

            if (crossings.Count < 2)
            {
                continue;
            }

            crossings.Sort(static (left, right) => left.X.CompareTo(right.X));

            Array.Clear(rowCoverage);
            var winding = 0;
            for (var i = 0; i < crossings.Count - 1; i++)
            {
                winding += crossings[i].Direction;
                if (winding == 0)
                {
                    continue;
                }

                // 区間 [crossings[i].X, crossings[i + 1].X) の副標本を塗る
                var start = (int)Math.Ceiling((crossings[i].X * SubSamples) - 0.5);
                var end = (int)Math.Ceiling((crossings[i + 1].X * SubSamples) - 0.5);
                var from = Math.Max(0, start);
                var to = Math.Min(size * SubSamples, end);
                for (var column = from; column < to; column++)
                {
                    rowCoverage[column] = true;
                }
            }

            var rowOffset = (subRow / SubSamples) * size;
            for (var column = 0; column < rowCoverage.Length; column++)
            {
                if (rowCoverage[column])
                {
                    coverage[rowOffset + (column / SubSamples)]++;
                }
            }
        }

        return coverage;
    }

    /// <summary>図形を折れ線の一覧へ変換する。</summary>
    /// <param name="figures">変換する図形。</param>
    /// <returns>閉じた折れ線の一覧。 図形が無い場合は空。</returns>
    private static List<Point[]> Flatten(IReadOnlyList<SvgPathFigure> figures)
    {
        var polygons = new List<Point[]>(figures.Count);
        foreach (var figure in figures)
        {
            var points = new List<Point>(figure.Segments.Count + 1)
            {
                new(figure.StartX, figure.StartY),
            };

            var currentX = figure.StartX;
            var currentY = figure.StartY;
            foreach (var segment in figure.Segments)
            {
                if (segment.IsCubic)
                {
                    for (var i = 1; i <= CurveSegments; i++)
                    {
                        var t = (double)i / CurveSegments;
                        var u = 1 - t;
                        var a = u * u * u;
                        var b = 3 * u * u * t;
                        var c = 3 * u * t * t;
                        var d = t * t * t;
                        points.Add(new Point(
                            (a * currentX) + (b * segment.C1X) + (c * segment.C2X) + (d * segment.X),
                            (a * currentY) + (b * segment.C1Y) + (c * segment.C2Y) + (d * segment.Y)));
                    }
                }
                else
                {
                    points.Add(new Point(segment.X, segment.Y));
                }

                currentX = segment.X;
                currentY = segment.Y;
            }

            if (points.Count >= 3)
            {
                polygons.Add([.. points]);
            }
        }

        return polygons;
    }

    /// <summary>折れ線の頂点（画素座標）。</summary>
    /// <param name="X">X 座標。</param>
    /// <param name="Y">Y 座標。</param>
    private readonly record struct Point(double X, double Y);
}
