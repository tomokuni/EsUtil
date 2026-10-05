using EsUtil.Feature.Loupe.Presenter.Logging;
using Xunit;
using DrawingColor = System.Drawing.Color;

namespace EsUtil.Feature.Loupe.Tests.Presenter.Logging;

/// <summary><see cref="LoupeLogMessage"/> の単体テスト（外観の変更ログ）。</summary>
/// <remarks>
/// 「値を所有する主体が記録する」規則により、提示層が単一所有する外観（<see cref="LoupeAppearance"/>）の変更を<br/>
/// 本層が記録する。<b>変化した項目だけ</b>を出力する（外観の再適用でログが溢れないため）。<br/>
/// ログは UI 初期化なしで検証できる（LoggerMessage の定義は UI フレームワークに依存しない）。
/// </remarks>
public class LoupeLogMessageTests
{
    [Fact]
    internal void LogAppearanceChanged_変化した項目だけを記録する()
    {
        var logger = new RecordingLogger();
        var before = LoupeAppearance.Default;
        var after = before with { BorderThickness = 5, ShowCrosshair = false };

        logger.LogAppearanceChanged(before, after);

        Assert.Equal(
            [
                "[Loupe] Setting: ShowCrosshair True → False",
                "[Loupe] Setting: BorderThickness 2 → 5",
            ],
            logger.Messages);
    }

    [Fact]
    internal void LogAppearanceChanged_同値なら記録しない()
    {
        var logger = new RecordingLogger();
        var before = LoupeAppearance.Default;

        logger.LogAppearanceChanged(before, before with { });

        Assert.Empty(logger.Messages);
    }

    [Fact]
    internal void LogAppearanceChanged_色はAARRGGBB形式で記録する()
    {
        var logger = new RecordingLogger();
        var before = LoupeAppearance.Default;
        var after = before with { CrossColor = DrawingColor.FromArgb(128, 255, 0, 0) };

        logger.LogAppearanceChanged(before, after);

        Assert.Equal(
            "[Loupe] Setting: CrossColor #66FF0000 → #80FF0000",
            Assert.Single(logger.Messages));
    }

    [Fact]
    internal void LogAppearanceChanged_複数項目の変更を定義順に記録する()
    {
        var logger = new RecordingLogger();
        var before = LoupeAppearance.Default;
        var after = before with
        {
            PasteboardColor = DrawingColor.FromArgb(1, 2, 3, 4),
            ShowBorder = false,
            ShowCoordLabel = false,
        };

        logger.LogAppearanceChanged(before, after);

        Assert.Equal(3, logger.Messages.Count);
        Assert.Equal("[Loupe] Setting: PasteboardColor #FF535353 → #01020304", logger.Messages[0]);
        Assert.Equal("[Loupe] Setting: ShowBorder True → False", logger.Messages[1]);
        Assert.Equal("[Loupe] Setting: ShowCoordLabel True → False", logger.Messages[2]);
    }

    [Fact]
    internal void LogAppearanceChanged_外観の全項目を対象にする()
    {
        var logger = new RecordingLogger();

        // 全項目を変更した場合、定義した項目数ぶんのログが出る（項目の追加漏れを検出する）
        var after = LoupeAppearance.Default with
        {
            PasteboardColor = DrawingColor.FromArgb(1, 0, 0, 0),
            BorderColor = DrawingColor.FromArgb(2, 0, 0, 0),
            CrossColor = DrawingColor.FromArgb(3, 0, 0, 0),
            CoordLabelColor = DrawingColor.FromArgb(4, 0, 0, 0),
            CoordLabelBackgroundColor = DrawingColor.FromArgb(5, 0, 0, 0),
            CoordLabelFontSize = 13.0,
            CoordLabelCornerRadius = 3,
            CoordLabelOffset = 4.0,
            ShowCrosshair = false,
            ShowBorder = false,
            BorderThickness = 5,
            ShowCoordLabel = false,
        };

        logger.LogAppearanceChanged(LoupeAppearance.Default, after);

        Assert.Equal(12, logger.Messages.Count);
    }

    [Fact]
    internal void LogAppearanceChanged_実数項目をカルチャに依存せず記録する()
    {
        var logger = new RecordingLogger();
        var before = LoupeAppearance.Default;
        var after = before with { CoordLabelFontSize = 24.5 };

        logger.LogAppearanceChanged(before, after);

        Assert.Equal(
            "[Loupe] Setting: CoordLabelFontSize 12 → 24.5",
            Assert.Single(logger.Messages));
    }

    [Fact]
    internal void LogSettingChanged_定義名と出力書式を固定する()
    {
        var logger = new RecordingLogger();

        logger.LogSettingChanged("ClientToPixelScaling", 1.0, 1.25);

        Assert.Equal("[Loupe] Setting: ClientToPixelScaling 1 → 1.25", Assert.Single(logger.Messages));
    }
}
