using Xunit;
using DrawingColor = System.Drawing.Color;


namespace EsUtil.Feature.Loupe.Tests;

/// <summary><see cref="LoupeAppearance"/> のテスト（UI 非依存・並列実行）。</summary>
/// <remarks>
/// 色・フォント・角丸・枠線の太さ・表示可否は描画にだけ必要な設定であり、本層が単一所有する。<br/>
/// 既定値と部分変更を検証する。 実際の描画結果は描画 API を持つ UI 側のテストが画素で検証する。
/// </remarks>
public class LoupeAppearanceTests
{
    [Fact]
    internal void 既定色が期待どおりである()
    {
        var paint = new LoupeAppearance();

        // ペーストボード（画像外）は不透明の中性グレー、十字線は赤 40%、枠線は LightGray
        Assert.Equal(DrawingColor.FromArgb(255, 0x53, 0x53, 0x53), paint.PasteboardColor);
        Assert.Equal(DrawingColor.FromArgb(102, 255, 0, 0), paint.CrossColor);
        Assert.Equal(DrawingColor.FromArgb(255, 0xD3, 0xD3, 0xD3), paint.BorderColor);
        // 座標ラベルは白文字・黒 60% 背景
        Assert.Equal(DrawingColor.White, paint.CoordLabelColor);
        Assert.Equal(DrawingColor.FromArgb(153, 0, 0, 0), paint.CoordLabelBackgroundColor);
    }

    /// <summary>表示可否と枠線の太さの既定を検証する。</summary>
    [Fact]
    internal void 既定の表示可否と枠線の太さが期待どおりである()
    {
        var paint = new LoupeAppearance();

        Assert.True(paint.ShowCrosshair);
        Assert.True(paint.ShowBorder);
        Assert.True(paint.ShowCoordLabel);
        Assert.Equal(EsUtil.Feature.Loupe.LoupeCore.Limits.BorderThicknessDefault, paint.BorderThickness);
    }

    /// <summary>中心座標ラベルの体裁の既定を検証する。</summary>
    [Fact]
    internal void 既定のラベル体裁が期待どおりである()
    {
        var paint = new LoupeAppearance();

        Assert.Equal(LoupeAppearance.CoordLabelFontSizeDefault, paint.CoordLabelFontSize);
        Assert.Equal(LoupeAppearance.CoordLabelCornerRadiusDefault, paint.CoordLabelCornerRadius);
        Assert.Equal(LoupeAppearance.CoordLabelOffsetDefault, paint.CoordLabelOffset);
    }

    /// <summary>枠線の太さが生成時に安全域へ補正されることを検証する。</summary>
    [Fact]
    internal void 枠線の太さが生成時に安全域へ補正される()
    {
        var paint = new LoupeAppearance { BorderThickness = 1000 };

        Assert.Equal(EsUtil.Feature.Loupe.LoupeCore.Limits.BorderThicknessMax, paint.BorderThickness);
    }

    /// <summary>中心座標ラベルの表示可否が設定と中心の有無で決まることを検証する。</summary>
    /// <param name="show">表示の設定。</param>
    /// <param name="hasCenter">拡大中心が設定されているかどうか。</param>
    /// <param name="expected">期待する判定結果。</param>
    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, false)]
    internal void ShouldShowCoordLabel_設定と中心の有無で判定する(bool show, bool hasCenter, bool expected)
    {
        var paint = new LoupeAppearance { ShowCoordLabel = show };

        Assert.Equal(expected, paint.ShouldShowCoordLabel(hasCenter));
    }

    /// <summary>ペーストボード（画像外）の既定が不透明の中性グレーであることを検証する。</summary>
    /// <remarks>画像外は無地として見える（透明を指定した場合は背後の要素がそのまま見える）。</remarks>
    [Fact]
    internal void 既定のペーストボードは不透明の中性グレーである()
    {
        var paint = new LoupeAppearance();

        Assert.Equal(DrawingColor.FromArgb(255, 0x53, 0x53, 0x53), paint.PasteboardColor);
    }

    [Fact]
    internal void with式で一部だけ変更できる()
    {
        var paint = new LoupeAppearance();

        var changed = paint with { BorderColor = DrawingColor.Red };

        Assert.Equal(DrawingColor.Red, changed.BorderColor);
        // 他の項目は維持される
        Assert.Equal(paint.PasteboardColor, changed.PasteboardColor);
    }

    [Fact]
    internal void 等価性は値で判定される()
    {
        var a = new LoupeAppearance { CrossColor = DrawingColor.Red, PasteboardColor = DrawingColor.Blue };
        var b = new LoupeAppearance { CrossColor = DrawingColor.Red, PasteboardColor = DrawingColor.Blue };

        Assert.Equal(a, b);
        Assert.NotEqual(a, a with { PasteboardColor = DrawingColor.Green });
    }

    [Fact]
    internal void Defaultは既定値と同じ値を持つ()
    {
        Assert.Equal(new LoupeAppearance(), LoupeAppearance.Default);
    }
}
