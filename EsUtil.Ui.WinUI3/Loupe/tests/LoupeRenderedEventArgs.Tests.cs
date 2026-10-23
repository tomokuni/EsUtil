using EsUtil.Platform.Core.Geometry;
using EsUtil.Ui.WinUI3.Interop.Drawing;
using Xunit;

namespace EsUtil.Ui.WinUI3.Loupe.Tests;

/// <summary><see cref="LoupeRenderedEventArgs"/>（ルーペの描画完了通知）のテスト。</summary>
/// <remarks>
/// イベントの発火は実際の表示（ウィンドウ）を必要とするため、引数の契約（保持する値と追加描画の受け付け）のみを検証する。<br/>
/// 描画 API を公開しないため、引数は描画先ではなく矩形の列を持つ。
/// </remarks>
public class LoupeRenderedEventArgsTests
{
    [Fact]
    internal void 保持した値を取得できる()
    {
        var overlay = new DrawList();
        var plan = CreatePlan();

        var args = new LoupeRenderedEventArgs(overlay, plan);

        Assert.Same(overlay, args.Overlay);
        Assert.Equal(plan, args.Content);
        Assert.Equal(new EsPoint(8, 8), args.Content.CaptureTopLeft);
    }

    [Fact]
    internal void AddRect_追加した矩形が順に並ぶ()
    {
        var overlay = new DrawList();
        var args = new LoupeRenderedEventArgs(overlay, CreatePlan());

        args.AddRect(1, 2, 3, 4, 0xFF112233);
        args.AddRect(5, 6, 7, 8, 0xFF445566);

        Assert.Equal(2, overlay.RectCount);
        Assert.Equal(1, overlay.Rects[0].Bounds.X);
        Assert.Equal(System.Drawing.Color.FromArgb(255, 0x11, 0x22, 0x33), overlay.Rects[0].Color);
        Assert.Equal(7, overlay.Rects[1].Bounds.Width);
    }

    [Fact]
    internal void AddRect_大きさが0以下の矩形は追加しない()
    {
        var overlay = new DrawList();
        var args = new LoupeRenderedEventArgs(overlay, CreatePlan());

        args.AddRect(1, 2, 0, 4, 0xFF112233);
        args.AddRect(1, 2, 3, 0, 0xFF112233);

        Assert.Equal(0, overlay.RectCount);
    }

    private static LoupeDrawPlan CreatePlan()
        => new(
            LoupeAppearance.Default,
            new EsRect(0, 0, 64, 64),
            null,
            [],
            [],
            [],
            [],
            new EsPoint(8, 8),
            2,
            64,
            1);
}
