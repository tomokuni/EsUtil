using EsUtil.Platform.Core.Coord;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Native;
using Microsoft.Extensions.Logging;
using Xunit;

namespace EsUtil.Feature.Marquee.WinForms.Tests;

/// <summary><see cref="MarqueeControl"/> の公開 API と通知のテスト。</summary>
/// <remarks>
/// <b>ハンドルを作らない範囲だけを検証する</b>: メッセージ ループを持たないため、<br/>
/// ウィンドウ ハンドルを要求する操作（<c>CreateControl</c>・実際の描画・マウス入力・フォーカス）は対象外である。<br/>
/// 入力の解釈と状態遷移は UI 非依存のコアが単一所有するため、本コントロールの検証は<br/>
/// 「公開設定の転送」「通知のイベントへの転送」「ハンドルが無くても安全に呼べること」に絞る。<br/>
/// 接頭辞はプロセス全体の状態であるため、コレクションで直列化する（ログ規約 §7）。
/// </remarks>
[Collection(MarqueeLogPrefixDefinition.Name)]
public sealed class MarqueeControlTests
{
    // ====================================================================
    // 生成と既定値
    // ====================================================================

    /// <summary>既定の状態で生成されることを検証する。</summary>
    [Fact]
    internal void 生成_既定の状態になる()
    {
        using var control = new MarqueeControl();

        Assert.False(control.HasSelection);
        Assert.Equal(new EsRect(0, 0, 0, 0), control.SelectionSourceRect);
        Assert.False(control.CurrentSnapshot.IsMouseDown);
        Assert.False(control.CurrentSnapshot.HasSelection);
        Assert.False(control.CurrentSnapshot.IsNudgeActive);

        Assert.Equal(MarqueeOptions.Default.HandleSize, control.Options.HandleSize, 6);
        Assert.Equal(MarqueeAppearance.Default.ShowHandles, control.Appearance.ShowHandles);
        Assert.Null(control.HighlightSourceRect);
        Assert.Equal(1.0, control.ClientToSourceScaling, 6);
        Assert.Equal(1.0, control.ClientToPixelScaling, 6);
        Assert.Equal(1.0, control.DisplayScaleScaling, 6);
        Assert.Null(control.LogPrefix);
        Assert.Null(control.DrawPlanResolver);
        Assert.Null(control.SizeLabelOverrideProvider);
        Assert.Null(control.SizeLabelDisplayScalingProvider);
    }

    /// <summary>下の画像を透かすため背景が透明であることを検証する。</summary>
    /// <remarks>透明の背景は <see cref="ControlStyles.SupportsTransparentBackColor"/> を伴わないと設定できない。</remarks>
    [Fact]
    internal void 生成_背景は透明である()
    {
        using var control = new MarqueeControl();

        Assert.Equal(Color.Transparent.ToArgb(), control.BackColor.ToArgb());
    }

    /// <summary>カーソルサービスを注入して生成できることを検証する。</summary>
    /// <remarks>注入は nudge のカーソル制御を差し替えるための入口である（既定は Windows 既定実装）。</remarks>
    [Fact]
    internal void 生成_カーソルサービスを注入できる()
    {
        var cursorService = new CursorServiceSpy();
        using var control = new MarqueeControl(cursorService);

        Assert.False(control.HasSelection);
        Assert.False(control.TryGetCurrentLoupePosition(out _));
    }

    // ====================================================================
    // 公開設定の転送
    // ====================================================================

    /// <summary>設定（<see cref="MarqueeControl.Options"/>）が保持され、ファサードへ伝搬することを検証する。</summary>
    [Fact]
    internal void 設定_オプションを設定できる()
    {
        using var control = new MarqueeControl();

        control.Options = MarqueeOptions.Default with { HandleSize = 20.0, MinSelectionSize = 12.0 };

        Assert.Equal(20.0, control.Options.HandleSize, 6);
        Assert.Equal(12.0, control.Options.MinSelectionSize, 6);
    }

    /// <summary>設定（<see cref="MarqueeOptions"/>）の安全域外の値が補正されることを検証する。</summary>
    /// <remarks>補正の規則はコア（<c>MarqueeCore.Limits</c>）が単一所有する（本層は値を渡すだけである）。</remarks>
    [Fact]
    internal void 設定_オプションの範囲外は補正される()
    {
        using var control = new MarqueeControl();

        control.Options = MarqueeOptions.Default with { HandleSize = 1000.0 };

        Assert.Equal(256.0, control.Options.HandleSize, 6);
    }

