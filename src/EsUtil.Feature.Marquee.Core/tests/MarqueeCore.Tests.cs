using EsUtil.Platform.Core;
using EsUtil.Platform.Core.Native;
using EsUtil.Platform.Core.Coord;
using EsUtil.Platform.Core.Geometry;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using EsUtil.Feature.Marquee.Core.Contract;
using EsUtil.Feature.Marquee.Core.Interaction;

namespace EsUtil.Feature.Marquee.Tests;

/// <summary><see cref="MarqueeCore"/>（公開ファサード）の単体テスト（操作・効果の適用・設定の伝搬・描画内容の解決）。</summary>
/// <remarks>
/// UI 層とホストが使う唯一の窓口であるファサードの振る舞いを、UI フレームワークなしで検証する。<br/>
/// 入力の解釈そのもの（状態遷移の判断）は <see cref="MarqueeSession"/> のテストが検証するため、<br/>
/// 本テストは「効果の適用順」「設定の伝搬」「描画内容の委譲」「通知の転送」に焦点を当てる。
/// </remarks>
public class MarqueeCoreTests
{
    /// <summary>ファサードと効果のスパイを生成する。</summary>
    /// <param name="cursor">カーソルサービス。 null 可。</param>
    /// <param name="effects">生成した効果のスパイ。</param>
    /// <param name="options">初期設定。 null の場合は既定値。</param>
    /// <returns>ファサード。</returns>
    private static MarqueeCore CreateMarquee(ICursorService? cursor, out EffectsSpy effects, MarqueeOptions? options = null)
    {
        effects = new EffectsSpy();
        return new MarqueeCore(effects, cursor, options) { Log = NullLogger.Instance };
    }

    // ====================================================================
    // 生成
    // ====================================================================

    [Fact]
    public void 生成_効果がnullなら例外を投げる()
    {
        Assert.Throws<ArgumentNullException>(() => new MarqueeCore(null!));
    }

    [Fact]
    public void 生成_初期設定を保持する()
    {
        var options = MarqueeOptions.Default with { HandleSize = 20.0 };

        var marquee = CreateMarquee(null, out _, options);

        Assert.Same(options, marquee.Options);
    }

    /// <summary>効果を何もしない実装（確保量の計測に使う）。</summary>
    private sealed class NoOpEffects : MarqueeEffectsBase
    {
    }

    /// <summary>ポインター移動の処理がヒープを確保しないことを検証する。</summary>
    /// <remarks>
    /// ポインター移動は操作中に連続して発生するため、通知・スナップショットを値型にして確保を避ける<br/>
    /// （デリゲートの生成も行わない）。 計測は <see cref="GC.GetAllocatedBytesForCurrentThread"/> で行う。
    /// </remarks>
    [Fact]
    public void Move_ポインター移動はヒープを確保しない()
    {
        var marquee = new MarqueeCore(new NoOpEffects());
        marquee.SetSelection(new EsRect(10, 10, 100, 80));
        marquee.Press(new EsPoint(50, 50), 1);
        marquee.Move(new EsPoint(51, 51));

        for (var i = 0; i < 100; i++)
        {
            marquee.Move(new EsPoint(60 + i, 60));
        }

        GC.Collect();
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1000; i++)
        {
            marquee.Move(new EsPoint(60 + i, 60));
        }

