using System.Globalization;
using EsUtil.Platform.Core.Coord;
using EsUtil.Platform.Core.Geometry;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using EsUtil.Feature.Loupe.Core.Logging;

namespace EsUtil.Feature.Loupe.Tests.Core.Logging;

/// <summary><see cref="LoupeLogMessage"/> の LoggerMessage 生成コード（IsEnabled 両パス）を検証するテスト。</summary>
/// <remarks>
/// ログ定義はコアが出力するものだけを本コアが持つ（UI が出力する定義はコントロール化の層が持つ）。<br/>
/// <see cref="RecordingLogger"/>（IsEnabled=true）と <see cref="NullLogger"/>（IsEnabled=false）の両方で<br/>
/// 全メソッドを直接呼び、LoggerMessage 生成コードの state 走査と IsEnabled 分岐の両パスを網羅する。
/// </remarks>
public class LoupeLogMessageTests
{
    /// <summary>実ロガー設定時に全ログメソッドが実行されることを検証する。</summary>
    [Fact]
    internal void 実ロガー設定時に全ログメソッドが実行される()
    {
        var logger = new RecordingLogger();
        InvokeAll(logger);

        Assert.True(logger.IsEnabled(LogLevel.Trace));
        Assert.Equal(6, logger.Messages.Count);
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

        Assert.Equal("[Loupe] Facade created: RequestedSize=160, ZoomLevel=4", logger.Messages[0]);
        Assert.Equal("[Loupe] Logger attached", logger.Messages[1]);
        Assert.Equal("[Loupe] Setting: ZoomLevel 4 → 8", logger.Messages[2]);
        Assert.Equal("[Loupe] Setting: RequestedSize 160 → 240", logger.Messages[3]);
        Assert.Equal("[Loupe] Setting: CenterPoint (1.5, 2.5) → null", logger.Messages[4]);
        Assert.Equal("[Loupe] Setting: ZoomLevel 4 → 8", logger.Messages[5]);
    }

    /// <summary>生成の記録が Information、ロガー設定の記録が Debug で出力されることを検証する。</summary>
    /// <remarks>生成はホストがログを設定した時点で 1 度だけ記録する（構築時は出力先が未設定のため）。</remarks>
    [Fact]
    internal void 生成とロガー設定の記録レベルが期待どおりである()
    {
        var logger = new RecordingLogger();

        logger.LogFacadeCreated(160, 4);
        logger.LogLoggerAttached();

        Assert.Equal([LogLevel.Information, LogLevel.Debug], logger.Levels);
    }

    /// <summary>拡大中心の記録が最も低いレベル（Trace）で出力されることを検証する。</summary>
    /// <remarks>中心はマウス移動ごとに変わるため、既定の出力では溢れさせない。</remarks>
    [Fact]
    internal void 拡大中心の記録はTraceで出力する()
    {
        var logger = new RecordingLogger();

        logger.LogCenterPointChanged(new EsPoint(10, 20), new EsPoint(30, 40));

        Assert.Equal([LogLevel.Trace], logger.Levels);
        Assert.Equal("[Loupe] Setting: CenterPoint (10, 20) → (30, 40)", Assert.Single(logger.Messages));
    }

    /// <summary>座標変換ポートの変更が未設定と実装型名で記録されることを検証する。</summary>
    [Fact]
    internal void 座標変換ポートの変更は未設定と実装型名で記録する()
    {
        var logger = new RecordingLogger();
        var port = new CoordTransform(p => p, p => p, () => null);

        logger.LogCoordTransformChanged(null, port);
        logger.LogCoordTransformChanged(port, port);   // 同値 → 出力しない
        logger.LogCoordTransformChanged(port, null);

        Assert.Equal(
            [
                "[Loupe] Setting: CoordTransform null → CoordTransform",
                "[Loupe] Setting: CoordTransform CoordTransform → null",
            ],
            logger.Messages);
    }

    /// <summary>拡大中心のログが両 UI 共通の書式（括弧付き座標）で出力されることを検証する。</summary>
    [Fact]
    internal void 拡大中心のログは括弧付き座標で出力する()
    {
        var logger = new RecordingLogger();

        logger.LogCenterPointChanged(new EsPoint(10, 20), null);
        logger.LogCenterPointChanged(null, new EsPoint(30, 40));

        Assert.Equal("[Loupe] Setting: CenterPoint (10, 20) → null", logger.Messages[0]);
        Assert.Equal("[Loupe] Setting: CenterPoint null → (30, 40)", logger.Messages[1]);
    }

