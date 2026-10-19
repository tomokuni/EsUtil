using EsUtil.Platform.Core.Coord;
using EsUtil.Platform.Core.Geometry;

namespace EsUtil.Feature.Loupe;

/// <summary>ルーペの幾何（表示レイアウトと描画 1 フレーム分）の解決を担う部分。</summary>
/// <remarks>
/// <b>本コアの公開する幾何の入口は <see cref="ResolveLayout"/> と <see cref="ResolveGeometry"/> である</b>:<br/>
/// 装飾（十字線・枠線）・文言・表示可否・描画順は描画の都合として描画層（本コアを参照する上位の層）が解決するため、<br/>
/// コアは値を決定した幾何だけを返す（描画 API も色も持たない）。<br/>
/// <b>本クラスは親領域のサイズを保持しない</b>: 保持すると「領域の判定」が暗黙の状態になり、値をいつ解決したかに依存する<br/>
/// （親領域のサイズは毎回引数で受け取る）。<br/>
/// <b>表示サイズは縮小しない</b>: 要求サイズをそのまま使う（領域に収められない場合は配置しない）。<br/>
/// <b>可視性</b>: 公開の入口は上記 2 つだけで、内部手順（表示位置の補正）は本 partial の private static に閉じる。
/// </remarks>
public sealed partial class LoupeCore
{
    // ====================================================================
    // 表示レイアウト（表示サイズ・表示位置）
    // ====================================================================

    /// <summary>親領域のサイズを通知して表示レイアウト（表示サイズ・表示位置）を解決する。</summary>
    /// <param name="parentSize">親領域のサイズ（DIP）。 未確定（幅または高さが 0 以下）の場合は位置を補正しない。</param>
    /// <returns>表示レイアウト（表示サイズと表示位置。 拡大中心が未設定の場合と、親領域が表示サイズを収められない場合は位置が null）。</returns>
    /// <remarks>
    /// 処理フロー:<br/>
    /// 1. 表示サイズは要求サイズをそのまま使う（領域に合わせて縮小しない）。<br/>
    /// 2. 拡大中心が未設定の場合と、親領域（確定している場合）が正方形を収められない場合は位置を null として返す（配置しない = 描画しない）。<br/>
    /// 3. 拡大中心を座標変換ポートで親領域 DIP へ変換し、オフセットを加えて親領域内へ補正した位置を返す。<br/>
    /// 親領域が未確定（未接続・レイアウト前）の場合、位置は補正しない（補正可否の判断はコアが単一所有する）。<br/>
    /// <b>はみ出しの補正</b>: 拡大中心が領域の端に寄った場合は、表示サイズを変えずに位置で合わせる（<see cref="EsSize.ClampOffset"/>）。<br/>
    /// <b><see cref="ResolveGeometry"/> との違い</b>: 本メソッドは配置（親領域 DIP）、<see cref="ResolveGeometry"/> は描画（物理ピクセル）を決める。
    /// </remarks>
    public LoupeLayout ResolveLayout(EsSize parentSize)
    {
        var size = (double)Options.RequestedSize;

        if (CenterPoint is not { } center || !CanContain(parentSize, size))
        {
            return new LoupeLayout(size, null);
        }

        var centerDip = _coordPort.OrIdentity().ToDip(center);
        return new LoupeLayout(size, ComputePosition(size, parentSize, centerDip, PositionOffset));
    }

    // ====================================================================
    // 描画の幾何（1 フレーム分）
    // ====================================================================

