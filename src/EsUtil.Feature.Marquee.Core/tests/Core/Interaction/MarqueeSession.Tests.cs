using EsUtil.Platform.Core;
using EsUtil.Platform.Core.Coord;
using EsUtil.Platform.Core.Geometry;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using EsUtil.Feature.Marquee.Core.Interaction;

namespace EsUtil.Feature.Marquee.Tests.Core.Interaction;

/// <summary><see cref="MarqueeSession"/> の単体テスト（入力の解釈・状態遷移の判断・通知・効果）。</summary>
/// <remarks>
/// UI 層から切り出した判断（nudge セッション・ダブルクリックの確定/キャンセル・ハンドルのスナップ・<br/>
/// クリック検出・最小サイズによるキャンセル・エコー移動の抑制・カーソル種別）を、UI フレームワークなしで検証する。<br/>
/// 境界（<see cref="ICoordTransform"/> の Bounds）が不要なケースは既定の <see cref="CoordTransform.Identity"/>（境界なし）を使用する。
/// </remarks>
public class MarqueeSessionTests
{
    /// <summary>セッションと通知の収集先を生成する。</summary>
    /// <returns>セッションと収集した通知のリスト。</returns>
    private static MarqueeSession CreateSession(out List<MarqueeNotice> notices)
    {
        var session = new MarqueeSession { Log = NullLogger.Instance };
        var collected = new List<MarqueeNotice>();
        session.Notified += (_, notice) => collected.Add(notice);
        notices = collected;
        return session;
    }

    /// <summary>対象の境界 200x200 を持つセッションを生成する（境界クリップ検証用）。</summary>
    /// <param name="notices">収集した通知のリスト。</param>
    /// <returns>境界 200x200 のセッション。</returns>
    private static MarqueeSession CreateBoundedSession(out List<MarqueeNotice> notices)
    {
        var session = CreateSession(out notices);
        session.SetPort(new CoordTransform(p => p, p => p, () => new EsSize(200, 200)));
        return session;
    }

    /// <summary>通知種別の一覧を取得する。</summary>
    /// <param name="notices">通知のリスト。</param>
    /// <returns>通知種別の一覧。</returns>
    private static List<MarqueeNoticeKind> Kinds(List<MarqueeNotice> notices)
        => [.. notices.Select(n => n.Kind)];

    /// <summary>指定種別の最初の通知を取得する。</summary>
    /// <param name="notices">通知のリスト。</param>
    /// <param name="kind">通知種別。</param>
    /// <returns>一致する通知。</returns>
    private static MarqueeNotice Find(List<MarqueeNotice> notices, MarqueeNoticeKind kind)
        => notices.First(n => n.Kind == kind);

    // ====================================================================
    // Press（押下）
    // ====================================================================

    [Fact]
    internal void Press_新規選択を開始しSelectionStartedを通知する()
    {
        var session = CreateSession(out var notices);

        var feedback = session.Press(new EsPoint(10, 10), clickCount: 1);

        Assert.True(session.IsDragging);
        Assert.False(session.HasSelection);
        Assert.Equal(new EsRect(10, 10, 0, 0), session.SelectionSourceRect);
        Assert.Equal([MarqueeNoticeKind.SelectionStarted], Kinds(notices));
        Assert.True(feedback.Redraw);
        Assert.True(feedback.RequestFocus);
        Assert.Equal(MarqueeCaptureAction.Capture, feedback.Capture);
    }

    [Fact]
    internal void Press_ハンドルヒットでポインターをハンドル点へスナップする()
    {
        var session = CreateSession(out var notices);
        session.SetSelection(new EsRect(50, 50, 100, 80));

        // 右下ハンドル（150, 130）付近を押下する（ヒット半径内だが角とずれた位置）
        var feedback = session.Press(new EsPoint(148, 128), clickCount: 1);

        // スナップ: ドラッグ基準点とマウス位置がハンドル点（150, 130）になり、カーソルもそこへ移動する
        Assert.Equal(new EsPoint(150, 130), session.LastMousePosition);
        Assert.True(feedback.Cursor.HasFlag(MarqueeCursorActions.MoveTo));
        Assert.Equal(new EsPoint(150, 130), feedback.CursorSourcePosition);
        Assert.Equal(MarqueeNoticeKind.MarkerPositionMoved, notices[^1].Kind);
        Assert.Equal(new EsPoint(150, 130), notices[^1].Position);

        // 基準点がハンドル点へ揃っているため、ハンドル点への移動では矩形が変わらない
        session.Move(new EsPoint(150, 130));
        Assert.Equal(new EsRect(50, 50, 100, 80), session.SelectionSourceRect);
    }

