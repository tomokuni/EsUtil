namespace EsUtil.Feature.Thumbnail;

/// <summary>画像の 4 辺と 4 隅へ落とす影の描画計画（全 UI 版で共通の唯一の定義）。</summary>
/// <remarks>
/// <b>基準</b>: AvaloniaUI の <c>BoxShadow</c>（<c>0 0 10 0 #8A000000</c>）を実測して写した値である。<br/>
/// 画像を並べる一覧とキャプチャのプレビューは、すべてこの計画から影を描く<br/>
/// （UI 版ごとに影の濃さ・広がりがずれないようにする）。<br/>
/// <br/>
/// <b>4 辺を同じ濃さにする理由</b>: 影を下方向へずらすと上辺だけが薄くなり、画像と影の間に<br/>
/// 白っぽい隙間が見える（実測で指摘）。 4 辺とも画像の端で最も濃く、外側へ向けて同じ割合で薄くする。<br/>
/// <br/>
/// <b>描き方</b>: <see cref="BuildDeviceBands"/> が返す<b>1 画素の帯</b>を単色で塗るだけである<br/>
/// （グラデーション ブラシの端の扱いに依存しないため、どの UI 版でも同じ結果になる）。<br/>
/// <b>4 隅もグラデーションを使わない</b>: 隅の画素の濃さは<b>画像の隅の頂点からの距離</b>（斜めの距離）で<br/>
/// 決める。 辺と同じ濃さの変化が斜めにもそのまま続き、四角い塊や濃い点にならない<br/>
/// （放射グラデーションは描画 API ごとに半径・中心の解釈が揺れ、隅だけ濃い点や薄い筋として見えた。 実測）。<br/>
/// <br/>
/// <b>等価性</b>: 値は生成時に確定する（<see cref="Default"/> を共有して使う）。
/// </remarks>
public sealed class ThumbnailShadowPlan : IThumbnailShadowPlan
{
    /// <summary>既定の濃さの実測プロファイル（画像の端からの距離 → 不透明度）。</summary>
    /// <remarks>
    /// AvaloniaUI の <c>BoxShadow</c>（ぼかし 10 DIP・黒 54%）の明色の画面での実測値。<br/>
    /// 添字 0 は距離 1 DIP、以降 1 DIP 刻みで距離 10 DIP まで（それ以降は 0）。
    /// </remarks>
    private static readonly double[] s_defaultProfile =
        [0.245, 0.195, 0.150, 0.112, 0.079, 0.050, 0.033, 0.021, 0.012, 0.005];

    /// <summary>濃さの実測プロファイル。</summary>
    private readonly double[] _alphaProfile;

    /// <summary>すべての既定値で影の計画を生成する（実測値。 4 辺・4 隅・余白も同時に確定する）。</summary>
    /// <remarks>プロファイルの長さがぼかしの広がり（DIP）になる（1 DIP 刻み）。</remarks>
    public ThumbnailShadowPlan()
        : this(0x8A000000, null)
    {
    }

    /// <summary>影の計画を生成する。</summary>
    /// <param name="colorArgb">影の色（<c>0xAARRGGBB</c>）。 透過度は停止点が上書きする。</param>
    /// <param name="alphaProfile">濃さのプロファイル（距離 1 DIP 以降の不透明度）。 null の場合は実測値。</param>
    /// <exception cref="ArgumentException"><paramref name="alphaProfile"/> が空の場合。</exception>
    /// <remarks>ぼかしの広がりはプロファイルの長さで決まる（1 DIP 刻み）。</remarks>
    public ThumbnailShadowPlan(uint colorArgb, IReadOnlyList<double>? alphaProfile)
    {
        if (alphaProfile is { Count: 0 })
        {
            throw new ArgumentException("濃さのプロファイルが空です。", nameof(alphaProfile));
        }

        _alphaProfile = alphaProfile is null ? s_defaultProfile : [.. alphaProfile];
        ColorArgb = colorArgb;
        BlurRadius = _alphaProfile.Length;
        Edges =
        [
            CreateEdge(ThumbnailShadowSide.Top, outerAtStart: true),
            CreateEdge(ThumbnailShadowSide.Right, outerAtStart: false),
            CreateEdge(ThumbnailShadowSide.Bottom, outerAtStart: false),
            CreateEdge(ThumbnailShadowSide.Left, outerAtStart: true),
        ];
        CornerStops = CreateCornerStops();
    }

