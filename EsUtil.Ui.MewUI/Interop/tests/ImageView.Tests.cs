using EsUtil.Ui.MewUI.Interop.Imaging;
using Xunit;
using DrawingColor = System.Drawing.Color;

namespace EsUtil.Ui.MewUI.Interop.Tests;

/// <summary><see cref="ImageView"/> のプロパティと継承のテスト。</summary>
/// <remarks>
/// MewUI の要素生成はプラットフォームホストの登録を必要としないため、UI を表示せずに検証できる。<br/>
/// 描画の判断は <see cref="ImageRenderPlanTests"/> が担う。
/// </remarks>
public class ImageViewTests
{
    [Fact]
    internal void Source_既定はnull()
    {
        var view = new ImageView();

        Assert.Null(view.Source);
    }

    [Fact]
    internal void Source_設定した値をそのまま返す()
    {
        var view = new ImageView();
        using var bitmap = TestHelper.CreateBitmap(2, 2, DrawingColor.Red);

        view.Source = bitmap;

        Assert.Same(bitmap, view.Source);
    }

    [Fact]
    internal void Source_派生クラスでも基底へ登録したプロパティを共有する()
    {
        // MewProperty を基底で登録するため、派生クラスのインスタンスでも同じプロパティとして扱えることを固定する
        var view = new UniformView();
        using var bitmap = TestHelper.CreateBitmap(2, 2, DrawingColor.Red);

        view.Source = bitmap;

        Assert.Same(bitmap, view.Source);
        Assert.True(view.UniformDrawExposed);
    }

    [Fact]
    internal void Source_nullへ戻せる()
    {
        var view = new ImageView();
        using var bitmap = TestHelper.CreateBitmap(2, 2, DrawingColor.Red);
        view.Source = bitmap;

        view.Source = null;

        Assert.Null(view.Source);
    }

    /// <summary>縦横比を保つ配置を選ぶ派生クラス（検証用に <see cref="ImageView.UniformDraw"/> を公開する）。</summary>
    private sealed class UniformView : ImageView
    {
        /// <summary>縦横比を保って収める。</summary>
        protected override bool UniformDraw => true;

        /// <summary>縦横比を保つ指定を公開する（検証用）。</summary>
        public bool UniformDrawExposed => UniformDraw;
    }
}
