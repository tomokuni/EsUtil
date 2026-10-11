using System.Diagnostics;
using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using EsUtil.Platform.Core.Imaging;
using EsUtil.Feature.Loupe.AvaloniaUI.Internal;
using Xunit;

namespace EsUtil.Feature.Loupe.AvaloniaUI.Tests.Internal;

/// <summary>
/// <see cref="EsReadOnlyBitmap"/> の Avalonia WriteableBitmap 変換テスト（ヘッドレス依存・直列実行）。
/// </summary>
/// <remarks>
/// <c>Bitmap.ToEsBitmap()</c> / <c>bitmap.ToAvaloniaWriteableBitmap()</c> は
/// <c>WriteableBitmap</c> の生成・ピクセルコピーを伴うため、Avalonia ヘッドレスプラットフォームが必要。<br/>
/// そのため <see cref="AvaloniaHeadlessGroup"/> 内で直列実行する（純粋計算テストは
/// <c>EsBitmapTests</c> で並列実行）。<br/>
/// サイズ・ストライドを主に検証する（ピクセル値はヘッドレス環境で不安定なため検証しない）。
/// </remarks>
/// <remarks>コンストラクタ（xUnit が <see cref="ITestOutputHelper"/> を注入する）。</remarks>
/// <param name="output">テスト出力ヘルパー。</param>
[Collection("AvaloniaHeadless")]
public class EsBitmapAvaloniaTests(ITestOutputHelper output)
{
    /// <summary>テスト出力（xUnit v3 でコンソール出力をキャプチャする）。</summary>
    private readonly ITestOutputHelper _output = output;

    /// <summary>テスト用の WriteableBitmap を生成する。</summary>
    private static WriteableBitmap CreateBitmap(int width, int height)
        => new(new PixelSize(width, height), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Opaque);

    [AvaloniaFact]
    internal void ToAvaloniaWriteableBitmap_サイズが一致する()
    {
        using var bmp = EsBitmap.Create(8, 8);
        bmp.SetReadOnly();
        using var read = EsReadOnlyBitmap.FromBitmap(bmp);

        var result = read.ToAvaloniaWriteableBitmap();

        Assert.Equal(new PixelSize(8, 8), result.PixelSize);
    }

    [AvaloniaFact]
    internal void ToEsBitmap_サイズが一致する()
    {
        var src = CreateBitmap(8, 8);

        using var bmp = src.ToEsBitmap();

        Assert.Equal(8, bmp.Size.Width);
        Assert.Equal(8, bmp.Size.Height);
    }

    [AvaloniaFact]
    internal void Avalonia_往復変換でサイズが一致する()
    {
        // Bitmap → EsReadOnlyBitmap → Bitmap の往復でサイズが保持される。
        var src = CreateBitmap(12, 10);

        using var bmp = src.ToEsBitmap();
        bmp.SetReadOnly();
        using var read = EsReadOnlyBitmap.FromBitmap(bmp);
        var back = read.ToAvaloniaWriteableBitmap();

        Assert.Equal(src.PixelSize, back.PixelSize);
    }

    [AvaloniaFact]
    internal void ToEsBitmap_元Bitmapは変更されない()
    {
        var src = CreateBitmap(8, 8);

        using var bmp = src.ToEsBitmap();

        // 元のサイズが保持される（複製のため元は変更されない）
        Assert.Equal(new PixelSize(8, 8), src.PixelSize);
    }

    [AvaloniaFact]
    internal void ToEsBitmap_内部ストライドが幅4倍以上になる()
    {
        // Avalonia の WriteableBitmap.RowBytes はプラットフォーム依存で幅の4バイト以上になり得る。
        // EsReadOnlyBitmap は幅の4バイト以上を保持し、以降のピクセル演算で正しく扱う。
        var src = CreateBitmap(7, 5);

        using var bmp = src.ToEsBitmap();

        Assert.Equal(7, bmp.Size.Width);
        Assert.Equal(5, bmp.Size.Height);
        Assert.True(bmp.Stride >= 7 * 4);
    }

    // ====================================================================
    // パフォーマンス計測（ToAvaloniaWriteableBitmap vs ToAvaloniaBitmap）
    // ====================================================================

    /// <summary>計測結果のログ出力（計測の可視化。 アサートはせず計測結果のみ）。</summary>
    /// <param name="label">計測ラベル。</param>
    /// <param name="width">画像幅。</param>
    /// <param name="height">画像高さ。</param>
    /// <param name="writeableMs">ToAvaloniaWriteableBitmap の累計ミリ秒。</param>
    /// <param name="bitmapMs">ToAvaloniaBitmap の累計ミリ秒。</param>
    /// <param name="iterations">繰り返し回数。</param>
    private void LogBenchmark(string label, int width, int height, double writeableMs, double bitmapMs, int iterations)
    {
        // xUnit v3 では Console.WriteLine がキャプチャされないため、ITestOutputHelper 経由で出力する。
        var ratio = bitmapMs > 0 ? writeableMs / bitmapMs : double.NaN;
        _output.WriteLine(
            $"[ベンチ] {label} ({width}x{height}) x{iterations}: " +
            $"ToAvaloniaWriteableBitmap={writeableMs:F3}ms " +
            $"/ ToAvaloniaBitmap={bitmapMs:F3}ms " +
            $"({(double.IsNaN(ratio) ? "N/A" : $"{ratio:F2}x")})");
    }