    /// <summary>既定の影の計画（実測値）を取得する。</summary>
    public static ThumbnailShadowPlan Default { get; } = new();

    /// <summary>影のぼかしの広がり（DIP）を取得する。</summary>
    /// <value>画像の端からこの距離まで影を落とす。 既定は 10。</value>
    public double BlurRadius { get; }

    /// <summary>影の色（<c>0xAARRGGBB</c>）を取得する。</summary>
    /// <value>既定は黒 54%（<c>0x8A000000</c>）。</value>
    public uint ColorArgb { get; }

    /// <summary>4 辺の計画（描画順は上辺・右辺・下辺・左辺）を取得する。</summary>
    public IReadOnlyList<ThumbnailShadowEdge> Edges { get; }

    /// <summary>4 隅の放射グラデーションの停止点（隅を 1 つの要素で塗る UI が使う）を取得する。</summary>
    /// <remarks>
    /// 束ねた帯で隅を塗る UI（Skia / GDI+）は <see cref="BuildDeviceBands"/> を使う。<br/>
    /// 要素を並べる UI（WinUI 3 の一覧）は本停止点を使う。
    /// </remarks>
    public IReadOnlyList<ThumbnailShadowStop> CornerStops { get; }

    /// <summary>画像の周囲へ確保する影の余白を取得する。</summary>
    /// <value>4 辺とも <see cref="BlurRadius"/>。</value>
    /// <remarks>アイテムの寸法を決めるときに使う（画像の外側にこの分の余地を確保する）。</remarks>
    public ThumbnailShadowInsets Insets => new(BlurRadius, BlurRadius, BlurRadius, BlurRadius);

    /// <summary>指定の辺の計画を取得する。</summary>
    /// <param name="side">辺。</param>
    /// <returns>辺の計画。</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="side"/> が未定義の場合。</exception>
    public ThumbnailShadowEdge Edge(ThumbnailShadowSide side)
    {
        foreach (var edge in Edges)
        {
            if (edge.Side == side)
            {
                return edge;
            }
        }

        throw new ArgumentOutOfRangeException(nameof(side), side, "未定義の辺です。");
    }

    /// <summary>画像の端から <paramref name="distance"/> DIP 外側の影の不透明度を取得する。</summary>
    /// <param name="distance">画像の端からの距離（DIP）。</param>
    /// <returns>不透明度（0〜1）。</returns>
    /// <remarks>
    /// 距離 1 DIP 以下は端の値（最も濃い。 画像のすぐ外側が最も濃く、影として自然に見える）、<br/>
    /// <see cref="BlurRadius"/> 以上は 0 として扱う。
    /// </remarks>
    public double AlphaAtDistance(double distance)
    {
        if (distance <= 1)
        {
            return _alphaProfile[0];
        }

        if (distance >= BlurRadius)
        {
            return distance > BlurRadius ? 0 : _alphaProfile[^1];
        }

        var index = (int)Math.Floor(distance) - 1;
        var fraction = distance - Math.Floor(distance);
        var near = _alphaProfile[index];
        var far = _alphaProfile[index + 1];
        return near + ((far - near) * fraction);
    }

    /// <summary>画像の端から <paramref name="distance"/> DIP 外側の影の色を取得する。</summary>
    /// <param name="distance">画像の端からの距離（DIP）。</param>
    /// <returns>色（<c>0xAARRGGBB</c>。 透過度が影の濃さ）。</returns>
    public uint ColorAtDistance(double distance)
        => ((uint)Math.Round(Math.Clamp(AlphaAtDistance(distance), 0, 1) * 255) << 24) | (ColorArgb & 0x00FFFFFF);

