using EsUtil.Platform.Core.Coord;
using EsUtil.Platform.Core.Geometry;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace EsUtil.Feature.Loupe.Tests;

/// <summary><see cref="LoupeCore"/>（公開ファサード）の単体テスト（表示レイアウト・描画入力・設定）。</summary>
/// <remarks>
/// UI 層とホストが使う唯一の窓口であるファサードの振る舞いを、UI フレームワークなしで検証する。<br/>
/// 本テストは「設定からの解決」「ポートとオフセットの適用」「描画入力の組み立て」に焦点を当てる。
/// </remarks>
public class LoupeCoreTests
{
    /// <summary>親領域のサイズ（表示サイズのクランプ基準）。</summary>
    private static readonly EsSize ParentSize = new(400, 300);

    /// <summary>対象 → 親領域 DIP を 2 倍で返す座標変換ポートを生成する。</summary>
    /// <returns>ToDip が 2 倍の座標変換ポート。</returns>
    private static ICoordTransform CreateDoublingPort()
        => new CoordTransform(p => new EsPoint(p.X / 2, p.Y / 2), p => new EsPoint(p.X * 2, p.Y * 2), () => null);

    // ====================================================================
    // 生成・設定
    // ====================================================================

    [Fact]
    internal void 生成_既定の設定を保持する()
    {
        var loupe = new LoupeCore();

        Assert.Same(LoupeOptions.Default, loupe.Options);
        Assert.Equal(LoupeCore.Limits.ClientToPixelScalingDefault, loupe.ClientToPixelScaling);
        Assert.Null(loupe.CenterPoint);
        Assert.Equal(0.0, loupe.PositionOffset);
    }

    [Fact]
    internal void 生成_指定した設定を保持する()
    {
        var options = new LoupeOptions { RequestedSize = 200, ZoomLevel = 8 };

        var loupe = new LoupeCore(options);

        Assert.Equal(200, loupe.Options.RequestedSize);
        Assert.Equal(8, loupe.Options.ZoomLevel);
    }

    [Theory]
    [InlineData(0.0, 0.1)]
    [InlineData(-1.0, 0.1)]
    [InlineData(100.0, 8.0)]
    [InlineData(double.NaN, 1.0)]
    [InlineData(double.PositiveInfinity, 1.0)]
    internal void ClientToPixelScaling_安全域外の指定は補正して保持する(double input, double expected)
    {
        // 補正の規則は LoupeCore.Limits が単一所有する（UI の依存関係プロパティを経由しない代入でも範囲内になる）
        var loupe = new LoupeCore() { ClientToPixelScaling = input };

        Assert.Equal(expected, loupe.ClientToPixelScaling);
    }

    // ====================================================================
    // 表示サイズ（親領域へのクランプ）
    // ====================================================================

    [Fact]
    internal void ResolveLayout_要求サイズをそのまま表示サイズにする()
    {
        var loupe = new LoupeCore(new LoupeOptions { RequestedSize = 200 });

        var layout = loupe.ResolveLayout(new EsSize(120, 300));

        // 要求サイズは領域に合わせて縮小しない
        Assert.Equal(200, layout.ClientSize);
    }

    [Fact]
    internal void ResolveLayout_親領域が未確定なら要求サイズと中心の位置を返す()
    {
        var loupe = new LoupeCore(new LoupeOptions { RequestedSize = 200 }) { CenterPoint = new EsPoint(500, 600) };

        var layout = loupe.ResolveLayout(default);

        Assert.Equal(200, layout.ClientSize);
        Assert.Equal(new EsPoint(500, 600), layout.Position);
    }

    [Fact]
    internal void ResolveLayout_設定変更で表示サイズを再導出する()
    {
        var loupe = new LoupeCore(new LoupeOptions { RequestedSize = 200 });

        loupe.Options = loupe.Options with { RequestedSize = 400 };

        Assert.Equal(400, loupe.ResolveLayout(ParentSize).ClientSize);
    }

    // ====================================================================
    // 表示レイアウト（位置）
    // ====================================================================

    [Fact]
    internal void ResolveLayout_中心未設定なら配置しない()
    {
        var loupe = new LoupeCore();

        var layout = loupe.ResolveLayout(ParentSize);

        Assert.Equal(160, layout.ClientSize);
        Assert.Null(layout.Position);
    }

    [Fact]
    internal void ResolveLayout_中心とオフセットから位置を求める()
    {
        var loupe = new LoupeCore() { CenterPoint = new EsPoint(50, 60), PositionOffset = 20 };

        var layout = loupe.ResolveLayout(ParentSize);

        Assert.Equal(new EsPoint(70, 80), layout.Position);
    }

