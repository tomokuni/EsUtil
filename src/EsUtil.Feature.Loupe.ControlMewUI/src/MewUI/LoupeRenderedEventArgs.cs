using SkiaSharp;

namespace EsUtil.Feature.Loupe.MewUI;

/// <summary>ルーペの描画完了時に上位へ通知する描画先と描画計画。</summary>
/// <remarks>
/// <see cref="LoupeControl.Rendered"/> イベントの引数。<br/>
/// MewUI 版のルーペは中間ビットマップを作らず画面のキャンバスへ直接描画するため、<br/>
/// ホスト側はイベントハンドラ内で <see cref="Canvas"/> へ追加描画（オーバーレイ等）を行う。<br/>
/// <b>中心座標ラベルは <see cref="LoupeControl"/> がテキスト要素として表示する</b>ため、本引数には含めない。<br/>
/// 座標系は次のとおり:<br/>
/// - <see cref="Canvas"/>: ルーペの描画先（1 ピクセル = ルーペ画像の 1 ピクセル）<br/>
/// - <see cref="LoupeDrawPlan.CaptureTopLeft"/>: 対象座標（元画像ピクセル座標）<br/>
/// - <see cref="LoupeDrawPlan.ZoomLevel"/>: 対象座標 → ルーペ画像ピクセル座標の変換係数（拡大率と同値）<br/>
/// オーバーレイの描画位置は <c>(対象座標 − CaptureTopLeft) × ZoomLevel</c> で求められる<br/>
/// （導出値は描画計画が単一所有するため、本引数は複製しない）。<br/>
/// 拡大中心は、描画に必要な値をホストが既に保持しているため通知しない<br/>
/// （必要な場合は <see cref="LoupeControl"/> の公開プロパティから取得する）。
/// </remarks>
/// <param name="canvas">ルーペを描画したキャンバス（描画済み。 追加描画はイベントハンドラ内で行う）。</param>
/// <param name="content">実際に描画した描画計画（切り出し領域の左上・拡大率・描画面のサイズを持つ）。</param>
public sealed class LoupeRenderedEventArgs(SKCanvas canvas, LoupeDrawPlan content) : EventArgs
{
    /// <summary>ルーペを描画したキャンバスを取得する。</summary>
    /// <value>描画済みのキャンバス（座標系はルーペ画像のピクセル）。 ホストはここへオーバーレイを追加描画できる。</value>
    /// <remarks>イベントハンドラ内でのみ使用できる（描画が終わると内容は表示へ反映される）。</remarks>
    public SKCanvas Canvas { get; } = canvas;

    /// <summary>実際に描画した描画計画を取得する。</summary>
    /// <value>
    /// 切り出し領域の左上（<see cref="LoupeDrawPlan.CaptureTopLeft"/>）・拡大率（<see cref="LoupeDrawPlan.ZoomLevel"/>）・
    /// 描画面のサイズ（<see cref="LoupeDrawPlan.ClientPixelSize"/>）を持つ計画。
    /// </value>
    /// <remarks>
    /// オーバーレイの座標変換に必要な値は本値から取得する（値を複製しない）。<br/>
    /// 中心未設定時は <see cref="LoupeDrawPlan.CaptureTopLeft"/> が null（オーバーレイ描画不可）。
    /// </remarks>
    public LoupeDrawPlan Content { get; } = content;
}
