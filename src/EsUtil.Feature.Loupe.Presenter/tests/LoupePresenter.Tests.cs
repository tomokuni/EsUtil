using EsUtil.Platform.Core.Coord;
using EsUtil.Platform.Core.Geometry;
using Microsoft.Extensions.Logging;
using Xunit;
using DrawingColor = System.Drawing.Color;

namespace EsUtil.Feature.Loupe.Tests;

/// <summary><see cref="LoupePresenter"/> の単体テスト（外観の保持・変更の記録・コアへの伝搬）。</summary>
/// <remarks>
/// 提示層が単一所有するもの（外観・描画計画）のうち、外観の保持と変更ログ、および
/// コアへ伝搬する値の通過の記録を検証する（描画計画の解決は <see cref="LoupeDrawPlanTests"/> が担う）。<br/>
/// UI フレームワークに依存しないため、UI 初期化なしの <c>[Fact]</c> で並列検証できる。
/// </remarks>
public class LoupePresenterTests
{
    /// <summary>外観が既定で設定され、差し替えが保持されることを検証する。</summary>
    [Fact]
    internal void 外観_既定はDefaultで差し替えが保持される()
    {
        var presenter = new LoupePresenter();
        Assert.Equal(LoupeAppearance.Default, presenter.Appearance);

        var appearance = LoupeAppearance.Default with { ShowCrosshair = false, CrossColor = DrawingColor.Red };
        presenter.Appearance = appearance;

        Assert.Equal(appearance, presenter.Appearance);
    }

    /// <summary>外観の変更が変化した項目として記録されることを検証する。</summary>
    /// <remarks>
    /// 「値を所有する主体が記録する」規則により、提示層が保持する外観の変更は提示層が記録する<br/>
    /// （ホストは前後の値を渡さず、同値判定・差分検出を持たない）。
    /// </remarks>
    [Fact]
    internal void 外観_変更した項目だけを記録する()
    {
        var presenter = new LoupePresenter();
        var logger = new RecordingLogger();
        presenter.Log = logger;
        logger.Messages.Clear();

        presenter.Appearance = LoupeAppearance.Default with { ShowCrosshair = false };

        Assert.Equal("[Loupe] Setting: ShowCrosshair True → False", Assert.Single(logger.Messages));
    }

    /// <summary>同値の再設定では記録しないことを検証する。</summary>
    [Fact]
    internal void 外観_同値の再設定では記録しない()
    {
        var presenter = new LoupePresenter();
        var logger = new RecordingLogger();
        presenter.Log = logger;
        logger.Messages.Clear();

        presenter.Appearance = LoupeAppearance.Default with { };

        Assert.Empty(logger.Messages);
    }

    /// <summary>本層の生成がロガーの設定時に 1 度だけ記録されることを検証する（注入しない場合）。</summary>
    /// <remarks>生成時に出力先が未設定のため、ロガーが設定された時点で記録する。</remarks>
    [Fact]
    internal void Log_本層の生成はロガーの設定時に一度だけ記録する()
    {
        var presenter = new LoupePresenter();
        var logger = new RecordingLogger();

        presenter.Log = logger;
        presenter.Log = logger;   // 再設定（生成の記録は繰り返さない）

        Assert.Equal(
            [
                "[Loupe] Facade created: RequestedSize=160, ZoomLevel=4",
                "[Loupe] Logger attached",
                "[Loupe] Presenter created",
                "[Loupe] Logger attached",
                "[Loupe] Logger attached",
                "[Loupe] Logger attached",
            ],
            logger.Messages);
    }

    /// <summary>コンストラクターでロガーを注入すると、その場で本層とコアの生成が記録されることを検証する。</summary>
    /// <remarks>注入時は出力先が確定しているため、コンストラクター呼び出しの記録をその場で出力する。</remarks>
    [Fact]
    internal void 生成_コンストラクターでロガーを注入すると生成を記録する()
    {
        var logger = new RecordingLogger();

        var presenter = new LoupePresenter(logger);

        Assert.Equal(
            [
                "[Loupe] Facade created: RequestedSize=160, ZoomLevel=4",
                "[Loupe] Logger attached",
                "[Loupe] Presenter created",
                "[Loupe] Logger attached",
            ],
            logger.Messages);

        // 注入後に同じロガーを設定し直しても生成の記録は繰り返さない
        presenter.Log = logger;

        Assert.Equal(6, logger.Messages.Count);
    }

