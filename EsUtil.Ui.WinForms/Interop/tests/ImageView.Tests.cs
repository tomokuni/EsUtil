using EsUtil.Ui.WinForms.Interop.Imaging;
using Xunit;
using DrawingColor = System.Drawing.Color;

namespace EsUtil.Ui.WinForms.Interop.Tests;

/// <summary><see cref="ImageView"/> のプロパティと描画のテスト。</summary>
/// <remarks>
/// ハンドル（ウィンドウ）を必要としないよう、描画は <c>RenderTo</c> をオフスクリーンへ向けて要求する。<br/>
/// 配置の計算そのものは <see cref="EsImageDrawMath"/> のテストが担うため、本テストは<br/>
/// 「プロパティの往復」と「配置方法の選択（派生クラス）」を固定する。
/// </remarks>
public sealed class ImageViewTests
{
    /// <summary><see cref="ImageView.Source"/> の既定が null であることを検証する。</summary>
    [Fact]
    internal void Source_既定はnull()
        => Assert.Null(new ImageView().Source);

    /// <summary>設定した画像がそのまま読み出せることを検証する。</summary>
    [Fact]
    internal void Source_設定した値をそのまま返す()
    {
        using var view = new ImageView();
        using var bitmap = TestHelper.CreateBitmap(2, 2, DrawingColor.Red);

        view.Source = bitmap;

        Assert.Same(bitmap, view.Source);
    }

    /// <summary>null へ戻せることを検証する。</summary>
    [Fact]
    internal void Source_nullへ戻せる()
    {
        using var view = new ImageView();
        using var bitmap = TestHelper.CreateBitmap(2, 2, DrawingColor.Red);
        view.Source = bitmap;

        view.Source = null;

        Assert.Null(view.Source);
    }

    /// <summary>派生クラスでも同じプロパティを共有することを検証する。</summary>
    [Fact]
    internal void Source_派生クラスでも共有する()
    {
        using var view = new UniformView();
        using var bitmap = TestHelper.CreateBitmap(2, 2, DrawingColor.Red);

        view.Source = bitmap;

        Assert.Same(bitmap, view.Source);
        Assert.True(view.UniformDrawExposed);
    }

    /// <summary>領域全体へ引き伸ばす配置が指定の領域を覆うことを検証する。</summary>
    [Fact]
    internal void RenderTo_Fillは領域全体を覆う()
    {
        using var view = new ImageView();
        using var bitmap = TestHelper.CreateBitmap(2, 2, DrawingColor.FromArgb(255, 0, 0, 255));
        view.Source = bitmap;
        using var target = TestHelper.CreateTarget(6, 6);

        view.RenderTo(target.Graphics, target.Size);

        Assert.Equal(DrawingColor.FromArgb(255, 0, 0, 255), target.GetPixel(0, 0));
        Assert.Equal(DrawingColor.FromArgb(255, 0, 0, 255), target.GetPixel(5, 5));
    }

    /// <summary>縦横比を保つ配置を選ぶ派生クラスが余白を残すことを検証する。</summary>
    [Fact]
    internal void RenderTo_Uniformは余白を残す()
    {
        using var view = new UniformView();
        using var bitmap = TestHelper.CreateBitmap(4, 2, DrawingColor.FromArgb(255, 0, 0, 255));
        view.Source = bitmap;
        using var target = TestHelper.CreateTarget(8, 8);

        view.RenderTo(target.Graphics, target.Size);

        Assert.Equal(TestHelper.EmptyPixel, target.GetPixel(4, 0));
        Assert.Equal(DrawingColor.FromArgb(255, 0, 0, 255), target.GetPixel(4, 3));
    }

    /// <summary>画像が未設定の場合は何も描画しないことを検証する。</summary>
    [Fact]
    internal void RenderTo_画像が無ければ描かない()
    {
        using var view = new ImageView();
        using var target = TestHelper.CreateTarget(4, 4);

        view.RenderTo(target.Graphics, target.Size);

        Assert.Equal(TestHelper.EmptyPixel, target.GetPixel(0, 0));
    }

    /// <summary>破棄済みの画像を設定しても例外にならないことを検証する。</summary>
    /// <remarks>
    /// コアの画像は読み取りの面を共有するため、ホストが先に破棄しうる（描画のたびに例外を出さない）。
    /// </remarks>
    [Fact]
    internal void Source_破棄済みの画像は描画しない()
    {
        using var view = new ImageView();
        var bitmap = TestHelper.CreateBitmap(2, 2, DrawingColor.Red);
        bitmap.Dispose();
        using var target = TestHelper.CreateTarget(4, 4);

        view.Source = bitmap;
        view.RenderTo(target.Graphics, target.Size);

        Assert.Equal(TestHelper.EmptyPixel, target.GetPixel(0, 0));
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