    [Fact]
    internal void Press_移動ハンドルではポインターをスナップしない()
    {
        var session = CreateSession(out _);
        session.SetSelection(new EsRect(50, 50, 100, 80));

        // 矩形内（中心からずれた位置）を押下する → 移動（Move）
        var feedback = session.Press(new EsPoint(70, 70), clickCount: 1);

        Assert.False(feedback.Cursor.HasFlag(MarqueeCursorActions.MoveTo));

        // スナップしないため、押下位置からの移動量がそのまま矩形へ反映される
        session.Move(new EsPoint(80, 80));
        Assert.Equal(new EsRect(60, 60, 100, 80), session.SelectionSourceRect);
    }

    [Fact]
    internal void Press_ダブルクリック_矩形内は確定を通知し選択を破棄する()
    {
        var session = CreateSession(out var notices);
        session.SetSelection(new EsRect(50, 50, 100, 80));
        notices.Clear();

        var feedback = session.Press(new EsPoint(100, 90), clickCount: 2);

        Assert.Equal(
            [MarqueeNoticeKind.ConfirmRequested],
            Kinds(notices));
        Assert.False(session.HasSelection);
        Assert.True(feedback.Redraw);
        Assert.False(feedback.RequestFocus);
    }

    [Fact]
    internal void Press_ダブルクリック_矩形外はキャンセルを通知し選択を維持する()
    {
        var session = CreateSession(out var notices);
        session.SetSelection(new EsRect(50, 50, 100, 80));
        notices.Clear();

        var feedback = session.Press(new EsPoint(10, 10), clickCount: 2);

        Assert.Equal(
            [MarqueeNoticeKind.CancelRequested],
            Kinds(notices));
        Assert.True(session.HasSelection);
        Assert.False(feedback.Redraw);
    }

    [Fact]
    internal void Press_nudge中はnudge位置を押下位置としカーソルを再表示する()
    {
        var session = CreateSession(out var notices);

        // 未選択のまま右へ nudge（始点が 1 だけ進む）
        session.NudgeByKey(EsKey.Right, new EsPoint(100, 100));
        notices.Clear();

        var feedback = session.Press(session.State.StartPoint, clickCount: 1);

        Assert.False(session.NudgeActive);
        Assert.True(feedback.Cursor.HasFlag(MarqueeCursorActions.Show));
        Assert.Equal([MarqueeNoticeKind.MarkerPositionMoved, MarqueeNoticeKind.SelectionStarted], Kinds(notices));
    }

    // ====================================================================
    // Move（移動）
    // ====================================================================

    [Fact]
    internal void Move_ドラッグ中は選択矩形を更新しSelectionChangedを通知する()
    {
        var session = CreateSession(out var notices);
        session.Press(new EsPoint(10, 10), clickCount: 1);
        notices.Clear();

        var feedback = session.Move(new EsPoint(50, 50));

        // ドラッグ由来の選択矩形は閉区間（始点・終点のピクセルを含む）
        Assert.Equal(new EsRect(10, 10, 40, 40, EsRectInterval.Closed), session.SelectionSourceRect);
        Assert.Equal(
            [MarqueeNoticeKind.SelectionChanged, MarqueeNoticeKind.MarkerPositionMoved],
            Kinds(notices));
        Assert.True(feedback.Redraw);
        Assert.True(feedback.UpdateCursorShape);
    }

    [Fact]
    internal void Move_新規選択中は追従位置が終点を追従する()
    {
        var session = CreateSession(out var notices);
        session.Press(new EsPoint(10, 10), clickCount: 1);
        notices.Clear();

        session.Move(new EsPoint(50, 30));

        var marker = Find(notices, MarqueeNoticeKind.MarkerPositionMoved);
        Assert.Equal(new EsPoint(50, 30), marker.Position);
    }

