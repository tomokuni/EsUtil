using Avalonia;
using EsUtil.Platform.Core.Imaging;

namespace EsUtil.Feature.Loupe.AvaloniaUI;

/// <summary>ルーペの描画完了時に上位へ通知する描画結果と表示情報。</summary>
/// <remarks>
/// <see cref="LoupeControl.Rendered"/> イベントの引数。<br/>
/// 描画済みのルーペ画像（<see cref="EsBitmap"/>）と、ホストがオーバーレイを描くために必要な最小限の情報を表す。<br/>
/// ホスト側はイベントハンドラ内で <see cref="EsBitmap"/> のピクセルへオーバーレイ（破線・図形等）を<br/>
/// 直接描画できる（描画後の画像は <see cref="LoupeControl"/> が表示へ反映する）。<br/>
/// <b>中心座標ラベルは <see cref="LoupeControl"/> がテキスト要素として表示する</b>ため、本引数には含めない。<br/>
/// 座標系は次のとおり:<br/>
/// - <see cref="Bitmap"/>: ルーペ画像のピクセル座標<br/>
/// - <see cref="CaptureTopLeft"/>: 対象座標（元画像ピクセル座標）<br/>
/// - <see cref="Scale"/>: 対象座標 → ルーペ画像ピクセル座標の変換係数<br/>
/// オーバーレイの描画位置は <c>(対象座標 − CaptureTopLeft) × Scale</c> で求められる。<br/>
/// 表示位置・表示サイズ・拡大率・拡大中心は、描画に必要な値をホストが既に保持しているため通知しない<br/>
/// （必要な場合は <see cref="LoupeControl"/> の公開プロパティから取得する）。
/// </remarks>
/// <remarks>表示情報を初期化する。</remarks>
/// <param name="bitmap">描画済みのルーペ画像（EsBitmap）。</param>
/// <param name="captureTopLeft">切り出し領域の左上（対象座標）。 中心未設定時は null。</param>
/// <param name="scale">対象座標 → ルーペ画像ピクセル座標の変換係数。</param>
public sealed class LoupeRenderedEventArgs(EsBitmap bitmap, Point? captureTopLeft, double scale) : EventArgs
{

    /// <summary>描画済みのルーペ画像（EsBitmap）。 イベントハンドラ内でピクセルへオーバーレイを描画できる。</summary>
    /// <remarks>
    /// イベントハンドラ内で <see cref="Bitmap"/> のピクセルを更新した内容は、イベント終了後に
    /// <see cref="LoupeControl"/> が表示へ反映する（イベントは表示反映前に同期的に発火する）。<br/>
    /// <see cref="Bitmap"/> の所有権は <see cref="LoupeControl"/> が保持する（ホスト側で Dispose しないこと）。
    /// </remarks>
    public EsBitmap Bitmap { get; } = bitmap;

    /// <summary>切り出し領域の左上（対象座標）。 中心未設定時は null。</summary>
    /// <value>切り出し領域の左上（元画像ピクセル座標）。 null の場合はオーバーレイを描画できない。</value>
    public Point? CaptureTopLeft { get; } = captureTopLeft;

    /// <summary>対象座標 → ルーペ画像ピクセル座標の変換係数。</summary>
    /// <value>変換係数（拡大率と同値。 DPI スケーリングの影響を受けない）。</value>
    public double Scale { get; } = scale;
}
