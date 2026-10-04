using EsUtil.Platform.Core.Geometry;
using Xunit;
using DrawingColor = System.Drawing.Color;
using EsUtil.Feature.Marquee.Presenter.Drawing;

namespace EsUtil.Feature.Marquee.Tests;

/// <summary><see cref="MarqueePresenter.ResolveLabelPlacements"/> の単体テスト（実寸からラベル要素の配置を解決する）。</summary>
/// <remarks>
/// 文言・位置は提示層（<see cref="MarqueePresenter"/>）が、色・フォントサイズ・内側余白は外観（<see cref="MarqueeAppearance"/>）が<br/>
/// 単一所有するため、本テストは「実寸の反映」と「配置指示への写像」だけを検証する。<br/>
/// <br/>
/// UI フレームワークに依存しないため、UI 初期化なしの <c>[Fact]</c> で並列検証できる。
/// </remarks>
public class MarqueePresenterLabelPlanTests
{
    /// <summary>測定に使う固定の実寸（テキストの実測値を模した値）。</summary>
    private static readonly EsSize MeasuredSize = new(42.0, 14.0);

    /// <summary>テスト用のサイズラベルのテキスト配置を生成する（注目矩形の左上へ置く）。</summary>
    /// <param name="text">文言。</param>
    /// <returns>テキスト配置。</returns>
    private static MarqueeTextPlacement CreateSizeLabel(string text = "(10, 20) - (30, 40),  (20×20)")
        => new(text, new EsPoint(10, 20), default);

    /// <summary>テスト用のカーソル座標ラベルのテキスト配置を生成する（カーソル + オフセットへ置き、領域内へクランプする）。</summary>
    /// <param name="text">文言。</param>
    /// <returns>テキスト配置。</returns>
    private static MarqueeTextPlacement CreateCoordLabel(string text = "(50, 60)")
        => new(text, new EsPoint(58, 68), new EsSize(200, 200));

    /// <summary>実寸を固定で返す測定関数。</summary>
    /// <param name="text">測定する文言。</param>
    /// <param name="fontSize">フォントサイズ。</param>
    /// <returns>固定の実寸。</returns>
    private static EsSize Measure(string text, double fontSize) => MeasuredSize;

    /// <summary>テスト用の外観を生成する（本テストは色・フォントサイズ・内側余白の写像を検証する）。</summary>
    /// <returns>外観。</returns>
    private static MarqueeAppearance CreateAppearance()
        => new()
        {
            SizeLabelColor = DrawingColor.White,
            SizeLabelBackgroundColor = DrawingColor.FromArgb(153, 0, 0, 0),
            SizeLabelFontSize = 14.0,
            CoordLabelColor = DrawingColor.Yellow,
            CoordLabelBackgroundColor = DrawingColor.FromArgb(153, 0, 0, 0),
            CoordLabelFontSize = 12.0,
        };

    [Fact]
    internal void Resolve_ラベルが無ければ空を返す()
    {
        var elements = MarqueePresenter.ResolveLabelPlacements(null, null, CreateAppearance(), Measure);

        Assert.Empty(elements);
    }

    [Fact]
    internal void Resolve_サイズラベルのみなら1件を返す()
    {
        var elements = MarqueePresenter.ResolveLabelPlacements(CreateSizeLabel("サイズ"), null, CreateAppearance(), Measure);

        var element = Assert.Single(elements);
        Assert.Equal("サイズ", element.Text);
        Assert.Equal(14.0, element.FontSize);
    }

    [Fact]
    internal void Resolve_カーソル座標ラベルのみなら1件を返す()
    {
        var elements = MarqueePresenter.ResolveLabelPlacements(null, CreateCoordLabel("(1, 2)"), CreateAppearance(), Measure);

        var element = Assert.Single(elements);
        Assert.Equal("(1, 2)", element.Text);
        Assert.Equal(12.0, element.FontSize);
    }

