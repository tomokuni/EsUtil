using EsUtil.Platform.Core.Coord;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Feature.Marquee.Core.Selection;

namespace EsUtil.Feature.Marquee;

/// <summary>選択矩形コントロールの 1 フレーム分の幾何（選択矩形・注目矩形・ハンドル・マーカー）の解決を担う部分。</summary>
/// <remarks>
/// <b>本コアの公開する描画の入口は <see cref="ResolveGeometry"/> である</b>: 装飾（暗転マスク・枠線・内側の塗り）・<br/>
/// 文言・表示可否・描画順は描画の都合として描画層（本コアを参照する上位の層）が解決するため、コアは幾何だけを返す。<br/>
/// <b>注目矩形と座標変換ポートは 1 回だけ解決する</b>: 外側の装飾・内側の装飾・サイズラベルが同じ注目矩形を使い、<br/>
/// すべての項目が同じ座標変換ポートを使う（各所が個別に解決すると矩形がずれ、変換の生成と評価が重複する）。<br/>
/// <b>可視性</b>: 公開の入口は <see cref="ResolveGeometry"/> だけであり、内部手順（ハンドル・マーカーの解決）は本アセンブリに限定する。
/// </remarks>
public sealed partial class MarqueeCore
{
    /// <summary>描画対象の 8 方向ハンドル（<see cref="MarqueeHitTarget.None"/> / <see cref="MarqueeHitTarget.Move"/> を除く）。</summary>
    /// <remarks>
    /// 列挙順は <see cref="MarqueeHitTarget"/> の宣言順。 描画の幾何の解決が本一覧を走査してハンドルの矩形を作る（一覧の単一所有）。<br/>
    /// 読み取り専用の一覧として共有する（呼び出し側が書き換えない）。
    /// </remarks>
    private static readonly IReadOnlyList<MarqueeHitTarget> s_directionalHandles =
        [.. Enum.GetValues<MarqueeHitTarget>().Where(static h => h is not (MarqueeHitTarget.None or MarqueeHitTarget.Move))];

    /// <summary>描画 1 フレーム分の幾何（選択矩形・注目矩形・ハンドル・マーカー）を解決する。</summary>
    /// <param name="request">描画の入力（設定・状態と描画時に決まる値）。</param>
    /// <returns>描画の幾何（注目矩形は DIP で終点の加算も解決済み、ハンドルとマーカーは DIP）。</returns>
    /// <remarks>
    /// 処理フロー:<br/>
    /// 1. 座標変換ポートを 1 回だけ解決する（未指定時は倍率から生成する）。<br/>
    /// 2. 注目矩形（ハイライト優先 → 選択）を 1 回だけ解決し、DIP へ変換する（終点の加算は矩形の <see cref="EsRect.Interval"/> が決める）。<br/>
    /// 3. ハンドル（選択がある場合のみ）とマーカー（nudge 中のみ）を解決する。<br/>
    /// 4. マウス位置の DIP 変換も済ませて返す（描画層が座標変換ポートを持たないようにする）。<br/>
    /// <br/>
    /// 寸法（ハンドルの一辺と枠線の太さ・マーカーの長さ）は設定から読む（本コアは固定値を持たない）。<br/>
    /// 表示可否（ハンドル・ラベルを描くか）は描画層（外観）が判断するため、本メソッドは常に値があれば幾何を返す。
    /// </remarks>
    public MarqueeGeometry ResolveGeometry(in MarqueeRenderRequest request)
    {
        var port = request.ResolvedPort;
        var focus = request.Focus;
        var focusDip = request.ResolveFocusDipRect(port);

        return new MarqueeGeometry(
            request.ClientSize,
            focusDip,
            focus,
            ResolveHandles(request, port),
            ResolveMarker(request, port),
            request.MouseSourcePosition,
            port.ToDip(request.MouseSourcePosition));
    }

    /// <summary>ドラッグハンドルの幾何を解決する。</summary>
    /// <param name="request">描画の入力。</param>
    /// <param name="port">解決済みの座標変換ポート（論理座標 → DIP）。</param>
    /// <returns>ハンドルの幾何。 選択が無い場合は null。</returns>
    /// <remarks>
    /// 中心位置（8 方向）と一辺サイズの適用を本メソッドが解決し、描画層へは矩形の一覧として渡す<br/>
    /// （位置の式を描画層が持つと、ハンドルのヒットテストと同じ規則が 2 箇所になる）。<br/>
    /// 枠線の太さも設定（<see cref="MarqueeOptions.HandleBorderThickness"/>）から解決して渡す<br/>
    /// （描画層が設定を読むと、幾何と線の太さの出所が分かれる）。<br/>
    /// 表示可否は描画層（外観）が判断する。
    /// </remarks>
    private static MarqueeHandles? ResolveHandles(in MarqueeRenderRequest request, ICoordTransform port)
    {
        if (request.SelectionSourceRect is not { } rect)
        {
            return null;
        }

        var options = request.Options;
        var dip = request.ToDipRect(rect, port);
        var half = options.HandleSize / 2;
        var selection = new MarqueeSelection(dip);
        var rects = new List<EsRect>(s_directionalHandles.Count);
        foreach (var handle in s_directionalHandles)
        {
            var center = selection.Handle(handle);
            rects.Add(new EsRect(center.X - half, center.Y - half, options.HandleSize, options.HandleSize));
        }

        return new MarqueeHandles(rects, options.HandleBorderThickness);
    }

    /// <summary>始点マーカー（十字線）の幾何を解決する。</summary>
    /// <param name="request">描画の入力。</param>
    /// <param name="port">解決済みの座標変換ポート（論理座標 → DIP）。</param>
    /// <returns>マーカーの幾何。 nudge 中でない場合は null。</returns>
    /// <remarks>
    /// 色と線の太さは体裁（描画層の外観）が単一所有するため、本メソッドは中心と半幅だけを返す<br/>
    /// （幾何に太さを持たせると、同じ線の太さがコアと外観の 2 箇所に存在することになる）。
    /// </remarks>
    private static MarqueeMarker? ResolveMarker(in MarqueeRenderRequest request, ICoordTransform port)
    {
        if (!request.MarkerActive)
        {
            return null;
        }

        return new MarqueeMarker(
            port.ToDip(request.MarkerPosition),
            request.Options.MarkerLength / 2);
    }
}