    [Fact]
    internal void ResolveLayout_親領域の外へは配置しない()
    {
        var loupe = new LoupeCore() { CenterPoint = new EsPoint(390, 290) };

        var layout = loupe.ResolveLayout(ParentSize);

        // 親領域 400x300・表示サイズ 160 では左上の上限が (240, 140) になる
        Assert.Equal(new EsPoint(240, 140), layout.Position);
    }

    [Fact]
    internal void ResolveLayout_親領域が未確定ならクランプしない()
    {
        var loupe = new LoupeCore() { CenterPoint = new EsPoint(500, 600) };

        var layout = loupe.ResolveLayout(default);

        Assert.Equal(new EsPoint(500, 600), layout.Position);
    }

    [Fact]
    internal void ResolveLayout_座標変換ポートを適用する()
    {
        var loupe = new LoupeCore() { CenterPoint = new EsPoint(10, 20) };
        loupe.SetCoordTransform(CreateDoublingPort());

        var layout = loupe.ResolveLayout(ParentSize);

        Assert.Equal(new EsPoint(20, 40), layout.Position);
    }

    [Fact]
    internal void ResolveLayout_ポート未設定は等価変換になる()
    {
        var loupe = new LoupeCore() { CenterPoint = new EsPoint(10, 20) };

        loupe.SetCoordTransform(null);

        var layout = loupe.ResolveLayout(ParentSize);

        Assert.Equal(new EsPoint(10, 20), layout.Position);
    }

    /// <summary>中心 + オフセットが負の位置になる場合は 0 へクランプされることを検証する。</summary>
    [Fact]
    internal void ResolveLayout_負の位置は0にクランプする()
    {
        var loupe = new LoupeCore() { CenterPoint = new EsPoint(0, 0), PositionOffset = -50 };

        var layout = loupe.ResolveLayout(ParentSize);

        Assert.Equal(new EsPoint(0, 0), layout.Position);
    }

    /// <summary>親領域が表示サイズを収められない場合は配置しないことを検証する。</summary>
    [Fact]
    internal void ResolveLayout_親領域が収められない場合は配置しない()
    {
        // 要求サイズ 300 は親領域 100x100 に収まらない（縮小もしない）
        var loupe = new LoupeCore(LoupeOptions.Default with { RequestedSize = 300 })
        {
            CenterPoint = new EsPoint(0, 0),
            PositionOffset = -50,
        };

        var layout = loupe.ResolveLayout(new EsSize(100, 100));

        Assert.Equal(300, layout.ClientSize);
        Assert.Null(layout.Position);
    }

    // ====================================================================
    // 描画の幾何
    // ====================================================================

    [Fact]
    internal void ResolveGeometry_表示サイズと設定から幾何を組み立てる()
    {
        var options = LoupeOptions.Default with { RequestedSize = 160, ZoomLevel = 4 };
        var loupe = new LoupeCore(options)
        {
            CenterPoint = new EsPoint(10, 20),
            ClientToPixelScaling = 1.5,
        };

        var geometry = loupe.ResolveGeometry(160);

        Assert.Equal(new EsPoint(10, 20), geometry.Center);
        Assert.Equal(240, geometry.ClientPixelSize);
        Assert.Equal(4, geometry.ZoomLevel);
        Assert.Equal(1.5, geometry.ClientToPixelScaling);
    }

    [Fact]
    internal void ResolveGeometry_描画先の大きさで縮小しない()
    {
        var loupe = new LoupeCore(new LoupeOptions { RequestedSize = 300 });

        var geometry = loupe.ResolveGeometry(300);

        Assert.Equal(300, geometry.ClientPixelSize);
    }

    [Fact]
    internal void ResolveGeometry_表示サイズを物理ピクセルへ換算する()
    {
        var loupe = new LoupeCore(new LoupeOptions { RequestedSize = 300 });

        var layout = loupe.ResolveLayout(new EsSize(120, 400));
        var geometry = loupe.ResolveGeometry(layout.ClientSize);

        // 表示サイズ（DIP）は要求サイズのまま（縮小しない）、その値が物理ピクセルへ換算される
        Assert.Equal(300, geometry.ClientPixelSize);
    }

    [Fact]
    internal void ResolveGeometry_中心未設定でも組み立てられる()
    {
        var loupe = new LoupeCore();

        var geometry = loupe.ResolveGeometry(160);

        Assert.Null(geometry.Center);
    }

    // ====================================================================
    // 描画の幾何（切り出し領域・拡大率）
    // ====================================================================