        var perCall = (GC.GetAllocatedBytesForCurrentThread() - before) / 1000.0;
        Assert.True(perCall <= 16, $"ポインター移動 1 回の確保が {perCall} バイト（16 バイト以下を期待）");
    }

    [Fact]
    public void 生成_初期状態は未選択()
    {
        var marquee = CreateMarquee(null, out _);

        Assert.False(marquee.HasSelection);
        Assert.Equal(default, marquee.SelectionSourceRect);
        Assert.Equal(MarqueeHitTarget.None, marquee.CreateSnapshot().ActiveTarget);
    }

    // ====================================================================
    // 効果の適用（入力 → 効果）
    // ====================================================================

    [Fact]
    public void Press_フォーカスと再描画とキャプチャを要求する()
    {
        var marquee = CreateMarquee(null, out var effects);

        marquee.Press(new EsPoint(10, 10), clickCount: 1);

        // 適用順は「フォーカス → カーソル形状 → 再描画」で、キャプチャは最後に取得する
        Assert.Equal(["focus", "redraw", "capture:True"], effects.Calls);
    }

    [Fact]
    public void Release_ポインターキャプチャを解放する()
    {
        var marquee = CreateMarquee(null, out var effects);
        marquee.Press(new EsPoint(10, 10), clickCount: 1);
        effects.Calls.Clear();

        marquee.Release(new EsPoint(50, 40));

        Assert.Contains("capture:False", effects.Calls);
        Assert.Equal("capture:False", effects.Calls[^1]);
    }

    [Fact]
    public void Press_カーソルをハンドル点へ移動する()
    {
        var cursor = new CursorServiceSpy();
        var marquee = CreateMarquee(cursor, out _, MarqueeOptions.Default with { HandleHitRadius = 12.0 });
        marquee.SetSelection(new EsRect(50, 50, 100, 80));

        // 右下ハンドル（150, 130）付近を押下する → ハンドル点へスナップしてカーソルを移動する
        marquee.Press(new EsPoint(148, 128), clickCount: 1);

        // 画面物理座標は効果の座標変換（2 倍）で求まる
        Assert.Equal(new EsPointInt(300, 260), cursor.CursorPoint);
    }

    [Fact]
    public void Move_カーソル形状を更新する()
    {
        var marquee = CreateMarquee(null, out var effects);
        marquee.SetSelection(new EsRect(50, 50, 100, 80));
        effects.Calls.Clear();

        marquee.Move(new EsPoint(150, 90));

        // 右辺中央（MiddleRight）は左右リサイズの形状になる（種別の判断はコアが単一所有する）
        Assert.Contains("cursor:Horizontal", effects.Calls);
    }

    [Fact]
    public void Press_カーソル未指定でも効果を適用する()
    {
        var marquee = CreateMarquee(null, out var effects);
        marquee.SetSelection(new EsRect(50, 50, 100, 80));
        effects.Calls.Clear();

        // カーソルサービス未指定（null）でも例外にならず、カーソル移動以外の効果は適用される
        marquee.Press(new EsPoint(148, 128), clickCount: 1);

        Assert.Equal(["focus", "redraw", "capture:True"], effects.Calls);
    }

    // ====================================================================
    // キー入力
    // ====================================================================

    [Fact]
    public void KeyDown_Enterは確定を通知し処理済みを返す()
    {
        var marquee = CreateMarquee(null, out _);
        var notices = new List<MarqueeNotice>();
        marquee.Notified += (_, notice) => notices.Add(notice);
        marquee.SetSelection(new EsRect(10, 20, 100, 50));
        notices.Clear();

        var handled = marquee.KeyDown(EsKey.Enter, null);

        Assert.True(handled);
        Assert.Equal([MarqueeNoticeKind.ConfirmRequested], notices.Select(n => n.Kind));
        Assert.False(marquee.HasSelection);
    }

    [Fact]
    public void KeyDown_対象外のキーは処理しない()
    {
        var marquee = CreateMarquee(null, out var effects);

        var handled = marquee.KeyDown(EsKey.A, null);

        Assert.False(handled);
        Assert.Empty(effects.Calls);
    }

    [Fact]
    public void KeyDown_矢印キーはnudgeを開始し始点を移動する()
    {
        var marquee = CreateMarquee(null, out var effects);
        var notices = new List<MarqueeNotice>();
        marquee.Notified += (_, notice) => notices.Add(notice);

        var handled = marquee.KeyDown(EsKey.Right, new EsPoint(100, 100));

        // 未選択の nudge は始点を 1 対象ピクセル移動する（カーソルの非表示管理もコアが単一所有する）
        Assert.True(handled);
        Assert.Equal(["redraw"], effects.Calls);
        Assert.Equal(
            [MarqueeNoticeKind.SelectionChanged, MarqueeNoticeKind.MarkerPositionMoved],
            notices.Select(n => n.Kind));
        Assert.Equal(new EsPoint(101, 100), notices[^1].Position);
        Assert.False(marquee.HasSelection);
    }

    // ====================================================================
    // 設定の伝搬
    // ====================================================================

    [Fact]
    public void Options_境界外選択の禁止を既存の選択矩形へ即時適用する()
    {
        var marquee = CreateMarquee(null, out _, MarqueeOptions.Default with { ClampToSourceBounds = false });
        marquee.SetCoordTransform(new CoordTransform(p => p, p => p, () => new EsSize(100, 100)));
        marquee.SetSelection(new EsRect(50, 50, 200, 200));

        // 設定を ON へ切り替えると既存の選択矩形が境界内へクリップされる
        marquee.Options = marquee.Options with { ClampToSourceBounds = true };

        Assert.Equal(new EsRect(50, 50, 50, 50), marquee.SelectionSourceRect);
    }

    [Fact]
    public void Options_同値の再設定では再描画を要求しない()
    {
        var marquee = CreateMarquee(null, out var effects);
        effects.Calls.Clear();

        marquee.Options = marquee.Options;

        Assert.Empty(effects.Calls);
    }

    [Fact]
    public void Scaling_変更を保持し再描画は要求しない()
    {
        var marquee = CreateMarquee(null, out var effects);

        marquee.ClientToSourceScaling = 2.0;
        marquee.ClientToSourceScaling = 2.0;

        Assert.Equal(2.0, marquee.ClientToSourceScaling);
        // 設定の変更は UI の公開プロパティから渡されるため、描画の更新は UI が観測して行う
        // （コアが効果として再描画を要求するのは入力に伴う場合だけである）
        Assert.Equal(0, effects.RedrawCount);
    }

    // ====================================================================
    // 環境値（座標変換ポート）
    // ====================================================================

    [Fact]
    public void SetCoordTransform_未設定は等価変換になる()
    {
        var marquee = CreateMarquee(null, out _);

        marquee.SetCoordTransform(null);

        Assert.Equal(new EsPoint(3, 4), marquee.ToSource(new EsPoint(3, 4)));
        Assert.Equal(new EsPoint(3, 4), marquee.ToDip(new EsPoint(3, 4)));
    }

    [Fact]
    public void SetCoordTransform_設定したポートで変換する()
    {
        var marquee = CreateMarquee(null, out var effects);
        effects.Calls.Clear();

        // ToSource（DIP → 対象）は 1/2 倍、ToDip（対象 → DIP）は 2 倍
        marquee.SetCoordTransform(new CoordTransform(
            p => new EsPoint(p.X / 2, p.Y / 2), p => new EsPoint(p.X * 2, p.Y * 2), () => null));

        Assert.Equal(new EsPoint(5, 10), marquee.ToSource(new EsPoint(10, 20)));
        Assert.Equal(new EsPoint(20, 40), marquee.ToDip(new EsPoint(10, 20)));
        Assert.Contains("redraw", effects.Calls);
    }

    // ====================================================================
    // 通知の転送
    // ====================================================================

    [Fact]
    public void Notified_送信元はファサード自身になる()
    {
        var marquee = CreateMarquee(null, out _);
        object? sender = null;
        var notices = new List<MarqueeNotice>();
        marquee.Notified += (s, notice) => { sender = s; notices.Add(notice); };

        marquee.Press(new EsPoint(10, 10), clickCount: 1);

        Assert.Same(marquee, sender);
        Assert.Equal([MarqueeNoticeKind.SelectionStarted], notices.Select(n => n.Kind));
    }

    [Fact]
    public void Notified_購読者がいない場合は何も起きない()
    {
        var marquee = CreateMarquee(null, out _);
        marquee.SetSelection(new EsRect(10, 20, 100, 50));

        // 例外なく完了する（通知の発火は null 条件付き）
        marquee.RequestCancel();

        Assert.False(marquee.HasSelection);
    }

    // ====================================================================
    // 外部からの操作
    // ====================================================================

    [Fact]
    public void SetSelection_選択を設定し通知する()
    {
        var marquee = CreateMarquee(null, out _);
        var notices = new List<MarqueeNotice>();
        marquee.Notified += (_, notice) => notices.Add(notice);

        marquee.SetSelection(new EsRect(10, 20, 100, 50));

        Assert.True(marquee.HasSelection);
        Assert.Equal([MarqueeNoticeKind.SelectionChanged], notices.Select(n => n.Kind));
    }

    [Fact]
    public void ResetSelection_選択とnudgeを初期化する()
    {
        var marquee = CreateMarquee(null, out _);
        marquee.SetSelection(new EsRect(10, 20, 100, 50));

        marquee.ResetSelection();

        Assert.False(marquee.HasSelection);
    }

    [Fact]
    public void RequestConfirm_選択確定を通知する()
    {
        var marquee = CreateMarquee(null, out _);
        var notices = new List<MarqueeNotice>();
        marquee.Notified += (_, notice) => notices.Add(notice);
        marquee.SetSelection(new EsRect(10, 20, 100, 50));
        notices.Clear();

        marquee.RequestConfirm();

        Assert.Equal([MarqueeNoticeKind.ConfirmRequested], notices.Select(n => n.Kind));
        Assert.False(marquee.HasSelection);
    }

    [Fact]
    public void RequestCancel_選択を破棄して通知する()
    {
        var marquee = CreateMarquee(null, out _);
        var notices = new List<MarqueeNotice>();
        marquee.Notified += (_, notice) => notices.Add(notice);
        marquee.SetSelection(new EsRect(10, 20, 100, 50));
        notices.Clear();

        marquee.RequestCancel();

        Assert.Equal([MarqueeNoticeKind.CancelRequested], notices.Select(n => n.Kind));
        Assert.False(marquee.HasSelection);
    }

    [Fact]

    public void EndNudge_nudge中でなければ何もしない()
    {
        var marquee = CreateMarquee(null, out var effects);

        marquee.EndNudge();

        Assert.Empty(effects.Calls);
    }

    [Fact]
    public void EndNudge_nudge中はカーソルを再表示する()
    {
        var cursor = new CursorServiceSpy();
        var marquee = CreateMarquee(cursor, out _);
        marquee.KeyDown(EsKey.Right, new EsPoint(100, 100));

        marquee.EndNudge();

        // nudge の開始で非表示にし、終了で再表示する（最初の呼び出しが非表示、最後が再表示）
        Assert.False(cursor.ShowCalls[0]);
        Assert.True(cursor.ShowCalls[^1]);
    }

    [Fact]
    public void Log_設定したロガーを保持する()
    {
        var marquee = CreateMarquee(null, out _);
        var logger = new RecordingLogger();

        marquee.Log = logger;

        Assert.Same(logger, marquee.Log);
    }

    [Fact]
    public void Log_nullを設定するとNullLoggerへフォールバックする()
    {
        var marquee = CreateMarquee(null, out _);

        marquee.Log = null!;

        Assert.Same(NullLogger.Instance, marquee.Log);
    }

    /// <summary>生成の記録がロガーの設定時に一度だけ出力されることを検証する。</summary>
    /// <remarks>
    /// 生成の記録はロガーが最初に設定された時点で 1 度だけ行う（生成時は出力先が未設定のため、その場では記録できない）。
    /// </remarks>
    [Fact]
    public void Log_生成の記録はロガーの設定時に一度だけ出力する()
    {
        var effects = new EffectsSpy();
        var marquee = new MarqueeCore(effects, cursor: null, new MarqueeOptions { HandleSize = 14.0, MinSelectionSize = 9.0 });
        var logger = new RecordingLogger();

        marquee.Log = logger;
        marquee.Log = logger;   // 再設定（生成の記録は繰り返さない）

        Assert.Equal(
            [
                "[Marquee] Facade created: HandleSize=14, MinSelectionSize=9",
                "[Marquee] Logger attached",
                "[Marquee] Logger attached",
            ],
            logger.Messages);
    }

    /// <summary>コンストラクターでロガーを注入すると、その場で生成の記録が出力されることを検証する。</summary>
    /// <remarks>
    /// 注入時は出力先が確定しているため、コンストラクター呼び出しの記録（生成時の設定）をその場で出力する。<br/>
    /// コンストラクター由来の注入は 1 度だけであり、後からの再設定と同様に生成の記録は繰り返さない。
    /// </remarks>
    [Fact]
    public void 生成_コンストラクターでロガーを注入すると生成を記録する()
    {
        var effects = new EffectsSpy();
        var logger = new RecordingLogger();

        var marquee = new MarqueeCore(effects, cursor: null, new MarqueeOptions { HandleSize = 14.0 }, logger);

        Assert.Equal(
            [
                "[Marquee] Facade created: HandleSize=14, MinSelectionSize=6",
                "[Marquee] Logger attached",
            ],
            logger.Messages);

        // 注入後に同じロガーを設定し直しても生成の記録は繰り返さない
        marquee.Log = logger;

        Assert.Equal(3, logger.Messages.Count);
    }

    /// <summary>環境値（倍率）の変更が変化したときだけ記録されることを検証する。</summary>
    [Fact]
    public void ClientToSourceScaling_変更したときだけ記録する()
    {
        var marquee = CreateMarquee(null, out _);
        var logger = new RecordingLogger();
        marquee.Log = logger;
        logger.Messages.Clear();
        logger.Levels.Clear();

        marquee.ClientToSourceScaling = 1.25;
        marquee.ClientToSourceScaling = 1.25;   // 同値
        marquee.ClientToSourceScaling = 2.0;

        Assert.Equal(
            [
                "[Marquee] Setting: ClientToSourceScaling 1 → 1.25",
                "[Marquee] Setting: ClientToSourceScaling 1.25 → 2",
            ],
            logger.Messages);
        // 低頻度の設定変更は Debug で記録する
        Assert.All(logger.Levels, level => Assert.Equal(LogLevel.Debug, level));
    }

    /// <summary>ハイライト矩形の変更が最も低いレベル（Trace）で記録されることを検証する。</summary>
    /// <remarks>矩形はホバーのたびに変わりうる（最も頻度が高い）ため、既定の出力では溢れさせない。</remarks>
    [Fact]
    public void HighlightRect_変更を最も低いレベルで記録する()
    {
        var marquee = CreateMarquee(null, out _);
        var logger = new RecordingLogger();
        marquee.Log = logger;
        logger.Messages.Clear();
        logger.Levels.Clear();

        marquee.HighlightSourceRect = new EsRect(1, 2, 3, 4);
        marquee.HighlightSourceRect = new EsRect(1, 2, 3, 4);   // 同値
        marquee.HighlightSourceRect = new EsRect(5, 6, 7, 8);
        marquee.HighlightSourceRect = null;

        Assert.Equal(
            [
                "[Marquee] Setting: HighlightSourceRect null → (1, 2, 3, 4)",
                "[Marquee] Setting: HighlightSourceRect (1, 2, 3, 4) → (5, 6, 7, 8)",
                "[Marquee] Setting: HighlightSourceRect (5, 6, 7, 8) → null",
            ],
            logger.Messages);
        Assert.All(logger.Levels, level => Assert.Equal(LogLevel.Trace, level));
    }

    /// <summary>座標変換ポートの設定が低頻度のメソッド呼び出しとして記録されることを検証する。</summary>
    [Fact]
    public void SetCoordTransform_設定を記録する()
    {
        var marquee = CreateMarquee(null, out _);
        var logger = new RecordingLogger();
        marquee.Log = logger;
        logger.Messages.Clear();
        logger.Levels.Clear();

        marquee.SetCoordTransform(CoordTransform.Identity);
        marquee.SetCoordTransform(CoordTransform.Identity);   // 同一インスタンス（同値扱い）
        marquee.SetCoordTransform(null);

        // ポートは値として比較・整形できないため、設定の有無と実装型名で記録する
        Assert.Equal(2, logger.Messages.Count);
        Assert.StartsWith("[Marquee] Setting: CoordTransform null → ", logger.Messages[0], StringComparison.Ordinal);
        Assert.Equal("[Marquee] Setting: CoordTransform CoordTransform → null", logger.Messages[1]);
        Assert.All(logger.Levels, level => Assert.Equal(LogLevel.Debug, level));
    }
}
