using Aprillz.MewUI;
using Aprillz.MewUI.Controls;
using EsUtil.Platform.Core.Native;
using EsUtil.Platform.Core.Coord;
using EsUtil.Platform.Core.Geometry;
using Microsoft.Extensions.Logging;
using SkiaSharp;
using Xunit;
using DrawingColor = System.Drawing.Color;

namespace EsUtil.Feature.Marquee.MewUI.Tests;

/// <summary>テストで使用するカーソルサービスのスパイ（呼び出しを記録する）。</summary>
/// <remarks>位置の取得・移動は記録のみ行い、表示切替の呼び出し履歴を検証に使用する。</remarks>
internal sealed class CursorServiceSpy : ICursorService
{
    /// <summary><see cref="ShowCursor"/> に渡された値の履歴（呼び出し順）。</summary>
    public List<bool> ShowCalls { get; } = [];

    /// <summary>カーソル位置の設定履歴（呼び出し順）。</summary>
    public List<EsPointInt> MovedCalls { get; } = [];

    /// <summary><see cref="ICursorService.GetCursorScreenPoint"/> の戻り値。</summary>
    public EsPointInt CursorPoint { get; set; }

    /// <inheritdoc/>
    public EsPointInt GetCursorScreenPoint() => CursorPoint;

    /// <inheritdoc/>
    public void SetCursorScreenPoint(EsPointInt point)
    {
        CursorPoint = point;
        MovedCalls.Add(point);
    }

    /// <inheritdoc/>
    public void ShowCursor(bool show) => ShowCalls.Add(show);
}

/// <summary><see cref="MarqueeControl"/>（MewUI 版）の単体テスト（公開 API 経由の状態・イベント検証）。</summary>
/// <remarks>
/// コントロールの生成とプロパティ設定・公開メソッドのみを検証するため、ウィンドウ表示を必要としない。<br/>
/// 入力（マウス・キー）はウィンドウを必要とするため、公開 API 経由で検証できる範囲に限定する。<br/>
/// カーソルサービスはスパイを注入する（Win32 実装を生成しない）。
/// </remarks>
public class MarqueeControlTests
{
    /// <summary>テスト用の MarqueeControl を生成する。</summary>
    /// <param name="cursor">カーソルサービスのスパイ。</param>
    /// <returns>生成したコントロール。</returns>
    private static MarqueeControl CreateControl(CursorServiceSpy cursor)
        => new(cursor) { Width = 200, Height = 200, Options = TestOptions };

    /// <summary>テスト用の設定（最小サイズを 10 にしてドラッグ判定をしやすくする）。</summary>
    private static MarqueeOptions TestOptions
        => MarqueeOptions.Default with { HandleHitRadius = 12.0, MinSelectionSize = 10.0 };

    /// <summary>対象の境界 200x200 を持つコントロールを生成する。</summary>
    /// <param name="cursor">カーソルサービスのスパイ。</param>
    /// <returns>座標変換ポートを設定したコントロール。</returns>
    private static MarqueeControl CreateBounded(CursorServiceSpy cursor)
    {
        var control = CreateControl(cursor);
        control.SetCoordTransform(new CoordTransform(p => p, p => p, () => new EsSize(200, 200)));
        return control;
    }

    // ====================================================================
    // 設定の既定値
    // ====================================================================

    [Fact]
    internal void 設定の既定値がAvalonia版と一致する()
    {
        var control = CreateControl(new CursorServiceSpy());
        var options = control.Options;

        // ハンドルの枠線の既定は装飾の枠線と同じ DarkGray（#FFA9A9A9。 設定画面の選択枠の色と一致）
        // 外観の既定は枠線と暗転を描く
        Assert.Equal(2.0, control.Appearance.OuterBorderThickness);
        Assert.True(control.Appearance.ShowHandles);
        Assert.Equal(10.0, options.HandleSize);
        Assert.Equal(12.0, options.HandleHitRadius);
        Assert.Equal(10.0, options.MinSelectionSize); // CreateControl で設定
        Assert.True(options.ClampToSourceBounds);
        Assert.Equal(16.0, options.MarkerLength);
        Assert.True(control.Appearance.ShowCoordLabel);
        Assert.Equal(1.0, control.ClientToPixelScaling);

        // 設定を指定しない場合の既定値はコアの MarqueeOptions.Default と一致する
        Assert.Equal(MarqueeOptions.Default, new MarqueeControl().Options);
    }

