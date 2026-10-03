using Xunit;
using DrawingColor = System.Drawing.Color;


namespace EsUtil.Feature.Marquee.Tests;

/// <summary><see cref="MarqueeAppearance"/> のテスト（UI 非依存・並列実行）。</summary>
/// <remarks>
/// 色・ラベルの体裁・表示可否・枠線の太さは<b>描画にだけ必要な設定</b>であり、本層が単一所有する<br/>
/// （コアの寸法・対話パラメータは <c>MarqueeOptions</c> が持つ）。 既定値と未指定の塗り色の解決を検証する。<br/>
/// 実際の描画結果は描画 API を持つ UI 側のテストが画素で検証する。
/// </remarks>
public class MarqueeAppearanceTests
{
    [Fact]
    public void 既定色が期待どおりである()
    {
        var paint = new MarqueeAppearance();

        // ハンドルは白の塗り・ダークグレーの枠線、マーカーは白
        Assert.Equal(DrawingColor.White, paint.HandleColor);
        Assert.Equal(DrawingColor.FromArgb(255, 169, 169, 169), paint.HandleBorderColor);
        Assert.Equal(DrawingColor.White, paint.MarkerColor);
        // ラベルは白文字・黒 60% 背景
        Assert.Equal(DrawingColor.White, paint.SizeLabelColor);
        Assert.Equal(DrawingColor.FromArgb(153, 0, 0, 0), paint.SizeLabelBackgroundColor);
        Assert.Equal(DrawingColor.White, paint.CoordLabelColor);
        Assert.Equal(DrawingColor.FromArgb(153, 0, 0, 0), paint.CoordLabelBackgroundColor);
    }

    [Fact]
    public void 既定の暗転マスクは黒50パーセントである()
    {
        Assert.Equal(DrawingColor.FromArgb(128, 0, 0, 0), new MarqueeAppearance().OverlayColor);
    }

    [Fact]
    public void 既定の外枠はハンドル枠と同じ色である()
    {
        var paint = new MarqueeAppearance();

        // 外枠とハンドル枠は同じ矩形に重なるため、既定値を共有する
        Assert.Equal(MarqueeAppearance.DefaultBorderColor, paint.OuterBorderColor);
        Assert.Equal(paint.HandleBorderColor, paint.OuterBorderColor);
    }

    [Fact]
    public void 既定の内側の塗りと枠線は透明である()
    {
        var paint = new MarqueeAppearance();

        // 描くかどうかは表示可否のフラグが単一所有する（色のアルファを代用にしない）
        Assert.Equal(DrawingColor.Transparent, paint.InnerFillColor);
        Assert.Equal(DrawingColor.Transparent, paint.InnerBorderColor);
    }

    [Fact]
    public void 未指定のハンドル塗り色は白へ解決される()
    {
        // 全成分 0（default）はハンドルが見えなくなるため白へ解決する
        Assert.Equal(DrawingColor.White, (new MarqueeAppearance { HandleColor = default }).HandleColor);
        Assert.Equal(DrawingColor.White, (new MarqueeAppearance { HandleColor = DrawingColor.FromArgb(0, 0, 0, 0) }).HandleColor);
    }

    [Fact]
    public void 指定したハンドル塗り色はそのまま使われる()
    {
        // アルファ 1 以上は指定値を尊重する（半透明の指定も許す）
        Assert.Equal(DrawingColor.Red, (new MarqueeAppearance { HandleColor = DrawingColor.Red }).HandleColor);
        Assert.Equal(
            DrawingColor.FromArgb(1, 2, 3, 4),
            (new MarqueeAppearance { HandleColor = DrawingColor.FromArgb(1, 2, 3, 4) }).HandleColor);
    }

    [Fact]
    public void with式で一部だけ変更できる()
    {
        var paint = new MarqueeAppearance();

        var changed = paint with { OverlayColor = DrawingColor.Red };

        Assert.Equal(DrawingColor.Red, changed.OverlayColor);
        // 他の項目は維持される
        Assert.Equal(paint.HandleColor, changed.HandleColor);
        Assert.Equal(paint.OuterBorderColor, changed.OuterBorderColor);
    }

    [Fact]
    public void 等価性は値で判定される()
    {
        var a = new MarqueeAppearance { OverlayColor = DrawingColor.Red, InnerFillColor = DrawingColor.Blue };
        var b = new MarqueeAppearance { OverlayColor = DrawingColor.Red, InnerFillColor = DrawingColor.Blue };

        Assert.Equal(a, b);
        Assert.NotEqual(a, a with { InnerFillColor = DrawingColor.Green });
    }

