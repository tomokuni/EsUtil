using EsUtil.Feature.Marquee.Core.Interaction;
using EsUtil.Platform.Core.Coord;
using EsUtil.Platform.Core.Geometry;
using Xunit;

namespace EsUtil.Feature.Marquee.Tests.Core.Interaction;

/// <summary><see cref="MarqueeState"/> の単体テスト（状態遷移・ヒットテスト・ドラッグ・nudge・境界クリップ）。</summary>
/// <remarks>
/// UI フレームワークに依存しないため <c>[Fact]</c> で並列検証できる。<br/>
/// 境界（<see cref="ICoordTransform"/> の Bounds）が不要なケースは既定の <see cref="CoordTransform.Identity"/>（境界なし）を使用する。
/// </remarks>
public class MarqueeStateTests
{
    /// <summary>対象の境界 200x200 を持つ状態を生成する（境界クリップ検証用）。</summary>
    /// <returns>境界 200x200 の状態。</returns>
    private static MarqueeState CreateBounded()
    {
        var state = new MarqueeState();
        state.SetPort(new CoordTransform(p => p, p => p, () => new EsSize(200, 200)));
        return state;
    }

    /// <summary>対象の境界 200x200 と指定の選択矩形を持つ状態を生成する（境界クリップ検証用）。</summary>
    /// <param name="selectionSourceRect">初期の選択矩形（対象座標）。</param>
    /// <returns>境界 200x200 の状態。</returns>
    private static MarqueeState CreateBounded(EsRect selectionSourceRect)
    {
        var state = CreateBounded();
        state.SetSelection(selectionSourceRect);
        return state;
    }

    // ====================================================================
    // GetInteractionState（nudge・ドラッグ・選択の合成）
    // ====================================================================

    [Fact]
    internal void GetInteractionState_nudgeと選択とドラッグの組合せで状態を合成する()
    {
        var state = new MarqueeState();

        // 未選択・非ドラッグ・非 nudge
        Assert.Equal(MarqueeInteractionState.NoSelection, state.GetInteractionState());

        // ドラッグ中
        state.StartNewSelection(new EsPoint(10, 20));
        Assert.Equal(MarqueeInteractionState.Dragging, state.GetInteractionState());

        // 選択あり（ドラッグ終了後）
        state.EndDrag();
        state.SetSelection(new EsRect(0, 0, 10, 10));
        Assert.Equal(MarqueeInteractionState.Selected, state.GetInteractionState());
    }

    [Fact]
    internal void GetInteractionState_nudge中はドラッグと選択より優先される()
    {
        // 未選択・非ドラッグ → 始点決め nudge
        var state = new MarqueeState();
        state.BeginNudge();
        Assert.Equal(MarqueeInteractionState.StartPointNudging, state.GetInteractionState());

        // ドラッグ中（選択なし）→ ドラッグ中 nudge
        state.StartNewSelection(new EsPoint(10, 20));
        Assert.Equal(MarqueeInteractionState.DraggingNudging, state.GetInteractionState());

        // 選択あり → 選択 nudge（選択がドラッグより優先される）
        state.UpdateDrag(new EsPoint(60, 60), 10.0, clampToBounds: false);
        Assert.Equal(MarqueeInteractionState.SelectedNudging, state.GetInteractionState());
    }

    // ====================================================================
    // CreateSnapshot
    // ====================================================================

    [Fact]
    internal void CreateSnapshot_現在の状態を全項目で返す()
    {
        var state = new MarqueeState();
        state.StartNewSelection(new EsPoint(10, 10));
        state.UpdateDrag(new EsPoint(60, 50), 10.0, clampToBounds: false);
        state.StartDrag(MarqueeHitTarget.TopRight, new EsPoint(60, 10));
        state.SetMousePosition(new EsPoint(7, 8));
        state.BeginPress();
        state.BeginNudge();

        var snapshot = state.CreateSnapshot();

        // nudge 中は選択がドラッグより優先される（GetInteractionState の優先順位）
        Assert.Equal(MarqueeInteractionState.SelectedNudging, snapshot.State);
        // ドラッグ由来の選択矩形は閉区間（始点・終点のピクセルを含む）
        Assert.Equal(new EsRect(10, 10, 50, 40, EsRectInterval.Closed), snapshot.SelectionSourceRect);
        Assert.Equal(MarqueeHitTarget.TopRight, snapshot.ActiveTarget);
        Assert.Equal(new EsPoint(7, 8), snapshot.MousePosition);
        Assert.True(snapshot.IsMouseDown);
        Assert.True(snapshot.IsNudgeActive);
        Assert.True(snapshot.HasSelection);
    }