    [Fact]
    internal void Move_nudge中はマウス位置を上書きしない()
    {
        var session = CreateSession(out _);
        session.Press(new EsPoint(10, 10), clickCount: 1);
        session.Move(new EsPoint(80, 80));
        session.NudgeByKey(EsKey.Right, new EsPoint(80, 80));
        var nudged = session.LastMousePosition;

        // nudge 中（SetCursorPos 由来のエコー移動）はマウス位置・選択矩形を上書きしない
        session.Move(new EsPoint(500, 500));

        Assert.Equal(nudged, session.LastMousePosition);
    }

    [Fact]
    internal void Move_エコー移動は1回だけ抑制する()
    {
        var session = CreateSession(out var notices);
        session.Press(new EsPoint(10, 10), clickCount: 1);
        session.Move(new EsPoint(80, 80));
        session.NudgeByKey(EsKey.Right, new EsPoint(80, 80));
        var nudgePosition = session.MarkerPosition;
        session.Release(new EsPoint(80, 80));
        notices.Clear();

        // 1 回目: 抑制されて通知なし（カーソル形状のみ更新）
        var first = session.Move(nudgePosition);
        Assert.Empty(notices);
        Assert.True(first.UpdateCursorShape);

        // 2 回目: 抑制は解除済みで通知される
        session.Move(nudgePosition);
        Assert.Contains(MarqueeNoticeKind.MarkerPositionMoved, Kinds(notices));
    }

    // ====================================================================
    // Release（解放）
    // ====================================================================

    [Fact]
    internal void Release_最小サイズ未満の矩形はキャンセルする()
    {
        var session = CreateSession(out var notices);
        session.ApplyOptions(session.Options with { MinSelectionSize = 10.0 });
        session.Press(new EsPoint(10, 10), clickCount: 1);
        session.Move(new EsPoint(15, 15));
        notices.Clear();

        session.Release(new EsPoint(15, 15));

        Assert.Contains(MarqueeNoticeKind.SelectionCancelled, Kinds(notices));
        Assert.DoesNotContain(MarqueeNoticeKind.SelectionCompleted, Kinds(notices));
        Assert.False(session.HasSelection);
        Assert.False(session.IsDragging);
    }

    [Fact]
    internal void Release_ドラッグ完了でSelectionCompletedを通知する()
    {
        var session = CreateSession(out var notices);
        session.Press(new EsPoint(10, 10), clickCount: 1);
        session.Move(new EsPoint(80, 80));
        notices.Clear();

        session.Release(new EsPoint(80, 80));

        Assert.Contains(MarqueeNoticeKind.SelectionCompleted, Kinds(notices));
        Assert.Equal(
            new EsRect(10, 10, 70, 70, EsRectInterval.Closed),
            Find(notices, MarqueeNoticeKind.SelectionCompleted).Snapshot.SelectionSourceRect);
        Assert.False(session.IsDragging);
    }

    [Fact]
    internal void Release_nudge中はカーソルをnudge位置へ戻して再表示する()
    {
        var session = CreateSession(out var notices);
        session.SetSelection(new EsRect(50, 50, 100, 80));
        session.Press(new EsPoint(100, 90), clickCount: 1);
        session.NudgeByKey(EsKey.Right, new EsPoint(100, 90));
        var nudgePosition = session.MarkerPosition;

        var feedback = session.Release(new EsPoint(500, 500));

        Assert.False(session.NudgeActive);
        Assert.True(feedback.Cursor.HasFlag(MarqueeCursorActions.MoveTo));
        Assert.Equal(nudgePosition, feedback.CursorSourcePosition);
        Assert.True(feedback.Cursor.HasFlag(MarqueeCursorActions.Show));
        Assert.True(feedback.UpdateCursorShape);

        // 抑制: nudge 位置へのエコー移動は矩形を上書きしない（通知も発生しない）
        var rectBefore = session.SelectionSourceRect;
        notices.Clear();
        session.Move(nudgePosition);
        Assert.Empty(notices);
        Assert.Equal(rectBefore, session.SelectionSourceRect);
    }

    // ====================================================================
    // KeyDown / NudgeByKey（キーボード）
    // ====================================================================

    [Fact]
    internal void KeyDown_Escapeでキャンセル要求を通知し選択を破棄する()
    {
        var session = CreateSession(out var notices);
        session.SetSelection(new EsRect(50, 50, 100, 80));
        notices.Clear();

        var feedback = session.KeyDown(EsKey.Escape, null);

        Assert.True(feedback.Handled);
        Assert.True(feedback.Redraw);
        Assert.Equal([MarqueeNoticeKind.CancelRequested], Kinds(notices));
        Assert.False(session.HasSelection);
    }