    [Fact]
    internal void ResolveGeometry_切り出しの左上と拡大率を解決する()
    {
        var loupe = new LoupeCore(new LoupeOptions { RequestedSize = 160, ZoomLevel = 4 })
        {
            CenterPoint = new EsPoint(50, 60),
        };
        var geometry = loupe.ResolveGeometry(160);

        // 切り出し一辺 = 160 / 4 = 40 のため、左上は中心から 20 引いた位置になる
        Assert.Equal(new EsPoint(30, 40), geometry.CaptureTopLeft);
        Assert.Equal(4, geometry.ZoomLevel);
    }

    [Fact]
    internal void ResolveGeometry_中心未設定なら切り出し領域を返さない()
    {
        var loupe = new LoupeCore();
        var geometry = loupe.ResolveGeometry(160);

        Assert.Null(geometry.CaptureTopLeft);
        Assert.Equal(4, geometry.ZoomLevel);
    }

    /// <summary>拡大率が切り出しサイズの切り上げに影響されないことを検証する。</summary>
    /// <param name="clientPixelSize">ルーペの一辺のサイズ（物理ピクセル）。</param>
    /// <remarks>
    /// 実描画は切り出し領域（切り出しサイズの対象ピクセル）を <c>ZoomLevel</c> 倍で描くため、<br/>
    /// 変換係数は切り出しサイズで割った値（<c>物理ピクセル / 切り出しサイズ</c>）ではなく <c>ZoomLevel</c> になる。
    /// </remarks>
    [Theory]
    [InlineData(160)]
    [InlineData(150)]
    [InlineData(200)]
    internal void ResolveGeometry_拡大率は切り出しサイズの切り上げに影響されない(int clientPixelSize)
    {
        var loupe = new LoupeCore(new LoupeOptions { ZoomLevel = 4 }) { CenterPoint = new EsPoint(50, 60) };

        Assert.Equal(4, loupe.ResolveGeometry(clientPixelSize).ZoomLevel);
    }

    /// <summary>拡大率が DPI スケーリング係数に依存しないことを検証する。</summary>
    /// <param name="dipToDeviceScaling">表示 DIP → 物理ピクセルのスケーリング係数。</param>
    /// <remarks>
    /// 物理ピクセル数は DIP とスケーリングに依存するが、描画の倍率（対象 → 描画ピクセル）は変わらない。<br/>
    /// 破線オーバーレイの位置が DPI でずれないことを保証する。
    /// </remarks>
    [Theory]
    [InlineData(1.0)]
    [InlineData(1.25)]
    [InlineData(2.0)]
    internal void ResolveGeometry_拡大率はDPIスケーリングに依存しない(double dipToDeviceScaling)
    {
        var loupe = new LoupeCore(new LoupeOptions { ZoomLevel = 4 }) { CenterPoint = new EsPoint(50, 60) };
        var clientPixelSize = DpiScaler.ScaleToPixel(160, dipToDeviceScaling);

        Assert.Equal(4, loupe.ResolveGeometry(clientPixelSize).ZoomLevel);
    }

    /// <summary>表示レイアウト・描画の幾何の解決がヒープを確保しないことを検証する。</summary>
    /// <remarks>
    /// 描画のたび（マウス移動・スクロール）に呼ばれるため、値型だけで組み立てる。<br/>
    /// 計測は <see cref="GC.GetAllocatedBytesForCurrentThread"/> で行う。
    /// </remarks>
    [Fact]
    internal void レイアウトと描画の幾何の解決はヒープを確保しない()
    {
        var loupe = new LoupeCore() { CenterPoint = new EsPoint(50, 60) };
        loupe.SetCoordTransform(CreateDoublingPort());

        for (var i = 0; i < 100; i++)
        {
            loupe.ResolveLayout(ParentSize);
            loupe.ResolveGeometry(160);
        }

        GC.Collect();
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1000; i++)
        {
            loupe.ResolveLayout(ParentSize);
            loupe.ResolveGeometry(160);
        }

