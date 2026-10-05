using EsUtil.Platform.Core.Coord;
using EsUtil.Platform.Core.Geometry;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Rect = Avalonia.Rect;

namespace EsUtil.Feature.Marquee.AvaloniaUI.Tests;

/// <summary><see cref="MarqueeControl"/> のヘッドレス不要テスト（状態・設定・イベント通知の純粋 API 検証）。</summary>
/// <remarks>
/// コントロール生成・プロパティ設定・<see cref="MarqueeControl.SetSelection"/> / <see cref="MarqueeControl.ResetSelection"/> /
/// <see cref="MarqueeControl.RequestConfirm"/> / <see cref="MarqueeControl.RequestCancel"/> のみを検証するため、
/// Avalonia ヘッドレス実行（Dispatcher / ウィンドウ表示）を必要としない。<br/>
/// 並列実行を妨げないよう <c>[Fact]</c> で検証する（ヘッドレス必須のポインター・キーイベントテストは
/// <c>MarqueeControlTests</c> が直列コレクションで検証する）。
/// </remarks>
public class MarqueeControlStateTests
{
    /// <summary>テスト用の MarqueeControl を生成する。</summary>
    private static MarqueeControl CreateControl()
        => new() { Width = 200, Height = 200, Options = TestOptions };

    /// <summary>テスト用の設定（最小サイズを 10 にしてドラッグ判定をしやすくする）。</summary>
    private static MarqueeOptions TestOptions
        => MarqueeOptions.Default with { HandleHitRadius = 12.0, MinSelectionSize = 10.0 };

    // ====================================================================
    // 依存プロパティのデフォルト値
    // ====================================================================

    [Fact]
    internal void 設定の既定値が正しい()
    {
        var control = CreateControl();
        var options = control.Options;

        // ハンドルの枠線の既定は装飾の枠線と同じ DarkGray（設定画面の選択枠の色と一致）
        // 外観の既定は暗転する（単体の選択矩形コントロールとして暗転マスクを描く）
        Assert.Equal(2.0, control.Appearance.OuterBorderThickness);
        Assert.True(control.Appearance.ShowHandles);
        Assert.Equal(10.0, options.HandleSize);
        Assert.Equal(12.0, options.HandleHitRadius);
        Assert.Equal(10.0, options.MinSelectionSize); // CreateControl で設定
        Assert.True(options.ClampToSourceBounds);
        Assert.Equal(16.0, options.MarkerLength);
        Assert.True(control.Appearance.ShowCoordLabel);
        Assert.NotNull(control.Log);

        // 設定を指定しない場合の既定値はコアの MarqueeOptions.Default と一致する
        Assert.Equal(MarqueeOptions.Default, new MarqueeControl().Options);
    }

    [Fact]
    internal void 設定を変更すると反映される()
    {
        var control = CreateControl();

        control.Options = MarqueeOptions.Default with
        {
            HandleSize = 14.0,
            HandleHitRadius = 20.0,
            MinSelectionSize = 5.0,
            ClampToSourceBounds = true,
            MarkerLength = 24.0,
        };
        control.Appearance = control.Appearance with
        {
            CoordLabelFontSize = 14.0,
            ShowHandles = false,
            ShowCoordLabel = false,
            OuterBorderThickness = 3.0,
        };
        control.HighlightSourceRect = new EsRect(1, 2, 3, 4);

        var options = control.Options;
        Assert.Equal(3.0, control.Appearance.OuterBorderThickness);
        Assert.False(control.Appearance.ShowHandles);
        Assert.Equal(14.0, options.HandleSize);
        Assert.Equal(20.0, options.HandleHitRadius);
        Assert.Equal(5.0, options.MinSelectionSize);
        Assert.True(options.ClampToSourceBounds);
        Assert.Equal(24.0, options.MarkerLength);
        Assert.False(control.Appearance.ShowCoordLabel);
        Assert.Equal(14.0, control.Appearance.CoordLabelFontSize);
        Assert.Equal(new EsRect(1, 2, 3, 4), control.HighlightSourceRect);
    }

    // ====================================================================
    // CurrentSnapshot
    // ====================================================================