    /// <summary>真偽設定値のログが変化したときだけ出力されることを検証する。</summary>
    /// <remarks>
    /// 表示可否（<c>Show*</c>）は真偽値のため、数値と同じ書式（<c>True</c> / <c>False</c>）で出力する<br/>
    /// （<c>bool</c> は <see cref="IFormattable"/> を実装しないため本体の汎用版とは別の経路になる）。
    /// </remarks>
    [Fact]
    internal void 真偽設定値のログは変化したときだけ出力する()
    {
        var logger = new RecordingLogger();

        logger.LogSettingChanged("ShowBorder", true, false);   // 変化 → 出力
        logger.LogSettingChanged("ShowBorder", false, false);  // 同値 → 出力しない

        Assert.Equal("[Loupe] Setting: ShowBorder True → False", Assert.Single(logger.Messages));
    }

    /// <summary>数値設定値のログが変化したときだけ出力されることを検証する。</summary>
    [Fact]
    internal void 数値設定値のログは変化したときだけ出力する()
    {
        var logger = new RecordingLogger();

        logger.LogSettingChanged("ZoomLevel", 4, 8);          // 変化 → 出力
        logger.LogSettingChanged("ZoomLevel", 8, 8);          // 同値 → 出力しない
        logger.LogSettingChanged("ClientToPixelScaling", 1.0, 1.5);  // 変化 → 出力
        logger.LogSettingChanged("ClientToPixelScaling", 1.5, 1.5);  // 同値 → 出力しない

        Assert.Equal(2, logger.Messages.Count);
        Assert.Equal("[Loupe] Setting: ZoomLevel 4 → 8", logger.Messages[0]);
        Assert.Equal("[Loupe] Setting: ClientToPixelScaling 1 → 1.5", logger.Messages[1]);
    }

    /// <summary>数値のログがカルチャに依存しないことを検証する。</summary>
    [Fact]
    internal void 数値のログはカルチャに依存しない()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            // 小数の区切りがコンマのカルチャでも不変の表記（ピリオド）で出力する
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");
            var logger = new RecordingLogger();

            logger.LogSettingChanged("ClientToPixelScaling", 1.0, 2.5);

            Assert.Equal("[Loupe] Setting: ClientToPixelScaling 1 → 2.5", logger.Messages[0]);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    /// <summary>拡大中心のログが同値の再設定では出力されないことを検証する。</summary>
    /// <remarks>中心はマウス移動のたびに設定されるため、同値判定はコアが単一所有する。</remarks>
    [Fact]
    internal void 拡大中心のログは同値なら出力しない()
    {
        var logger = new RecordingLogger();

        logger.LogCenterPointChanged(new EsPoint(10, 20), new EsPoint(10, 20));
        logger.LogCenterPointChanged(null, null);

        Assert.Empty(logger.Messages);
    }

    /// <summary>設定（LoupeOptions）の変更ログが変化した項目だけを出力することを検証する。</summary>
    /// <remarks>設定を 1 つの値へ統合したため、変更された項目の判定（差分検出）はコアが単一所有する。</remarks>
    [Fact]
    internal void 設定の変更ログは変化した項目だけを出力する()
    {
        var logger = new RecordingLogger();
        var oldValue = new LoupeOptions();

        logger.LogOptionsChanged(oldValue, oldValue with { RequestedSize = 240, ZoomLevel = 8 });

        Assert.Equal(
            [
                "[Loupe] Setting: RequestedSize 160 → 240",
                "[Loupe] Setting: ZoomLevel 4 → 8",
            ],
            logger.Messages);
    }

    /// <summary>設定の変更ログが全項目を対象にすることを検証する。</summary>
    /// <remarks>
    /// 各項目のログが同値判定を行うため、全項目を対象にしても変化した項目しか出力されない<br/>
    /// （対象外の項目を設けると、その項目の変更だけがログから漏れる）。
    /// </remarks>
    [Fact]
    internal void 設定の変更ログは全項目を対象にする()
    {
        var logger = new RecordingLogger();
        var oldValue = new LoupeOptions();
        var newValue = oldValue with
        {
            RequestedSize = 240,
            ZoomLevel = 8,
        };

        logger.LogOptionsChanged(oldValue, newValue);

        Assert.Equal(
            [
                "[Loupe] Setting: RequestedSize 160 → 240",
                "[Loupe] Setting: ZoomLevel 4 → 8",
            ],
            logger.Messages);
    }

    /// <summary>全ログメソッドを指定ロガーで呼び出す。</summary>
    /// <param name="logger">ロガー。</param>
    /// <remarks>出力書式の検証がメッセージの並びに依存するため、呼び出す順序を固定する。</remarks>
    private static void InvokeAll(ILogger logger)
    {
        logger.LogFacadeCreated(160, 4);                                              // 1
        logger.LogLoggerAttached();                                                   // 2
        logger.LogSettingChanged("ZoomLevel", "4", "8");                             // 3
        logger.LogSettingChanged("RequestedSize", 160, 240);                          // 4
        logger.LogCenterPointChanged(new EsPoint(1.5, 2.5), null);                    // 5
        logger.LogCoordTransformChanged(null, null);                                  // 6（同値のため出力しない）
        logger.LogOptionsChanged(new LoupeOptions(), new LoupeOptions { ZoomLevel = 8 });  // 7
    }
}