    [Theory]
    [InlineData(EsKey.Enter)]
    [InlineData(EsKey.Return)]
    internal void KeyDown_EnterとReturnで確定要求を通知する(EsKey key)
    {
        var session = CreateSession(out var notices);
        session.SetSelection(new EsRect(50, 50, 100, 80));
        notices.Clear();

        var feedback = session.KeyDown(key, null);

        Assert.True(feedback.Handled);
        Assert.Equal([MarqueeNoticeKind.ConfirmRequested], Kinds(notices));
        Assert.False(session.HasSelection);
    }

    [Fact]
    internal void KeyDown_選択なしのEnterでも処理済みとして扱う()
    {
        var session = CreateSession(out var notices);

        var feedback = session.KeyDown(EsKey.Enter, null);

        // 確定要求は発火しないが、キーは処理済み（上位が既定動作へ流さない）
        Assert.True(feedback.Handled);
        Assert.Empty(notices);
        Assert.False(feedback.Redraw);
    }

    [Fact]
    internal void KeyDown_矢印キーでnudgeしカーソルを非表示にする()
    {
        var session = CreateSession(out var notices);

        var feedback = session.KeyDown(EsKey.Left, new EsPoint(100, 100));

        Assert.True(feedback.Handled);
        Assert.True(feedback.Redraw);
        Assert.True(feedback.Cursor.HasFlag(MarqueeCursorActions.Hide));
        Assert.True(feedback.Cursor.HasFlag(MarqueeCursorActions.MoveTo));
        Assert.True(session.NudgeActive);
        Assert.Contains(MarqueeNoticeKind.SelectionChanged, Kinds(notices));
        Assert.Contains(MarqueeNoticeKind.MarkerPositionMoved, Kinds(notices));
    }

    [Fact]
    internal void KeyDown_選択確定後はnudgeしない()
    {
        var session = CreateSession(out var notices);
        session.SetSelection(new EsRect(50, 50, 100, 80));
        notices.Clear();

        var feedback = session.KeyDown(EsKey.Left, null);

        Assert.False(feedback.Handled);
        Assert.False(session.NudgeActive);
        Assert.Empty(notices);
    }

    [Fact]
    internal void KeyDown_矢印キー以外は処理しない()
    {
        var session = CreateSession(out _);

        var feedback = session.KeyDown(EsKey.A, null);

        Assert.False(feedback.Handled);
        Assert.False(session.NudgeActive);
    }

    [Fact]
    internal void NudgeByKey_未選択は基点をカーソル位置へ同期する()
    {
        var session = CreateSession(out _);

        // カーソル位置 (100, 100) を基点として右へ 1 対象ピクセル進む
        session.NudgeByKey(EsKey.Right, new EsPoint(100, 100));

        Assert.Equal(new EsPoint(101, 100), session.State.StartPoint);
    }

    [Fact]
    internal void NudgeByKey_nudge中の2回目以降は基点を再同期しない()
    {
        var session = CreateSession(out _);
        session.NudgeByKey(EsKey.Right, new EsPoint(100, 100));

        // nudge 中にカーソルが別の位置へ動いても基点を再同期しない（移動が相殺されない）
        session.NudgeByKey(EsKey.Right, new EsPoint(500, 500));

        Assert.Equal(new EsPoint(102, 100), session.State.StartPoint);
    }

    [Fact]
    internal void NudgeByKey_nudge中はカーソル位置を記録する()
    {
        var session = CreateSession(out _);

        session.NudgeByKey(EsKey.Right, new EsPoint(100, 100));

        Assert.Equal(new EsPoint(100, 100), session.LastMousePosition);
    }

    [Fact]
    internal void NudgeByKey_カーソル位置が取得できない場合は基点を同期しない()
    {
        var session = CreateSession(out _);

        session.NudgeByKey(EsKey.Right, null);

        // カーソル位置が不明な場合は基点（既定値）から動かさない
        Assert.Equal(new EsPoint(1, 0), session.State.StartPoint);
    }

    // ====================================================================
    // EndNudge（nudge の終了）
    // ====================================================================

