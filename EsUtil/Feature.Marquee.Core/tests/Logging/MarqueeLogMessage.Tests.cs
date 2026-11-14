using System.Globalization;
using EsUtil.Feature.Marquee.Logging;
using EsUtil.Platform.Core.Geometry;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using DrawingColor = System.Drawing.Color;

namespace EsUtil.Feature.Marquee.Tests.Logging;

/// <summary><see cref="MarqueeLogMessage"/> の LoggerMessage 生成コード（IsEnabled 両パス）を検証するテスト。</summary>
/// <remarks>
/// ログ定義は internal のため、<see cref="RecordingLogger"/>（IsEnabled=true）と<br/>
/// <see cref="NullLogger"/>（IsEnabled=false）の両方で全メソッドを直接呼び、<br/>
/// LoggerMessage 生成コードの state 走査と IsEnabled 分岐の両パスを網羅する。
/// </remarks>
public class MarqueeLogMessageTests
{
    /// <summary>実ロガー設定時に全ログメソッドが実行されることを検証する。</summary>
    [Fact]
    internal void 実ロガー設定時に全ログメソッドが実行される()
    {
        var logger = new RecordingLogger();
        InvokeAll(logger);

        Assert.True(logger.IsEnabled(LogLevel.Trace));
        Assert.Equal(10, logger.Messages.Count);
    }

    /// <summary>無効ロガーでも全ログメソッドが安全に実行されることを検証する。</summary>
    [Fact]
    internal void 無効ロガーでも全ログメソッドが安全に実行される()
    {
        // IsEnabled=false（NullLogger）でも生成コードの IsEnabled 分岐（false パス）が実行される
        InvokeAll(NullLogger.Instance);
    }

    /// <summary>出力書式が両 UI で共通の書式と一致することを検証する。</summary>
    [Fact]
    internal void 出力書式が期待どおりである()
    {
        var logger = new RecordingLogger();
        InvokeAll(logger);

        Assert.Equal("[Marquee] マウス押下: 位置=(10, 20) クリック回数=1 選択あり=False", logger.Messages[0]);
        Assert.Equal("[Marquee] マウス解放: 位置=(10, 20) ドラッグ中=True 選択あり=True 範囲=(10, 20, 30, 40)", logger.Messages[1]);
        Assert.Equal("[Marquee] ダブルクリック: 位置=(10, 20) 内側=True", logger.Messages[2]);
        Assert.Equal("[Marquee] マウス押下前の位置: 位置=(10, 20)", logger.Messages[3]);
        Assert.Equal("[Marquee] マウス解放後の位置: 位置=(10, 20)", logger.Messages[4]);
        Assert.Equal("[Marquee] ドラッグ開始: ハンドル=Move 位置=(10, 20)", logger.Messages[5]);
        Assert.Equal("[Marquee] キー押下: キー=Left 処理済み=True", logger.Messages[6]);
        Assert.Equal("[Marquee] 確定が要求されました: 範囲=(10, 20, 30, 40)", logger.Messages[7]);
        Assert.Equal("[Marquee] キャンセルが要求されました", logger.Messages[8]);
        Assert.Equal("[Marquee] 設定: MinSelectionSize 3 → 5", logger.Messages[9]);
    }

    /// <summary>設定（MarqueeOptions）の変更ログが変化した項目だけを出力することを検証する。</summary>
    /// <remarks>設定を 1 つの値へ統合したため、変更された項目の判定（差分検出）はログ定義が単一所有する。</remarks>
    [Fact]
    internal void 設定の変更ログは変化した項目だけを出力する()
    {
        var logger = new RecordingLogger();

        MarqueeLogMessage.LogOptionsChanged(
            logger,
            MarqueeOptions.Default,
            MarqueeOptions.Default with { HandleSize = 20.0, MarkerLength = 24.0 });
        MarqueeLogMessage.LogOptionsChanged(logger, MarqueeOptions.Default, MarqueeOptions.Default);

        Assert.Equal(
            [
                "[Marquee] 設定: HandleSize 10 → 20",
                "[Marquee] 設定: MarkerLength 16 → 24",
            ],
            logger.Messages);
    }

    /// <summary>数値の変更ログが同値なら出力せず、カルチャに依存しないことを検証する。</summary>
    [Fact]
    internal void 数値の変更ログは同値なら出力せずカルチャに依存しない()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            // 小数の区切りがコンマのカルチャでも不変の表記（ピリオド）で出力する
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");
            var logger = new RecordingLogger();