    /// <summary>外観（<see cref="MarqueeControl.Appearance"/>）が保持されることを検証する。</summary>
    [Fact]
    internal void 設定_外観を設定できる()
    {
        using var control = new MarqueeControl();

        control.Appearance = MarqueeAppearance.Default with { ShowHandles = false, ShowCoordLabel = false };

        Assert.False(control.Appearance.ShowHandles);
        Assert.False(control.Appearance.ShowCoordLabel);
    }

    /// <summary>ハイライト矩形の設定と解除ができることを検証する。</summary>
    [Fact]
    internal void 設定_ハイライト矩形を設定して解除できる()
    {
        using var control = new MarqueeControl();

        control.HighlightSourceRect = new EsRect(10, 20, 30, 40);
        Assert.Equal(new EsRect(10, 20, 30, 40), control.HighlightSourceRect);

        control.HighlightSourceRect = null;
        Assert.Null(control.HighlightSourceRect);
    }

    /// <summary>倍率の設定がコアの安全域（0.1〜8.0）へ補正されることを検証する。</summary>
    /// <param name="value">設定する値。</param>
    /// <param name="expected">期待する値。</param>
    [Theory]
    [InlineData(2.0, 2.0)]
    [InlineData(0.1, 0.1)]
    [InlineData(100.0, 8.0)]
    [InlineData(0.0, 0.1)]
    [InlineData(-5.0, 0.1)]
    internal void 設定_倍率は安全域へ補正される(double value, double expected)
    {
        using var control = new MarqueeControl();

        control.ClientToSourceScaling = value;

        Assert.Equal(expected, control.ClientToSourceScaling, 6);
    }

    /// <summary>表示スケーリング係数と表示スケーリングのプロバイダが保持されることを検証する。</summary>
    [Fact]
    internal void 設定_表示スケーリングを設定できる()
    {
        using var control = new MarqueeControl();
        Func<EsRect, double> provider = _ => 1.5;

        control.DisplayScaleScaling = 1.25;
        control.SizeLabelDisplayScalingProvider = provider;

        Assert.Equal(1.25, control.DisplayScaleScaling, 6);
        Assert.Same(provider, control.SizeLabelDisplayScalingProvider);
    }

    /// <summary>サイズラベルの上書きプロバイダが保持されることを検証する。</summary>
    [Fact]
    internal void 設定_サイズラベルの上書きを設定できる()
    {
        using var control = new MarqueeControl();
        Func<string?> provider = () => "800 x 600";

        control.SizeLabelOverrideProvider = provider;

        Assert.Same(provider, control.SizeLabelOverrideProvider);
    }

    /// <summary>描画計画の差し替え関数が保持されることを検証する。</summary>
    [Fact]
    internal void 設定_描画計画の差し替えを設定できる()
    {
        using var control = new MarqueeControl();
        Func<MarqueeDrawPlan, MarqueeDrawPlan> resolver = plan => plan;

        control.DrawPlanResolver = resolver;

        Assert.Same(resolver, control.DrawPlanResolver);
    }

    /// <summary>表示スケーリング係数（物理ピクセルへの換算）が保持されることを検証する。</summary>
    [Fact]
    internal void 設定_表示ピクセル倍率を設定できる()
    {
        using var control = new MarqueeControl();

        control.ClientToPixelScaling = 1.5;

        Assert.Equal(1.5, control.ClientToPixelScaling, 6);
    }

    // ====================================================================
    // 選択の操作
    // ====================================================================

    /// <summary>選択矩形を設定して初期化できることを検証する。</summary>
    [Fact]
    internal void 選択_設定と初期化ができる()
    {
        using var control = new MarqueeControl();

        control.SetSelection(new EsRect(10, 20, 30, 40));
        Assert.True(control.HasSelection);
        Assert.Equal(new EsRect(10, 20, 30, 40), control.SelectionSourceRect);

        control.ResetSelection();
        Assert.False(control.HasSelection);
        Assert.Equal(new EsRect(0, 0, 0, 0), control.SelectionSourceRect);
    }