    [Fact]
    internal void EndNudge_nudge中はカーソルを再表示し再描画する()
    {
        var session = CreateSession(out _);
        session.NudgeByKey(EsKey.Right, new EsPoint(10, 10));

        var feedback = session.EndNudge();

        Assert.False(session.NudgeActive);
        Assert.True(feedback.Redraw);
        Assert.True(feedback.Cursor.HasFlag(MarqueeCursorActions.Show));
    }

    [Fact]
    internal void EndNudge_nudge中でなければ何もしない()
    {
        var session = CreateSession(out _);

        var feedback = session.EndNudge();

        Assert.False(feedback.Redraw);
        Assert.Equal(MarqueeCursorActions.None, feedback.Cursor);
    }

    // ====================================================================
    // 外部操作（復元・初期化・確定・キャンセル・境界クリップ）
    // ====================================================================

    [Fact]
    internal void SetSelection_選択矩形を復元しSelectionChangedを通知する()
    {
        var session = CreateSession(out var notices);

        var feedback = session.SetSelection(new EsRect(50, 50, 100, 80));

        Assert.Equal(new EsRect(50, 50, 100, 80), session.SelectionSourceRect);
        Assert.True(session.HasSelection);
        Assert.Equal([MarqueeNoticeKind.SelectionChanged], Kinds(notices));
        Assert.True(feedback.Redraw);
    }

    [Fact]
    internal void SetSelection_境界内にクリップしてから設定する()
    {
        var session = CreateBoundedSession(out _);
        session.ApplyOptions(session.Options with { ClampToSourceBounds = true });

        session.SetSelection(new EsRect(150, 150, 100, 100));

        // 境界 200x200 に収まるように切り詰められる
        Assert.Equal(new EsRect(150, 150, 50, 50), session.SelectionSourceRect);
    }

    [Fact]
    internal void SetSelection_クリップ無効時は境界外の矩形もそのまま設定する()
    {
        var session = CreateBoundedSession(out _);
        session.ApplyOptions(session.Options with { ClampToSourceBounds = false });

        session.SetSelection(new EsRect(150, 150, 100, 100));

        Assert.Equal(new EsRect(150, 150, 100, 100), session.SelectionSourceRect);
    }

    [Fact]
    internal void Reset_全状態を初期化する()
    {
        var session = CreateSession(out _);
        session.Press(new EsPoint(10, 10), clickCount: 1);
        session.Move(new EsPoint(80, 80));

        var feedback = session.Reset();

        Assert.False(session.HasSelection);
        Assert.False(session.IsDragging);
        Assert.False(session.NudgeActive);
        Assert.True(feedback.Redraw);
    }

    [Fact]
    internal void Reset_nudge中はカーソルの再表示を要求する()
    {
        var session = CreateSession(out _);
        session.SetSelection(new EsRect(50, 50, 100, 80));
        session.Press(new EsPoint(100, 90), clickCount: 1);
        session.NudgeByKey(EsKey.Right, new EsPoint(100, 90));

        // 対話状態の破棄（nudge の終了とカーソル再表示の要求を含む）は Reset が単一所有する
        var feedback = session.Reset();

        Assert.False(session.NudgeActive);
        Assert.False(session.HasSelection);
        Assert.False(session.IsDragging);
        Assert.True(feedback.Redraw);
        Assert.True(feedback.Cursor.HasFlag(MarqueeCursorActions.Show));
    }

    [Fact]
    internal void SetSelection_nudge中はカーソルの再表示を要求する()
    {
        var session = CreateSession(out _);
        session.SetSelection(new EsRect(50, 50, 100, 80));
        session.Press(new EsPoint(100, 90), clickCount: 1);
        session.NudgeByKey(EsKey.Right, new EsPoint(100, 90));

        var feedback = session.SetSelection(new EsRect(10, 20, 30, 40));

        Assert.False(session.NudgeActive);
        Assert.Equal(new EsRect(10, 20, 30, 40), session.SelectionSourceRect);
        Assert.True(feedback.Cursor.HasFlag(MarqueeCursorActions.Show));
    }