        var perCall = (GC.GetAllocatedBytesForCurrentThread() - before) / 1000.0;
        Assert.True(perCall <= 16, $"レイアウトと描画の幾何 1 回の確保が {perCall} バイト（16 バイト以下を期待）");
    }

    // ====================================================================
    // 設定変更の記録
    // ====================================================================

    /// <summary>ロガーの設定が保持され、null は出力なしへ吸収されることを検証する。</summary>
    [Fact]
    internal void Log_設定したロガーを保持しnullは出力なしへ吸収する()
    {
        var loupe = new LoupeCore();
        var logger = new RecordingLogger();

        loupe.Log = logger;
        Assert.Same(logger, loupe.Log);

        loupe.Log = null!;
        Assert.Same(NullLogger.Instance, loupe.Log);
    }

    /// <summary>生成の記録がロガーの設定時に 1 度だけ出力されることを検証する（注入しない場合）。</summary>
    /// <remarks>構築時に出力先が未設定のため、ロガーが設定された時点で生成時の設定を記録する。</remarks>
    [Fact]
    internal void Log_生成の記録はロガーの設定時に一度だけ出力する()
    {
        var loupe = new LoupeCore(new LoupeOptions { RequestedSize = 200, ZoomLevel = 8 });
        var logger = new RecordingLogger();

        loupe.Log = logger;
        loupe.Log = logger;   // 再設定（生成の記録は繰り返さない）

        Assert.Equal(
            [
                "[Loupe] Facade created: RequestedSize=200, ZoomLevel=8",
                "[Loupe] Logger attached",
                "[Loupe] Logger attached",
            ],
            logger.Messages);
    }

    /// <summary>コンストラクターでロガーを注入すると、その場で生成の記録が出力されることを検証する。</summary>
    /// <remarks>
    /// 注入時は出力先が確定しているため、コンストラクター呼び出しの記録（生成時の設定）をその場で出力する。<br/>
    /// コンストラクター由来の注入は 1 度だけであり、後からの再設定と同様に生成の記録は繰り返さない。
    /// </remarks>
    [Fact]
    internal void 生成_コンストラクターでロガーを注入すると生成を記録する()
    {
        var logger = new RecordingLogger();

        var loupe = new LoupeCore(new LoupeOptions { RequestedSize = 200, ZoomLevel = 8 }, logger);

        Assert.Equal(
            [
                "[Loupe] Facade created: RequestedSize=200, ZoomLevel=8",
                "[Loupe] Logger attached",
            ],
            logger.Messages);

        // 注入後に同じロガーを設定し直しても生成の記録は繰り返さない
        loupe.Log = logger;

        Assert.Equal(
            [
                "[Loupe] Facade created: RequestedSize=200, ZoomLevel=8",
                "[Loupe] Logger attached",
                "[Loupe] Logger attached",
            ],
            logger.Messages);
    }

    /// <summary>コンストラクターでロガーを注入した場合も、設定の変更が記録されることを検証する。</summary>
    [Fact]
    internal void 生成_コンストラクター注入後も設定変更を記録する()
    {
        var logger = new RecordingLogger();
        var loupe = new LoupeCore(new LoupeOptions(), logger);
        logger.Messages.Clear();

        loupe.PositionOffset = 20;

        Assert.Equal("[Loupe] Setting: PositionOffset 0 → 20", Assert.Single(logger.Messages));
    }

    /// <summary>座標変換ポートの設定が変化したときだけ記録されることを検証する。</summary>
    [Fact]
    internal void SetCoordTransform_変更したときだけ記録する()
    {
        var loupe = new LoupeCore { Log = new RecordingLogger() };
        var logger = (RecordingLogger)loupe.Log;
        logger.Messages.Clear();

        var port = CreateDoublingPort();
        loupe.SetCoordTransform(port);
        loupe.SetCoordTransform(port);   // 同値（同じインスタンス）
        loupe.SetCoordTransform(null);

        Assert.Equal(
            [
                "[Loupe] Setting: CoordTransform null → CoordTransform",
                "[Loupe] Setting: CoordTransform CoordTransform → null",
            ],
            logger.Messages);
    }

    /// <summary>拡大中心の変更が最も低いレベル（Trace）で記録されることを検証する。</summary>
    /// <remarks>中心はマウス移動ごとに設定されるため、既定の出力では溢れさせない。</remarks>
    [Fact]
    internal void CenterPoint_変更はTraceで記録する()
    {
        var loupe = new LoupeCore { Log = new RecordingLogger() };
        var logger = (RecordingLogger)loupe.Log;
        logger.Messages.Clear();
        logger.Levels.Clear();

        loupe.CenterPoint = new EsPoint(10, 20);

        Assert.Equal([LogLevel.Trace], logger.Levels);
        Assert.Equal("[Loupe] Setting: CenterPoint null → (10, 20)", Assert.Single(logger.Messages));
    }

    /// <summary>表示位置オフセットの変更が変化したときだけ記録されることを検証する。</summary>
    [Fact]
    internal void PositionOffset_変更したときだけ記録する()
    {
        var loupe = new LoupeCore { Log = new RecordingLogger() };
        var logger = (RecordingLogger)loupe.Log;
        logger.Messages.Clear();

        loupe.PositionOffset = 20;
        loupe.PositionOffset = 20;   // 同値
        loupe.PositionOffset = 8;

        Assert.Equal(
            [
                "[Loupe] Setting: PositionOffset 0 → 20",
                "[Loupe] Setting: PositionOffset 20 → 8",
            ],
            logger.Messages);
    }
}
