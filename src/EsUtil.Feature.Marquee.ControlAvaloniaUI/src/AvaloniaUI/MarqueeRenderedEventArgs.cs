using Avalonia.Media;

namespace EsUtil.Feature.Marquee.AvaloniaUI;

/// <summary>描画完了（コントロールが図形を描き終えたこと）を通知するイベント引数。</summary>
/// <remarks>
/// <b>ホスト固有の付加情報を描くための入口である</b>: サイズラベルとカーソル座標ラベルは<br/>
/// コントロールがテキスト要素として配置するが、操作ラベル・情報ラベルなどアプリ固有の情報は<br/>
/// 本コントロールの概念ではないため、ホストが本イベントで自前で描く。<br/>
/// <br/>
/// <b>描画順はコアが単一所有する</b>: <see cref="MarqueeDrawPlan.Layers"/> は図形の順序を持ち、<br/>
/// ラベル（テキスト要素）はコントロールの子要素として描かれた後（＝最上位）に本イベントが発火する。<br/>
/// すなわちホストが描いた内容はラベルより前面に置かれる。<br/>
/// <br/>
/// <b>コンテキストの有効期間</b>: <see cref="Context"/> は <c>Render</c> の実行中のみ有効である。<br/>
/// ハンドラ内で描画を完了させること（後から使うために保持しない）。
/// </remarks>
public sealed class MarqueeRenderedEventArgs : EventArgs
{
    /// <summary>描画コンテキスト（<c>Render</c> の実行中のみ有効）。</summary>
    /// <value>コントロールの描画コンテキスト。 ホストはここへ付加情報を描く。</value>
    public DrawingContext Context { get; }

    /// <summary>1 フレーム分の描画内容（装飾・ハンドル・始点マーカー・ラベル・層の順序）。</summary>
    /// <value>コントロールが実際に描画に使った値（描くものが無い項目は空・null）。</value>
    /// <remarks>
    /// 文字（ラベル）の契約も含むため、ホストは選択矩形や注目矩形を参照して付加情報を配置できる。<br/>
    /// 文字の有無は <see cref="MarqueeDrawPlan.HasText"/> で判定できる。
    /// </remarks>
    public MarqueeDrawPlan Content { get; }

    /// <summary>描画完了イベント引数を初期化する。</summary>
    /// <param name="context">描画コンテキスト（<c>Render</c> の実行中のみ有効）。</param>
    /// <param name="content">1 フレーム分の描画計画。</param>
    public MarqueeRenderedEventArgs(DrawingContext context, MarqueeDrawPlan content)
    {
        Context = context;
        Content = content;
    }
}
