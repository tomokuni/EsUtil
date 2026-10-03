using Xunit;
using DrawingColor = System.Drawing.Color;
using EsUtil.Feature.Marquee.Core.Logging;
using EsUtil.Feature.Marquee.Presenter.Logging;

namespace EsUtil.Feature.Marquee.Tests.Presenter.Logging;

/// <summary><see cref="MarqueePresenterLogMessage"/> の単体テスト（外観の変更ログ）。</summary>
/// <remarks>
/// 「値を所有する主体が記録する」規則により、提示層が単一所有する外観（<see cref="MarqueeAppearance"/>）の変更を<br/>
/// 本層が記録する。<b>変化した項目だけ</b>を出力する（外観の再適用でログが溢れないため）。<br/>
/// ログは UI 初期化なしで検証できる（LoggerMessage の定義は UI フレームワークに依存しない）。
/// </remarks>
public class MarqueePresenterLogMessageTests
{
    [Fact]
    public void LogAppearanceChanged_変化した項目だけを記録する()
    {
        var logger = new RecordingLogger();
        var before = MarqueeAppearance.Default;
        var after = before with { OuterBorderThickness = 5, ShowHandles = false };

        logger.LogAppearanceChanged(before, after);

        Assert.Equal(
            [
                "[Marquee.Presenter] Setting: ShowHandles True → False",
                "[Marquee.Presenter] Setting: OuterBorderThickness 2 → 5",
            ],
            logger.Messages);
    }

    [Fact]
    public void LogAppearanceChanged_同値なら記録しない()
    {
        var logger = new RecordingLogger();
        var before = MarqueeAppearance.Default;

        logger.LogAppearanceChanged(before, before with { });

        Assert.Empty(logger.Messages);
    }

    [Fact]
    public void LogAppearanceChanged_色はAARRGGBB形式で記録する()
    {
        var logger = new RecordingLogger();
        var before = MarqueeAppearance.Default;
        var after = before with { OverlayColor = DrawingColor.FromArgb(255, 0, 0, 0) };

        logger.LogAppearanceChanged(before, after);

        Assert.Equal(
            "[Marquee.Presenter] Setting: OverlayColor #80000000 → #FF000000",
            Assert.Single(logger.Messages));
    }

    [Fact]
    public void LogAppearanceChanged_外観の全項目を対象にする()
    {
        var logger = new RecordingLogger();

        // 全項目を変更した場合、定義した項目数ぶんのログが出る（項目の追加漏れを検出する）
        var after = MarqueeAppearance.Default with
        {
            HandleColor = DrawingColor.FromArgb(1, 0, 0, 0),
            HandleBorderColor = DrawingColor.FromArgb(2, 0, 0, 0),
            MarkerColor = DrawingColor.FromArgb(3, 0, 0, 0),
            SizeLabelColor = DrawingColor.FromArgb(4, 0, 0, 0),
            SizeLabelBackgroundColor = DrawingColor.FromArgb(5, 0, 0, 0),
            CoordLabelColor = DrawingColor.FromArgb(6, 0, 0, 0),
            CoordLabelBackgroundColor = DrawingColor.FromArgb(7, 0, 0, 0),
            OverlayColor = DrawingColor.FromArgb(8, 0, 0, 0),
            OuterBorderColor = DrawingColor.FromArgb(9, 0, 0, 0),
            InnerFillColor = DrawingColor.FromArgb(10, 0, 0, 0),
            InnerBorderColor = DrawingColor.FromArgb(11, 0, 0, 0),
            SizeLabelFontSize = 13.0,
            CoordLabelFontSize = 11.0,
            LabelPadding = 5.0,
            MarkerThickness = 3.0,
            CoordLabelOffset = 12.0,
            ShowHandles = false,
            ShowSizeLabel = false,
            ShowCoordLabel = false,
            ShowOverlay = false,
            ShowOuterBorder = false,
            ShowFullOverlay = true,
            ShowInnerFill = true,
            ShowInnerBorder = true,
            OuterBorderThickness = 3.0,
            InnerBorderThickness = 4.0,
        };

        logger.LogAppearanceChanged(MarqueeAppearance.Default, after);

        Assert.Equal(26, logger.Messages.Count);
    }

    [Fact]
    public void LogSettingChanged_定義名と出力書式を固定する()
    {
        var logger = new RecordingLogger();

        logger.LogSettingChanged("DisplayScaleScaling", 0.0, 1.25);

        Assert.Equal("[Marquee.Presenter] Setting: DisplayScaleScaling 0 → 1.25", Assert.Single(logger.Messages));
    }
}