    [Fact]
    internal void RequestConfirm_nudge中はカーソルの再表示を要求する()
    {
        var session = CreateSession(out _);
        session.SetSelection(new EsRect(50, 50, 100, 80));
        session.Press(new EsPoint(100, 90), clickCount: 1);
        // 選択の復元はドラッグを終了させるがマウス押下状態は残るため、押下中（nudge 許可）かつ非ドラッグで確定できる
        session.SetSelection(new EsRect(50, 50, 100, 80));
        session.NudgeByKey(EsKey.Right, new EsPoint(100, 90));

        var feedback = session.RequestConfirm();

        Assert.False(session.NudgeActive);
        Assert.False(session.HasSelection);
        Assert.True(feedback.Cursor.HasFlag(MarqueeCursorActions.Show));
    }

    [Fact]
    internal void RequestCancel_nudge中はカーソルの再表示を要求する()
    {
        var session = CreateSession(out _);
        session.NudgeByKey(EsKey.Right, new EsPoint(100, 100));

        var feedback = session.RequestCancel();

        Assert.False(session.NudgeActive);
        Assert.True(feedback.Redraw);
        Assert.True(feedback.Cursor.HasFlag(MarqueeCursorActions.Show));
    }

    [Fact]
    internal void RequestConfirm_選択確定後は矩形付きで通知し選択をクリアする()
    {
        var session = CreateSession(out var notices);
        session.SetSelection(new EsRect(50, 50, 100, 80));
        notices.Clear();

        var feedback = session.RequestConfirm();

        var notice = Assert.Single(notices);
        Assert.Equal(MarqueeNoticeKind.ConfirmRequested, notice.Kind);
        Assert.Equal(new EsRect(50, 50, 100, 80), notice.Snapshot.SelectionSourceRect);
        Assert.False(session.HasSelection);
        Assert.True(feedback.Redraw);
    }

    [Fact]
    internal void RequestConfirm_選択なしでは何も通知しない()
    {
        var session = CreateSession(out var notices);

        var feedback = session.RequestConfirm();

        Assert.Empty(notices);
        Assert.False(feedback.Redraw);
    }

    [Fact]
    internal void RequestConfirm_ドラッグ中は何も通知しない()
    {
        var session = CreateSession(out var notices);
        session.Press(new EsPoint(10, 10), clickCount: 1);
        notices.Clear();

        session.RequestConfirm();

        Assert.Empty(notices);
    }

    [Fact]
    internal void RequestCancel_選択ありなら破棄して通知する()
    {
        var session = CreateSession(out var notices);
        session.SetSelection(new EsRect(50, 50, 100, 80));
        notices.Clear();

        var feedback = session.RequestCancel();

        Assert.Equal([MarqueeNoticeKind.CancelRequested], Kinds(notices));
        Assert.False(session.HasSelection);
        Assert.True(feedback.Redraw);
    }

    [Fact]
    internal void RequestCancel_選択なしでも通知する()
    {
        var session = CreateSession(out var notices);

        var feedback = session.RequestCancel();

        Assert.Equal([MarqueeNoticeKind.CancelRequested], Kinds(notices));
        Assert.False(feedback.Redraw);
    }

    [Fact]
    internal void ApplyClampToBounds_境界を超える選択矩形をクリップし通知する()
    {
        var session = CreateBoundedSession(out var notices);
        // 境界外の選択を作るため、いったんクリップを無効にする（既定は有効のため SetSelection でクリップされる）
        session.ApplyOptions(session.Options with { ClampToSourceBounds = false });
        session.SetSelection(new EsRect(150, 150, 100, 100));
        notices.Clear();

        var feedback = session.ApplyClampToBounds();

        Assert.Equal(new EsRect(150, 150, 50, 50), session.SelectionSourceRect);
        Assert.Equal([MarqueeNoticeKind.SelectionChanged], Kinds(notices));
        Assert.True(feedback.Redraw);
    }

    [Fact]
    internal void ApplyClampToBounds_境界内の矩形は通知しない()
    {
        var session = CreateBoundedSession(out var notices);
        session.SetSelection(new EsRect(10, 10, 50, 50));
        notices.Clear();

        session.ApplyClampToBounds();

        Assert.Empty(notices);
    }

    // ====================================================================
    // 設定の反映（ApplyOptions）
    // ====================================================================

    [Fact]
    internal void ApplyOptions_対話パラメータを反映する()
    {
        var session = CreateSession(out _);

        session.ApplyOptions(new MarqueeOptions
        {
            MinSelectionSize = 20.0,
            HandleHitRadius = 30.0,
            ClampToSourceBounds = false,
        });

        Assert.Equal(20.0, session.Options.MinSelectionSize);
        Assert.Equal(30.0, session.Options.HandleHitRadius);
        Assert.False(session.Options.ClampToSourceBounds);
    }