            MarqueeLogMessage.LogSettingChanged(logger, "HandleSize", 10.0, 20.0);
            MarqueeLogMessage.LogSettingChanged(logger, "HandleSize", 20.0, 20.0);
            MarqueeLogMessage.LogSettingChanged(logger, "MinSelectionSize", 6, 7);
            MarqueeLogMessage.LogSettingChanged(logger, "ClampToSourceBounds", true, true);

            Assert.Equal(
                [
                    "[Marquee] 設定: HandleSize 10 → 20",
                    "[Marquee] 設定: MinSelectionSize 6 → 7",
                ],
                logger.Messages);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    internal void LogAppearanceChanged_変化した項目だけを記録する()
    {
        var logger = new RecordingLogger();
        var before = MarqueeAppearance.Default;
        var after = before with { BorderThickness = 5, ShowHandles = false };

        logger.LogAppearanceChanged(before, after);

        Assert.Equal(
            [
                "[Marquee] 設定: ShowHandles True → False",
                "[Marquee] 設定: BorderThickness 2 → 5",
            ],
            logger.Messages);
    }

    [Fact]
    internal void LogAppearanceChanged_同値なら記録しない()
    {
        var logger = new RecordingLogger();
        var before = MarqueeAppearance.Default;

        logger.LogAppearanceChanged(before, before with { });

        Assert.Empty(logger.Messages);
    }

    [Fact]
    internal void LogAppearanceChanged_色はAARRGGBB形式で記録する()
    {
        var logger = new RecordingLogger();
        var before = MarqueeAppearance.Default;
        var after = before with { OuterMaskColor = DrawingColor.FromArgb(255, 0, 0, 0) };

        logger.LogAppearanceChanged(before, after);

        Assert.Equal(
            "[Marquee] 設定: OuterMaskColor #80000000 → #FF000000",
            Assert.Single(logger.Messages));
    }

    [Fact]
    internal void LogAppearanceChanged_外観の全項目を対象にする()
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
            OuterMaskColor = DrawingColor.FromArgb(8, 0, 0, 0),
            BorderColor = DrawingColor.FromArgb(9, 0, 0, 0),
            InnerMaskColor = DrawingColor.FromArgb(10, 0, 0, 0),
            SizeLabelFontSize = 13.0,
            CoordLabelFontSize = 11.0,
            LabelPadding = 5.0,
            MarkerThickness = 3.0,
            CoordLabelOffset = 12.0,
            ShowHandles = false,
            ShowSizeLabel = false,
            ShowCoordLabel = false,
            ShowOuterMask = false,
            ShowBorder = false,
            ShowInnerMask = true,
            BorderThickness = 3.0,
        };

        logger.LogAppearanceChanged(MarqueeAppearance.Default, after);

        Assert.Equal(22, logger.Messages.Count);
    }

    [Fact]
    internal void LogSettingChanged_定義名と出力書式を固定する()
    {
        var logger = new RecordingLogger();

        logger.LogSettingChanged("DisplayScaleScaling", 0.0, 1.25);

        Assert.Equal("[Marquee] 設定: DisplayScaleScaling 0 → 1.25", Assert.Single(logger.Messages));
    }

    /// <summary>全ログメソッドを指定ロガーで呼び出す。</summary>
    /// <param name="logger">ロガー。</param>
    private static void InvokeAll(ILogger logger)
    {
        MarqueeLogMessage.LogMouseDown(logger, 10, 20, 1, false);
        MarqueeLogMessage.LogMouseUp(logger, 10, 20, true, true, new EsRect(10, 20, 30, 40));
        MarqueeLogMessage.LogDoubleClick(logger, 10, 20, true);
        MarqueeLogMessage.LogPositionBeforeDown(logger, 10, 20);
        MarqueeLogMessage.LogPositionAfterUp(logger, 10, 20);
        MarqueeLogMessage.LogDragStart(logger, "Move", 10, 20);
        MarqueeLogMessage.LogKeyDown(logger, "Left", true);
        MarqueeLogMessage.LogConfirmRequested(logger, new EsRect(10, 20, 30, 40));
        MarqueeLogMessage.Logキャンセルが要求されました(logger);
        MarqueeLogMessage.LogSettingChanged(logger, "MinSelectionSize", "3", "5");
    }
}
