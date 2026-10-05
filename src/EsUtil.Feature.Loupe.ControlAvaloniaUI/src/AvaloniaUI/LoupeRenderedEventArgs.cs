using EsUtil.Platform.Core.Imaging;

namespace EsUtil.Feature.Loupe.AvaloniaUI;

/// <summary>ルーペの描画完了時に上位へ通知する描画結果と描画計画。</summary>
/// <remarks>
/// <see cref="LoupeControl.Rendered"/> イベントの引数。<br/>
/// 描画済みのルーペ画像（<see cref="EsBitmap"/>）と、実際に描画した描画計画（<see cref="LoupeDrawPlan"/>）を渡す。<br/>
/// ホスト側はイベントハンドラ内で <see cref="EsBitmap"/> のピクセルへオーバーレイ（破線・図形等）を<br/>
/// 直接描画できる（描画後の画像は <see cref="LoupeControl"/> が表示へ反映する）。<br/>
/// <b>中心座標ラベルは <see cref="LoupeControl"/> がテキスト要素として表示する</b>ため、本引数には含めない。<br/>
/// 座標系は次のとおり:<br/>
/// - <see cref="Bitmap"/>: ルーペ画像のピクセル座標<br/>
/// - <see cref="LoupeDrawPlan.CaptureTopLeft"/>: 対象座標（元画像ピクセル座標）<br/>
/// - <see cref="LoupeDrawPlan.ZoomLevel"/>: 対象座標 → ルーペ画像ピクセル座標の変換係数（拡大率と同値）<br/>
/// オーバーレイの描画位置は <c>(対象座標 − CaptureTopLeft) × ZoomLevel</c> で求められる<br/>
/// （導出値は描画計画が単一所有するため、本引数は複製しない）。<br/>
/// 表示位置・表示サイズは、描画に必要な値をホストが既に保持しているため通知しない<br/>
/// （必要な場合は <see cref="LoupeControl"/> の公開プロパティから取得する）。
/// </remarks>
/// <param name="bitmap">描画済みのルーペ画像（EsBitmap）。</param>
/// <param name="content">実際に描画した描画計画（切り出し領域の左上・拡大率・描画面のサイズを持つ）。</param>
public sealed class LoupeRenderedEventArgs(EsBitmap bitmap, LoupeDrawPlan content) : EventArgs
{
    /// <summary>描画済みのルーペ画像（EsBitmap）。 イベントハンドラ内でピクセルへオーバーレイを描画できる。</summary>
    /// <remarks>
    /// イベントハンドラ内で <see cref="Bitmap"/> のピクセルを更新した内容は、イベント終了後に
    /// <see cref="LoupeControl"/> が表示へ反映する（イベントは表示反映前に同期的に発火する）。<br/>
    /// <see cref="Bitmap"/> の所有権は <see cref="LoupeControl"/> が保持する（ホスト側で Dispose しないこと）。
    /// </remarks>
    public EsBitmap Bitmap { get; } = bitmap;

    /// <summary>実際に描画した描画計画を取得する。</summary>
    /// <value>切り出し領域の左上（<see cref="LoupeDrawPlan.CaptureTopLeft"/>）・拡大率・描画面のサイズを持つ計画。</value>
    /// <remarks>
    /// オーバーレイの座標変換に必要な値（対象座標の左上・拡大率）は本値から取得する（値を複製しない）。<br/>
    /// 中心未設定時は <see cref="LoupeDrawPlan.CaptureTopLeft"/> が null（オーバーレイ描画不可）。
    /// </remarks>
    public LoupeDrawPlan Content { get; } = content;
}
