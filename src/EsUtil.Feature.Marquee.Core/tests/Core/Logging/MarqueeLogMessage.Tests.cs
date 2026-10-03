using System.Globalization;
using EsUtil.Platform.Core.Geometry;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using EsUtil.Feature.Marquee.Core.Logging;

namespace EsUtil.Feature.Marquee.Tests.Core.Logging;

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
    public void 実ロガー設定時に全ログメソッドが実行される()
    {
        var logger = new RecordingLogger();
        InvokeAll(logger);

        Assert.True(logger.IsEnabled(LogLevel.Trace));
        Assert.Equal(10, logger.Messages.Count);
    }

    /// <summary>無効ロガーでも全ログメソッドが安全に実行されることを検証する。</summary>
    [Fact]
    public void 無効ロガーでも全ログメソッドが安全に実行される()
    {
        // IsEnabled=false（NullLogger）でも生成コードの IsEnabled 分岐（false パス）が実行される
        InvokeAll(NullLogger.Instance);
    }

    /// <summary>出力書式が両 UI で共通の書式と一致することを検証する。</summary>
    [Fact]
    public void 出力書式が期待どおりである()
    {
        var logger = new RecordingLogger();
        InvokeAll(logger);

        Assert.Equal("[Marquee.Core] MouseDown: pos=(10, 20) clickCount=1 hasSelection=False", logger.Messages[0]);
        Assert.Equal("[Marquee.Core] MouseUp: pos=(10, 20) wasDragging=True hasSelection=True", logger.Messages[1]);
        Assert.Equal("[Marquee.Core] DoubleClick: pos=(10, 20) inside=True", logger.Messages[2]);
        Assert.Equal("[Marquee.Core] Position before MouseDown: pos=(10, 20)", logger.Messages[3]);
        Assert.Equal("[Marquee.Core] Position after MouseUp: pos=(10, 20)", logger.Messages[4]);
        Assert.Equal("[Marquee.Core] DragStart: handle=Move pos=(10, 20)", logger.Messages[5]);
        Assert.Equal("[Marquee.Core] KeyDown: key=Left handled=True", logger.Messages[6]);
        Assert.Equal("[Marquee.Core] ConfirmRequested: rect=(10, 20, 30, 40)", logger.Messages[7]);
        Assert.Equal("[Marquee.Core] CancelRequested", logger.Messages[8]);
        Assert.Equal("[Marquee.Core] Setting: MinSelectionSize 3 → 5", logger.Messages[9]);
    }

    /// <summary>設定（MarqueeOptions）の変更ログが変化した項目だけを出力することを検証する。</summary>
    /// <remarks>設定を 1 つの値へ統合したため、変更された項目の判定（差分検出）はログ定義が単一所有する。</remarks>
    [Fact]
    public void 設定の変更ログは変化した項目だけを出力する()
    {
        var logger = new RecordingLogger();

        MarqueeLogMessage.LogOptionsChanged(
            logger,
            MarqueeOptions.Default,
            MarqueeOptions.Default with { HandleSize = 20.0, MarkerLength = 24.0 });
        MarqueeLogMessage.LogOptionsChanged(logger, MarqueeOptions.Default, MarqueeOptions.Default);

        Assert.Equal(
            [
                "[Marquee.Core] Setting: HandleSize 10 → 20",
                "[Marquee.Core] Setting: MarkerLength 16 → 24",
            ],
            logger.Messages);
    }

    /// <summary>数値の変更ログが同値なら出力せず、カルチャに依存しないことを検証する。</summary>
    [Fact]
    public void 数値の変更ログは同値なら出力せずカルチャに依存しない()
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
                    "[Marquee.Core] Setting: HandleSize 10 → 20",
                    "[Marquee.Core] Setting: MinSelectionSize 6 → 7",
                ],
                logger.Messages);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    /// <summary>全ログメソッドを指定ロガーで呼び出す。</summary>
    /// <param name="logger">ロガー。</param>
    private static void InvokeAll(ILogger logger)
    {
        MarqueeLogMessage.LogMouseDown(logger, 10, 20, 1, false);
        MarqueeLogMessage.LogMouseUp(logger, 10, 20, true, true);
        MarqueeLogMessage.LogDoubleClick(logger, 10, 20, true);
        MarqueeLogMessage.LogPositionBeforeDown(logger, 10, 20);
        MarqueeLogMessage.LogPositionAfterUp(logger, 10, 20);
        MarqueeLogMessage.LogDragStart(logger, "Move", 10, 20);
        MarqueeLogMessage.LogKeyDown(logger, "Left", true);
        MarqueeLogMessage.LogConfirmRequested(logger, new EsRect(10, 20, 30, 40));
        MarqueeLogMessage.LogCancelRequested(logger);
        MarqueeLogMessage.LogSettingChanged(logger, "MinSelectionSize", "3", "5");
    }
}
