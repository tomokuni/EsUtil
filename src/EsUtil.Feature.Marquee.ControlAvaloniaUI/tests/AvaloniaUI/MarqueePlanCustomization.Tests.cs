using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using EsUtil.Platform.Core.Geometry;
using Xunit;
using AvaloniaRect = Avalonia.Rect;

namespace EsUtil.Feature.Marquee.AvaloniaUI.Tests;

/// <summary>見た目のカスタマイズ（描画計画の差し替えと外観による表示可否）のテスト。</summary>
/// <remarks>
/// 差し替えは <see cref="MarqueeControl.DrawPlanResolver"/>、装飾の停止は外観（<see cref="MarqueeAppearance"/> の <c>Show*</c>）で行う<br/>
/// （提示層の派生は不要）。 Avalonia ヘッドレステストは並列実行非対応のため、同じコレクション内で直列実行する。
/// </remarks>
[Collection("AvaloniaHeadless")]
public class MarqueePlanCustomizationTests
{
    /// <summary>描画計画の差し替えがラベル要素（配置）と描画の両方へ効くことを検証する。</summary>
    [AvaloniaFact]
    internal void 描画計画の差し替えがラベル要素へ反映される()
    {
        var control = new MarqueeControl
        {
            Width = 200,
            Height = 200,
            DrawPlanResolver = static plan => plan with
            {
                Labels = [new MarqueeLabelPlacement(new EsRect(1, 2, 30, 20), 2, 2, "差し替え", 12, System.Drawing.Color.White, System.Drawing.Color.Black)],
            },
        };
        var window = new Window { Width = 200, Height = 200, Content = control };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        control.SetSelection(new AvaloniaRect(20, 20, 60, 60));
        Dispatcher.UIThread.RunJobs();
        window.CaptureRenderedFrame();
        Dispatcher.UIThread.RunJobs();

        // 差し替えた 1 件だけがラベル要素になる
        var labels = control.Children.OfType<Border>().ToList();
        Assert.Single(labels);
        Assert.Equal("差し替え", ((TextBlock)labels[0].Child!).Text);
        window.Close();
    }

    /// <summary>外観で装飾を止めても描画が成立し、包囲の層が計画から消えることを検証する。</summary>
    /// <remarks>
    /// ヘッドレスでは画素を検証できないため、描画完了通知が渡す計画で確認する（画素の検証は MewUI が担う）。<br/>
    /// 外観の <c>Show*</c> が false の項目は、計画の値と層の両方から消える（描画実装は可否を判断しない）。
    /// </remarks>
    [AvaloniaFact]
    internal void 外観で装飾を止めると層から消えて描画が成立する()
    {
        var control = new MarqueeControl
        {
            Width = 200,
            Height = 200,
            Appearance = MarqueeAppearance.Default with
            {
                ShowOverlay = false,
                ShowOuterBorder = false,
                ShowHandles = false,
                ShowMarker = false,
            },
        };
        var window = new Window { Width = 200, Height = 200, Content = control };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        MarqueeDrawPlan? captured = null;
        control.Rendered += (_, e) => captured = e.Content;

        control.SetSelection(new AvaloniaRect(20, 20, 60, 60));
        Dispatcher.UIThread.RunJobs();
        window.CaptureRenderedFrame();
        Dispatcher.UIThread.RunJobs();

        // 計画は解決され、止めた装飾は層にも値にも現れない（ラベルは要素が表示するため層に残る）
        Assert.NotNull(captured);
        Assert.DoesNotContain(MarqueeDrawLayer.Decoration, captured!.Value.Layers);
        Assert.DoesNotContain(MarqueeDrawLayer.Handles, captured.Value.Layers);
        Assert.DoesNotContain(MarqueeDrawLayer.Marker, captured.Value.Layers);
        Assert.Empty(captured.Value.OverlayDipRects);
        Assert.Null(captured.Value.Handles);
        window.Close();
    }

    /// <summary>差し替えと外観による停止が描画パスで併用できることを検証する。</summary>
    [AvaloniaFact]
    internal void 差し替えと外観の停止を併用できる()
    {
        var resolverCalls = 0;
        var control = new MarqueeControl
        {
            Width = 200,
            Height = 200,
            Appearance = MarqueeAppearance.Default with { ShowOverlay = false, ShowOuterBorder = false },
            DrawPlanResolver = plan =>
            {
                resolverCalls++;
                return plan with { OuterBorderDipRects = [new EsRect(1, 2, 3, 4)] };
            },
        };
        var window = new Window { Width = 200, Height = 200, Content = control };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        control.SetSelection(new AvaloniaRect(20, 20, 60, 60));
        Dispatcher.UIThread.RunJobs();
        window.CaptureRenderedFrame();
        Dispatcher.UIThread.RunJobs();

        // 解決は配置と描画の両方から呼ばれる（同じ入力なら 1 フレーム 1 回。 純粋関数である前提）
        Assert.True(resolverCalls > 0);
        window.Close();
    }
}