    /// <summary>画像の矩形から、影を塗る 1 画素幅の帯（4 辺と 4 隅）を作る。</summary>
    /// <param name="x">画像の左端（DIP）。</param>
    /// <param name="y">画像の上端（DIP）。</param>
    /// <param name="width">画像の幅（DIP）。</param>
    /// <param name="height">画像の高さ（DIP）。</param>
    /// <param name="scaling">DIP → 物理ピクセルの倍率（DPI の倍率）。</param>
    /// <returns>帯の一覧（上辺・右辺・下辺・左辺・4 隅の順。 各辺は画像に接する画素から外側へ）。</returns>
    /// <remarks>
    /// <b>これは「描画計画」の画素版である</b>: 辺の濃さを 1 画素ごとの単色へ落としているため、<br/>
    /// どの UI 版も「単色の矩形を塗る」だけで同じ結果になる（グラデーション ブラシの端の扱いに依存しない）。<br/>
    /// 濃さは画素の中心の距離で決める（<see cref="AlphaAtDistance"/>）。<br/>
    /// <b>画像の下へ潜らせる</b>: 内側の端は切り捨てるため、帯は画像の端の画素（画像と背景の混色）に<br/>
    /// 最大 1 画素掛かる。 影を画像より先に描けば隠れ、混色の下地が影になるため白い線が出ない。<br/>
    /// <b>4 隅</b>は<b>画像の隅の頂点からの距離</b>（斜めの距離）で濃さを決める。 辺と同じ濃さの変化が<br/>
    /// 斜めにもそのまま続くため、四角い塊や濃い点にならない（辺とは重ねない。 重ねると隅だけ濃くなる）。
    /// </remarks>
    public IReadOnlyList<ThumbnailShadowBand> BuildDeviceBands(
        double x, double y, double width, double height, double scaling)
    {
        var bands = new List<ThumbnailShadowBand>();

        if (width <= 0 || height <= 0 || scaling <= 0)
        {
            return bands;
        }

        var extent = (int)Math.Round(BlurRadius * scaling);

        // 画像の端は切り捨てる: 画像は端数のある位置へ描かれ、その最後の画素は画像と背景の
        // 混色になる。 影の内側を切り捨てて画像の下へ潜らせると、その混色の下地が影になり
        // 白い線が見えない（切り上げると 1 画素だけ背景が残る。 実測）。
        var left = (int)Math.Floor(x * scaling);
        var top = (int)Math.Floor(y * scaling);
        var right = (int)Math.Floor((x + width) * scaling);
        var bottom = (int)Math.Floor((y + height) * scaling);
        var pixelsWide = right - left;
        var pixelsHigh = bottom - top;
        if (extent <= 0 || pixelsWide <= 0 || pixelsHigh <= 0)
        {
            return bands;
        }

        // 4 辺は画像の幅・高さいっぱい（隅は AddCornerBands が塗る。 重ねない）
        for (var i = 0; i < extent; i++)
        {
            // i = 0 が画像に接する画素。 画素の中心までの距離で濃さを決める（勾配の見た目と同じ）
            var argb = ColorAtDistance((i + 0.5) / scaling);
            bands.Add(new ThumbnailShadowBand(left, top - 1 - i, pixelsWide, 1, argb));
            bands.Add(new ThumbnailShadowBand(left, bottom + i, pixelsWide, 1, argb));
            bands.Add(new ThumbnailShadowBand(left - 1 - i, top, 1, pixelsHigh, argb));
            bands.Add(new ThumbnailShadowBand(right + i, top, 1, pixelsHigh, argb));
        }

        // 画像の端の画素（切り捨てた行・列）も覆う: 画像の描画側は端の画素を切り捨てたり
        // 切り上げたりし、floor の行・列が画像でも影でも塗られず背景（白）が残ることがある（実測:
        // 拡大率によって画像の上辺に 1 画素の白い線）。 画像がその画素を塗る場合は画像に隠れる。
        // 行の帯と列の帯は重ねない（重なると四隅の 1 画素だけ濃い点になる）。
        var edgeArgb = ColorAtDistance(0.5 / scaling);
        bands.Add(new ThumbnailShadowBand(left, top, pixelsWide, 1, edgeArgb));
        if (pixelsHigh > 1)
        {
            bands.Add(new ThumbnailShadowBand(left, bottom - 1, pixelsWide, 1, edgeArgb));
        }

        var columnHeight = pixelsHigh - 2;
        if (columnHeight > 0)
        {
            bands.Add(new ThumbnailShadowBand(left, top + 1, 1, columnHeight, edgeArgb));
            if (pixelsWide > 1)
            {
                bands.Add(new ThumbnailShadowBand(right - 1, top + 1, 1, columnHeight, edgeArgb));
            }
        }

        // 4 隅（画像の外側の正方形）。 中心は画像の隅の頂点であり、距離は斜めに測る
        AddCornerBands(bands, left, top, extent, stepX: -1, stepY: -1, scaling);           // 左上
        AddCornerBands(bands, right - 1, top, extent, stepX: 1, stepY: -1, scaling);       // 右上
        AddCornerBands(bands, right - 1, bottom - 1, extent, stepX: 1, stepY: 1, scaling); // 右下
        AddCornerBands(bands, left, bottom - 1, extent, stepX: -1, stepY: 1, scaling);     // 左下

        return bands;
    }

