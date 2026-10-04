using EsUtil.Platform.Core.Geometry;
using Xunit;
using DrawingColor = System.Drawing.Color;
using EsUtil.Feature.Loupe.Presenter.Drawing;

namespace EsUtil.Feature.Loupe.Tests;

/// <summary><see cref="LoupePresenter.ResolveLabelPlacements"/> の単体テスト（実寸からラベル要素の配置を解決する）。</summary>
/// <remarks>
/// 文言・位置はコアが、色・フォントサイズ・角丸・内側余白は描画の設定が単一所有するため、<br/>
/// 本テストは「実寸の反映」と「配置指示への写像」だけを検証する。
/// <br/>
/// UI フレームワークに依存しないため、UI 初期化なしの <c>[Fact]</c> で並列検証できる。
/// </remarks>
public class LoupePresenterLabelPlanTests
{
    /// <summary>測定に使う固定の実寸（テキストの実測値を模した値）。</summary>
    private static readonly EsSize MeasuredSize = new(36.0, 14.0);

    /// <summary>テスト用の中心座標ラベルのテキスト配置を生成する（基準点はルーペ左上の内側）。</summary>
    /// <param name="text">文言。</param>
    /// <returns>テキスト配置。</returns>
    private static LoupeTextPlacement CreateCoordLabel(string text = "(50, 60)")
        => new(text, new EsPoint(2, 2), default);

    /// <summary>テスト用の描画に使う設定を生成する。</summary>
    /// <returns>描画に使う設定。</returns>
    private static LoupeAppearance CreatePaint()
        => new()
        {
            CoordLabelColor = DrawingColor.White,
            CoordLabelBackgroundColor = DrawingColor.FromArgb(153, 0, 0, 0),
            CoordLabelFontSize = 12.0,
            CoordLabelCornerRadius = 2,
        };

    /// <summary>実寸を固定で返す測定関数。</summary>
    /// <param name="text">測定する文言。</param>
    /// <param name="fontSize">フォントサイズ。</param>
    /// <returns>固定の実寸。</returns>
    private static EsSize Measure(string text, double fontSize) => MeasuredSize;

    [Fact]
    internal void Resolve_ラベルが無ければ空を返す()
    {
        var elements = LoupePresenter.ResolveLabelPlacements(null, CreatePaint(), Measure);

        Assert.Empty(elements);
    }

    [Fact]
    internal void Resolve_ラベルがあれば1件を返す()
    {
        var element = Assert.Single(LoupePresenter.ResolveLabelPlacements(CreateCoordLabel(), CreatePaint(), Measure));

        Assert.Equal("(50, 60)", element.Text);
        Assert.Equal(12.0, element.FontSize);
        Assert.Equal(DrawingColor.White, element.TextColor);
        Assert.Equal(2.0, element.CornerRadius);
    }

    [Fact]
    internal void Resolve_基準点が背景の左上になる()
    {
        var element = Assert.Single(LoupePresenter.ResolveLabelPlacements(CreateCoordLabel(), CreatePaint(), Measure));

        // 基準点 (2, 2) がそのまま背景の左上になり、幅は実寸 + 内側余白で決まる
        Assert.Equal(2, element.DipRect.X);
        Assert.Equal(2, element.DipRect.Y);
        Assert.Equal(MeasuredSize.Width + element.PaddingX * 2, element.DipRect.Width);
    }

    [Fact]
    internal void Resolve_上下の内側余白は左右より狭い()
    {
        var element = Assert.Single(LoupePresenter.ResolveLabelPlacements(CreateCoordLabel(), CreatePaint(), Measure));

        // ルーペ上端の帯へ収めるため、上下の余白は左右（共通の体裁値）より狭い
        Assert.True(element.PaddingY < element.PaddingX);
        Assert.Equal(MeasuredSize.Height + element.PaddingY * 2, element.DipRect.Height);
    }

    [Fact]
    internal void Resolve_文言とフォントサイズを測定関数へ渡す()
    {
        var calls = new List<(string Text, double FontSize)>();

        LoupePresenter.ResolveLabelPlacements(
            CreateCoordLabel("(7, 8)"),
            CreatePaint(),
            (text, fontSize) =>
            {
                calls.Add((text, fontSize));
                return MeasuredSize;
            });

        Assert.Equal([("(7, 8)", 12.0)], calls);
    }

    [Fact]
    internal void Resolve_測定関数がnullなら例外()
    {
        Assert.Throws<ArgumentNullException>(() => LoupePresenter.ResolveLabelPlacements(CreateCoordLabel(), CreatePaint(), null!));
    }

    [Fact]
    internal void Resolve_色がnullなら例外()
    {
        Assert.Throws<ArgumentNullException>(() => LoupePresenter.ResolveLabelPlacements(CreateCoordLabel(), null!, Measure));
    }
}