    [Fact]
    internal void Resolve_両方ある場合はサイズラベルが先で座標ラベルが後になる()
    {
        // 順序は本クラスが単一所有する（座標ラベルが手前 = 最後）
        var elements = MarqueePresenter.ResolveLabelPlacements(
            CreateSizeLabel("サイズ"), CreateCoordLabel(), CreateAppearance(), Measure);

        Assert.Equal(2, elements.Count);
        Assert.Equal("サイズ", elements[0].Text);
        Assert.Equal("(50, 60)", elements[1].Text);
    }

    [Fact]
    internal void Resolve_基準点を背景の左上に置く()
    {
        var element = Assert.Single(
            MarqueePresenter.ResolveLabelPlacements(CreateSizeLabel(), null, CreateAppearance(), Measure));

        // 基準点 (10, 20) がそのまま背景の左上になる
        Assert.Equal(new EsPoint(10, 20), element.DipRect.TopLeft);
        Assert.Equal(MeasuredSize.Width + MarqueeAppearance.LabelPaddingDefault * 2, element.DipRect.Width);
        Assert.Equal(MeasuredSize.Height + MarqueeAppearance.LabelPaddingDefault * 2, element.DipRect.Height);
    }

    [Fact]
    internal void Resolve_領域をはみ出す場合はクランプする()
    {
        // クランプ領域 (200, 200) へ収まらない位置を指定する
        var label = new MarqueeTextPlacement("(50, 60)", new EsPoint(190, 195), new EsSize(200, 200));

        var element = Assert.Single(MarqueePresenter.ResolveLabelPlacements(null, label, CreateAppearance(), Measure));

        Assert.Equal(200 - (MeasuredSize.Width + MarqueeAppearance.LabelPaddingDefault * 2), element.DipRect.X);
        Assert.Equal(200 - (MeasuredSize.Height + MarqueeAppearance.LabelPaddingDefault * 2), element.DipRect.Y);
    }

    [Fact]
    internal void Resolve_内側余白は体裁の値になる()
    {
        var element = Assert.Single(
            MarqueePresenter.ResolveLabelPlacements(CreateSizeLabel(), null, CreateAppearance(), Measure));

        Assert.Equal(MarqueeAppearance.LabelPaddingDefault, element.PaddingX);
        Assert.Equal(MarqueeAppearance.LabelPaddingDefault, element.PaddingY);
    }

    [Fact]
    internal void Resolve_内側余白の設定を配置へ反映する()
    {
        var appearance = CreateAppearance() with { LabelPadding = 7.0 };

        var element = Assert.Single(MarqueePresenter.ResolveLabelPlacements(CreateSizeLabel(), null, appearance, Measure));

        Assert.Equal(7.0, element.PaddingX);
        Assert.Equal(MeasuredSize.Width + 14.0, element.DipRect.Width);
    }

    [Fact]
    internal void Resolve_文言とフォントサイズを測定関数へ渡す()
    {
        var calls = new List<(string Text, double FontSize)>();

        MarqueePresenter.ResolveLabelPlacements(
            CreateSizeLabel("サイズ"),
            CreateCoordLabel("(7, 8)"),
            CreateAppearance(),
            (text, fontSize) =>
            {
                calls.Add((text, fontSize));
                return MeasuredSize;
            });

        Assert.Equal([("サイズ", 14.0), ("(7, 8)", 12.0)], calls);
    }

    [Fact]
    internal void Resolve_色を要素へ写す()
    {
        var element = Assert.Single(
            MarqueePresenter.ResolveLabelPlacements(null, CreateCoordLabel(), CreateAppearance(), Measure));

        // 色は外観（本層が単一所有する）から写す
        Assert.Equal(DrawingColor.Yellow, element.TextColor);
        Assert.Equal(DrawingColor.FromArgb(153, 0, 0, 0), element.BackgroundColor);
    }

    [Fact]
    internal void Resolve_測定関数がnullなら例外()
    {
        Assert.Throws<ArgumentNullException>(
            () => MarqueePresenter.ResolveLabelPlacements(CreateSizeLabel(), null, CreateAppearance(), null!));
    }

    [Fact]
    internal void Resolve_外観がnullなら例外()
    {
        Assert.Throws<ArgumentNullException>(
            () => MarqueePresenter.ResolveLabelPlacements(CreateSizeLabel(), null, null!, Measure));
    }
}
