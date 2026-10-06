using EsUtil.Platform.Core.Geometry;
using SkiaSharp;
using Xunit;

namespace EsUtil.Feature.Loupe.MewUI.Tests;

/// <summary><see cref="LoupeRenderedEventArgs"/>（ルーペの描画完了通知）のテスト。</summary>
/// <remarks>
/// イベントの発火は実際の描画（ウィンドウ）を必要とするため、引数の契約（保持する値）のみを検証する。<br/>
/// 描画完了時のオーバーレイ描画はホスト（<c>SelectionOverlayControl.MewUI</c>）のテストが担う。
/// </remarks>
public class LoupeRenderedEventArgsTests
{
    /// <summary>保持した値がそのまま取得できることを検証する。</summary>
    [Fact]
    internal void 保持した値を取得できる()
    {
        using var bitmap = new SKBitmap(new SKImageInfo(32, 32));
        using var canvas = new SKCanvas(bitmap);
        var plan = CreatePlan(new EsPoint(16, 16));

        var args = new LoupeRenderedEventArgs(canvas, plan);

        Assert.Same(canvas, args.Canvas);
        // 描画計画は値で比較できる（切り出し左上・拡大率・描画面のサイズを単一所有する）
        Assert.Equal(plan, args.Content);
        // 中心 (16,16)・拡大率 2 → 切り出し一辺 16、左上 = (16−8, 16−8) = (8, 8)
        Assert.Equal(new EsPoint(8, 8), args.Content.CaptureTopLeft);
    }

    /// <summary>中心が未設定の場合は切り出し左上が null になることを検証する。</summary>
    [Fact]
    internal void 中心が未設定の場合は切り出し左上がnullになる()
    {
        using var bitmap = new SKBitmap(new SKImageInfo(32, 32));
        using var canvas = new SKCanvas(bitmap);

        var args = new LoupeRenderedEventArgs(canvas, CreatePlan(center: null));

        Assert.Null(args.Content.CaptureTopLeft);
    }

    /// <summary>テスト用の描画計画を提示層（実際の解決経路）で解決する。</summary>
    /// <param name="center">拡大中心。 null の場合は中心未設定の計画になる。</param>
    /// <returns>描画計画。</returns>
    /// <remarks>実寸測定はダミーを渡す（本テストは計画の保持のみを検証する）。</remarks>
    private static LoupeDrawPlan CreatePlan(EsPoint? center)
    {
        var presenter = new LoupePresenter
        {
            Options = LoupeOptions.Default with { RequestedSize = 32, ZoomLevel = 2 },
            CenterPoint = center,
        };

        return presenter.ResolveDrawPlan(32, static (_, _) => default);
    }
}
