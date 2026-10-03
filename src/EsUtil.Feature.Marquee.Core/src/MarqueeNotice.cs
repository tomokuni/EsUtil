using EsUtil.Platform.Core.Geometry;

namespace EsUtil.Feature.Marquee;

/// <summary>コア（状態層）から UI 層への通知（種別・通知座標・スナップショット）。</summary>
/// <remarks>
/// UI 層は本通知を自 UI の公開イベント引数へ変換して発火するだけである（判断・状態更新はコアが実施済み）。<br/>
/// <see cref="Position"/> は点座標を表す通知（<see cref="MarqueeNoticeKind.MarkerPositionMoved"/> 等）で使用する。<br/>
/// 矩形の通知（SelectionChanged / SelectionCompleted 等）では選択矩形（<see cref="Snapshot"/>）を使用する。<br/>
/// <b>値型</b>である: ポインター移動のたびに通知するためヒープ確保を避ける。
/// </remarks>
/// <param name="Kind">通知の種別。</param>
/// <param name="Position">通知時点の座標（対象座標）。 点座標を持たない通知では既定値。</param>
/// <param name="Snapshot">通知時点の状態スナップショット。</param>
public readonly record struct MarqueeNotice(MarqueeNoticeKind Kind, EsPoint Position, MarqueeSnapshot Snapshot);