    [Fact]
    internal void CreateSnapshot_マウス位置を明示指定できる()
    {
        var state = new MarqueeState();
        state.SetMousePosition(new EsPoint(1, 1));

        var snapshot = state.CreateSnapshot(new EsPoint(100, 200));

        Assert.Equal(new EsPoint(100, 200), snapshot.MousePosition);
    }

    [Fact]
    internal void MarqueeSnapshot_HasSelection_幅か高さが0ならfalseを返す()
    {
        // 幅のみ（高さ 0）→ 選択として無効
        Assert.False(new MarqueeSnapshot(MarqueeInteractionState.Selected, new EsRect(0, 0, 100, 0), MarqueeHitTarget.None, default, false, false).HasSelection);
        // 高さのみ（幅 0）→ 選択として無効
        Assert.False(new MarqueeSnapshot(MarqueeInteractionState.Selected, new EsRect(0, 0, 0, 100), MarqueeHitTarget.None, default, false, false).HasSelection);
    }

    // ====================================================================
    // HitTest
    // ====================================================================

    [Fact]
    internal void HitTest_未選択は常にNoneを返す()
    {
        var state = new MarqueeState();

        Assert.Equal(MarqueeHitTarget.None, state.HitTest(new EsPoint(0, 0), 12.0));
    }

    [Fact]
    internal void HitTest_角は辺より優先され矩形内はMoveを返す()
    {
        var state = new MarqueeState();
        state.SetSelection(new EsRect(10, 20, 100, 80));

        // 角（左上・右下）
        Assert.Equal(MarqueeHitTarget.TopLeft, state.HitTest(new EsPoint(11, 21), 12.0));
        Assert.Equal(MarqueeHitTarget.BottomRight, state.HitTest(new EsPoint(109, 99), 12.0));
        // 左上の近傍は角が辺（MiddleLeft/TopCenter）より優先される
        Assert.Equal(MarqueeHitTarget.TopLeft, state.HitTest(new EsPoint(14, 24), 12.0));
        // 辺（上中央・中右）
        Assert.Equal(MarqueeHitTarget.TopCenter, state.HitTest(new EsPoint(60, 20), 12.0));
        Assert.Equal(MarqueeHitTarget.MiddleRight, state.HitTest(new EsPoint(110, 60), 12.0));
        // 矩形内 → Move
        Assert.Equal(MarqueeHitTarget.Move, state.HitTest(new EsPoint(60, 60), 12.0));
        // 矩形外かつハンドル半径外 → None
        Assert.Equal(MarqueeHitTarget.None, state.HitTest(new EsPoint(300, 300), 12.0));
    }

    // ====================================================================
    // StartNewSelection / UpdateDrag / EndDrag
    // ====================================================================

    [Fact]
    internal void StartNewSelection_始点を保持し0x0の矩形でドラッグを開始する()
    {
        var state = new MarqueeState();

        state.StartNewSelection(new EsPoint(10, 20));

        Assert.True(state.IsDragging);
        Assert.Equal(MarqueeHitTarget.None, state.ActiveTarget);
        Assert.Equal(new EsPoint(10, 20), state.StartPoint);
        Assert.Equal(new EsPoint(10, 20), state.LastMousePosition);
        Assert.Equal(new EsRect(10, 20, 0, 0), state.SelectionSourceRect);
        Assert.False(state.HasSelection);
    }