    /// <summary>コンストラクターでロガーを注入した場合も、通過と変更が記録されることを検証する。</summary>
    [Fact]
    internal void 生成_コンストラクター注入後も通過と変更を記録する()
    {
        var logger = new RecordingLogger();
        var presenter = new LoupePresenter(logger);
        logger.Messages.Clear();

        presenter.Options = LoupeOptions.Default with { ZoomLevel = 8 };
        presenter.Appearance = LoupeAppearance.Default with { ShowCrosshair = false };

        Assert.Equal(
            [
                "[Loupe] Setting: ZoomLevel 4 → 8",
                "[Loupe] Forwarded: Options RequestedSize=160, ZoomLevel=8",
                "[Loupe] Setting: ShowCrosshair True → False",
            ],
            logger.Messages);
    }

    /// <summary>コアへ伝搬するプロパティが本層の通過として Trace で記録されることを検証する。</summary>
    /// <remarks>
    /// 値の保持と変更の記録はコアが行うため、本層は「通過したこと」だけを残す。<br/>
    /// 値が変化した場合はコアの変更ログも出力される（コアの拡大中心も Trace のため）、<br/>
    /// 本層の接頭辞を持つ Trace の出力だけを抽出して検証する。
    /// </remarks>
    [Fact]
    internal void コアへ伝搬する値は通過をTraceで記録する()
    {
        var presenter = new LoupePresenter();
        var logger = new RecordingLogger();
        presenter.Log = logger;
        logger.Messages.Clear();
        logger.Levels.Clear();

        presenter.Options = LoupeOptions.Default with { RequestedSize = 200, ZoomLevel = 8 };
        presenter.ClientToPixelScaling = 1.25;
        presenter.CenterPoint = new EsPoint(10, 20);
        presenter.PositionOffset = 16;
        presenter.SetCoordTransform(new CoordTransform(p => p, p => p, () => null));

        var traceMessages = logger.Messages
            .Zip(logger.Levels, static (message, level) => (message, level))
            .Where(static entry =>
                entry.level == LogLevel.Trace &&
                entry.message.Contains("Forwarded: ", StringComparison.Ordinal))
            .Select(static entry => entry.message);

        Assert.Equal(
            [
                "[Loupe] Forwarded: Options RequestedSize=200, ZoomLevel=8",
                "[Loupe] Forwarded: ClientToPixelScaling 1.25",
                "[Loupe] Forwarded: CenterPoint (10, 20)",
                "[Loupe] Forwarded: PositionOffset 16",
                "[Loupe] Forwarded: SetCoordTransform CoordTransform",
            ],
            traceMessages);
    }

    /// <summary>設定（<see cref="LoupeOptions"/>）の変更がコアのログ定義で記録されることを検証する。</summary>
    /// <remarks>
    /// 設定を所有するのはコアであり、記録もコアが行う（提示層は委譲するだけである）。<br/>
    /// ログの出力先は提示層の <c>Log</c> からコアへ伝搬する。
    /// </remarks>
    [Fact]
    internal void 設定_コアのログ定義で記録する()
    {
        var presenter = new LoupePresenter();
        var logger = new RecordingLogger();
        presenter.Log = logger;
        logger.Messages.Clear();

        presenter.Options = LoupeOptions.Default with { ZoomLevel = 8 };

        // コアの変更ログ（Debug）と本層の通過ログ（Trace）がこの順で出力される
        Assert.Equal(
            [
                "[Loupe] Setting: ZoomLevel 4 → 8",
                "[Loupe] Forwarded: Options RequestedSize=160, ZoomLevel=8",
            ],
            logger.Messages);
    }
}