    [AvaloniaFact]
    internal void Benchmark_ToAvaloniaWriteableBitmap_vs_ToAvaloniaBitmap_ルーペサイズ()
    {
        // ルーペサンプルの既定サイズ（240x240）とテスト画像（800x600）で計測する。
        // ウォームアップ後、各 n 回の平均を測る。
        foreach (var (label, w, h) in new[] { ("ルーペ 240x240", 240, 240), ("ルーペ 800x600", 800, 600) })
        {
            using var bmp = EsBitmap.Create(w, h);
            bmp.SetReadOnly();
            using var read = EsReadOnlyBitmap.FromBitmap(bmp);
            const int n = 50;

            // ウォームアップ（JIT・Avalonia 初期化を乗せる）
            RunMeasurement(read, 3);

            // 計測（WriteableBitmap / Bitmap を個別に計測）
            var wbTime = RunWriteableMeasurement(read, n);
            var bmpTime = RunBitmapMeasurement(read, n);

            LogBenchmark(label, w, h, wbTime, bmpTime, n);
        }
    }

    [AvaloniaFact]
    internal void Benchmark_ToAvaloniaWriteableBitmap_vs_ToAvaloniaBitmap_4K画像()
    {
        // 4K（3840x2160）。 メモリ使用が大きいため繰り返し回数を減らす。
        using var bmp = EsBitmap.Create(3840, 2160);
        bmp.SetReadOnly();
        using var read = EsReadOnlyBitmap.FromBitmap(bmp);
        const int n = 5;

        // ウォームアップ（JIT・Avalonia 初期化を乗せる）
        RunMeasurement(read, 1);

        // 計測（WriteableBitmap / Bitmap を個別に計測）
        var wbTime = RunWriteableMeasurement(read, n);
        var bmpTime = RunBitmapMeasurement(read, n);

        LogBenchmark("4K 3840x2160", 3840, 2160, wbTime, bmpTime, n);
    }

    /// <summary>指定回数だけ <see cref="EsUtil.Feature.Loupe.AvaloniaUI.Internal.EsBitmapAvaloniaExtensions.ToAvaloniaWriteableBitmap(EsUtil.Platform.Core.Imaging.EsReadOnlyBitmap)"/> を実行し、累計ミリ秒を返す。</summary>
    /// <param name="bmp">計測対象のビットマップ。</param>
    /// <param name="iterations">繰り返し回数。</param>
    /// <returns>累計実行時間（ミリ秒）。</returns>
    private static double RunWriteableMeasurement(EsReadOnlyBitmap bmp, int iterations)
    {
        // 生成結果を蓄積してデッドコード除去（JIT 最適化）を防ぐ。
        int sink = 0;
        GC.Collect();
        GC.WaitForPendingFinalizers();
        var start = Stopwatch.GetTimestamp();
        for (var i = 0; i < iterations; i++)
        {
            using var result = bmp.ToAvaloniaWriteableBitmap();
            sink += result.PixelSize.Width + result.PixelSize.Height;
        }
        var end = Stopwatch.GetTimestamp();
        // 累算値は使わないが、最適化でループごと除去されないように生存させる
        GC.KeepAlive(sink);
        return (end - start) * 1000.0 / Stopwatch.Frequency;
    }

    /// <summary>指定回数だけ <see cref="EsUtil.Feature.Loupe.AvaloniaUI.Internal.EsBitmapAvaloniaExtensions.ToAvaloniaBitmap(EsUtil.Platform.Core.Imaging.EsReadOnlyBitmap)"/> を実行し、累計ミリ秒を返す。</summary>
    /// <param name="bmp">計測対象のビットマップ。</param>
    /// <param name="iterations">繰り返し回数。</param>
    /// <returns>累計実行時間（ミリ秒）。</returns>
    private static double RunBitmapMeasurement(EsReadOnlyBitmap bmp, int iterations)
    {
        // 生成結果を蓄積してデッドコード除去（JIT 最適化）を防ぐ。
        int sink = 0;
        GC.Collect();
        GC.WaitForPendingFinalizers();
        var start = Stopwatch.GetTimestamp();
        for (var i = 0; i < iterations; i++)
        {
            using var result = bmp.ToAvaloniaBitmap();
            sink += result.PixelSize.Width + result.PixelSize.Height;
        }
        var end = Stopwatch.GetTimestamp();
        // 累算値は使わないが、最適化でループごと除去されないように生存させる
        GC.KeepAlive(sink);
        return (end - start) * 1000.0 / Stopwatch.Frequency;
    }

    /// <summary>両メソッドを指定回数実行し、書き込み版の累計ミリ秒を返す（スキップ用のダミー計測）。</summary>
    /// <param name="bmp">計測対象のビットマップ。</param>
    /// <param name="iterations">繰り返し回数。</param>
    /// <returns>ダミー値（ウォームアップ用）。</returns>
    private static double RunMeasurement(EsReadOnlyBitmap bmp, int iterations)
    {
        _ = RunWriteableMeasurement(bmp, iterations);
        _ = RunBitmapMeasurement(bmp, iterations);
        return 0;
    }

}