    [Fact]
    public void Defaultは既定値と同じ値を持つ()
    {
        Assert.Equal(new MarqueeAppearance(), MarqueeAppearance.Default);
    }

    /// <summary>サイズラベルの表示可否が設定値と注目矩形の有無の両方で決まることを検証する。</summary>
    /// <param name="showSizeLabel">表示の設定値。</param>
    /// <param name="hasFocusRect">注目矩形があるかどうか。</param>
    /// <param name="expected">描画するかどうか。</param>
    /// <remarks>ラベルの基準点は注目矩形の左上であり、文言も矩形の寸法から作るため、矩形が無いときは描画しない。</remarks>
    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, false)]
    public void ShouldShowSizeLabel_設定値と矩形の有無で決まる(bool showSizeLabel, bool hasFocusRect, bool expected)
    {
        var appearance = MarqueeAppearance.Default with { ShowSizeLabel = showSizeLabel };

        Assert.Equal(expected, appearance.ShouldShowSizeLabel(hasFocusRect));
    }

    /// <summary>カーソル座標ラベルの表示可否が設定値で決まることを検証する。</summary>
    /// <param name="showCoordLabel">表示の設定値。</param>
    /// <param name="expected">描画するかどうか。</param>
    /// <remarks>基準点はカーソル位置であり、位置は描画の幾何が常に持つため矩形の有無のような条件は無い。</remarks>
    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void ShouldShowCoordLabel_設定値で決まる(bool showCoordLabel, bool expected)
    {
        var appearance = MarqueeAppearance.Default with { ShowCoordLabel = showCoordLabel };

        Assert.Equal(expected, appearance.ShouldShowCoordLabel());
    }

    /// <summary>暗転オーバーレイの描画可否が注目矩形の有無と全面暗転の設定で決まることを検証する。</summary>
    /// <param name="showFullOverlay">全面暗転の設定値。</param>
    /// <param name="hasFocusRect">注目矩形があるかどうか。</param>
    /// <param name="expected">描画するかどうか。</param>
    [Theory]
    [InlineData(false, true, true)]
    [InlineData(false, false, false)]
    [InlineData(true, true, true)]
    [InlineData(true, false, true)]
    public void ShouldDrawOverlay_矩形の有無と全面暗転で決まる(bool showFullOverlay, bool hasFocusRect, bool expected)
    {
        var appearance = MarqueeAppearance.Default with { ShowFullOverlay = showFullOverlay };

        Assert.Equal(expected, appearance.ShouldDrawOverlay(hasFocusRect));
    }

    /// <summary>既定の枠線の太さが両 UI の期待値と一致することを検証する。</summary>
    [Fact]
    public void 既定の枠線の太さは2である()
    {
        var appearance = new MarqueeAppearance();

        Assert.Equal(2.0, appearance.OuterBorderThickness);
        Assert.Equal(2.0, appearance.InnerBorderThickness);
    }

    /// <summary>枠線の太さが生成時に安全域（0.5〜64）へ補正されることを検証する。</summary>
    /// <param name="value">設定する値。</param>
    /// <param name="expected">補正後の値。</param>
    [Theory]
    [InlineData(0.0, 0.5)]
    [InlineData(-1.0, 0.5)]
    [InlineData(0.5, 0.5)]
    [InlineData(64.0, 64.0)]
    [InlineData(100.0, 64.0)]
    [InlineData(double.NaN, 2.0)]
    [InlineData(double.PositiveInfinity, 2.0)]
    public void 枠線の太さは生成時に安全域へ補正される(double value, double expected)
    {
        var appearance = MarqueeAppearance.Default with
        {
            OuterBorderThickness = value,
            InnerBorderThickness = value,
        };

        Assert.Equal(expected, appearance.OuterBorderThickness);
        Assert.Equal(expected, appearance.InnerBorderThickness);
    }

    /// <summary>既定では表示可否がすべて有効（内側の塗り・枠線だけ無効）であることを検証する。</summary>
    [Fact]
    public void 既定の表示可否は意図どおりである()
    {
        var appearance = new MarqueeAppearance();

        Assert.True(appearance.ShowHandles);
        Assert.True(appearance.ShowSizeLabel);
        Assert.True(appearance.ShowCoordLabel);
        Assert.True(appearance.ShowOverlay);
        Assert.True(appearance.ShowOuterBorder);
        Assert.False(appearance.ShowFullOverlay);
        Assert.False(appearance.ShowInnerFill);
        Assert.False(appearance.ShowInnerBorder);
    }
}
