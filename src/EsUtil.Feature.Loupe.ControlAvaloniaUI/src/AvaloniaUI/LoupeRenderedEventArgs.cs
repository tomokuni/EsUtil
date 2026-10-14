using Avalonia.Media;

namespace EsUtil.Feature.Loupe.AvaloniaUI;

/// <summary>描画完了（ルーペの内容を描き終えたこと）を通知するイベント引数。</summary>
/// <remarks>
/// <b>ホスト固有の付加情報を描くための入口である</b>: ルーペの内容（ペーストボード・拡大画像・十字線・枠線）は<br/>
/// コントロールが窓の描画面へ直接描くが、選択矩形の破線・検出した線分などアプリ固有の情報は<br/>
/// 本コントロールの概念ではないため、ホストが本イベントで自前で描く。<br/>
/// <br/>
/// <b>描画順</b>: ルーペの内容を描いた直後に発火するため、ホストが描いた内容は<b>ルーペの内容の上</b>に置かれる。<br/>
/// 中心座標ラベル（テキスト要素）は本イベントより後に描かれるため、常にホストの描画より前面になる。<br/>
/// <br/>
/// <b>コンテキストの有効期間</b>: <see cref="Context"/> は <c>Render</c> の実行中のみ有効である。<br/>
/// ハンドラ内で描画を完了させること（後から使うために保持しない）。
/// </remarks>
/// <param name="context">描画コンテキスト（<c>Render</c> の実行中のみ有効）。</param>
/// <param name="content">実際に描画した描画計画（切り出し領域の左上・拡大率・描画面のサイズを持つ）。</param>
public sealed class LoupeRenderedEventArgs(DrawingContext context, LoupeDrawPlan content) : EventArgs
{
    /// <summary>描画コンテキスト（<c>Render</c> の実行中のみ有効）。</summary>
    /// <value>ルーペの内容を描いた描画先。 ホストはここへ付加情報を描く。</value>
    public DrawingContext Context { get; } = context;

    /// <summary>実際に描画した描画計画を取得する。</summary>
    /// <value>切り出し領域の左上（<see cref="LoupeDrawPlan.CaptureTopLeft"/>）・拡大率・描画面のサイズを持つ計画。</value>
    /// <remarks>
    /// 付加情報の座標変換に必要な値（対象座標の左上・拡大率）は本値から取得する（値を複製しない）。<br/>
    /// 中心未設定時は <see cref="LoupeDrawPlan.CaptureTopLeft"/> が null（付加情報の配置基準が無い）。
    /// </remarks>
    public LoupeDrawPlan Content { get; } = content;
}