    /// <summary>1 隅分の帯（画素）を追加する。</summary>
    /// <param name="bands">追加先。</param>
    /// <param name="cornerX">画像の隅の頂点に接する画素の X（画像の内側の端）。</param>
    /// <param name="cornerY">画像の隅の頂点に接する画素の Y（画像の内側の端）。</param>
    /// <param name="extent">隅の正方形の一辺（画素）。</param>
    /// <param name="stepX">外側へ進む向き（左の隅は -1、右の隅は +1）。</param>
    /// <param name="stepY">外側へ進む向き（上の隅は -1、下の隅は +1）。</param>
    /// <param name="scaling">DIP → 物理ピクセルの倍率。</param>
    /// <remarks>
    /// 濃さは<b>画像の隅の頂点からの距離</b>（<c>√(dx² + dy²)</c>）で決めるため、辺の濃さの変化が<br/>
    /// 斜めにもそのまま続く（隅だけ濃い点や四角い塊にならない）。 同じ濃さが続く画素は 1 本の帯へまとめる<br/>
    /// （描画の呼び出しを減らす。 濃さは画素ごとに単調に薄くなるため、まとめても結果は変わらない）。
    /// </remarks>
    private void AddCornerBands(
        List<ThumbnailShadowBand> bands, int cornerX, int cornerY, int extent, int stepX, int stepY, double scaling)
    {
        for (var row = 0; row < extent; row++)
        {
            var y = cornerY + (stepY * (row + 1));
            var dy = row + 0.5;
            var column = 0;
            while (column < extent)
            {
                var dx = column + 0.5;
                var argb = ColorAtDistance(Math.Sqrt((dx * dx) + (dy * dy)) / scaling);
                var run = 1;
                while (column + run < extent)
                {
                    var next = column + run + 0.5;
                    if (ColorAtDistance(Math.Sqrt((next * next) + (dy * dy)) / scaling) != argb)
                    {
                        break;
                    }

                    run++;
                }

                var x = stepX < 0 ? cornerX - column - run : cornerX + 1 + column;
                bands.Add(new ThumbnailShadowBand(x, y, run, 1, argb));
                column += run;
            }
        }
    }

    /// <summary>1 辺分の計画を生成する。</summary>
    /// <param name="side">辺。</param>
    /// <param name="outerAtStart">グラデーションの始点が外側かどうか。</param>
    /// <returns>辺の計画。</returns>
    /// <remarks>濃さの変化は 4 辺で共通（<see cref="BlurRadius"/> の幅で端から外側へ薄くなる）。</remarks>
    private ThumbnailShadowEdge CreateEdge(ThumbnailShadowSide side, bool outerAtStart)
    {
        var steps = (int)Math.Ceiling(BlurRadius);
        var stops = new List<ThumbnailShadowStop>(steps + 1);

        // 外側（距離 = BlurRadius）から画像の端（距離 0）へ濃くする
        for (var step = steps; step >= 0; step--)
        {
            stops.Add(new ThumbnailShadowStop(1 - (step / BlurRadius), ColorAtDistance(step)));
        }

        return new ThumbnailShadowEdge(side, BlurRadius, outerAtStart, stops);
    }

    /// <summary>隅の放射グラデーションの停止点を生成する。</summary>
    /// <returns>停止点（中心からの距離の順）。</returns>
    /// <remarks>位置は「距離 ÷ <see cref="BlurRadius"/>」であり、辺の濃さの変化と同じ値になる。</remarks>
    private IReadOnlyList<ThumbnailShadowStop> CreateCornerStops()
    {
        var steps = (int)Math.Ceiling(BlurRadius);
        var stops = new List<ThumbnailShadowStop>(steps + 1);

        for (var step = 0; step <= steps; step++)
        {
            stops.Add(new ThumbnailShadowStop(step / BlurRadius, ColorAtDistance(step)));
        }

        return stops;
    }
}