    /// <summary>座標変換ポートを設定して解除できることを検証する。</summary>
    /// <remarks>
    /// 変換の適用（入力の座標系）はドラッグ操作でしか観測できないため、ここでは設定の経路が例外なく通ることと、<br/>
    /// 解除しても選択状態が壊れないことを固定する。
    /// </remarks>
    [Fact]
    internal void 設定_座標変換ポートを設定して解除できる()
    {
        using var control = new MarqueeControl();

        control.SetCoordTransform(CoordTransform.ClientToSource(() => 2.0));
        control.SetCoordTransform(CoordTransform.Identity);
        control.SetCoordTransform(null);

        Assert.False(control.HasSelection);
    }

    /// <summary>境界を持つ座標変換ポートでは選択が境界内へクリップされることを検証する。</summary>
    /// <remarks>
    /// 境界（<see cref="ICoordTransform.SourceArea"/>）はポートが提供し、クリップの判断はコアが単一所有する<br/>
    /// （本コントロールはポートを保持して委譲するだけである）。
    /// </remarks>
    [Fact]
    internal void 選択_境界を持つポートでは選択がクリップされる()
    {
        using var control = new MarqueeControl();

        control.SetCoordTransform(CoordTransform.ClientToSource(() => 1.0, () => new EsSize(100, 100)));
        control.SetSelection(new EsRect(50, 50, 200, 200));

        var rect = control.SelectionSourceRect;

        Assert.True(rect.Width > 0);
        Assert.True(rect.Height > 0);
        Assert.True(rect.Right <= 100.0 + 1e-6);
        Assert.True(rect.Bottom <= 100.0 + 1e-6);
    }

    // ====================================================================
    // 通知の転送
    // ====================================================================

    /// <summary>選択矩形の変更がイベントへ転送されることを検証する。</summary>
    [Fact]
    internal void 通知_選択の変更を転送する()
    {
        using var control = new MarqueeControl();
        SelectionRectEventArgs? received = null;
        control.SelectionChanged += (_, e) => received = e;

        control.SetSelection(new EsRect(1, 2, 3, 4));

        Assert.NotNull(received);
        Assert.Equal(new EsRect(1, 2, 3, 4), received.Rect);
        Assert.True(received.Snapshot.HasSelection);
    }

    /// <summary>確定要求がイベントへ転送され、選択が消えることを検証する。</summary>
    [Fact]
    internal void 通知_確定要求を転送する()
    {
        using var control = new MarqueeControl();
        control.SetSelection(new EsRect(5, 6, 7, 8));
        SelectionRectEventArgs? confirmed = null;
        control.ConfirmRequested += (_, e) => confirmed = e;

        control.RequestConfirm();

        Assert.NotNull(confirmed);
        Assert.Equal(new EsRect(5, 6, 7, 8), confirmed.Rect);
        Assert.False(control.HasSelection);
    }

    /// <summary>選択が無い場合の確定要求では何も通知されないことを検証する。</summary>
    [Fact]
    internal void 通知_選択が無ければ確定要求を転送しない()
    {
        using var control = new MarqueeControl();
        var raised = false;
        control.ConfirmRequested += (_, _) => raised = true;

        control.RequestConfirm();

        Assert.False(raised);
    }

    /// <summary>キャンセル要求がイベントへ転送され、選択が破棄されることを検証する。</summary>
    [Fact]
    internal void 通知_キャンセル要求を転送する()
    {
        using var control = new MarqueeControl();
        control.SetSelection(new EsRect(5, 6, 7, 8));
        var cancelled = false;
        control.CancelRequested += (_, _) => cancelled = true;

        control.RequestCancel();

        Assert.True(cancelled);
        Assert.False(control.HasSelection);
    }

    /// <summary>購読していないイベントは発火しても例外にならないことを検証する。</summary>
    [Fact]
    internal void 通知_購読していなくても例外にならない()
    {
        using var control = new MarqueeControl();

        control.SetSelection(new EsRect(1, 2, 3, 4));
        control.RequestCancel();
        control.RequestConfirm();
    }

    // ====================================================================
    // 追従位置
    // ====================================================================

