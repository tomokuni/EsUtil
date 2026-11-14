using Xunit;
using DrawingColor = System.Drawing.Color;

namespace EsUtil.Feature.Loupe.Tests;

/// <summary><see cref="LoupePresenter"/> の単体テスト（外観の保持・変更の記録）。</summary>
/// <remarks>
/// 提示層が単一所有するもの（外観・描画計画）のうち、外観の保持と変更ログ、
/// およびコアが所有する設定の変更ログを検証する（描画計画の解決は <see cref="LoupeDrawPlanTests"/> が担う）。<br/>
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

        Assert.Equal("[Loupe] 設定: ShowCrosshair True → False", Assert.Single(logger.Messages));
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
                "[Loupe] ファサードを作成しました: RequestedSize=160, ZoomLevel=4",
                "[Loupe] ロガーを接続しました",
                "[Loupe] 提示層を作成しました",
                "[Loupe] ロガーを接続しました",
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
                "[Loupe] ファサードを作成しました: RequestedSize=160, ZoomLevel=4",
                "[Loupe] ロガーを接続しました",
                "[Loupe] 提示層を作成しました",
            ],
            logger.Messages);

        // 注入後に同じロガーを設定し直しても生成の記録は繰り返さない（ロガーを接続しました だけが増える）
        presenter.Log = logger;

        Assert.Equal(4, logger.Messages.Count);
    }

    /// <summary>コンストラクターでロガーを注入した場合も、変更が記録されることを検証する。</summary>
    [Fact]
    internal void 生成_コンストラクター注入後も変更を記録する()
    {
        var logger = new RecordingLogger();
        var presenter = new LoupePresenter(logger);
        logger.Messages.Clear();

        presenter.Options = LoupeOptions.Default with { ZoomLevel = 8 };
        presenter.Appearance = LoupeAppearance.Default with { ShowCrosshair = false };

        Assert.Equal(
            [
                "[Loupe] 設定: ZoomLevel 4 → 8",
                "[Loupe] 設定: ShowCrosshair True → False",
            ],
            logger.Messages);
    }

    /// <summary>設定（<see cref="LoupeOptions"/>）の変更がコアのログ定義で記録されることを検証する。</summary>
    /// <remarks>
    /// 設定を所有するのはコアであり、記録もコアが行う（提示層は委譲するだけである）。<br/>
    /// ログの出力先は 1 つであり、提示層の <c>Log</c> がコアの出力先を設定する。
    /// </remarks>
    [Fact]
    internal void 設定_コアのログ定義で記録する()
    {
        var presenter = new LoupePresenter();
        var logger = new RecordingLogger();
        presenter.Log = logger;
        logger.Messages.Clear();

        presenter.Options = LoupeOptions.Default with { ZoomLevel = 8 };

        Assert.Equal(
            ["[Loupe] 設定: ZoomLevel 4 → 8"],
            logger.Messages);
    }
}
