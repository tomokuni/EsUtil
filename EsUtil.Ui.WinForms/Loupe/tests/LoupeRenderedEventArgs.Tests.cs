using Xunit;
using DrawingPixelFormat = System.Drawing.Imaging.PixelFormat;

namespace EsUtil.Ui.WinForms.Loupe.Tests;

/// <summary><see cref="LoupeRenderedEventArgs"/>（ルーペの描画完了通知）のテスト。</summary>
/// <remarks>
/// イベントの発火は実際の描画（ハンドルを持つコントロール）を必要とするため、引数の契約（保持する値）のみを検証する。<br/>
/// 描画完了時のオーバーレイ描画はホスト（上位のアプリの合成コントロール）のテストが担う。
/// </remarks>
public class LoupeRenderedEventArgsTests
{
    /// <summary>保持した値がそのまま取得できることを検証する。</summary>
    [Fact]
    internal void 保持した値を取得できる()
    {
        using var bitmap = new Bitmap(32, 32, DrawingPixelFormat.Format32bppPArgb);
        using var graphics = Graphics.FromImage(bitmap);
        var plan = CreatePlan(new EsPoint(16, 16));

        var args = new LoupeRenderedEventArgs(graphics, plan);

        Assert.Same(graphics, args.Graphics);

        // 描画計画は値で比較できる（切り出し左上・拡大率・描画面のサイズを単一所有する）
        Assert.Equal(plan, args.Content);

        // 中心 (16,16)・拡大率 2・一辺 32 → 可視域の半径 8、左上 = (16−8, 16−8) = (8, 8)
        Assert.Equal(new EsPoint(8, 8), args.Content.CaptureTopLeft);
    }

    /// <summary>中心が未設定の場合は切り出し左上が null になることを検証する。</summary>
    [Fact]
    internal void 中心が未設定の場合は切り出し左上がnullになる()
    {
        using var bitmap = new Bitmap(32, 32, DrawingPixelFormat.Format32bppPArgb);
        using var graphics = Graphics.FromImage(bitmap);

        var args = new LoupeRenderedEventArgs(graphics, CreatePlan(center: null));

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
