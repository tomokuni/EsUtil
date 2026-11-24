using Xunit;
using EsUtil.Feature.Annotation.Core.Defaults;

namespace EsUtil.Feature.Annotation.Core.Tests;

/// <summary><see cref="AnnotationDraftDefaultsFactory"/> の規則（設定の値を安全域へ補正する）を検証する。</summary>
public sealed class AnnotationDraftDefaultsFactoryTests
{
    /// <summary>数値は安全域へ補正され、枠線と吹き出しへ同じ値が使われる。</summary>
    [Fact]
    internal void FromText_数値を安全域へ補正する()
    {
        var defaults = AnnotationDraftDefaultsFactory.FromText(
            frameBorderColor: "赤",
            borderThickness: 0,
            frameFillColor: "#FF102030",
            cornerRadius: double.NaN,
            bubbleBorderColor: "青",
            bubbleFillColor: "解釈できない色",
            textColor: "黒",
            fontSize: 0,
            bodyPadding: -5,
            tailWidth: double.MaxValue);

        Assert.Equal(AnnotationLimits.BorderThicknessMin, defaults.FrameAppearance.BorderThickness);
        Assert.Equal(AnnotationLimits.BorderThicknessMin, defaults.BubbleAppearance.BorderThickness);
        Assert.Equal(0, defaults.FrameAppearance.CornerRadius);
        Assert.Equal(0, defaults.BubbleAppearance.CornerRadius);
        Assert.Equal(AnnotationLimits.FontSizeMin, defaults.TextStyle.FontSize);
        Assert.Equal(0, defaults.BodyPadding);
        Assert.Equal(AnnotationLimits.TailWidthMax, defaults.TailWidth);
    }

    /// <summary>解釈できない色は透明として扱う（例外にしない）。</summary>
    [Fact]
    internal void FromText_解釈できない色は透明にする()
    {
        var defaults = AnnotationDraftDefaultsFactory.FromText(
            frameBorderColor: "解釈できない色",
            borderThickness: AnnotationDefaults.BorderThicknessDefault,
            frameFillColor: "#GGGGGGGG",
            cornerRadius: AnnotationDefaults.CornerRadiusDefault,
            bubbleBorderColor: null,
            bubbleFillColor: null,
            textColor: null,
            fontSize: AnnotationDefaults.FontSizeDefault,
            bodyPadding: AnnotationDefaults.BodyPaddingDefault,
            tailWidth: AnnotationDefaults.TailWidthDefault);

        Assert.Equal(System.Drawing.Color.Transparent, defaults.FrameAppearance.Border);
        Assert.Equal(System.Drawing.Color.Transparent, defaults.FrameAppearance.Fill);
        Assert.Equal(System.Drawing.Color.Transparent, defaults.BubbleAppearance.Border);
        Assert.Equal(System.Drawing.Color.Transparent, defaults.TextStyle.Color);
    }
}