    [Fact]
    internal void ApplyOptions_同値の再適用では効果を返さない()
    {
        var session = CreateSession(out var notices);
        var logger = new RecordingLogger();
        session.Log = logger;

        var feedback = session.ApplyOptions(session.Options);

        Assert.False(feedback.Redraw);
        Assert.False(feedback.Handled);
        Assert.Empty(notices);
        Assert.Empty(logger.Messages);
    }

    [Fact]
    internal void ApplyOptions_外観のみの変更でも変更した項目を記録する()
    {
        var session = CreateSession(out _);
        var logger = new RecordingLogger();
        session.Log = logger;

        // 設定は 1 つの値であるため、外観だけの変更も変化した項目として記録する
        session.ApplyOptions(session.Options with
        {
            HandleSize = 20.0,
            MarkerLength = 40.0,
        });

        Assert.Equal(
            [
                "[Marquee] Setting: HandleSize 10 → 20",
                "[Marquee] Setting: MarkerLength 16 → 40",
            ],
            logger.Messages);
    }

    /// <summary>設定の変更ログが変化した項目だけを出力することを検証する。</summary>
    [Fact]
    internal void ApplyOptions_設定の変更ログは変化した項目だけを出力する()
    {
        var session = CreateSession(out _);
        var logger = new RecordingLogger();
        session.Log = logger;

        // 同値の再適用では何も出力しない
        session.ApplyOptions(session.Options);

        Assert.Empty(logger.Messages);

        // 1 項目だけ変えるとその項目だけが出力される
        session.ApplyOptions(session.Options with { HandleHitRadius = 30.0 });

        Assert.Equal("[Marquee] Setting: HandleHitRadius 12 → 30", Assert.Single(logger.Messages));
    }

    [Fact]
    internal void ApplyOptions_境界外選択の禁止の切替でログと即時クリップを行う()
    {
        var session = CreateBoundedSession(out var notices);
        var logger = new RecordingLogger();
        session.Log = logger;
        // 境界外の選択を作るため、いったんクリップを無効にする
        session.ApplyOptions(session.Options with { ClampToSourceBounds = false });
        session.SetSelection(new EsRect(150, 150, 100, 100));
        notices.Clear();
        logger.Messages.Clear();

        var feedback = session.ApplyOptions(session.Options with { ClampToSourceBounds = true });

        Assert.Equal(new EsRect(150, 150, 50, 50), session.SelectionSourceRect);
        Assert.Equal([MarqueeNoticeKind.SelectionChanged], Kinds(notices));
        Assert.True(feedback.Redraw);
        Assert.Equal("[Marquee] Setting: ClampToSourceBounds False → True", Assert.Single(logger.Messages));
    }

    [Fact]
    internal void ApplyOptions_初期設定をコンストラクタで受け取る()
    {
        var session = new MarqueeSession(
            new MarqueeOptions { MinSelectionSize = 25.0, HandleHitRadius = 40.0, ClampToSourceBounds = false })
        {
            Log = NullLogger.Instance,
        };

        Assert.Equal(25.0, session.Options.MinSelectionSize);
        Assert.Equal(40.0, session.Options.HandleHitRadius);
        Assert.False(session.Options.ClampToSourceBounds);
    }

    [Fact]
    internal void ApplyOptions_設定を省略すると既定値を使用する()
    {
        var session = new MarqueeSession(null) { Log = NullLogger.Instance };

        Assert.Equal(MarqueeOptions.Default, session.Options);
        Assert.Equal(6.0, session.Options.MinSelectionSize);
        Assert.Equal(12.0, session.Options.HandleHitRadius);
        Assert.True(session.Options.ClampToSourceBounds);
    }

    // ====================================================================
    // 座標変換（ポート・ヒットテスト半径）
    // ====================================================================

    [Fact]
    internal void ToSourceとToDip_ポートの変換を適用する()
    {
        var session = CreateSession(out _);
        // 表示スケール 2.0（1 DIP = 0.5 対象）のポート
        session.SetPort(CoordTransform.ClientToSource(() => 0.5));

        Assert.Equal(new EsPoint(5, 10), session.ToSource(new EsPoint(10, 20)));
        Assert.Equal(new EsPoint(20, 40), session.ToDip(new EsPoint(10, 20)));
    }