    [Fact]
    internal void UpdateDrag_新規選択は始点と現在位置から矩形を作成する()
    {
        var state = new MarqueeState();
        state.StartNewSelection(new EsPoint(10, 20));

        state.UpdateDrag(new EsPoint(60, 80), 10.0, clampToBounds: false);

        // ドラッグ由来の選択矩形は閉区間（始点・終点のピクセルを含む）
        Assert.Equal(new EsRect(10, 20, 50, 60, EsRectInterval.Closed), state.SelectionSourceRect);
        Assert.Equal(new EsPoint(60, 80), state.LastMousePosition);
    }

    [Fact]
    internal void UpdateDrag_ハンドル変形は開始時の矩形を基準にする()
    {
        var state = new MarqueeState();
        state.SetSelection(new EsRect(10, 20, 100, 80));
        state.StartDrag(MarqueeHitTarget.BottomRight, new EsPoint(110, 100));

        state.UpdateDrag(new EsPoint(130, 130), 10.0, clampToBounds: false);

        Assert.Equal(new EsRect(10, 20, 120, 110), state.SelectionSourceRect);
        Assert.Equal(MarqueeHitTarget.BottomRight, state.ActiveTarget);
    }

    [Fact]
    internal void UpdateDrag_境界内クリップ時は新規選択の始点と終点をともにクリップする()
    {
        var state = CreateBounded();
        state.StartNewSelection(new EsPoint(-50, -50));

        state.UpdateDrag(new EsPoint(300, 300), 10.0, clampToBounds: true);

        // 始点・終点とも境界内へクリップされるため、矩形は画像外へはみ出さない
        Assert.Equal(new EsRect(0, 0, 200, 200, EsRectInterval.Closed), state.SelectionSourceRect);
    }

    [Fact]
    internal void UpdateDrag_境界内クリップ時はMoveで矩形全体がシフトされる()
    {
        var state = CreateBounded(new EsRect(180, 180, 50, 40));
        state.StartDrag(MarqueeHitTarget.Move, new EsPoint(200, 200));

        state.UpdateDrag(new EsPoint(260, 260), 10.0, clampToBounds: true);

        // サイズを維持したまま境界内へシフトする（Right=230 Bottom=220 → シフト量は X=-30 / Y=-20）
        Assert.Equal(new EsRect(150, 160, 50, 40), state.SelectionSourceRect);
    }

    [Fact]
    internal void EndDrag_ドラッグ状態とハンドルを解除する()
    {
        var state = new MarqueeState();
        state.SetSelection(new EsRect(10, 20, 100, 80));
        state.StartDrag(MarqueeHitTarget.TopLeft, new EsPoint(10, 20));

        state.EndDrag();

        Assert.False(state.IsDragging);
        Assert.Equal(MarqueeHitTarget.None, state.ActiveTarget);
        // 矩形は維持される（ドラッグ終了は選択を破棄しない）
        Assert.Equal(new EsRect(10, 20, 100, 80), state.SelectionSourceRect);
    }

    // ====================================================================
    // Nudge
    // ====================================================================

    [Fact]
    internal void Nudge_未選択は始点を移動する()
    {
        var state = new MarqueeState();
        state.SetMousePosition(new EsPoint(10, 20));
        state.BeginNudge();

        state.Nudge(3, -4, 10.0, clampToBounds: false);

        Assert.Equal(new EsPoint(13, 16), state.StartPoint);
        Assert.False(state.HasSelection);
    }

    [Fact]
    internal void Nudge_選択確定後は終点を移動する()
    {
        var state = new MarqueeState();
        state.StartNewSelection(new EsPoint(10, 20));
        state.UpdateDrag(new EsPoint(60, 60), 10.0, clampToBounds: false);
        state.EndDrag();

        state.Nudge(5, 5, 10.0, clampToBounds: false);

        // 始点（左上）は固定され、終点（右下）が移動する
        Assert.Equal(new EsRect(10, 20, 55, 45, EsRectInterval.Closed), state.SelectionSourceRect);
        Assert.Equal(new EsPoint(65, 65), state.LastMousePosition);
    }

