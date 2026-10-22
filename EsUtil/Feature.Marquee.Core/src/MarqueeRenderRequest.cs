using EsUtil.Platform.Core.Coord;
using EsUtil.Platform.Core.Geometry;

namespace EsUtil.Feature.Marquee;

/// <summary>1 フレーム分の描画の幾何を解決するための入力（設定・状態と、描画時に決まる値を 1 つにまとめた値）。</summary>
/// <param name="Options">選択矩形の設定（ハンドルの寸法・十字マーカーの長さ・対話パラメータ）。</param>
/// <param name="ClientSize">描画領域のサイズ（DIP。 原点は常に 0）。</param>
/// <param name="SelectionSourceRect">選択矩形（対象座標）。 選択なしの場合は null（<b>null が「選択なし」を意味する</b>）。 区間の流儀は作成元が決める（ドラッグ由来は <see cref="EsRectInterval.Closed"/>）。</param>
/// <param name="MarkerActive">始点マーカー（十字線）を描くかどうか（nudge 中かつ十字線が可視の場合に true）。</param>
/// <param name="MarkerPosition">始点マーカーの中心（対象座標）。</param>
/// <param name="MouseSourcePosition">直近のマウス位置（対象座標。 カーソル座標ラベルの文言の基準）。</param>
/// <param name="Port">対象座標 ↔ ローカル DIP の座標変換ポート。 null の場合は <paramref name="ClientToSourceScaling"/> を倍率として適用する。</param>
/// <param name="ClientToSourceScaling">ポート未指定の場合に適用する倍率（対象座標 / ローカル DIP。 0 以下・等倍は等価変換）。</param>
/// <remarks>
/// 設定と状態の単一値（<see cref="Options"/>）に、描画時にしか決まらない値（描画領域のサイズ・座標変換ポート・倍率）を加えたものである。<br/>
/// <b>UI 層は本値を自分で組み立てない</b>: セッションの状態を写す組み立て順は <see cref="MarqueeCore.CreateRequest"/> が単一所有する。<br/>
/// UI 層は本値を <see cref="MarqueeCore.ResolveGeometry"/> へ渡すだけで、描画に必要な値の意味・単位を持たない。<br/>
/// <b>本値から導出する値は本値が単一所有する</b>: 座標変換ポートの決定（<see cref="ResolvedPort"/>）、注目矩形の決定（<see cref="Focus"/> /<br/>
/// <see cref="ResolveFocusDipRect"/>）、論理座標の矩形を DIP 矩形へ変換する手順（<see cref="ToDipRect"/> / <see cref="ToSourceRect"/>）は本値のメンバーが持つ。<br/>
/// UI フレームワークに依存しないため、UI 初期化なしの <c>[Fact]</c> で並列検証できる。
/// </remarks>
public readonly record struct MarqueeRenderRequest(
    MarqueeOptions Options,
    EsSize ClientSize,
    EsRect? SelectionSourceRect,
    bool MarkerActive,
    EsPoint MarkerPosition,
    EsPoint MouseSourcePosition,
    ICoordTransform? Port,
    double ClientToSourceScaling)
{
    /// <summary>使用する座標変換ポートを取得する（ポート優先・未指定なら倍率から生成）。</summary>
    /// <value>使用する座標変換ポート。</value>
    /// <remarks>解決の規則は <see cref="ResolvePort"/> が単一所有する。</remarks>
    public ICoordTransform ResolvedPort => ResolvePort(Port, ClientToSourceScaling);

    /// <summary>描画面の全体を取得する（左上を原点（0, 0）とする矩形）。</summary>
    /// <value>幅・高さが <see cref="ClientSize"/> の矩形（DIP）。</value>
    internal EsRect ClientRect => new(0, 0, ClientSize.Width, ClientSize.Height);

    /// <summary>注目矩形（暗転マスクを抜く領域）を取得する。</summary>
    /// <value>注目矩形（対象座標）。 ハイライトも選択も無い場合は null。</value>
    /// <remarks>
    /// 優先順（ハイライト → 選択）の規則は <see cref="ResolveFocusRect"/> が単一所有する（本値は設定と状態を渡すだけである）。<br/>
    /// 終点の扱い（<c>+1</c> するか）は選ばれた矩形の <see cref="EsRect.Interval"/> が持ち、<see cref="ToDipRect"/> が読む<br/>
    /// （ハイライト矩形は検出結果が全ピクセルを含むため <see cref="EsRectInterval.HalfOpen"/>、ドラッグの選択矩形は <see cref="EsRectInterval.Closed"/>）。
    /// </remarks>
    internal EsRect? Focus => ResolveFocusRect(HighlightSourceRect, SelectionSourceRect);

    /// <summary>ハイライト矩形（ホストが与える実行時状態・対象座標）を取得または設定する。</summary>
    /// <value>ハイライト矩形（null は「ハイライトしない」）。 選択矩形より優先される。</value>
    /// <remarks>選択矩形と異なり選択コントロールの操作では変化しない（ホストが与える）。</remarks>
    public EsRect? HighlightSourceRect { get; init; }

    /// <summary>注目矩形を DIP 座標で取得する。</summary>
    /// <param name="port">解決済みの座標変換ポート（1 フレームに 1 回だけ解決した値を渡す）。</param>
    /// <returns>注目矩形（DIP）。 ハイライトも選択も無い場合は null。</returns>
    /// <remarks>
    /// 装飾の枠線・内側の塗り・サイズラベルが同じ注目矩形を使うため、<br/>
    /// 呼び出し側は本メソッドを 1 回だけ呼び、結果を各項目へ渡す（各所が個別に変換すると矩形がずれる）。<br/>
    /// 選ばれた矩形だけを DIP 変換する（両方を変換して片方を捨てない）。
    /// </remarks>
    internal EsRect? ResolveFocusDipRect(ICoordTransform port)
        => Focus is { } focus ? global::EsUtil.Feature.Marquee.MarqueeRenderRequest.ToDipRect(focus, port) : null;

    /// <summary>設定・状態と描画時に決まる値から描画の入力を組み立てる。</summary>
    /// <param name="options">選択矩形の設定。</param>
    /// <param name="clientSize">描画領域のサイズ（DIP。 原点は常に 0）。</param>
    /// <param name="selectionSourceRect">選択矩形（対象座標）。 選択なしの場合は null。</param>
    /// <param name="markerActive">始点マーカー（十字線）を描くかどうか。</param>
    /// <param name="markerPosition">始点マーカーの中心（対象座標）。</param>
    /// <param name="mouseSourcePosition">直近のマウス位置（対象座標）。</param>
    /// <param name="port">対象座標 ↔ ローカル DIP の座標変換ポート。 null の場合は倍率を使う。</param>
    /// <param name="dipToSourceScaling">ポート未指定の場合に適用する倍率（対象座標 / ローカル DIP）。</param>
    /// <param name="highlightSourceRect">ハイライト矩形（対象座標）。 null はハイライトなし。</param>
    /// <returns>描画の入力。</returns>
    /// <remarks>
    /// セッションの状態を写す組み立て順は <see cref="MarqueeCore.CreateRequest"/> が単一所有し、<br/>
    /// 本メソッドは値の組を 1 箇所に閉じる（UI 層が引数の並び順を知らずに済み、入力型の生成元が入力型にある）。
    /// </remarks>
    public static MarqueeRenderRequest Create(
        MarqueeOptions options,
        EsSize clientSize,
        EsRect? selectionSourceRect,
        bool markerActive,
        EsPoint markerPosition,
        EsPoint mouseSourcePosition,
        ICoordTransform? port,
        double dipToSourceScaling,
        EsRect? highlightSourceRect = null)
        => new(options, clientSize, selectionSourceRect, markerActive, markerPosition, mouseSourcePosition, port, dipToSourceScaling)
        {
            HighlightSourceRect = highlightSourceRect,
        };

    /// <summary>座標変換ポートを決定する（ポート優先・未指定なら倍率から等価スケールを生成）。</summary>
    /// <param name="port">座標変換ポート。 指定されている場合はそれをそのまま使う。</param>
    /// <param name="dipToSourceScaling">倍率（対象座標 / ローカル DIP）。 ポート未指定の場合に適用する。</param>
    /// <returns>使用する座標変換ポート。</returns>
    /// <remarks>
    /// 「ポートがあればそれを使い、無ければ倍率（0 以下・等倍は等価変換）を使う」という規則を単一所有する。<br/>
    /// 各 UI はポートと倍率をそのまま渡すだけで、どちらを使うかを判断しない（両 UI で同じ結果になる）。
    /// </remarks>
    internal static ICoordTransform ResolvePort(ICoordTransform? port, double dipToSourceScaling)
    {
        if (port is { } specified)
        {
            return specified;
        }

        return DpiScaler.IsValidScaling(dipToSourceScaling) && dipToSourceScaling != 1.0
            ? CoordTransform.ClientToSource(() => dipToSourceScaling)
            : CoordTransform.Identity;
    }

    /// <summary>対象座標の矩形をポート経由で DIP 矩形へ変換する。</summary>
    /// <param name="logical">対象座標の矩形。</param>
    /// <param name="port">解決済みの座標変換ポート（1 フレームに 1 回だけ解決した値を渡す）。</param>
    /// <returns>DIP 座標の矩形。</returns>
    /// <remarks>
    /// 終点の扱いは <paramref name="logical"/> の <see cref="EsRect.Interval"/> が決める（呼び出し側は渡さない）。<br/>
    /// <see cref="EsRectInterval.Closed"/>（ドラッグの選択矩形）は終点（Right / Bottom）に <c>+1</c> してから DIP 変換する<br/>
    /// （終点のピクセルも非暗転領域に含める）。<br/>
    /// <see cref="EsRectInterval.HalfOpen"/>（ホストが与えるハイライト矩形・検出結果）は <c>+1</c> しない<br/>
    /// （既に全ピクセルを含む矩形を <c>+1</c> すると 1px 大きく表示される）。
    /// </remarks>
    internal static EsRect ToDipRect(EsRect logical, ICoordTransform port)
    {
        var topLeft = port.ToDip(new EsPoint(logical.X, logical.Y));
        var bottomRight = port.ToDip(logical.IsClosedInterval
            ? new EsPoint(logical.Right + 1, logical.Bottom + 1)
            : new EsPoint(logical.Right, logical.Bottom));

        return new EsRect(topLeft.X, topLeft.Y, bottomRight.X - topLeft.X, bottomRight.Y - topLeft.Y);
    }

    /// <summary>DIP 矩形を対象座標の矩形へ逆換算する（左上と右下の変換から組む）。</summary>
    /// <param name="dipRect">DIP 座標の矩形。</param>
    /// <param name="port">解決済みの座標変換ポート（<see cref="ICoordTransform.ToSource"/> = DIP → 対象）。</param>
    /// <returns>対象座標の矩形。</returns>
    /// <remarks>ホストが「矩形から表示スケールを解決する」ために使う（コアはモニタを知らない）。</remarks>
    internal static EsRect ToSourceRect(EsRect dipRect, ICoordTransform port)
    {
        var topLeft = port.ToSource(dipRect.TopLeft);
        var bottomRight = port.ToSource(dipRect.BottomRight);

        return new EsRect(topLeft.X, topLeft.Y, bottomRight.X - topLeft.X, bottomRight.Y - topLeft.Y);
    }

    /// <summary>注目矩形（暗転マスクを抜く領域）を決定する。</summary>
    /// <param name="highlightSourceRect">ハイライト矩形。 null の場合はハイライトなし。</param>
    /// <param name="selectionSourceRect">選択矩形。 null の場合は選択なし。</param>
    /// <returns>注目矩形（対象座標）。 どちらも null の場合は null。</returns>
    /// <remarks>
    /// <b>矩形の有無は null が表す</b>: 有効フラグを別に持つと「フラグは true だが矩形が無い」という<br/>
    /// 意味の無い状態を表現でき、設定経路によって扱いが変わる（矩形を 1 つに絞ることで不可能にする）。<br/>
    /// <b>終点の扱いは矩形が持つ</b>: 選択矩形は <see cref="EsRectInterval.Closed"/>（終点のピクセルも含む）、<br/>
    /// ハイライト矩形は <see cref="EsRectInterval.HalfOpen"/>（ホストが全ピクセルを含む矩形を与える）であり、<br/>
    /// 本メソッドは矩形を選ぶだけで <c>+1</c> の判断を持たない（判断は矩形の生成箇所が単一所有する）。
    /// </remarks>
    internal static EsRect? ResolveFocusRect(EsRect? highlightSourceRect, EsRect? selectionSourceRect)
        => highlightSourceRect ?? selectionSourceRect;
}
