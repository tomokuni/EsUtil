using SkiaSharp;

// 複製元: EsUtil.Ui.MewUI.Marquee/src/MewUI/MarqueeRenderedEventArgs.cs（独立パッケージ用の意図的な複製。 .github/依存関係.md §4.1）
namespace EsUtil.Ui.WinUI3.Marquee;

/// <summary>描画完了（コントロールが図形とラベルを描き終えたこと）を通知するイベント引数。</summary>
/// <remarks>
/// <b>ホスト固有の付加情報を描くための入口である</b>: サイズラベルとカーソル座標ラベルは<br/>
/// コントロールが描画面へ描くが、操作ラベル・情報ラベルなどアプリ固有の情報は<br/>
/// 本コントロールの概念ではないため、ホストが本イベントで自前で描く。<br/>
/// <br/>
/// <b>描画順</b>: 本イベントはコントロールの既定の描画（図形・ラベル）の後に発火するため、<br/>
/// ホストが描いた内容はコントロールが描いた内容より前面に置かれる。<br/>
/// 層の順序（<see cref="MarqueeDrawPlan.Layers"/>）は描画層が単一所有する。<br/>
/// <br/>
/// <b>キャンバスの有効期間</b>: <see cref="Canvas"/> は描画中のみ有効である。<br/>
/// <b>座標系</b>: <see cref="Canvas"/> には DPI スケーリングが適用済みのため、ホストは DIP 座標で描ける。
/// </remarks>
/// <param name="canvas">描画先のキャンバス（DPI スケーリング適用済み）。</param>
/// <param name="content">1 フレーム分の描画内容。</param>
public sealed class MarqueeRenderedEventArgs(SKCanvas canvas, MarqueeDrawPlan content) : EventArgs
{
    /// <summary>描画先のキャンバス（DPI スケーリング適用済み・描画中のみ有効）。</summary>
    /// <value>コントロールの描画面。 ホストはここへ付加情報を描く。</value>
    public SKCanvas Canvas { get; } = canvas;

    /// <summary>1 フレーム分の描画内容（装飾・ハンドル・始点マーカー・ラベル・層の順序）。</summary>
    /// <value>コントロールが実際に描画に使った値（描くものが無い項目は空・null）。</value>
    /// <remarks>
    /// ラベルの契約も含むため、ホストは注目矩形（<see cref="MarqueeDrawPlan.FocusDipRect"/>）を基準に付加情報を配置できる。<br/>
    /// 文字の有無は <see cref="MarqueeDrawPlan.HasText"/> で判定できる。
    /// </remarks>
    public MarqueeDrawPlan Content { get; } = content;
}