    /// <summary>ポインターがコントロールの外にある間は追従位置を取り直さないことを検証する。</summary>
    /// <remarks>
    /// 追従位置の取り直しは nudge とドラッグ以外で、かつポインターがコントロールの上にある場合だけ行う<br/>
    /// （ポインターの進入は実際のマウス入力でしか発生しないため、ハンドルを作らない本テストでは「取り直さない」側を固定する）。
    /// </remarks>
    [Fact]
    internal void 追従_ポインターが外にある間は取り直さない()
    {
        using var control = new MarqueeControl();

        Assert.False(control.TryGetCurrentLoupePosition(out var position));
        Assert.Equal(default, position);
    }

    /// <summary>ドラッグ中は追従位置を取り直さないことを検証する。</summary>
    [Fact]
    internal void 追従_ドラッグ中は取り直さない()
    {
        var cursorService = new CursorServiceSpy();
        using var control = new MarqueeControl(cursorService);

        control.SetSelection(new EsRect(1, 2, 3, 4));
        control.RequestConfirm();

        Assert.False(control.TryGetCurrentLoupePosition(out var position));
        Assert.Equal(default, position);
    }

    // ====================================================================
    // 描画
    // ====================================================================

    /// <summary>描画計画を描画面へ描けることを検証する（内部の描画実装の入口）。</summary>
    [Fact]
    internal void 描画_計画を描画面へ描ける()
    {
        var plan = TestHelper.CreatePlan(MarqueeAppearance.Default, new EsSize(20, 20), []);
        using var bitmap = TestHelper.CreateCanvas(20, 20);
        using var graphics = Graphics.FromImage(bitmap);

        MarqueeControl.DrawPlan(graphics, in plan);
    }

    /// <summary>描画面が null の場合に引数例外になることを検証する。</summary>
    [Fact]
    internal void 描画_描画面がnullならArgumentNullExceptionになる()
    {
        var plan = TestHelper.CreatePlan(MarqueeAppearance.Default, new EsSize(20, 20), []);

        Assert.Throws<ArgumentNullException>(() => MarqueeControl.DrawPlan(null!, in plan));
    }

    /// <summary>再描画の要求がハンドル未生成でも安全に呼べることを検証する。</summary>
    [Fact]
    internal void 描画_再描画要求はハンドルが無くても安全である()
    {
        using var control = new MarqueeControl();

        control.InvalidateRender();
    }

    // ====================================================================
    // ログ
    // ====================================================================

    /// <summary>ロガーを設定できることを検証する（未設定は出力なしへフォールバックする）。</summary>
    [Fact]
    internal void ログ_ロガーを設定して解除できる()
    {
        var logger = new RecordingLogger();
        using var control = new MarqueeControl();

        control.Log = logger;
        Assert.Same(logger, control.Log);

        control.Log = null!;
        Assert.NotNull(control.Log);
        Assert.False(control.Log.IsEnabled(LogLevel.Error));
    }

    /// <summary>接頭辞を設定すると定義クラスへ反映され、空白で既定値へ戻ることを検証する。</summary>
    [Fact]
    internal void ログ_接頭辞を設定して既定値へ戻せる()
    {
        try
        {
            using var control = new MarqueeControl();

            control.LogPrefix = "[Test]";
            Assert.Equal("[Test]", control.LogPrefix);
            Assert.Equal("[Test]", MarqueeLogMessage.Prefix);

            control.LogPrefix = "   ";
            Assert.Equal(MarqueeLogMessage.DefaultPrefix, MarqueeLogMessage.Prefix);
        }
        finally
        {
            MarqueeLogMessage.Prefix = MarqueeLogMessage.DefaultPrefix;
        }
    }

    /// <summary>カーソル位置を返すだけのテスト用サービス。</summary>
    /// <remarks>注入の経路を検証するために使う（実際のカーソルは動かさない）。</remarks>
    private sealed class CursorServiceSpy : ICursorService
    {
        /// <summary>取得するカーソル位置。</summary>
        public EsPointInt ScreenPoint { get; set; } = new(100, 200);

        /// <inheritdoc />
        public bool TryGetCursorScreenPoint(out EsPointInt point)
        {
            point = ScreenPoint;
            return true;
        }

        /// <inheritdoc />
        public bool TrySetCursorScreenPoint(EsPointInt point)
        {
            ScreenPoint = point;
            return true;
        }

        /// <inheritdoc />
        public void ShowCursor(bool show)
        {
            // テストではカーソルを操作しない（表示状態を保持する必要がない）
        }
    }
}
