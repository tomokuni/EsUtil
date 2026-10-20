using EsUtil.Platform.Core.Geometry;
using EsUtil.Ui.WinUI3.Loupe.Internal;
using Xunit;

namespace EsUtil.Ui.WinUI3.Loupe.Tests;

/// <summary><see cref="LoupeRenderedEventArgs"/>（ルーペの描画完了通知）のテスト。</summary>
/// <remarks>
/// イベントの発火は実際の表示（ウィンドウ）を必要とするため、引数の契約（保持する値と追加描画の受け付け）のみを検証する。<br/>
/// 移行前は描画先キャンバスを渡していたが、描画 API を公開しないため矩形の列を渡す形へ変わった。
/// </remarks>
public class LoupeRenderedEventArgsTests
{
    [Fact]
    internal void 保持した値を取得できる()
    {
        var overlay = new LoupeRectList();
        var plan = CreatePlan();

        var args = new LoupeRenderedEventArgs(overlay, plan);

        Assert.Same(overlay, args.Overlay);
        Assert.Equal(plan, args.Content);
        Assert.Equal(new EsPoint(8, 8), args.Content.CaptureTopLeft);
    }

    [Fact]
    internal void AddRect_追加した矩形が順に並ぶ()
    {
        var overlay = new LoupeRectList();
        var args = new LoupeRenderedEventArgs(overlay, CreatePlan());

        args.AddRect(1, 2, 3, 4, 0xFF112233);
        args.AddRect(5, 6, 7, 8, 0xFF445566);

        Assert.Equal(2, overlay.Count);
        Assert.Equal(1, overlay.Rects[0].X);
        Assert.Equal(0xFF112233u, overlay.Rects[0].ColorArgb);
        Assert.Equal(7, overlay.Rects[1].Width);
    }

    [Fact]
    internal void AddRect_大きさが0以下の矩形は追加しない()
    {
        var overlay = new LoupeRectList();
        var args = new LoupeRenderedEventArgs(overlay, CreatePlan());

        args.AddRect(1, 2, 0, 4, 0xFF112233);
        args.AddRect(1, 2, 3, 0, 0xFF112233);

        Assert.Equal(0, overlay.Count);
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