    [Fact]
    internal void ToSource_ポート未設定時は等価変換になる()
    {
        var session = CreateSession(out _);

        Assert.Equal(new EsPoint(10, 20), session.ToSource(new EsPoint(10, 20)));
    }

    [Fact]
    internal void Press_スケール変換ではヒットテスト半径も対象単位へ換算する()
    {
        var session = CreateSession(out _);
        // 表示スケール 2.0（1 DIP = 0.5 対象）にヒット半径 12 DIP を設定する（対象半径 6）
        session.SetPort(CoordTransform.ClientToSource(() => 0.5));
        session.ApplyOptions(session.Options with { HandleHitRadius = 12.0 });
        session.SetSelection(new EsRect(50, 50, 100, 80));

        // 角から 5 対象離れた位置はヒットする（対象半径 6 以内）
        var hit = session.Press(new EsPoint(150 + 5, 130 + 5), clickCount: 1);
        Assert.True(hit.Cursor.HasFlag(MarqueeCursorActions.MoveTo));

        // 角から 20 対象離れた位置はハンドルにヒットしない（矩形外のため新規選択）
        session.Reset();
        session.SetSelection(new EsRect(50, 50, 100, 80));
        var miss = session.Press(new EsPoint(150 + 20, 130 + 20), clickCount: 1);
        Assert.False(miss.Cursor.HasFlag(MarqueeCursorActions.MoveTo));
    }
    // ====================================================================
    // ToCursorKind（ハンドル → カーソル形状の種別）
    // ====================================================================

    /// <summary>ハンドル種別がカーソル形状の種別へ解決することを検証する。</summary>
    [Fact]
    internal void ToCursorKind_対角と辺のハンドルが同じ種別へ解決する()
    {
        // 対角のハンドルは同じ向きのカーソル（左上 = 右下 / 右上 = 左下）
        Assert.Equal(MarqueeCursorKind.DiagonalNWSE, MarqueeSession.ToCursorKind(MarqueeHitTarget.TopLeft));
        Assert.Equal(MarqueeCursorKind.DiagonalNWSE, MarqueeSession.ToCursorKind(MarqueeHitTarget.BottomRight));
        Assert.Equal(MarqueeCursorKind.DiagonalNESW, MarqueeSession.ToCursorKind(MarqueeHitTarget.TopRight));
        Assert.Equal(MarqueeCursorKind.DiagonalNESW, MarqueeSession.ToCursorKind(MarqueeHitTarget.BottomLeft));

        // 辺のハンドルは向きごとのカーソル
        Assert.Equal(MarqueeCursorKind.Vertical, MarqueeSession.ToCursorKind(MarqueeHitTarget.TopCenter));
        Assert.Equal(MarqueeCursorKind.Vertical, MarqueeSession.ToCursorKind(MarqueeHitTarget.BottomCenter));
        Assert.Equal(MarqueeCursorKind.Horizontal, MarqueeSession.ToCursorKind(MarqueeHitTarget.MiddleLeft));
        Assert.Equal(MarqueeCursorKind.Horizontal, MarqueeSession.ToCursorKind(MarqueeHitTarget.MiddleRight));

        // 矩形内は移動
        Assert.Equal(MarqueeCursorKind.Move, MarqueeSession.ToCursorKind(MarqueeHitTarget.Move));
    }

    /// <summary>ハンドル外は既定の種別になることを検証する。</summary>
    [Fact]
    internal void ToCursorKind_ハンドル外は既定の種別を返す()
        => Assert.Equal(MarqueeCursorKind.Default, MarqueeSession.ToCursorKind(MarqueeHitTarget.None));

    /// <summary>すべての有効なハンドルが既定以外の種別へ解決することを検証する。</summary>
    [Fact]
    internal void ToCursorKind_全ハンドルが既定以外の種別へ解決する()
    {
        foreach (var handle in Enum.GetValues<MarqueeHitTarget>())
        {
            if (handle == MarqueeHitTarget.None)
            {
                continue;
            }

            Assert.NotEqual(MarqueeCursorKind.Default, MarqueeSession.ToCursorKind(handle));
        }
    }
}
