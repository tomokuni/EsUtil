using EsUtil.Platform.Core.Geometry;

namespace EsUtil.Feature.Loupe;

/// <summary>ルーペの表示レイアウト（DIP。 表示サイズと表示位置）。</summary>
/// <param name="ClientSize">ルーペの一辺の表示サイズ（DIP。 要求サイズをそのまま使う）。</param>
/// <param name="Position">ルーペの表示位置（左上・親領域の DIP 座標）。 配置できない場合は null。</param>
/// <remarks>
/// <see cref="Position"/> が null になるのは、拡大中心が未設定の場合と、親領域が表示サイズを収められない場合である<br/>
/// （「配置しない」の判断をコアが単一所有し、UI 層はセルフサイズの反映だけを行う）。<br/>
/// 配置をホストが行う UI では <see cref="Position"/> をそのまま配置に使う。<br/>
/// <b><see cref="ClientSize"/> は整数化しない</b>: 要素のサイズ（幅・高さ）と描画入力は同じ値でなければならず、<br/>
/// 途中で切り捨てると同じ表示サイズを求める 2 経路が最大 1 DIP ずれる（描画ビットマップと要素の大きさが食い違う）。
/// </remarks>
public readonly record struct LoupeLayout(double ClientSize, EsPoint? Position);
