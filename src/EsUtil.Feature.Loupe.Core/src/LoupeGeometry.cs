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
/// <b>導出値も本値が単一所有する</b>: 切り出し領域の一辺・描画面の矩形・切り出し領域の左上・拡大画像の切り出しと描画の矩形は本値のメンバーが持つ<br/>
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
    /// <value>物理ピクセルサイズを拡大率で割った切り上げ（余りを 1 区画として数える）。<see cref="ClientPixelSize"/> は 1 以上のため 1 以上になる。</value>
    /// <remarks>
    /// 切り出しの実行（<c>EsBitmap.Crop</c>）と十字線の交点（切り出した領域を等倍で並べたときの中央のブロック）が同じ値を共有する。<br/>
    /// 切り捨てると端の区画が失われ、切り出し領域の右端・下端が埋まらない。<br/>
    /// 導出元（物理ピクセルサイズと拡大率）を位置パラメータにも持つと 2 つの値が食い違いうるため、本値が導出する。
    /// </remarks>
    public int CaptureSize => CeilDiv(ClientPixelSize, ZoomLevel);

    /// <summary>描画先の全領域（ルーペの一辺が作る正方形）を取得する。</summary>
    /// <value>左上を原点（0, 0）とし、幅・高さが <see cref="ClientPixelSize"/> の矩形（物理ピクセル）。</value>
    /// <remarks>描画先の原点は常に 0 である（描画層が矩形を組み立てると原点の取り違えが生じるため、本値が導出する）。<br/>
    /// 描画層は本矩形へ下地を塗る（本コアは塗りも色も持たない）。
    /// </remarks>
    public EsRect ClientPixelRect => new(0, 0, ClientPixelSize, ClientPixelSize);

    /// <summary>拡大中心から切り出し領域の左上（対象座標）を取得する。</summary>
    /// <value>切り出し領域の左上（対象座標）。<see cref="Center"/> が未設定の場合は null（切り出し領域を持たない）。</value>
    /// <remarks>
    /// 中心の整数化（<c>(int)</c>）は切り出し（<c>EsBitmap.Crop</c>）と同じ丸めにする<br/>
    /// （切り出しの実行は描画層が、切り出し領域の通知は描画層が本値から組み立てる）。
    /// </remarks>
    public EsPoint? CaptureTopLeft => Center is { } center
        ? new EsPoint((int)center.X - CaptureSize / 2, (int)center.Y - CaptureSize / 2)
        : null;

    /// <summary>拡大画像の切り出しと描画の矩形を取得する。</summary>
    /// <value>切り出しと描画の矩形。<see cref="Center"/> が未設定の場合は null（切り出し領域を持たない）。</value>
    /// <remarks>
    /// 「切り出した領域を左上原点から <see cref="ZoomLevel"/> 倍の大きさで描く」規則を単一所有する。<br/>
    /// 切り出し元の画像は含めない（切り出しの実行は描画層が <c>EsBitmap.Crop</c> で行う）。<br/>
    /// <b>描画先は切り上げ分だけ表面より大きくなりうる</b>（最大 <c>ZoomLevel - 1</c> 物理ピクセル）。<br/>
    /// はみ出した部分は描画先のクリップに任せる（切り捨てると拡大の倍率が整数でなくなり画素がぼやける）。
    /// </remarks>
    public LoupeMagnifiedImage? MagnifiedImage => CaptureTopLeft is not { } topLeft
        ? null
        : new LoupeMagnifiedImage(
            new EsRectInt((int)topLeft.X, (int)topLeft.Y, CaptureSize, CaptureSize),
            new EsRect(0, 0, CaptureSize, CaptureSize),
            new EsRect(0, 0, CaptureSize * ZoomLevel, CaptureSize * ZoomLevel));

    /// <summary>整数の切り上げ除算を行う（余りを 1 区画として数える）。</summary>
    /// <param name="value">割られる値（物理ピクセルサイズ）。 1 以上。</param>
    /// <param name="divisor">割る値（1 区画の長さ = 拡大率）。 1 以上。</param>
    /// <returns>切り上げた商（区画数）。</returns>
    /// <remarks>切り捨てると端の区画が失われる（除数は 1 以上を前提とする。 0 を渡すと <see cref="double"/> の除算が無限大になる）。</remarks>
    private static int CeilDiv(int value, int divisor) => (int)Math.Ceiling((double)value / divisor);
}
