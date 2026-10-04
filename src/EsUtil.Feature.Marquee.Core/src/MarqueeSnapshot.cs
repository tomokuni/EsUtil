using EsUtil.Platform.Core.Geometry;

namespace EsUtil.Feature.Marquee;

/// <summary>Marquee イベントのスナップショット（通知時点の状態・選択矩形・アクティブハンドル・マウス位置・操作フラグ）。</summary>
/// <remarks>
/// 各イベント引数（選択矩形イベント / 位置イベント）に含まれ、上位は Marquee の状態をポーリングせずイベントだけで状態を把握できる。<br/>
/// <see cref="SelectionSourceRect"/> は選択矩形（対象座標）。 ドラッグ中はドラッグ中の矩形、nudge 中は nudge 適用後の矩形。<br/>
/// <see cref="ActiveTarget"/> はアクティブなドラッグハンドル。 新規選択・移動・未選択時は <see cref="MarqueeHitTarget.None"/>。<br/>
/// ドラッグ中かどうかは <see cref="State"/>（<see cref="MarqueeInteractionState.Dragging"/> / <see cref="MarqueeInteractionState.DraggingNudging"/>）で判定できる（同じ真実を 2 つ持たない）。<br/>
/// <see cref="IsNudgeActive"/> は nudge（矢印キー微調整）中かどうか。 ホストが nudge 中の表示切替等に使用できる。<br/>
/// コア型（<see cref="EsRect"/> / <see cref="EsPoint"/>）のみに依存し、UI フレームワークへ依存しない（各 UI 実装の境界で変換する）。<br/>
/// <b>値型</b>である: ポインター移動のたびに通知するためヒープ確保を避ける（値セマンティクスで比較できる）。<br/>
/// 項目の並びは「要約 → 主要データ → 位置 → 操作フラグ」の順（可読性のため）。
/// </remarks>
/// <param name="State">対話状態（nudge・ドラッグ・選択の合成）。</param>
/// <param name="SelectionSourceRect">選択矩形（対象座標）。 ドラッグ中の場合はドラッグ中の矩形。</param>
/// <param name="ActiveTarget">アクティブなドラッグハンドル。 None はハンドルなし（新規選択・移動・未選択）。</param>
/// <param name="MousePosition">通知時点のマウス位置（対象座標）。</param>
/// <param name="IsMouseDown">マウスボタン（左）が押下中かどうか。</param>
/// <param name="IsNudgeActive">nudge（矢印キー微調整）中かどうか。</param>
public readonly record struct MarqueeSnapshot(
    MarqueeInteractionState State,
    EsRect SelectionSourceRect,
    MarqueeHitTarget ActiveTarget,
    EsPoint MousePosition,
    bool IsMouseDown,
    bool IsNudgeActive)
{
    /// <summary>選択矩形が有効かどうか（幅・高さが 0 より大きい選択状態）。</summary>
    /// <remarks><see cref="SelectionSourceRect"/> から導出する（利便プロパティ。 コンストラクタ引数には含めない）。</remarks>
    public bool HasSelection => SelectionSourceRect.Width > 0 && SelectionSourceRect.Height > 0;
}
