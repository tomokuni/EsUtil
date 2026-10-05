using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Native;
using Xunit;
using Rect = Avalonia.Rect;

namespace EsUtil.Feature.Marquee.AvaloniaUI.Tests;

/// <summary>派生提示層の差し替え（<see cref="MarqueeControl.CreatePresenter"/> の上書き）のテスト。</summary>
/// <remarks>
/// 差し替えの契約は 2 つある:<br/>
/// 1. 生成は初回参照時に行われ、<b>派生クラスの初期化が終わってから</b>上書きが呼ばれる（コンストラクターからは呼ばない）。<br/>
/// 2. 上書きが返した提示層の描画計画が実際の描画に使われる。<br/>
/// Avalonia ヘッドレステストは並列実行非対応のため、同じコレクション（<c>AvaloniaHeadless</c>）内で直列実行する。
/// </remarks>
[Collection("AvaloniaHeadless")]
public class MarqueePresenterSubstitutionTests
{
    /// <summary>上書きが呼ばれた時点の派生クラスの初期化状況（コンストラクターで設定する）。</summary>
    private static string? s_derivedStateAtCreation;

    /// <summary>外枠の矩形を差し替える派生提示層。</summary>
    private sealed class SubstitutingPresenter : MarqueePresenter
    {
        /// <summary>差し替え後の外枠（1 本だけ）。</summary>
        public static readonly EsRect[] Sentinel = [new(1, 2, 3, 4)];

        /// <summary>提示層を初期化する。</summary>
        /// <param name="effects">効果の適用先。</param>
        /// <param name="cursor">カーソルサービス。</param>
        /// <param name="options">初期設定。</param>
        public SubstitutingPresenter(IMarqueeEffects effects, ICursorService? cursor, MarqueeOptions? options)
            : base(effects, cursor, options)
        {
            s_derivedStateAtCreation = s_derivedState;
        }

        /// <inheritdoc />
        public override MarqueeDrawPlan ResolveDrawPlan(EsSize clientSize, Func<string, double, EsSize> measureText)
            => base.ResolveDrawPlan(clientSize, measureText) with { OuterBorderDipRects = Sentinel };
    }

    /// <summary>派生クラスの初期化で設定するマーカー（上書きが初期化後に呼ばれたことの検証に使う）。</summary>
    private static string? s_derivedState;

    /// <summary>派生提示層を使う派生コントロール。</summary>
    /// <remarks>
    /// マーカーは<b>プロパティ初期化子</b>で設定する（基本コンストラクターより後に走るため、<br/>
    /// 生成が基本コンストラクターで行われていれば上書きは未設定のマーカーを見ることになる）。
    /// </remarks>
    private sealed class DerivedControl : MarqueeControl
    {
        /// <summary>派生クラスの初期化が完了したかどうかを取得する（マーカーを設定する）。</summary>
        /// <value>派生クラスの初期化子が実行された場合は true。</value>
        public bool IsDerivedInitialized { get; } = SetMarker();

        /// <summary>派生クラスの初期化（プロパティ初期化子）でマーカーを設定する。</summary>
        /// <returns>true（初期化済みであることを表す）。</returns>
        private static bool SetMarker()
        {
            s_derivedState = "initialized";
            return true;
        }

        /// <inheritdoc />
        protected override MarqueePresenter CreatePresenter(
            IMarqueeEffects effects, ICursorService cursor, MarqueeOptions options)
            => new SubstitutingPresenter(effects, cursor, options);
    }

    /// <summary>上書きは派生クラスの初期化が終わってから呼ばれることを検証する。</summary>
    /// <remarks>
    /// 生成をコンストラクターで行うと派生の初期化前に上書きが走るため（アナライザー MA0056 が禁じる）、<br/>
    /// 初回参照時まで遅延している。 本テストはその契約を固定する。
    /// </remarks>
    [AvaloniaFact]
    internal void 派生提示層の生成は派生クラスの初期化後に行われる()
    {
        s_derivedState = null;
        s_derivedStateAtCreation = null;

        var control = new DerivedControl();
        Assert.True(control.IsDerivedInitialized, "派生クラスの初期化が完了していない（テストの前提が成立していない）");

        // 生成前は何も作られていない（遅延）
        Assert.Null(s_derivedStateAtCreation);

        // 提示層に触れる（HasSelection は提示層へ委譲する）
        _ = control.HasSelection;

        Assert.Equal("initialized", s_derivedStateAtCreation);
    }

    /// <summary>上書きが返した提示層の描画計画が描画に使われることを検証する。</summary>
    [AvaloniaFact]
    internal void 派生提示層の描画計画が描画に使われる()
    {
        var control = new DerivedControl { Width = 200, Height = 200 };
        var window = new Window { Width = 200, Height = 200, Content = control };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        MarqueeDrawPlan? captured = null;
        control.Rendered += (_, e) => captured = e.Content;

        control.SetSelection(new Rect(20, 20, 60, 60));
        Dispatcher.UIThread.RunJobs();
        window.CaptureRenderedFrame();
        Dispatcher.UIThread.RunJobs();

        Assert.NotNull(captured);
        // 差し替えた外枠（1 本）がそのまま使われる（既定の 4 辺ではない）
        Assert.Equal(SubstitutingPresenter.Sentinel, captured!.Value.OuterBorderDipRects);
        window.Close();
    }
}
