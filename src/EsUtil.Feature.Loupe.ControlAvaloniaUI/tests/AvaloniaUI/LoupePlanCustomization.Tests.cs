using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using EsUtil.Platform.Core.Geometry;
using EsUtil.SaveEvidence.Platform.AvaloniaUI.Imaging;
using Xunit;
using DrawingColor = System.Drawing.Color;

namespace EsUtil.Feature.Loupe.AvaloniaUI.Tests;

/// <summary>見た目のカスタマイズ（描画計画の差し替えと既定描画の可否）のテスト。</summary>
/// <remarks>
/// 差し替えは <see cref="LoupeControl.DrawPlanResolver"/> と <see cref="LoupeControl.ShowDefaultDecorations"/> で行う<br/>
/// （提示層の派生は不要）。 Avalonia ヘッドレステストは並列実行非対応のため、同じコレクション内で直列実行する。
/// </remarks>
[Collection("AvaloniaHeadless")]
public class LoupePlanCustomizationTests
{
    /// <summary>描画計画の差し替えがラベル要素（配置）へ反映されることを検証する。</summary>
    [AvaloniaFact]
    internal void 描画計画の差し替えがラベル要素へ反映される()
    {
        var control = new LoupeControl
        {
            Options = LoupeOptions.Default with { RequestedSize = 100 },
            IsVisible = true,
            CenterPoint = new Avalonia.Point(10, 10),
            DrawPlanResolver = static plan => plan with
            {
                Labels = [new LoupeLabelPlacement(new EsRect(1, 2, 30, 20), 2, 2, "差し替え", 12, DrawingColor.White, DrawingColor.Black, 2)],
            },
        };
        var panel = new Panel { Width = 200, Height = 200 };
        panel.Children.Add(control);
        panel.Measure(new Avalonia.Size(200, 200));
        panel.Arrange(new Avalonia.Rect(0, 0, 200, 200));

        // 差し替えたラベルが要素として配置される
        var labels = control.Children.OfType<Border>().ToList();
        Assert.Single(labels);
        Assert.Equal("差し替え", ((TextBlock)labels[0].Child!).Text);
    }

    /// <summary>既定の装飾の停止と差し替えが併用でき、画像が欠けないことを検証する。</summary>
    /// <remarks>
    /// 装飾を止めてもペーストボードと拡大画像は描かれるため、描画結果は成立する（画像欠け・ゴーストが起きない）。<br/>
    /// 画素の検証は MewUI（Skia）が担う。
    /// </remarks>
    [AvaloniaFact]
    internal async Task 既定の装飾を停止しても描画が成立する()
    {
        var control = new LoupeControl
        {
            Options = LoupeOptions.Default with { RequestedSize = 100, ZoomLevel = 2 },
            IsVisible = true,
            ShowDefaultDecorations = false,
        };
        var panel = new Panel { Width = 200, Height = 200 };
        panel.Children.Add(control);
        panel.Measure(new Avalonia.Size(200, 200));
        panel.Arrange(new Avalonia.Rect(0, 0, 200, 200));

        control.Source = EsBitmapAvaloniaExtensions.CreateAvaloniaWriteableBitmap(200, 200, Avalonia.Media.Colors.Blue);
        control.CenterPoint = new Avalonia.Point(100, 100);

        LoupeDrawPlan? captured = null;
        control.Rendered += (_, e) => captured = e.Content;
        await control.WaitForRenderedAsync();

        Assert.NotNull(captured);
        Assert.False(control.ShowDefaultDecorations);
        // 層は装飾の可否とは独立に解決される（装飾を描かないのは描画実装のフラグによる）
        Assert.Contains(LoupeDrawLayer.Image, captured!.Value.Layers);
    }

    // ====================================================================
    // 描画計画の解決の回数（1 フレーム = 1 回）
    // ====================================================================

    /// <summary>配置と描画が同じ描画計画を共有する（1 フレーム 1 回の解決）ことを検証する。</summary>
    /// <remarks>
    /// 配置（<see cref="LoupeControl.ArrangeOverride"/> 相当）と描画（ビットマップ生成）はどちらも同じ表示サイズを渡すため、<br/>
    /// 2 回目の解決はキャッシュを返す（十字線・枠線の矩形とラベルの実寸測定をやり直さない）。
    /// </remarks>
    [AvaloniaFact]
    internal async Task 配置と描画で描画計画の解決は1回になる()
    {
        var calls = 0;
        var control = new LoupeControl
        {
            Options = LoupeOptions.Default with { RequestedSize = 100, ZoomLevel = 2 },
            IsVisible = true,
            CenterPoint = new Avalonia.Point(50, 50),
            Source = EsBitmapAvaloniaExtensions.CreateAvaloniaWriteableBitmap(200, 200, Avalonia.Media.Colors.Blue),
            DrawPlanResolver = plan => { calls++; return plan; },
        };
        var panel = new Panel { Width = 200, Height = 200 };
        panel.Children.Add(control);
        panel.Measure(new Avalonia.Size(200, 200));
        panel.Arrange(new Avalonia.Rect(0, 0, 200, 200));

        // 配置で 1 回だけ解決される
        Assert.Equal(1, calls);

        await control.WaitForRenderedAsync();

        // 描画は配置が解決した計画を使う（解決の回数は増えない）
        Assert.Equal(1, calls);
    }

    /// <summary>状態の変更で描画計画が再解決されることを検証する。</summary>
    /// <remarks>計画に効く入力の変更は <see cref="LoupeControl.RequestRender"/> が保持中の計画を破棄する（描画経路が再解決する）。</remarks>
    [AvaloniaFact]
    internal async Task 状態の変更で描画計画が再解決される()
    {
        var calls = 0;
        var control = new LoupeControl
        {
            Options = LoupeOptions.Default with { RequestedSize = 100, ZoomLevel = 2 },
            IsVisible = true,
            CenterPoint = new Avalonia.Point(50, 50),
            Source = EsBitmapAvaloniaExtensions.CreateAvaloniaWriteableBitmap(200, 200, Avalonia.Media.Colors.Blue),
            DrawPlanResolver = plan => { calls++; return plan; },
        };
        var panel = new Panel { Width = 200, Height = 200 };
        panel.Children.Add(control);
        panel.Measure(new Avalonia.Size(200, 200));
        panel.Arrange(new Avalonia.Rect(0, 0, 200, 200));

        Assert.Equal(1, calls);

        // 計画に効く入力（外観）の変更 → 破棄して再解決する
        control.Appearance = control.Appearance with { ShowCrosshair = false };
        await control.WaitForRenderedAsync();

        Assert.Equal(2, calls);
    }
}