    [Fact]
    internal void Nudge_ハンドル中はリサイズし次のUpdateDragの基準を更新する()
    {
        var state = new MarqueeState();
        state.SetSelection(new EsRect(10, 20, 100, 80));
        state.StartDrag(MarqueeHitTarget.BottomRight, new EsPoint(110, 100));

        state.Nudge(4, 6, 10.0, clampToBounds: false);

        Assert.Equal(new EsRect(10, 20, 104, 86), state.SelectionSourceRect);

        // nudge 後の UpdateDrag は nudge の結果を基準にする（基準点がハンドル位置へ更新される）
        state.UpdateDrag(new EsPoint(114, 106), 10.0, clampToBounds: false);
        Assert.Equal(new EsRect(10, 20, 104, 86), state.SelectionSourceRect);
    }

    [Fact]
    internal void Nudge_始点が画像外でも矩形は画像外へはみ出さない()
    {
        var state = CreateBounded();
        state.StartNewSelection(new EsPoint(-100, -100));
        state.UpdateDrag(new EsPoint(200, 200), 10.0, clampToBounds: false);

        state.Nudge(10, 10, 10.0, clampToBounds: true);

        // 始点は境界内へクリップされ、終点も境界内に収まる
        Assert.Equal(new EsRect(0, 0, 200, 200, EsRectInterval.Closed), state.SelectionSourceRect);
    }

    // ====================================================================
    // GetMarkerPosition
    // ====================================================================

    [Fact]
    internal void GetMarkerPosition_未選択は始点を返す()
    {
        var state = new MarqueeState();
        state.StartNewSelection(new EsPoint(10, 20));
        state.EndDrag();

        Assert.Equal(new EsPoint(10, 20), state.GetMarkerPosition());
    }

    [Fact]
    internal void GetMarkerPosition_新規選択ドラッグ中は終点を返す()
    {
        var state = new MarqueeState();
        state.StartNewSelection(new EsPoint(10, 20));
        state.UpdateDrag(new EsPoint(60, 60), 10.0, clampToBounds: false);

        Assert.Equal(new EsPoint(60, 60), state.GetMarkerPosition());
    }

    [Fact]
    internal void GetMarkerPosition_リサイズハンドル中はハンドル位置を返す()
    {
        var state = new MarqueeState();
        state.SetSelection(new EsRect(10, 20, 100, 80));
        state.StartDrag(MarqueeHitTarget.TopRight, new EsPoint(110, 20));

        Assert.Equal(new EsPoint(110, 20), state.GetMarkerPosition());
    }

    [Fact]
    internal void GetMarkerPosition_移動ハンドル中はマウス位置を返す()
    {
        var state = new MarqueeState();
        state.SetSelection(new EsRect(10, 20, 100, 80));
        state.StartDrag(MarqueeHitTarget.Move, new EsPoint(45, 67));

        Assert.Equal(new EsPoint(45, 67), state.GetMarkerPosition());
    }

    // ====================================================================
    // Reset / SetSelection（nudge 終了時のカーソル再表示）
    // ====================================================================

    [Fact]
    internal void Reset_全状態を初期化する()
    {
        var state = new MarqueeState();
        state.StartNewSelection(new EsPoint(10, 20));
        state.UpdateDrag(new EsPoint(60, 60), 10.0, clampToBounds: false);
        state.StartDrag(MarqueeHitTarget.Move, new EsPoint(5, 6));
        state.BeginPress();
        state.SuppressEcho(new EsPoint(1, 2));

        state.Reset();

        Assert.Equal(MarqueeHitTarget.None, state.ActiveTarget);
        Assert.False(state.HasSelection);
        Assert.False(state.IsDragging);
        Assert.False(state.IsMouseDown);
        Assert.False(state.IsNudgeActive);
        Assert.Equal(default, state.StartPoint);
        // 抑制位置も破棄される（過去の抑制が次の移動を食わない）
        Assert.False(state.ConsumeSuppressedEcho(new EsPoint(1, 2), 0.5));
    }

    [Fact]
    internal void Reset_nudge中はnudge状態を終了する()
    {
        var state = new MarqueeState();
        state.BeginNudge();

        state.Reset();

        Assert.False(state.IsNudgeActive);
    }

