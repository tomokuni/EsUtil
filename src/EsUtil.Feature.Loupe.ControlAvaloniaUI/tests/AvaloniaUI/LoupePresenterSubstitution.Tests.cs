using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using EsUtil.Platform.Core.Geometry;
using EsUtil.SaveEvidence.Platform.AvaloniaUI.Imaging;
using Xunit;

namespace EsUtil.Feature.Loupe.AvaloniaUI.Tests;

/// <summary>派生提示層の差し替え（<see cref="LoupeControl.CreatePresenter"/> の上書き）のテスト。</summary>
/// <remarks>
/// 差し替えの契約は 2 つある:<br/>
/// 1. 生成は初回参照時に行われ、<b>派生クラスの初期化が終わってから</b>上書きが呼ばれる（コンストラクターからは呼ばない）。<br/>
/// 2. 上書きが返した提示層の描画計画が実際の描画に使われる。<br/>
/// Avalonia ヘッドレステストは並列実行非対応のため、同じコレクション（<c>AvaloniaHeadless</c>）内で直列実行する。
/// </remarks>
[Collection("AvaloniaHeadless")]
public class LoupePresenterSubstitutionTests
{
    /// <summary>上書きが呼ばれた時点の派生クラスの初期化状況（コンストラクターで設定する）。</summary>
    private static string? s_derivedStateAtCreation;

    /// <summary>派生クラスの初期化で設定するマーカー（上書きが初期化後に呼ばれたことの検証に使う）。</summary>
    private static string? s_derivedState;

    /// <summary>十字線の矩形を差し替える派生提示層。</summary>
    private sealed class SubstitutingPresenter : LoupePresenter
    {
        /// <summary>差し替え後の十字線（1 本だけ）。</summary>
        public static readonly EsRect[] Sentinel = [new(3, 4, 5, 6)];

        /// <summary>提示層を初期化する。</summary>
        public SubstitutingPresenter() => s_derivedStateAtCreation = s_derivedState;

        /// <inheritdoc />
        public override LoupeDrawPlan ResolveDrawPlan(double clientSize, Func<string, double, EsSize> measureText)
            => base.ResolveDrawPlan(clientSize, measureText) with { CrosshairPixelRects = Sentinel };
    }

    /// <summary>派生提示層を使う派生コントロール。</summary>
    /// <remarks>
    /// マーカーは<b>プロパティ初期化子</b>で設定する（基本コンストラクターより後に走るため、<br/>
    /// 生成が基本コンストラクターで行われていれば上書きは未設定のマーカーを見ることになる）。
    /// </remarks>
    private sealed class DerivedControl : LoupeControl
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

        /// <summary>見本の設定をして表示する（プロパティ設定は提示層の生成を起動する）。</summary>
        public DerivedControl()
        {
            Options = LoupeOptions.Default with { RequestedSize = 100, ZoomLevel = 2 };
            IsVisible = true;
        }

        /// <inheritdoc />
        protected override LoupePresenter CreatePresenter() => new SubstitutingPresenter();
    }

    /// <summary>上書きは派生クラスの初期化が終わってから呼ばれることを検証する。</summary>
    /// <remarks>
    /// 生成をコンストラクターで行うと派生の初期化前に上書きが走るため（アナライザー MA0056 が禁じる）、<br/>
    /// 初回参照時まで遅延している。 本テストは「生成が基本コンストラクターで行われない」契約を固定する
    /// （行われていればマーカーが未設定のまま上書きが呼ばれる）。
    /// </remarks>
    [AvaloniaFact]
    internal void 派生提示層の生成は派生クラスの初期化後に行われる()
    {
        s_derivedState = null;
        s_derivedStateAtCreation = null;

        // コンストラクターのプロパティ設定が提示層の生成を起動する
        var control = new DerivedControl();

        Assert.True(control.IsDerivedInitialized, "派生クラスの初期化が完了していない（テストの前提が成立していない）");
        Assert.Equal("initialized", s_derivedStateAtCreation);
    }

    /// <summary>上書きが返した提示層の描画計画が描画に使われることを検証する。</summary>
    [AvaloniaFact]
    internal async Task 派生提示層の描画計画が描画に使われる()
    {
        var control = new DerivedControl();
        var panel = new Panel { Width = 200, Height = 200 };
        panel.Children.Add(control);
        panel.Measure(new Avalonia.Size(200, 200));
        panel.Arrange(new Avalonia.Rect(0, 0, 200, 200));

        LoupeDrawPlan? captured = null;
        control.Rendered += (_, e) => captured = e.Content;

        control.Source = EsBitmapAvaloniaExtensions.CreateAvaloniaWriteableBitmap(200, 200, Avalonia.Media.Colors.Blue);
        control.CenterPoint = new Avalonia.Point(100, 100);
        await control.WaitForRenderedAsync();

        Assert.NotNull(captured);
        // 差し替えた十字線（1 本）がそのまま使われる（既定の 2 本ではない）
        Assert.Equal(SubstitutingPresenter.Sentinel, captured!.Value.CrosshairPixelRects);
    }
}
