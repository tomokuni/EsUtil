using EsUtil.Platform.Core.Geometry;

namespace EsUtil.Feature.Loupe;

/// <summary>ルーペの描画 1 フレーム分の幾何（コアが責任を持つ値の全量）。</summary>
/// <param name="Center">拡大中心（対象座標）。 ファサードの <c>LoupeCore.CenterPoint</c>（UI が設定する状態）を 1 フレーム分の幾何へ写した値である。 未設定は null（切り出し領域と中心座標ラベルの基準を持たない）。</param>
/// <param name="ClientSize">ルーペの一辺の表示サイズ（DIP。 ラベルの位置の基準であり、物理ピクセルサイズの換算元）。</param>
/// <param name="ClientPixelSize">ルーペの一辺のサイズ（物理ピクセル。 切り上げ・最低 1）。 描画先の表面は本値と同じ大きさで用意する（呼び出し側の責務）。</param>
/// <param name="ClientToPixelScaling">表示 DIP → 物理ピクセルのスケーリング係数（環境値）。</param>
/// <param name="ZoomLevel">拡大率（設定の補正済みの値）。</param>
/// <remarks>
/// <b>本値がコアの公開する描画の入口である</b>: コアは拡大画像と描画面の幾何だけを返し、<br/>
/// 装飾（十字線・枠線）・文言・表示可否・描画順は描画の都合として描画層（本コアを参照する上位の層）が解決する。<br/>
/// <b>導出値も本値が単一所有する</b>: 切り出し領域の一辺・描画面の矩形・切り出し領域の中心と左上・切り出し領域内での中心位置・拡大画像の切り出しと描画の矩形は本値のメンバーが持つ<br/>
/// （描画層が切り上げや矩形を組み立てると、切り出しの実行と十字線の交点とで規則が分かれる）。<br/>
/// <b>寸法は換算済みの値を保持する</b>: DIP の表示サイズを物理ピクセルへ換算する手順はコアが単一所有する<br/>
/// （描画層は <see cref="ClientPixelSize"/> をそのまま使う）。<br/>
/// 描画先の表面（描画バッファ）は本値と同じ大きさで用意する（本コアは描画先を知らないため、
/// 表面の大きさの判定は呼び出し側が行う）。<br/>
/// 拡大元画像（<c>EsBitmap</c>）は含めない（型が UI に依存するため、描画の起動時に引数で受け取る）。<br/>
/// UI フレームワークに依存しないため、UI 初期化なしの <c>[Fact]</c> で並列検証できる。
/// </remarks>
public readonly record struct LoupeGeometry(
    EsPoint? Center,
    double ClientSize,
    int ClientPixelSize,
    double ClientToPixelScaling,
    int ZoomLevel)
{
    /// <summary>切り出し領域の一辺（対象ピクセル）を取得する。</summary>
    /// <value>物理ピクセルサイズを拡大率で割った切り上げ + 1（実効原点の端数を覆うための 1 画素）。<see cref="ClientPixelSize"/> は 1 以上のため 2 以上になる。</value>
    /// <remarks>
    /// 切り出しの実行（<c>EsBitmap.Crop</c>）と、切り出し領域の導出（<see cref="CaptureCropTopLeft"/> / <see cref="MagnifiedImage"/>）が同じ値を共有する。<br/>
    /// 切り捨てると端の区画が失われ、切り出し領域の右端・下端が埋まらない。<br/>
    /// <b>+1 する理由</b>: 可視域の左上（<see cref="CaptureTopLeft"/>）は小数を含むため、その切り捨て（<see cref="CaptureCropTopLeft"/>）から<br/>
    /// 可視域の右端・下端までを覆うには <c>切り上げ + 1</c> が必要になる（可視域に 1 画素足りないと端に下地の色が見える）。<br/>
    /// 導出元（物理ピクセルサイズと拡大率）を位置パラメータにも持つと 2 つの値が食い違いうるため、本値が導出する。
    /// </remarks>
    public int CaptureSize => CeilDiv(ClientPixelSize, ZoomLevel) + 1;

    /// <summary>描画先の全領域（ルーペの一辺が作る正方形）を取得する。</summary>
    /// <value>左上を原点（0, 0）とし、幅・高さが <see cref="ClientPixelSize"/> の矩形（物理ピクセル）。</value>
    /// <remarks>描画先の原点は常に 0 である（描画層が矩形を組み立てると原点の取り違えが生じるため、本値が導出する）。<br/>
    /// 描画層は本矩形へ下地を塗る（本コアは塗りも色も持たない）。
    /// </remarks>
    public EsRect ClientPixelRect => new(0, 0, ClientPixelSize, ClientPixelSize);

    /// <summary>可視域の対象座標での半径（切り出し領域の半分）を取得する。</summary>
    /// <value>可視域の半径（対象ピクセル。 中心が整数でない場合は小数を含む）。</value>
    /// <remarks>
    /// 可視域（描画面に写る範囲）は物理ピクセルサイズ ÷ 拡大率であり、その半分が本値である。<br/>
    /// <b>十字線は本値が位置を決める</b>: 拡大中心は常に描画面の中心（<c>ClientPixelSize / 2</c>）へ写るため、<br/>
    /// 拡大中心が小数でも十字線はルーペの中心から動かない（<see cref="CaptureCenterOffset"/>）。
    /// </remarks>
    private double CaptureRadius => ClientPixelSize / (2.0 * ZoomLevel);

    /// <summary>可視域の左上（対象座標）を取得する。</summary>
    /// <value>可視域の左上（対象座標。 小数を含む実効原点）。<see cref="Center"/> が未設定の場合は null（切り出し領域を持たない）。</value>
    /// <remarks>
    /// <b>座標変換（対象座標 → 描画面のピクセル）は本値を原点にする</b>: ホストが描画完了通知（<c>Rendered</c>）で<br/>
    /// オーバーレイを描くときの <c>(対象座標 − 本値) × ZoomLevel</c> も本値を使う。<br/>
    /// 本値は拡大中心が描画面の中心へ写るように決める（<c>中心 − 可視域の半径</c>）ため、<b>小数を含む</b>。<br/>
    /// 切り出し（<c>EsBitmap.Crop</c>）の原点は整数である必要があるため、実際の切り出しは <see cref="CaptureCropTopLeft"/>（本値の切り捨て）から行い、<br/>
    /// 描画面への配置は <see cref="CaptureDrawOffset"/> で差を埋める。
    /// </remarks>
    public EsPoint? CaptureTopLeft => Center is { } center
        ? new EsPoint(center.X - CaptureRadius, center.Y - CaptureRadius)
        : null;

    /// <summary>切り出し（<c>EsBitmap.Crop</c>）の原点（対象ピクセル）を取得する。</summary>
    /// <value>切り出しの原点（対象ピクセル。 可視域の左上を切り捨てた値）。<see cref="Center"/> が未設定の場合は null。</value>
    /// <remarks>
    /// 可視域（<see cref="CaptureTopLeft"/>）が小数を含むため、切り出しはその切り捨てから始める<br/>
    /// （切り上げると可視域の左端・上端が 1 画素欠ける）。 差は <see cref="CaptureDrawOffset"/> が埋める。
    /// </remarks>
    public EsPointInt? CaptureCropTopLeft => CaptureTopLeft is { } origin
        ? new EsPointInt((int)Math.Floor(origin.X), (int)Math.Floor(origin.Y))
        : null;

    /// <summary>切り出した画像を描画面へ置く位置（物理ピクセル）を取得する。</summary>
    /// <value>描画面の左上を原点とした配置位置（整数）。<see cref="Center"/> が未設定の場合は null。</value>
    /// <remarks>
    /// <see cref="CaptureCropTopLeft"/>（切り捨て）と <see cref="CaptureTopLeft"/>（実効原点）の差を拡大率で換算して埋める。<br/>
    /// 丸めの残りは最大 0.5 物理ピクセルであり、<b>描画の位置は整数のまま保たれる</b>（小数へ置くと拡大した画素がぼやける）。<br/>
    /// 十字線とホストのオーバーレイは本値の丸めを使わない（実効原点から直接位置を求めるため、中心からずれない）。
    /// </remarks>
    public EsPointInt? CaptureDrawOffset => CaptureTopLeft is { } origin && CaptureCropTopLeft is { } crop
        ? new EsPointInt(
            (int)Math.Round((crop.X - origin.X) * ZoomLevel, MidpointRounding.AwayFromZero),
            (int)Math.Round((crop.Y - origin.Y) * ZoomLevel, MidpointRounding.AwayFromZero))
        : null;

    /// <summary>可視域内での拡大中心の位置（対象ピクセル）を取得する。</summary>
    /// <value>可視域の左上から見た中心の位置（拡大中心が未設定でも値を持つ）。</value>
    /// <remarks>
    /// <b>十字線の位置は本値が決める</b>: 描画面での中心の位置は「可視域内の中心位置 × 拡大率」であり、<br/>
    /// 本値は可視域の半径そのものであるため、<b>十字線は常に描画面の中心（<c>ClientPixelSize / 2</c>）になる</b><br/>
    /// （拡大中心が小数でも十字線は動かない。 ホストのオーバーレイは <see cref="CaptureTopLeft"/> を原点にするため、<br/>
    /// 拡大中心と一致する対象座標（ドラッグ中のハンドル等）は十字線と同じ位置へ写る）。
    /// </remarks>
    public EsPoint CaptureCenterOffset => new(CaptureRadius, CaptureRadius);

    /// <summary>拡大画像の切り出しと描画の矩形を取得する。</summary>
    /// <value>切り出しと描画の矩形。<see cref="Center"/> が未設定の場合は null（切り出し領域を持たない）。</value>
    /// <remarks>
    /// 「切り出した領域を <see cref="ZoomLevel"/> 倍の大きさで描く」規則を単一所有する。<br/>
    /// 切り出し元の画像は含めない（切り出しの実行は描画層が <c>EsBitmap.Crop</c> で行う）。<br/>
    /// <b>切り出しは整数の原点から行い、描画の位置で実効原点との差を埋める</b><br/>
    /// （<see cref="CaptureCropTopLeft"/> / <see cref="CaptureDrawOffset"/>。 描画の位置は整数のまま保たれる）。<br/>
    /// <b>描画先は可視域より大きくなりうる</b>（切り上げの余りと +1 の分。 最大 <c>ZoomLevel</c> 物理ピクセル程度）。<br/>
    /// はみ出した部分は描画先のクリップに任せる（切り捨てると拡大の倍率が整数でなくなり画素がぼやける）。
    /// </remarks>
    public LoupeMagnifiedImage? MagnifiedImage => CaptureCropTopLeft is not { } crop || CaptureDrawOffset is not { } offset
        ? null
        : new LoupeMagnifiedImage(
            new EsRectInt(crop.X, crop.Y, CaptureSize, CaptureSize),
            new EsRect(0, 0, CaptureSize, CaptureSize),
            new EsRect(offset.X, offset.Y, CaptureSize * ZoomLevel, CaptureSize * ZoomLevel));

    /// <summary>整数の切り上げ除算を行う（余りを 1 区画として数える）。</summary>
    /// <param name="value">割られる値（物理ピクセルサイズ）。 1 以上。</param>
    /// <param name="divisor">割る値（1 区画の長さ = 拡大率）。 1 以上。</param>
    /// <returns>切り上げた商（区画数）。</returns>
    /// <remarks>切り捨てると端の区画が失われる（除数は 1 以上を前提とする。 0 を渡すと <see cref="double"/> の除算が無限大になる）。</remarks>
    private static int CeilDiv(int value, int divisor) => (int)Math.Ceiling((double)value / divisor);
}