    [Fact]
    internal void SetSelection_矩形を復元しドラッグ状態を解除する()
    {
        var state = new MarqueeState();
        state.SetSelection(new EsRect(0, 0, 10, 10));
        state.StartDrag(MarqueeHitTarget.TopLeft, new EsPoint(0, 0));

        state.SetSelection(new EsRect(30, 40, 50, 60));

        Assert.Equal(new EsRect(30, 40, 50, 60), state.SelectionSourceRect);
        Assert.False(state.IsDragging);
        Assert.Equal(MarqueeHitTarget.None, state.ActiveTarget);
    }

    [Fact]
    internal void SetSelection_nudge中はnudge状態を終了する()
    {
        var state = new MarqueeState();
        state.BeginNudge();

        state.SetSelection(new EsRect(30, 40, 50, 60));

        Assert.Equal(new EsRect(30, 40, 50, 60), state.SelectionSourceRect);
        Assert.False(state.IsNudgeActive);
    }

    [Fact]
    internal void EndNudge_nudge中はnudge状態を終了し報告する()
    {
        var state = new MarqueeState();
        state.BeginNudge();

        var ended = state.EndNudge();

        Assert.True(ended);
        Assert.False(state.IsNudgeActive);
    }

    [Fact]
    internal void EndNudge_nudge中でなければ何もしない()
    {
        var state = new MarqueeState();

        Assert.False(state.EndNudge());
    }

    // ====================================================================
    // ClampSelectionToBounds / ClipRectToBounds
    // ====================================================================

    [Fact]
    internal void ClampSelectionToBounds_境界を超える矩形をクリップする()
    {
        var state = CreateBounded(new EsRect(-50, -30, 300, 260));

        var changed = state.ClampSelectionToBounds();

        Assert.True(changed);
        Assert.Equal(new EsRect(0, 0, 200, 200), state.SelectionSourceRect);
    }

    [Fact]
    internal void ClampSelectionToBounds_境界内の矩形は変更しない()
    {
        var state = CreateBounded(new EsRect(20, 20, 100, 80));

        var changed = state.ClampSelectionToBounds();

        Assert.False(changed);
        Assert.Equal(new EsRect(20, 20, 100, 80), state.SelectionSourceRect);
    }

    [Fact]
    internal void ClampSelectionToBounds_未選択は変更しない()
    {
        var state = CreateBounded();

        Assert.False(state.ClampSelectionToBounds());
    }

    [Fact]
    internal void ClipRectToBounds_境界が未設定なら元の矩形を返す()
    {
        var state = new MarqueeState();
        var rect = new EsRect(-50, -30, 300, 260);

        Assert.Equal(rect, state.ClipRectToBounds(rect));
    }

    // ====================================================================
    // IsNear（2 点の近傍判定）
    // ====================================================================

    /// <summary>2 点が半径以内にあることを検証する。</summary>
    [Fact]
    internal void IsNear_半径以内はtrueを返す()
        => Assert.True(MarqueeState.IsNear(new EsPoint(10, 10), new EsPoint(15, 12), 5.0));

    /// <summary>いずれかの軸が半径を超えると false になることを検証する。</summary>
    [Fact]
    internal void IsNear_半径を超えるとfalseを返す()
    {
        Assert.False(MarqueeState.IsNear(new EsPoint(10, 10), new EsPoint(16, 12), 5.0));
        // X は範囲内・Y が範囲外
        Assert.False(MarqueeState.IsNear(new EsPoint(10, 10), new EsPoint(12, 16), 5.0));
    }

    // ====================================================================
    // ClampPointToBounds（点の境界内クリップ）
    // ====================================================================

    /// <summary>点が境界の内外で期待どおりにクリップされることを検証する。</summary>
    [Fact]
    internal void ClampPointToBounds_境界内へクリップする()
    {
        var bounds = new EsRect(0, 0, 200, 100);

        Assert.Equal(new EsPoint(10, 10), MarqueeState.ClampPointToBounds(new EsPoint(10, 10), bounds));
        // 左端・上端を超える → 0 にクランプ
        Assert.Equal(new EsPoint(0, 0), MarqueeState.ClampPointToBounds(new EsPoint(-5, -5), bounds));
        // 右端・下端を超える → 境界へクランプ
        Assert.Equal(new EsPoint(200, 100), MarqueeState.ClampPointToBounds(new EsPoint(250, 120), bounds));
    }
}