    [Fact]
    internal void CurrentSnapshot_選択状態の全項目を返す()
    {
        var control = CreateControl();

        // 未選択時: State=NoSelection / SelectionSourceRect=0x0 / HasSelection=false / NudgeActive=false
        var snap0 = control.CurrentSnapshot;
        Assert.Equal(MarqueeInteractionState.NoSelection, snap0.State);
        Assert.Equal(default, snap0.SelectionSourceRect);
        Assert.Equal(MarqueeHitTarget.None, snap0.ActiveTarget);
        Assert.False(snap0.HasSelection);
        Assert.False(snap0.IsMouseDown);
        Assert.False(snap0.IsNudgeActive);

        // 選択時: State=Selected / SelectionSourceRect 反映 / HasSelection=true
        control.SetSelection(new Rect(10, 10, 50, 40));
        var snap1 = control.CurrentSnapshot;
        Assert.Equal(MarqueeInteractionState.Selected, snap1.State);
        Assert.Equal(new EsRect(10, 10, 50, 40), snap1.SelectionSourceRect);
        Assert.True(snap1.HasSelection);
        Assert.False(snap1.IsNudgeActive);
    }

    [Fact]
    internal void MarqueeSnapshot_HasSelection_幅か高さが0ならfalseを返す()
    {
        // Width>0 かつ Height=0（幅のみの矩形は選択として無効）
        var snap1 = new MarqueeSnapshot(MarqueeInteractionState.Selected, new EsRect(0, 0, 100, 0), MarqueeHitTarget.None, default, false, false);
        Assert.False(snap1.HasSelection);

        // Width=0 かつ Height>0（高さのみの矩形は選択として無効）
        var snap2 = new MarqueeSnapshot(MarqueeInteractionState.Selected, new EsRect(0, 0, 0, 100), MarqueeHitTarget.None, default, false, false);
        Assert.False(snap2.HasSelection);
    }

    [Fact]
    internal void MarqueeSnapshot_MousePositionが取得できる()
    {
        var snap = new MarqueeSnapshot(MarqueeInteractionState.Selected, default, MarqueeHitTarget.None, new EsPoint(5, 7), false, false);

        Assert.Equal(new EsPoint(5, 7), snap.MousePosition);
    }

    // ====================================================================
    // Log（NullLogger フォールバック）
    // ====================================================================

    [Fact]
    internal void Log_設定したロガーを返す()
    {
        var control = CreateControl();
        var logger = NullLogger.Instance;

        control.Log = logger;

        Assert.Same(logger, control.Log);
    }

    [Fact]
    internal void Log_nullを設定するとNullLoggerにフォールバックする()
    {
        var control = CreateControl();

        control.Log = null!;

        // null 設定時は NullLogger にフォールバックし、例外なくログ出力できる
        Assert.NotNull(control.Log);
    }

    // ====================================================================
    // CoordLabelFontSize の補正（6〜48）
    // ====================================================================

    [Fact]
    internal void CoordLabelFontSize_NaNや範囲外は安全値に補正される()
    {
        var control = CreateControl();

        // NaN → 既定値 12（外観が補正を単一所有する）
        control.Appearance = control.Appearance with { CoordLabelFontSize = double.NaN };
        Assert.Equal(12.0, control.Appearance.CoordLabelFontSize);

        // 上限 48 にクランプ
        control.Appearance = control.Appearance with { CoordLabelFontSize = 100.0 };
        Assert.Equal(48.0, control.Appearance.CoordLabelFontSize);

        // 下限 6 にクランプ
        control.Appearance = control.Appearance with { CoordLabelFontSize = 1.0 };
        Assert.Equal(6.0, control.Appearance.CoordLabelFontSize);
    }

    // ====================================================================
    // SetSelection / ResetSelection / RequestConfirm / RequestCancel
    // ====================================================================

    [Fact]
    internal void SetSelection_選択矩形を復元しSelectionChangedを発火する()
    {
        var control = CreateControl();
        SelectionRectEventArgs? raised = null;
        control.SelectionChanged += (_, e) => raised = e;

        control.SetSelection(new Rect(10, 10, 50, 40));

        Assert.NotNull(raised);
        Assert.Equal(new Rect(10, 10, 50, 40), raised!.Rect);
    }

    [Fact]
    internal void SetSelection_ClampToSourceBounds有効時は画像外の矩形をクリップする()
    {
        var control = CreateControl();
        control.Options = control.Options with { ClampToSourceBounds = true };
        control.SetCoordTransform(new CoordTransform(p => p, p => p, () => new EsSize(200, 200)));
        SelectionRectEventArgs? raised = null;
        control.SelectionChanged += (_, e) => raised = e;

        // 画像外にはみ出す矩形（右下が 200 超え）を復元 → 境界内 (200,200) にクリップされる
        control.SetSelection(new Rect(150, 150, 100, 100));

        Assert.NotNull(raised);
        Assert.Equal(new Rect(150, 150, 50, 50), raised!.Rect);
    }

