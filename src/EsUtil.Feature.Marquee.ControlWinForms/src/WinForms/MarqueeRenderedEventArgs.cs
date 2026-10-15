namespace EsUtil.Feature.Marquee.WinForms;

/// <summary>描画完了（コントロールが描画計画を描き終えたこと）を通知するイベント引数。</summary>
/// <remarks>
/// <b>ホスト固有の付加情報を描くための入口である</b>: サイズラベルとカーソル座標ラベルは<br/>
/// コントロールが描画計画に従って描くが、操作ラベル・情報ラベルなどアプリ固有の情報は<br/>
/// 本コントロールの概念ではないため、ホストが本イベントで自前で描く。<br/>
/// <br/>
/// <b>描画順はコアが単一所有する</b>: <see cref="MarqueeDrawPlan.Layers"/> は図形の順序を持ち、<br/>
/// ラベル（文字）は計画の末尾の層として描かれた後（＝最上位）に本イベントが発火する。<br/>
/// すなわちホストが描いた内容はラベルより前面に置かれる。<br/>
/// <br/>
/// <b>描画面の有効期間</b>: <see cref="Graphics"/> は描画中のみ有効である（保持しない）。<br/>
/// <b>座標系</b>: <see cref="Graphics"/> には DPI の拡大が適用済みのため、ホストは DIP 座標で描ける。
/// </remarks>
/// <param name="graphics">描画先（DPI の拡大が適用済み）。</param>
/// <param name="content">1 フレーム分の描画内容。</param>
public sealed class MarqueeRenderedEventArgs(Graphics graphics, MarqueeDrawPlan content) : EventArgs
{
    /// <summary>描画先の描画面（DPI の拡大が適用済み・描画中のみ有効）を取得する。</summary>
    /// <value>コントロールの描画面。 ホストはここへ付加情報を描く。</value>
    public Graphics Graphics { get; } = graphics;

    /// <summary>1 フレーム分の描画内容（装飾・ハンドル・始点マーカー・ラベル・層の順序）を取得する。</summary>
    /// <value>コントロールが実際に描画に使った値（描くものが無い項目は空・null）。</value>
    /// <remarks>
    /// ラベルの契約も含むため、ホストは注目矩形（<see cref="MarqueeDrawPlan.FocusDipRect"/>）を基準に付加情報を配置できる。<br/>
    /// 文字の有無は <see cref="MarqueeDrawPlan.HasText"/> で判定できる。
    /// </remarks>
    public MarqueeDrawPlan Content { get; } = content;
}
