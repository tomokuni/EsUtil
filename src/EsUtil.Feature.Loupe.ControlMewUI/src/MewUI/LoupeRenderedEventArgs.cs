using EsUtil.Platform.Core.Geometry;
using SkiaSharp;

namespace EsUtil.Feature.Loupe.MewUI;

/// <summary>ルーペの描画完了時に上位へ通知する描画先と表示情報。</summary>
/// <remarks>
/// <see cref="LoupeControl.Rendered"/> イベントの引数。<br/>
/// MewUI 版のルーペは中間ビットマップを作らず画面のキャンバスへ直接描画するため、<br/>
/// ホスト側はイベントハンドラ内で <see cref="Canvas"/> へ追加描画（オーバーレイ等）を行う。<br/>
/// <b>中心座標ラベルは <see cref="LoupeControl"/> がテキスト要素として表示する</b>ため、本引数には含めない。<br/>
/// 座標系は次のとおり:<br/>
/// - <see cref="Canvas"/>: ルーペの描画先（1 ピクセル = ルーペ画像の 1 ピクセル）<br/>
/// - <see cref="CaptureTopLeft"/>: 対象座標（元画像ピクセル座標）<br/>
/// - <see cref="Scale"/>: 対象座標 → ルーペ画像ピクセル座標の変換係数<br/>
/// オーバーレイの描画位置は <c>(対象座標 − CaptureTopLeft) × Scale</c> で求められる。<br/>
/// 拡大率・拡大中心は、描画に必要な値をホストが既に保持しているため通知しない<br/>
/// （必要な場合は <see cref="LoupeControl"/> の公開プロパティから取得する）。
/// </remarks>
/// <remarks>表示情報を初期化する。</remarks>
/// <param name="canvas">ルーペを描画したキャンバス（描画済み。 追加描画はイベントハンドラ内で行う）。</param>
/// <param name="clientPixelSize">ルーペの一辺のサイズ（物理ピクセル）。</param>
/// <param name="captureTopLeft">切り出し領域の左上（対象座標）。 中心未設定時は null。</param>
/// <param name="scale">対象座標 → ルーペ画像ピクセル座標の変換係数。</param>
public sealed class LoupeRenderedEventArgs(SKCanvas canvas, int clientPixelSize, EsPoint? captureTopLeft, double scale) : EventArgs
{

    /// <summary>ルーペを描画したキャンバスを取得する。</summary>
    /// <value>描画済みのキャンバス（座標系はルーペ画像のピクセル）。 ホストはここへオーバーレイを追加描画できる。</value>
    /// <remarks>イベントハンドラ内でのみ使用できる（描画が終わると内容は表示へ反映される）。</remarks>
    public SKCanvas Canvas { get; } = canvas;

    /// <summary>ルーペの一辺のサイズ（物理ピクセル）を取得する。</summary>
    /// <value>一辺の物理ピクセル数。</value>
    public int ClientPixelSize { get; } = clientPixelSize;

    /// <summary>切り出し領域の左上（対象座標）を取得する。</summary>
    /// <value>切り出し領域の左上。 中心が未設定の場合は null（オーバーレイを描画できない）。</value>
    public EsPoint? CaptureTopLeft { get; } = captureTopLeft;

    /// <summary>対象座標 → ルーペ画像ピクセル座標の変換係数を取得する。</summary>
    /// <value>変換係数（拡大率と同値。 DPI スケーリングの影響を受けない）。</value>
    public double Scale { get; } = scale;
}
