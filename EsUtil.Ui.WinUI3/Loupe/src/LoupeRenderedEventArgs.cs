using EsUtil.Ui.WinUI3.Loupe.Internal;

namespace EsUtil.Ui.WinUI3.Loupe;

/// <summary>ルーペの描画完了時に上位へ通知する追加描画の入力先と描画計画。</summary>
/// <remarks>
/// <see cref="LoupeControl.Rendered"/> イベントの引数。<br/>
/// WinUI 3 版のルーペは XAML と Composition の要素で描くため、<b>描画 API をホストへ公開しない</b>。<br/>
/// ホストは <see cref="AddRect"/> で矩形を追加する（本コントロールが計画の層より前面・ラベルより背面へ置く）。<br/>
/// <b>中心座標ラベルは本コントロールが描く</b>ため、本引数には含めない。<br/>
/// 座標系は次のとおり:<br/>
/// - <see cref="AddRect"/> の座標: ルーペの描画面（1 ピクセル = ルーペ画像の 1 ピクセル）<br/>
/// - <see cref="LoupeDrawPlan.CaptureTopLeft"/>: 対象座標（元画像ピクセル座標）の可視域の左上（**小数を含む実効原点**）<br/>
/// - <see cref="LoupeDrawPlan.ZoomLevel"/>: 対象座標 → ルーペ画像ピクセル座標の変換係数（拡大率と同値）<br/>
/// 追加する矩形の位置は <c>(対象座標 − CaptureTopLeft) × ZoomLevel</c> で求められる<br/>
/// （導出値は描画計画が単一所有するため、本引数は複製しない）。<br/>
/// 拡大中心は、描画に必要な値をホストが既に保持しているため通知しない<br/>
/// （必要な場合は <see cref="LoupeControl"/> の公開プロパティから取得する）。
/// </remarks>
/// <param name="overlay">追加描画を受け取る矩形の列（ルーペ画像のピクセル座標）。</param>
/// <param name="content">実際に描画した描画計画（切り出し領域の左上・拡大率・描画面のサイズを持つ）。</param>
public sealed class LoupeRenderedEventArgs(LoupeRectList overlay, LoupeDrawPlan content) : EventArgs
{
    /// <summary>追加描画を受け取る矩形の列を取得する。</summary>
    /// <value>ホストが <see cref="AddRect"/> で追加した矩形の列。</value>
    internal LoupeRectList Overlay { get; } = overlay;

    /// <summary>実際に描画した描画計画を取得する。</summary>
    /// <value>切り出し領域の左上（<see cref="LoupeDrawPlan.CaptureTopLeft"/>）・拡大率（<see cref="LoupeDrawPlan.ZoomLevel"/>）・描画面のサイズ（<see cref="LoupeDrawPlan.ClientPixelSize"/>）を持つ計画。</value>
    /// <remarks>
    /// 追加描画の座標変換に必要な値は本値から取得する（値を複製しない）。<br/>
    /// 中心未設定時は <see cref="LoupeDrawPlan.CaptureTopLeft"/> が null（追加描画は不可）。
    /// </remarks>
    public LoupeDrawPlan Content { get; } = content;

    /// <summary>ルーペの描画面へ矩形を追加する。</summary>
    /// <param name="x">左端（ルーペ画像のピクセル）。</param>
    /// <param name="y">上端（ルーペ画像のピクセル）。</param>
    /// <param name="width">幅（ピクセル）。 0 以下の場合は追加しない。</param>
    /// <param name="height">高さ（ピクセル）。 0 以下の場合は追加しない。</param>
    /// <param name="argb">塗り色（<c>0xAARRGGBB</c>。 透過度はアルファ）。</param>
    /// <remarks>
    /// 追加した矩形は計画の層（拡大画像・十字線・枠線）より前面、中心座標ラベルより背面へ置かれる。<br/>
    /// 移行前は <c>SKCanvas</c> へ直接描いていたが、描画 API を公開しないため矩形の追加に置き換えた。
    /// </remarks>
    public void AddRect(double x, double y, double width, double height, uint argb)
        => Overlay.Add(x, y, width, height, argb);
}