    [Fact]
    internal void 設定を変更すると反映される()
    {
        var control = CreateControl(new CursorServiceSpy());

        control.Options = MarqueeOptions.Default with
        {
            HandleSize = 14.0,
            MarkerLength = 24.0,
        };
        control.Appearance = MarqueeAppearance.Default with
        {
            ShowHandles = false,
            ShowCoordLabel = false,
            OuterBorderThickness = 3.0,
        };
        control.HighlightSourceRect = new EsRect(1, 2, 3, 4);

        var options = control.Options;
        Assert.Equal(3.0, control.Appearance.OuterBorderThickness);
        Assert.False(control.Appearance.ShowHandles);
        Assert.Equal(14.0, options.HandleSize);
        Assert.Equal(24.0, options.MarkerLength);
        Assert.False(control.Appearance.ShowCoordLabel);
        Assert.Equal(new EsRect(1, 2, 3, 4), control.HighlightSourceRect);
    }

    [Fact]
    internal void CoordLabelFontSize_NaNや範囲外は安全値に補正される()
    {
        var control = CreateControl(new CursorServiceSpy());

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
    // CurrentSnapshot
    // ====================================================================

    [Fact]
    internal void CurrentSnapshot_未選択時の全項目が初期値になる()
    {
        var control = CreateControl(new CursorServiceSpy());

        var snapshot = control.CurrentSnapshot;

        Assert.Equal(MarqueeInteractionState.NoSelection, snapshot.State);
        Assert.Equal(default, snapshot.SelectionSourceRect);
        Assert.Equal(MarqueeHitTarget.None, snapshot.ActiveTarget);
        Assert.False(snapshot.HasSelection);
        Assert.False(snapshot.IsMouseDown);
        Assert.False(snapshot.IsNudgeActive);
    }

    [Fact]
    internal void CurrentSnapshot_選択時は選択状態を返す()
    {
        var control = CreateControl(new CursorServiceSpy());

        control.SetSelection(new EsRect(10, 10, 50, 40));

        var snapshot = control.CurrentSnapshot;
        Assert.Equal(MarqueeInteractionState.Selected, snapshot.State);
        Assert.Equal(new EsRect(10, 10, 50, 40), snapshot.SelectionSourceRect);
        Assert.True(snapshot.HasSelection);
    }

    // ====================================================================
    // SetSelection / ResetSelection / 境界クリップ
    // ====================================================================

    [Fact]
    internal void SetSelection_選択矩形を復元しSelectionChangedを発火する()
    {
        var control = CreateControl(new CursorServiceSpy());
        SelectionRectEventArgs? raised = null;
        control.SelectionChanged += (_, e) => raised = e;

        control.SetSelection(new EsRect(10, 10, 50, 40));

        Assert.NotNull(raised);
        Assert.Equal(new EsRect(10, 10, 50, 40), raised!.Rect);
        // Snapshot はイベント引数の矩形と同一の選択矩形（対象座標）を保持する
        Assert.Equal(new EsRect(10, 10, 50, 40), raised.Snapshot.SelectionSourceRect);
        Assert.Equal(MarqueeInteractionState.Selected, raised.Snapshot.State);
    }

    [Fact]
    internal void SetSelection_境界外の矩形は境界内へクリップされる()
    {
        var control = CreateBounded(new CursorServiceSpy());
        SelectionRectEventArgs? raised = null;
        control.SelectionChanged += (_, e) => raised = e;

        // 画像外にはみ出す矩形（右下が 200 超え）を復元 → 境界内 (200,200) にクリップされる
        control.SetSelection(new EsRect(150, 150, 100, 100));

        Assert.NotNull(raised);
        Assert.Equal(new EsRect(150, 150, 50, 50), raised!.Rect);
    }

    [Fact]
    internal void ClampToSourceBounds_ON切替時に既存の選択矩形がクリップされる()
    {
        var control = CreateBounded(new CursorServiceSpy());
        var rects = new List<EsRect>();
        control.SelectionChanged += (_, e) => rects.Add(e.Rect);

        // 既定は ON のため、OFF → ON の切替シナリオを明示する
        control.Options = control.Options with { ClampToSourceBounds = false };

        // 画像外にはみ出す矩形（左上が負・右下が 200 超え）を選択状態にする（OFF のためクリップされない）
        control.SetSelection(new EsRect(-50, -30, 300, 260));
        Assert.Equal(new EsRect(-50, -30, 300, 260), rects[^1]);

        // ON に切り替えた瞬間に境界内 (0,0)-(200,200) へクリップされる
        control.Options = control.Options with { ClampToSourceBounds = true };
        Assert.Equal(new EsRect(0, 0, 200, 200), rects[^1]);
    }

    [Fact]
    internal void ClampToSourceBounds_ON切替時に境界内の矩形は変更されない()
    {
        var control = CreateBounded(new CursorServiceSpy());
        var rects = new List<EsRect>();
        control.SelectionChanged += (_, e) => rects.Add(e.Rect);

        control.SetSelection(new EsRect(20, 20, 100, 80));
        Assert.Single(rects);

        // 境界内の矩形なので、ON に切り替えてもクリップされない（SelectionChanged は再発火しない）
        control.Options = control.Options with { ClampToSourceBounds = true };

        Assert.Single(rects);
        Assert.Equal(new EsRect(20, 20, 100, 80), rects[^1]);
    }

    [Fact]
    internal void ResetSelection_選択状態を初期化する()
    {
        var control = CreateControl(new CursorServiceSpy());
        control.SetSelection(new EsRect(10, 10, 50, 40));

        control.ResetSelection();

        Assert.False(control.CurrentSnapshot.HasSelection);
        Assert.Equal(MarqueeInteractionState.NoSelection, control.CurrentSnapshot.State);
    }

    // ====================================================================
    // RequestConfirm / RequestCancel
    // ====================================================================

    [Fact]
    internal void RequestConfirm_選択確定後にConfirmRequestedを発火し選択をクリアする()
    {
        var control = CreateControl(new CursorServiceSpy());
        SelectionRectEventArgs? raised = null;
        control.ConfirmRequested += (_, e) => raised = e;

        control.SetSelection(new EsRect(10, 10, 50, 40));
        control.RequestConfirm();

        Assert.NotNull(raised);
        Assert.Equal(new EsRect(10, 10, 50, 40), raised!.Rect);
        // 確定後は選択状態がクリアされる（Marquee の残留を防ぐ）
        Assert.False(control.CurrentSnapshot.HasSelection);
    }

    [Fact]
    internal void RequestConfirm_選択なしでは発火しない()
    {
        var control = CreateControl(new CursorServiceSpy());
        var raised = false;
        control.ConfirmRequested += (_, _) => raised = true;

        control.RequestConfirm();

        Assert.False(raised);
    }

    [Fact]
    internal void RequestCancel_選択ありなら選択を破棄してCancelRequestedを発火する()
    {
        var control = CreateControl(new CursorServiceSpy());
        var raised = false;
        control.CancelRequested += (_, _) => raised = true;

        control.SetSelection(new EsRect(10, 10, 50, 40));
        control.RequestCancel();

        Assert.True(raised);
        Assert.False(control.CurrentSnapshot.HasSelection);
    }

    [Fact]
    internal void RequestCancel_選択なしでもCancelRequestedを発火する()
    {
        var control = CreateControl(new CursorServiceSpy());
        var raised = false;
        control.CancelRequested += (_, _) => raised = true;

        control.RequestCancel();

        Assert.True(raised);
    }

    // ====================================================================
    // ログ
    // ====================================================================

    [Fact]
    internal void Log_既定はNullLoggerで例外にならない()
    {
        var control = CreateControl(new CursorServiceSpy());

        // ログ出力を伴う操作を実行しても例外にならない（既定は NullLogger）
        control.Options = control.Options with { ClampToSourceBounds = false };
        control.SetSelection(new EsRect(10, 10, 50, 40));
        control.RequestCancel();
    }

    [Fact]
    internal void Log_null設定はNullLoggerへフォールバックする()
    {
        var control = CreateControl(new CursorServiceSpy());

        control.Log = null!;
        control.RequestCancel();
    }

    [Fact]
    internal void Log_設定変更を出力する()
    {
        var control = CreateControl(new CursorServiceSpy());
        var logger = new RecordingLogger();
        control.Log = logger;
        logger.Messages.Clear();
        logger.Levels.Clear();

        var options = control.Options with { ClampToSourceBounds = false };
        control.Options = options;

        // 「値を所有する主体が記録する」規則により、コアが Debug で記録し、提示層を通過したことを Trace で記録する
        // （どの層まで値が伝搬したかをログの接頭辞から追える）
        Assert.Equal(
            [
                "[Marquee] Setting: ClampToSourceBounds True → False",
                $"[Marquee] Forwarded: Options HandleSize={options.HandleSize}, MinSelectionSize={options.MinSelectionSize}",
            ],
            logger.Messages);
        Assert.Equal([LogLevel.Debug, LogLevel.Trace], logger.Levels);
    }

    [Fact]
    internal void Log_同じ値の再設定では出力しない()
    {
        var control = CreateControl(new CursorServiceSpy());
        var logger = new RecordingLogger();
        control.Log = logger;
        logger.Messages.Clear();

        // 既定値（true）と同じ値を設定してもログは流れない（外観設定の再適用でログが溢れない）
        control.Options = control.Options with { ClampToSourceBounds = true };

        Assert.Empty(logger.Messages);
    }

    [Fact]
    internal void Log_外観の変更は変化した項目だけを出力する()
    {
        var control = CreateControl(new CursorServiceSpy());
        var logger = new RecordingLogger();
        control.Log = logger;
        logger.Messages.Clear();

        // 外観を保持するのは提示層であり、変更の記録も提示層が行う（コアは外観を知らない）。
        // 変化した項目だけを出力する（外観の再適用でログが溢れない）。
        control.Appearance = control.Appearance with { HandleBorderColor = DrawingColor.Red };

        Assert.Equal(
            "[Marquee] Setting: HandleBorderColor #FFA9A9A9 → #FFFF0000",
            Assert.Single(logger.Messages));
    }

    [Fact]
    internal void Log_外観の同値再設定では出力しない()
    {
        var control = CreateControl(new CursorServiceSpy());
        var logger = new RecordingLogger();
        control.Log = logger;
        logger.Messages.Clear();

        control.Appearance = control.Appearance with { };

        Assert.Empty(logger.Messages);
    }

    [Fact]
    internal void Log_確定要求を出力する()
    {
        var control = CreateControl(new CursorServiceSpy());
        var logger = new RecordingLogger();
        control.Log = logger;
        control.SetSelection(new EsRect(10, 10, 50, 40));

        control.RequestConfirm();

        Assert.Contains("[Marquee] ConfirmRequested: rect=(10, 10, 50, 40)", logger.Messages);
    }

    // ====================================================================
    // 基底 UIElement のマウスイベント（本 UI 固有の公開面）
    // ====================================================================

    /// <summary>基底 <c>MouseDown</c> は処理済み（<c>Handled</c>）でも通知されることを検証する。</summary>
    /// <remarks>
    /// 本コントロールは押下・解放の公開イベントを持たない（基底 <c>UIElement</c> が同名のイベントを提供するため）。<br/>
    /// 基底の <c>OnMouseDown</c> は <c>Handled</c> を見ずに <c>MouseDown</c> を発火する（実装を実測で確認した）ため、<br/>
    /// 本コントロールが処理済みにしてもホストの購読者は呼ばれる。<br/>
    /// <b>本番のコントロールでは検証できない</b>: <c>MouseEventArgs.GetPosition</c> がウィンドウ接続を要求するため、<br/>
    /// 配送の規則だけを検証用の最小コントロールで実測する。
    /// </remarks>
    [Fact]
    internal void 基底のMouseDownはHandledでも通知される()
    {
        var probe = new MouseEventProbe();
        var raised = 0;
        probe.MouseDown += _ => raised++;

        var e = CreateMouseArgs(leftButton: true);
        probe.RaiseDown(e);
        e.Handled = true;
        probe.RaiseDown(e);

        Assert.Equal(2, raised);
    }

    /// <summary>基底 <c>MouseUp</c> も処理済み（<c>Handled</c>）でも通知されることを検証する。</summary>
    [Fact]
    internal void 基底のMouseUpはHandledでも通知される()
    {
        var probe = new MouseEventProbe();
        var raised = 0;
        probe.MouseUp += _ => raised++;

        var e = CreateMouseArgs(leftButton: true);
        probe.RaiseUp(e);
        e.Handled = true;
        probe.RaiseUp(e);

        Assert.Equal(2, raised);
    }

    /// <summary>検証用のマウスイベント引数を生成する。</summary>
    /// <param name="leftButton">左ボタンが押下されたか。</param>
    /// <returns>生成したイベント引数。</returns>
    private static MouseEventArgs CreateMouseArgs(bool leftButton)
        => new(default, default, MouseButton.Left, leftButton, false, false, clickCount: 1, ModifierKeys.None);

    /// <summary>基底 <see cref="UIElement"/> のマウスイベント配送を実測するための検証用コントロール。</summary>
    /// <remarks>
    /// 本番のコントロールは座標変換（<c>GetPosition</c>）でウィンドウを要求するため、<br/>
    /// 配送の規則だけを切り出して検証する（保護メソッドを公開して呼び出す）。
    /// </remarks>
    private sealed class MouseEventProbe : Control
    {
        /// <summary>保護された <c>OnMouseDown</c> を呼び出す。</summary>
        /// <param name="e">イベント引数。</param>
        public void RaiseDown(MouseEventArgs e) => OnMouseDown(e);

        /// <summary>保護された <c>OnMouseUp</c> を呼び出す。</summary>
        /// <param name="e">イベント引数。</param>
        public void RaiseUp(MouseEventArgs e) => OnMouseUp(e);
    }
    /// <summary>1 フレーム分の描画内容は 1 回だけ解決されることを検証する。</summary>
    /// <remarks>
    /// 配置と描画で同じ描画内容を共有しないと、選択矩形・装飾・ラベル文言の解決と確保が 1 フレームで 2 回走る。<br/>
    /// サイズラベルの文言プロバイダは <see cref="MarqueePresenter.ResolveDrawPlan"/> が 1 回だけ評価するため、<br/>
    /// その呼び出し回数が 1 フレームの解決回数になる（選択がありサイズラベルを表示する場合のみ評価される）。<br/>
    /// 同じ基準サイズで配置を繰り返しても再解決しない（状態が変わった場合だけ再解決する）。
    /// </remarks>
    [Fact]
    internal void 描画内容は1フレームにつき1回だけ解決される()
    {
        var cursor = new CursorServiceSpy();
        var control = CreateControl(cursor);
        control.SetSelection(new EsRect(20, 20, 60, 60));

        var resolves = 0;
        control.SizeLabelOverrideProvider = () =>
        {
            resolves++;
            return "S";
        };

        control.Arrange(new Rect(0, 0, 200, 200));
        control.Arrange(new Rect(0, 0, 200, 200));

        Assert.Equal(1, resolves);
    }

    // ====================================================================
    // 見た目のカスタマイズ（描画計画の差し替えと既定描画の可否）
    // ====================================================================

    /// <summary>描画計画の差し替えが配置（ラベル要素）と描画の両方へ効くことを検証する。</summary>
    /// <remarks>
    /// 差し替えた <see cref="MarqueeDrawPlan.Labels"/> は配置フェーズでラベル要素になるため、<br/>
    /// 子要素の文言で差し替えが効いたことを確認できる（キャンバスを必要としない）。
    /// </remarks>
    [Fact]
    internal void 描画計画の差し替えがラベル要素へ反映される()
    {
        var cursor = new CursorServiceSpy();
        var control = CreateControl(cursor);
        control.SetSelection(new EsRect(20, 20, 60, 60));
        control.DrawPlanResolver = static plan => plan with
        {
            Labels = [new MarqueeLabelPlacement(new EsRect(1, 2, 30, 20), 2, 2, "差し替え", 12, DrawingColor.White, DrawingColor.Black, IsVisible: true)],
        };

        control.Arrange(new Rect(0, 0, 200, 200));

        // 差し替えた 1 件だけがラベル要素になる
        var labels = control.Children.OfType<Border>().ToList();
        Assert.Single(labels);
        Assert.Equal("差し替え", ((TextBlock)labels[0].Child!).Text);
    }

    /// <summary>差し替えを設定すると保持中の計画が破棄され、次の解決で反映されることを検証する。</summary>
    [Fact]
    internal void 描画計画の差し替えは次の解決で反映される()
    {
        var cursor = new CursorServiceSpy();
        var control = CreateControl(cursor);
        control.SetSelection(new EsRect(20, 20, 60, 60));
        control.Arrange(new Rect(0, 0, 200, 200));

        var before = control.Children.OfType<Border>().Select(b => ((TextBlock)b.Child!).Text).ToList();

        control.DrawPlanResolver = static plan => plan with
        {
            Labels = [new MarqueeLabelPlacement(new EsRect(1, 2, 30, 20), 2, 2, "後から", 12, DrawingColor.White, DrawingColor.Black, IsVisible: true)],
        };
        control.Arrange(new Rect(0, 0, 200, 200));

        var after = control.Children.OfType<Border>().Select(b => ((TextBlock)b.Child!).Text).ToList();

        Assert.NotEqual(before, after);
        Assert.Contains("後から", after);
    }

    /// <summary>外観で装飾を止めると装飾の矩形が描かれないことを検証する。</summary>
    /// <remarks>
    /// 外観の <c>Show*</c> が false の項目は計画の値と層の両方から消えるため、描画実装へ別のスイッチを渡さない。
    /// </remarks>
    [Fact]
    internal void 外観で装飾を止めると装飾が描かれない()
    {
        using var bitmap = new SKBitmap(new SKImageInfo(64, 64));
        using var canvas = new SKCanvas(bitmap);

        var presenter = new MarqueePresenter(new StubEffects(), cursor: null, MarqueeOptions.Default);
        presenter.SetSelection(new EsRect(10, 10, 40, 40));
        var plan = presenter.ResolveDrawPlan(new EsSize(64, 64), static (_, _) => default);

        // 既定（装飾を描く）: 外枠の色の画素が現れる
        MarqueeSkiaPainter.Paint(canvas, in plan);
        Assert.Contains(plan.OuterBorderDipRects, rect => bitmap.GetPixel((int)rect.X, (int)rect.Y).Alpha > 0);

        // 外観で止める: 層と値は残るが、描画実装はフラグを見て描かない（何も描かれない）
        presenter.Appearance = MarqueeAppearance.Default with
        {
            ShowOverlay = false,
            ShowOuterBorder = false,
            ShowHandles = false,
            ShowMarker = false,
        };
        var stopped = presenter.ResolveDrawPlan(new EsSize(64, 64), static (_, _) => default);

        canvas.Clear(SKColors.Transparent);
        MarqueeSkiaPainter.Paint(canvas, in stopped);
        Assert.Contains(MarqueeDrawLayer.Decoration, stopped.Layers);
        Assert.Contains(MarqueeDrawLayer.Handles, stopped.Layers);
        Assert.Equal(0, bitmap.GetPixel(10, 10).Alpha);
        Assert.Equal(0, bitmap.GetPixel(32, 32).Alpha);
    }

    /// <summary>レイヤーを差し替えると既定の描画を止められることを検証する。</summary>
    /// <remarks>
    /// ホストが描画を全面的に自前で行う場合の入口である（計画の値を保ったまま層だけを空にする）。<br/>
    /// 外観（<c>Show*</c>）を変えずに止められるため、止めた後も計算値を使って自前の描画ができる。
    /// </remarks>
    [Fact]
    internal void レイヤーを差し替えると既定の描画を止められる()
    {
        using var bitmap = new SKBitmap(new SKImageInfo(64, 64));
        using var canvas = new SKCanvas(bitmap);

        var presenter = new MarqueePresenter(new StubEffects(), cursor: null, MarqueeOptions.Default);
        presenter.SetSelection(new EsRect(10, 10, 40, 40));
        var plan = presenter.ResolveDrawPlan(new EsSize(64, 64), static (_, _) => default);

        // ホストが層を空に差し替える（既定の描画は何も描かない）
        var host = plan with { Layers = [] };
        MarqueeSkiaPainter.Paint(canvas, in host);

        Assert.Empty(host.Layers);
        // 値は残る（ホストは計算値を使って自前で描ける）
        Assert.NotEmpty(host.OverlayDipRects);
        Assert.NotNull(host.Handles);
        Assert.Equal(0, bitmap.GetPixel(10, 10).Alpha);
        Assert.Equal(0, bitmap.GetPixel(32, 32).Alpha);
    }

    /// <summary>テスト用の効果の最小実装（描画計画の解決だけに使う）。</summary>
    private sealed class StubEffects : IMarqueeEffects
    {
        /// <inheritdoc/>
        public void RequestFocus() { }

        /// <inheritdoc/>
        public void RequestRedraw() { }

        /// <inheritdoc/>
        public void SetCursorShape(MarqueeCursorKind kind) { }

        /// <inheritdoc/>
        public void SetPointerCapture(bool capture) { }

        /// <inheritdoc/>
        public EsPointInt ToScreenPoint(EsPoint sourcePoint) => default;
    }
}
