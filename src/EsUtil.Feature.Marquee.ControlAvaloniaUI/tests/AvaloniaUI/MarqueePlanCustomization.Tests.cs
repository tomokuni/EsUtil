using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using EsUtil.Platform.Core.Geometry;
using Xunit;
using AvaloniaRect = Avalonia.Rect;

namespace EsUtil.Feature.Marquee.AvaloniaUI.Tests;

/// <summary>見た目のカスタマイズ（描画計画の差し替えと既定描画の可否）のテスト。</summary>
/// <remarks>
/// 差し替えは <see cref="MarqueeControl.DrawPlanResolver"/> と <see cref="MarqueeControl.ShowDefaultDecorations"/> で行う<br/>
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

    /// <summary>既定の装飾を描かない設定が描画計画へ反映されることを検証する。</summary>
    /// <remarks>
    /// ヘッドレスでは画素を検証できないため、描画完了通知が渡す計画（層の一覧）は変わらず、<br/>
    /// 装飾の有無は描画実装へ渡すフラグで決まることを契約として固定する（フラグ自体の画素検証は MewUI が担う）。
    /// </remarks>
    [AvaloniaFact]
    internal void 既定の装飾を描かない設定でも描画計画は解決される()
    {
        var control = new MarqueeControl { Width = 200, Height = 200, ShowDefaultDecorations = false };
        var window = new Window { Width = 200, Height = 200, Content = control };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        MarqueeDrawPlan? captured = null;
        control.Rendered += (_, e) => captured = e.Content;

        control.SetSelection(new AvaloniaRect(20, 20, 60, 60));
        Dispatcher.UIThread.RunJobs();
        window.CaptureRenderedFrame();
        Dispatcher.UIThread.RunJobs();

        // 計画は解決される（装飾を描かないのは描画実装のフラグによる）
        Assert.NotNull(captured);
        Assert.False(control.ShowDefaultDecorations);
        window.Close();
    }

    /// <summary>差し替えと既定描画の停止が描画パスで併用できることを検証する。</summary>
    [AvaloniaFact]
    internal void 差し替えと既定描画の停止を併用できる()
    {
        var resolverCalls = 0;
        var control = new MarqueeControl
        {
            Width = 200,
            Height = 200,
            ShowDefaultDecorations = false,
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

        // 解決は配置と描画の両方から呼ばれる（純粋関数である前提）
        Assert.True(resolverCalls > 0);
        window.Close();
    }
}