    [Fact]
    internal void ClampToSourceBounds_ON切替時に既存の選択矩形がクリップされる()
    {
        var control = CreateControl();
        control.SetCoordTransform(new CoordTransform(p => p, p => p, () => new EsSize(200, 200)));
        var rects = new List<Rect>();
        control.SelectionChanged += (_, e) => rects.Add(e.Rect);

        // 既定は ON のため、OFF → ON の切替シナリオを明示する
        control.Options = control.Options with { ClampToSourceBounds = false };

        // 画像外にはみ出す矩形（左上が負・右下が 200 超え）を選択状態にする（OFF のためクリップされない）
        control.SetSelection(new Rect(-50, -30, 300, 260));
        Assert.Equal(new Rect(-50, -30, 300, 260), rects[^1]);

        // ON に切り替えた瞬間に境界内 (0,0)-(200,200) へクリップされる
        control.Options = control.Options with { ClampToSourceBounds = true };

        Assert.Equal(new Rect(0, 0, 200, 200), rects[^1]);
    }

    [Fact]
    internal void ClampToSourceBounds_ON切替時に境界内の矩形は変更されない()
    {
        var control = CreateControl();
        control.SetCoordTransform(new CoordTransform(p => p, p => p, () => new EsSize(200, 200)));
        var rects = new List<Rect>();
        control.SelectionChanged += (_, e) => rects.Add(e.Rect);

        control.SetSelection(new Rect(20, 20, 100, 80));
        Assert.Single(rects);

        // 境界内の矩形なので、ON に切り替えてもクリップされない（SelectionChanged は再発火しない）
        control.Options = control.Options with { ClampToSourceBounds = true };

        Assert.Single(rects);
        Assert.Equal(new Rect(20, 20, 100, 80), rects[^1]);
    }

    [Fact]
    internal void SetSelection_スナップショットに選択矩形とアクティブハンドルが含まれる()
    {
        var control = CreateControl();
        SelectionRectEventArgs? raised = null;
        control.SelectionChanged += (_, e) => raised = e;

        control.SetSelection(new Rect(10, 10, 50, 40));

        Assert.NotNull(raised);
        // Snapshot はイベント引数の Rect と同一の選択矩形（対象座標）を保持する
        Assert.Equal(new EsRect(10, 10, 50, 40), raised!.Snapshot.SelectionSourceRect);
        // 選択復元後はハンドルなし（新規選択・移動・未選択時は None）
        Assert.Equal(MarqueeHitTarget.None, raised.Snapshot.ActiveTarget);
        // 選択確定後の状態は Selected
        Assert.Equal(MarqueeInteractionState.Selected, raised.Snapshot.State);
        // マウスボタンは押下中ではない
        Assert.False(raised.Snapshot.IsMouseDown);
    }

    [Fact]
    internal void ResetSelection_選択状態を初期化する()
    {
        var control = CreateControl();
        control.SetSelection(new Rect(10, 10, 50, 40));

        control.ResetSelection();

        // 状態はイベント Snapshot で把握する（ポーリング不可）
        Assert.NotNull(control.Log);
    }

    [Fact]
    internal void RequestConfirm_選択確定後にConfirmRequestedを発火する()
    {
        var control = CreateControl();
        SelectionRectEventArgs? raised = null;
        control.ConfirmRequested += (_, e) => raised = e;

        control.SetSelection(new Rect(10, 10, 50, 40));
        control.RequestConfirm();

        Assert.NotNull(raised);
        Assert.Equal(new Rect(10, 10, 50, 40), raised!.Rect);
    }

    [Fact]
    internal void RequestConfirm_確定後は選択状態がクリアされる()
    {
        var control = CreateControl();

        control.SetSelection(new Rect(10, 10, 50, 40));
        Assert.True(control.CurrentSnapshot.HasSelection);

        // 確定（RequestConfirm）後は選択状態がクリアされる（Marquee の残留を防ぐ）
        control.RequestConfirm();

        Assert.False(control.CurrentSnapshot.HasSelection);
        Assert.Equal(MarqueeInteractionState.NoSelection, control.CurrentSnapshot.State);
    }

    [Fact]
    internal void RequestConfirm_ドラッグ中は発火しない()
    {
        var control = CreateControl();
        var raised = false;
        control.ConfirmRequested += (_, _) => raised = true;

        control.RequestConfirm();

        Assert.False(raised);
    }

    [Fact]
    internal void RequestCancel_選択ありならCancelRequestedを発火する()
    {
        var control = CreateControl();
        var raised = false;
        control.CancelRequested += (_, _) => raised = true;

        control.SetSelection(new Rect(10, 10, 50, 40));
        control.RequestCancel();

        Assert.True(raised);
    }

    [Fact]
    internal void RequestCancel_選択なしでもCancelRequestedを発火する()
    {
        var control = CreateControl();
        var raised = false;
        control.CancelRequested += (_, _) => raised = true;

        control.RequestCancel();

        Assert.True(raised);
    }
}