    /// <summary>現在の状態から描画 1 フレーム分の幾何を解決する。</summary>
    /// <param name="clientSize">ルーペの一辺の表示サイズ（DIP）。 通常は <see cref="ResolveLayout"/> が返した値を渡す。</param>
    /// <returns>描画の幾何（描画面・拡大画像の切り出しと描画の矩形・切り出し領域の一辺を解決済み）。</returns>
    /// <remarks>
    /// <b><see cref="ResolveLayout"/> との役割の違い</b>: <see cref="ResolveLayout"/> は「どこへ・どの大きさで置くか」を<br/>
    /// 親領域 DIP の世界で決め、本メソッドは「何を・何ピクセルで描くか」を物理ピクセルの世界で決める。<br/>
    /// 表示サイズは呼び出し側が決めて渡す（本コアは表示サイズを縮小しないため、通常は要求サイズと同じ値になる）。<br/>
    /// 描画先の表面の大きさは本メソッドの関知しないところであり、本値が返す <see cref="LoupeGeometry.ClientPixelSize"/> と<br/>
    /// 同じ大きさの表面を用意するのは呼び出し側の責務である（本コアは描画先を知らないため判定しない）。<br/>
    /// <b>本メソッドがコアの公開する描画の唯一の入口である</b>: 装飾（十字線・枠線）・文言・表示可否・描画順は<br/>
    /// 描画の都合として描画層（本コアを参照する上位の層）が解決するため、コアは幾何だけを返す。<br/>
    /// DIP → 物理ピクセルの換算は本メソッドが単一所有する。
    /// </remarks>
    public LoupeGeometry ResolveGeometry(double clientSize)
    {
        var zoomLevel = Options.ZoomLevel;
        var pixelSize = DpiScaler.ScaleToPixel(clientSize, ClientToPixelScaling);

        return new LoupeGeometry(
            CenterPoint, clientSize, pixelSize, ClientToPixelScaling, zoomLevel);
    }

    // ====================================================================
    // 内部ヘルパー
    // ====================================================================

    /// <summary>ルーペの表示位置（左上）を計算する（オフセット加算・親領域内への補正）。</summary>
    /// <param name="clientSize">ルーペの表示サイズ（DIP・一辺。 要求サイズの値）。</param>
    /// <param name="parentSize">親領域のサイズ（DIP）。</param>
    /// <param name="centerDip">ルーペ中心（変換後の親領域 DIP 座標）。</param>
    /// <param name="offset">表示オフセット（カーソル右下方向）。</param>
    /// <returns>表示位置（左上座標）。</returns>
    /// <remarks>
    /// 表示位置は中心（DIP）+ オフセットとし、親領域内へ補正する。<br/>
    /// 親領域のサイズが未確定（幅または高さが 0 以下 = レイアウト前・親未接続）の場合は補正しない<br/>
    /// （「親サイズが確定しているか」の判定は <see cref="EsSize.HasArea"/>、補正は<br/>
    /// <see cref="EsSize.ClampOffset(double, double, EsAxis)"/> が単一所有する）。<br/>
    /// 表示サイズは縮小しないため、親領域が収められない場合は 0（左上寄せ）になる<br/>
    /// （そのような領域へ描画しない判断は <see cref="ResolveLayout"/> が行う）。
    /// </remarks>
    private static EsPoint ComputePosition(double clientSize, EsSize parentSize, EsPoint centerDip, double offset)
    {
        double mx = centerDip.X + offset;
        double my = centerDip.Y + offset;
        if (!parentSize.HasArea)
        {
            return new EsPoint(mx, my);
        }

        return new EsPoint(
            parentSize.ClampOffset(mx, clientSize, EsAxis.X),
            parentSize.ClampOffset(my, clientSize, EsAxis.Y));
    }

    /// <summary>領域が一辺 <paramref name="size"/> の正方形を収められるかを判定する。</summary>
    /// <param name="area">判定する領域（親領域。 DIP）。</param>
    /// <param name="size">収める正方形の一辺。</param>
    /// <returns>収められる場合は true。 領域が未確定の場合も true（レイアウト前は判定しない）。</returns>
    /// <remarks>表示サイズは縮小しないため、領域が正方形より小さい場合は配置できない（位置の補正では解決しない）。</remarks>
    private static bool CanContain(EsSize area, double size)
        => !area.HasArea || (area.Width >= size && area.Height >= size);
}
