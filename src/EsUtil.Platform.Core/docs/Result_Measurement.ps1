<#
.SYNOPSIS
画像（EsUtil.Platform.Core の Imaging）の所要とメモリを実測し、本スクリプトと同じフォルダの Result_Measurement.md の計測結果を差し替える。

.DESCRIPTION
一時プロジェクト（Release）を生成してこの場で実行し、公開 API だけを計測する。
計測値の所有者は Result_Measurement.md であり（.github/ドキュメント規約.md §1・§10）、
コードのコメント・仕様書・README へは数値を書かない。 再計測は本スクリプトの実行だけでよい。

出力する表は 2 軸である。
    - 縮小・減色は「品質（Low / Mid / High）× 元画像の寸法」を型ごとに並べる。
    - 同名メソッドは「型（EsBitmap / EsReadOnlyBitmap / EsPixmap）× 元画像の寸法」をメソッドごとに並べる。
    - セルは「所要 (ms) / マネージド確保 (B)」。 その型にメソッドが無いセルは「-」。

.PARAMETER Configuration
ビルド構成（既定 Release）。 Debug は最適化が無効なため比較用。

.PARAMETER Iterations
計測回数（既定 20。 中央値を採る）。

.PARAMETER Warmup
ウォームアップ回数（既定 3。 JIT とキャッシュを温める）。

.PARAMETER NoWrite
Result_Measurement.md を更新しない（結果を表示するだけ）。

.PARAMETER KeepWorkspace
一時プロジェクトを残す（計測プログラムを直接編集して試す場合）。

.EXAMPLE
pwsh -NoProfile -File src/EsUtil.Platform.Core/docs/Result_Measurement.ps1
#>
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')] [string] $Configuration = 'Release',
    [ValidateRange(1, 1000)] [int] $Iterations = 20,
    [ValidateRange(0, 100)] [int] $Warmup = 3,
    [switch] $NoWrite,
    [switch] $KeepWorkspace
)

$ErrorActionPreference = 'Stop'

# 本スクリプトはプロジェクトの docs/ 配下にあるため、リポジトリ ルートまで 3 段上がる
$repoRoot = Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $PSScriptRoot))
$coreProject = Join-Path $repoRoot 'src/EsUtil.Platform.Core/src/EsUtil.Platform.Core.csproj'
$docPath = Join-Path $PSScriptRoot 'Result_Measurement.md'
$startMarker = '<!-- 計測開始（src/EsUtil.Platform.Core/docs/Result_Measurement.ps1 が差し替える） -->'
$endMarker = '<!-- 計測終了 -->'

if (-not (Test-Path -LiteralPath $coreProject)) {
    throw "対象のプロジェクトが見つかりません: $coreProject"
}

# 一時プロジェクトはリポジトリの外へ置く（リポジトリのビルド設定を適用させない。 計測の再現性を保つ）
$workspace = Join-Path ([IO.Path]::GetTempPath()) 'SaveEvidence.Measurement'
if (Test-Path -LiteralPath $workspace) {
    Remove-Item -LiteralPath $workspace -Recurse -Force
}

New-Item -ItemType Directory -Path $workspace -Force | Out-Null

$projectFile = @'
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
    <RootNamespace>Measurement</RootNamespace>
    <AssemblyName>Measurement</AssemblyName>
    <InvariantGlobalization>true</InvariantGlobalization>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="__CORE_PROJECT__" />
  </ItemGroup>

</Project>
'@

$projectFile = $projectFile.Replace('__CORE_PROJECT__', $coreProject)
[IO.File]::WriteAllText((Join-Path $workspace 'Measurement.csproj'), $projectFile, [Text.UTF8Encoding]::new($false))

$program = @'
// Imaging の所要とメモリの実測（src/EsUtil.Platform.Core/docs/Result_Measurement.ps1 が生成して実行する）。
// 計測値の所有者は同じフォルダの Result_Measurement.md であり、本プログラムは数値を出力するだけである。
using System.Diagnostics;
using System.Globalization;
using System.Runtime;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Imaging;

namespace Measurement;

internal static class Program
{
    // 縮小先は比較のため長辺 320 に固定する
    private const int ResizeMaxSize = 320;

    private const string SectionCreation = "生成と複製";
    private const string SectionCrop = "切り出しとビュー";
    private const string SectionResize = "縮小";
    private const string SectionReduce = "減色";
    private const string SectionPixel = "画素の取得と設定と走査";
    private const string SectionFile = "ファイル入出力";

    private const string ClassBit = "EsBitmap";
    private const string ClassRead = "EsReadOnlyBitmap";
    private const string ClassView = "EsPixmap";

    // 計測に使う画像の内容（減色の費用は画素が入った箱の数で決まるため、内容を変えて測る）
    private const string ContentFlat = "平坦";
    private const string ContentGradient = "グラデーション";
    private const string ContentNoise = "ノイズ";

    // 凍結の有無（FromBitmap は凍結済みなら共有・未凍結なら複製になる）
    private const string FrozenNo = "未凍結（複製）";
    private const string FrozenYes = "凍結済み（共有）";

    /// <summary>グラデーションの折り返しの周期（画素）。</summary>
    /// <remarks>
    /// <b>折り返す理由</b>: 折り返さない勾配では、画像の一部を切り出したビュー（<c>EsPixmap</c>）の色の範囲が
    /// 元画像より狭くなり、<b>内容が違うもの同士を比べる</b>ことになります（減色の費用は色の範囲で変わります）。
    /// 素数の周期にすると、行の長さと揃わないため符号化の圧縮も効きすぎません。
    /// </remarks>
    private const int PatternPeriod = 251;

    /// <summary>ノイズの初期値（実行のたびに同じ画像にする）。</summary>
    private const uint NoiseSeed = 0x2545F491u;

    private static readonly SizeCase[] s_sizes =
    [
        new("3840x2160", 3840, 2160),
        new("1920x1080", 1920, 1080),
        new("640x360", 640, 360),
    ];

    /// <summary>減色を測る内容（見出し・計測の鍵・測るかどうか）。</summary>
    /// <remarks>
    /// グラデーションは品質の表（寸法 3840x2160）と同じ計測を使い回します（同じ処理を 2 度測って
    /// 表ごとに違う数値を載せないため）。
    /// </remarks>
    private static readonly (string Label, string Key, bool NeedsMeasure)[] s_reduceContents =
    [
        (ContentFlat, ContentFlat, true),
        (ContentGradient, "3840x2160", false),
        (ContentNoise, ContentNoise, true),
    ];

    /// <summary>縮小先を変えて測るときの長辺（長辺 320 は品質の表の計測を使い回す）。</summary>
    private static readonly int[] s_resizeTargets = [960, 1920];

    /// <summary>保存の形式（見出し・表の行の見出し・拡張子・オプション。 拡張子は形式と食い違わせない）。</summary>
    private static readonly (string Method, string Label, string Extension, EsImageSaveOptions Options)[] s_formats =
    [
        ("Save（BMP）", "BMP（形式を指定）", ".bmp", new EsImageSaveOptions { Format = EsImageFileFormat.Bmp }),
        ("Save（PNG）", "PNG（形式を指定）", ".png", new EsImageSaveOptions { Format = EsImageFileFormat.Png }),
        ("Save（JPEG）", "JPEG（品質 90）", ".jpg", new EsImageSaveOptions { Format = EsImageFileFormat.Jpeg, JpegQuality = 90 }),
        ("Save（GIF）", "GIF（256 色へ減色）", ".gif", new EsImageSaveOptions { Format = EsImageFileFormat.Gif, ColorCount = 256 }),
    ];

    private static readonly string[] s_classNames = [ClassBit, ClassRead, ClassView];
    private static readonly EsImageQuality[] s_qualities = [EsImageQuality.Low, EsImageQuality.Mid, EsImageQuality.High];
    private static readonly Dictionary<(string, string, string, string, string), Measured> s_cases = [];
    private static readonly List<string> s_report = [];

    /// <summary>最適化されたコードへ到達するための、ウォームアップの下限（回）。</summary>
    private const int WarmupFloor = 40;

    /// <summary>1 回の計測で本体を呼ぶ回数（タイマーの分解能に埋もれる操作で使う）。</summary>
    /// <remarks>1 マイクロ秒を下回る操作は、1 回の呼び出しではタイマーの刻みに埋もれます。 複数回呼んで 1 回あたりへ換算します。</remarks>
    private const int Repeat = 100;

    private static int s_iterations = 20;
    private static int s_warmup = WarmupFloor;
    private static int s_fileIterations = 5;
    private static int s_pixelIterations = 5;

    private static int Main(string[] args)
    {
        if (args.Length > 0 && int.TryParse(args[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var iterations) && iterations > 0)
        {
            s_iterations = iterations;
        }

        if (args.Length > 1 && int.TryParse(args[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var warmup) && warmup >= 0)
        {
            s_warmup = warmup;
        }

        // 少ない回数では最適化されていないコードを測ってしまうため、ウォームアップには下限を置く
        s_warmup = Math.Max(WarmupFloor, s_warmup);

        // ファイル入出力は 1 回あたりの費用が大きいため、回数を減らして総時間を抑える
        s_fileIterations = Math.Max(3, s_iterations / 4);

        // 画素の取得と設定は 1 回で 100 万画素を扱うため、回数を減らして総時間を抑える
        s_pixelIterations = Math.Max(1, s_iterations / 4);
        Console.OutputEncoding = Encoding.UTF8;

        MeasureCreation();
        MeasureCrop();
        MeasureResize();
        MeasureReduceColors();
        MeasurePixelAccess();
        if (OperatingSystem.IsWindows())
        {
            MeasureFiles();
            CleanUpFiles();
        }

        Emit();
        Console.Write(string.Join('\n', s_report));
        return 0;
    }

    // ====================================================================
    // 計測
    // ====================================================================

    /// <summary>生成・複製・変換（対称な入口）を型と寸法ごとに計測する。</summary>
    private static void MeasureCreation()
    {
        foreach (var size in s_sizes)
        {
            var label = size.Label;
            Measure(SectionCreation, "Create", ClassBit, label, "", () => new Item(EsBitmap.Create(size.Width, size.Height)));
            Measure(SectionCreation, "Create", ClassRead, label, "", () => new Item(EsReadOnlyBitmap.Create(size.Width, size.Height)));

            Measure(SectionCreation, "CreateFilled", ClassBit, label, "", () => new Item(EsBitmap.CreateFilled(size.Width, size.Height, static p => p.Clear())));
            Measure(SectionCreation, "CreateFilled", ClassRead, label, "", () => new Item(EsReadOnlyBitmap.CreateFilled(size.Width, size.Height, static p => p.Clear())));

            var buffer = new byte[size.Width * size.Height * EsBitmap.BytesPerPixel];
            Measure(SectionCreation, "FromPixelCopy", ClassBit, label, "", () => new Item(EsBitmap.FromPixelCopy(size.Width, size.Height, buffer)));
            Measure(SectionCreation, "FromPixelCopy", ClassRead, label, "", () => new Item(EsReadOnlyBitmap.FromPixelCopy(size.Width, size.Height, buffer)));

            using var bit = Create(size);
            using var read = ReadOnly(size);

            Measure(SectionCreation, "Copy", ClassBit, label, "", () => new Item(bit.Copy()));
            Measure(SectionCreation, "Copy", ClassRead, label, "", () => new Item(read.Copy()));

            Measure(SectionCreation, "FromReadOnlyBitmap", ClassBit, label, "", () => new Item(EsBitmap.FromReadOnlyBitmap(read)));
            Measure(SectionCreation, "FromBitmap", ClassRead, label, FrozenNo, () => new Item(EsReadOnlyBitmap.FromBitmap(bit)));

            // 凍結済みの画像は 0 コピーで共有され、未凍結の画像は複製される（同じ名前で費用が桁で違う）
            using (var frozen = Create(size))
            {
                frozen.SetReadOnly();
                Measure(SectionCreation, "FromBitmap", ClassRead, label, FrozenYes, () => new Item(EsReadOnlyBitmap.FromBitmap(frozen)));
            }

            using (var view = bit.ToPixmap(new EsRectInt(0, 0, size.Width, size.Height)))
            {
                Measure(SectionCreation, "FromPixmap", ClassBit, label, "", () => new Item(EsBitmap.FromPixmap(view)));
                Measure(SectionCreation, "FromPixmap", ClassRead, label, "", () => new Item(EsReadOnlyBitmap.FromPixmap(view)));
            }

            // 借用は呼び出し側が確保した領域を受ける（解放は本プログラムが行う）
            var pixels = Marshal.AllocHGlobal(size.Width * size.Height * EsBitmap.BytesPerPixel);
            try
            {
                Measure(SectionCreation, "InstallPixels", ClassBit, label, "", () => new Item(EsBitmap.InstallPixels(size.Width, size.Height, pixels)), repeat: Repeat);
                Measure(SectionCreation, "InstallPixels", ClassRead, label, "", () => new Item(EsReadOnlyBitmap.InstallPixels(size.Width, size.Height, pixels)), repeat: Repeat);
            }
            finally
            {
                Marshal.FreeHGlobal(pixels);
            }
        }
    }

    /// <summary>切り出し（複製）と 0 コピーのビューを型と寸法ごとに計測する。</summary>
    private static void MeasureCrop()
    {
        foreach (var size in s_sizes)
        {
            var label = size.Label;
            var rect = InnerRect(size);

            using var bit = Create(size);
            using var read = ReadOnly(size);

            Measure(SectionCrop, "Crop", ClassBit, label, "", () => new Item(bit.Crop(rect)));
            Measure(SectionCrop, "Crop", ClassRead, label, "", () => new Item(read.Crop(rect)));

            Measure(SectionCrop, "ToPixmap", ClassBit, label, "", () => new Item(bit.ToPixmap(rect)), repeat: Repeat);
            Measure(SectionCrop, "ToPixmap", ClassRead, label, "", () => new Item(read.ToPixmap(rect)), repeat: Repeat);

            // 入れ子は「ビューの一部を指すビュー」であり、生成元がビューでも成り立つ
            using var view = bit.ToPixmap(new EsRectInt(0, 0, size.Width, size.Height));
            Measure(SectionCrop, "ToPixmap", ClassView, label, "", () => new Item(view.ToPixmap(rect)), repeat: Repeat);

            using var parentBit = bit.ToPixmap(new EsRectInt(0, 0, size.Width, size.Height));
            using var parentRead = read.ToPixmap(new EsRectInt(0, 0, size.Width, size.Height));
            Measure(SectionCrop, "ToPixmap（入れ子）", ClassBit, label, "", () => new Item(parentBit.ToPixmap(rect)), repeat: Repeat);
            Measure(SectionCrop, "ToPixmap（入れ子）", ClassRead, label, "", () => new Item(parentRead.ToPixmap(rect)), repeat: Repeat);
            Measure(SectionCrop, "ToPixmap（入れ子）", ClassView, label, "", () => new Item(view.ToPixmap(rect)), repeat: Repeat);
        }
    }

    /// <summary>縮小を型・品質・寸法ごとに計測する（縮小先は長辺 320 に固定）。</summary>
    private static void MeasureResize()
    {
        using var source = Create(new SizeCase("source", 3840, 2160));

        foreach (var size in s_sizes)
        {
            var label = size.Label;
            using var bit = Create(size);
            using var read = ReadOnly(size);
            using var view = source.ToPixmap(new EsRectInt(0, 0, size.Width, size.Height));

            foreach (var quality in s_qualities)
            {
                var variant = quality.ToString();
                Measure(SectionResize, "Resize", ClassBit, label, variant, () => new Item(bit.Resize(ResizeMaxSize, quality)));
                Measure(SectionResize, "Resize", ClassRead, label, variant, () => new Item(read.Resize(ResizeMaxSize, quality)));
                Measure(SectionResize, "Resize", ClassView, label, variant, () => new Item(view.Resize(ResizeMaxSize, quality)));
            }
        }

        // 縮小先の大小で品質ごとの費用の順序が変わるため、縮小先を変えて測る（元画像は最大の寸法に固定）
        var largest = new SizeCase("3840x2160", 3840, 2160);
        using var targetSource = Create(largest);
        using var targetBit = Create(largest);
        using var targetRead = ReadOnly(largest);
        using var targetView = targetSource.ToPixmap(new EsRectInt(0, 0, largest.Width, largest.Height));
        foreach (var maxSize in s_resizeTargets)
        {
            foreach (var quality in s_qualities)
            {
                var key = TargetKey(maxSize);
                var variant = quality.ToString();
                Measure(SectionResize, "Resize", ClassBit, key, variant, () => new Item(targetBit.Resize(maxSize, quality)));
                Measure(SectionResize, "Resize", ClassRead, key, variant, () => new Item(targetRead.Resize(maxSize, quality)));
                Measure(SectionResize, "Resize", ClassView, key, variant, () => new Item(targetView.Resize(maxSize, quality)));
            }
        }
    }

    /// <summary>減色を型・品質・色数・ディザ・寸法ごとに計測する。</summary>
    private static void MeasureReduceColors()
    {
        using var source = Create(new SizeCase("source", 3840, 2160));

        foreach (var size in s_sizes)
        {
            var label = size.Label;
            using var bit = Create(size);
            using var read = ReadOnly(size);
            using var view = source.ToPixmap(new EsRectInt(0, 0, size.Width, size.Height));

            foreach (var quality in s_qualities)
            {
                var variant = ReduceVariant(256, quality, EsDitherMode.None);
                Measure(SectionReduce, "ReduceColors", ClassBit, label, variant, () => new Item(bit.ReduceColors(256, quality)));
                Measure(SectionReduce, "ReduceColors", ClassRead, label, variant, () => new Item(read.ReduceColors(256, quality)));
                Measure(SectionReduce, "ReduceColors", ClassView, label, variant, () => new Item(view.ReduceColors(256, quality)));
            }

            foreach (var dither in new[] { EsDitherMode.None, EsDitherMode.FloydSteinberg })
            {
                foreach (var colors in new[] { 16, 256 })
                {
                    var variant = ReduceVariant(colors, EsImageQuality.Mid, dither);
                    Measure(SectionReduce, "ReduceColors", ClassBit, label, variant, () => new Item(bit.ReduceColors(colors, EsImageQuality.Mid, dither)));
                    Measure(SectionReduce, "ReduceColors", ClassRead, label, variant, () => new Item(read.ReduceColors(colors, EsImageQuality.Mid, dither)));
                    Measure(SectionReduce, "ReduceColors", ClassView, label, variant, () => new Item(view.ReduceColors(colors, EsImageQuality.Mid, dither)));
                }
            }
        }

        // 減色の費用は「画素が入った箱の数」で決まるため、内容を変えて測る（平坦は下限・ノイズは上限の側）
        var contentSize = new SizeCase("3840x2160", 3840, 2160);
        foreach (var content in s_reduceContents)
        {
            if (!content.NeedsMeasure)
            {
                continue;
            }

            using var bit = CreateWith(contentSize, content.Key);
            using var read = ReadOnlyWith(contentSize, content.Key);
            using var view = bit.ToPixmap(new EsRectInt(0, 0, contentSize.Width, contentSize.Height));
            var variant = ReduceVariant(256, EsImageQuality.Mid, EsDitherMode.None);
            Measure(SectionReduce, "ReduceColors", ClassBit, content.Key, variant, () => new Item(bit.ReduceColors(256, EsImageQuality.Mid)));
            Measure(SectionReduce, "ReduceColors", ClassRead, content.Key, variant, () => new Item(read.ReduceColors(256, EsImageQuality.Mid)));
            Measure(SectionReduce, "ReduceColors", ClassView, content.Key, variant, () => new Item(view.ReduceColors(256, EsImageQuality.Mid)));
        }
    }

    /// <summary>画素の取得・設定・走査を型と寸法ごとに計測する。</summary>
    private static void MeasurePixelAccess()
    {
        const int accesses = 1_000_000;
        var pixelIterations = s_pixelIterations;
        using var source = Create(new SizeCase("source", 3840, 2160));

        foreach (var size in s_sizes)
        {
            var label = size.Label;
            using var bit = Create(size);
            using var read = ReadOnly(size);
            using var view = source.ToPixmap(new EsRectInt(0, 0, size.Width, size.Height));

            Measure(SectionPixel, "GetPixel", ClassBit, label, "", () => SumPixels(bit.Width, bit.Height, i => bit.GetPixel(i % bit.Width, (i / bit.Width) % bit.Height)), pixelIterations);
            Measure(SectionPixel, "GetPixel", ClassRead, label, "", () => SumPixels(read.Width, read.Height, i => read.GetPixel(i % read.Width, (i / read.Width) % read.Height)), pixelIterations);
            Measure(SectionPixel, "GetPixel", ClassView, label, "", () => SumPixels(view.Width, view.Height, i => view.GetPixel(i % view.Width, (i / view.Width) % view.Height)), pixelIterations);

            Measure(SectionPixel, "SetPixel", ClassBit, label, "", () =>
            {
                for (var i = 0; i < accesses; i++)
                {
                    bit.SetPixel(i % bit.Width, (i / bit.Width) % bit.Height, 0x11, 0x22, 0x33, 0xFF);
                }

                return new Item(null);
            }, pixelIterations);

            Measure(SectionPixel, "Clear", ClassBit, label, "", () =>
            {
                bit.Clear(0, 0, 0, 0);
                return new Item(null);
            });

            MeasureScan(SectionPixel, "ReadOnlyPixels", ClassBit, label, () => bit.ReadOnlyPixels);
            MeasureScan(SectionPixel, "ReadOnlyPixels", ClassRead, label, () => read.ReadOnlyPixels);
            MeasureScan(SectionPixel, "ReadOnlyPixels", ClassView, label, () => view.ReadOnlyPixels);

            Measure(SectionPixel, "Pixels", ClassBit, label, "", () =>
            {
                var pixels = bit.Pixels;
                for (var i = 0; i < pixels.Length; i++)
                {
                    pixels[i] = (byte)i;
                }

                return new Item(null);
            });
        }
    }

    /// <summary>ファイル入出力を型と寸法ごとに計測する（Windows 専用の符号化）。</summary>
    [SupportedOSPlatform("windows")]
    private static void MeasureFiles()
    {
        Directory.CreateDirectory(FileDirectory());

        foreach (var size in s_sizes)
        {
            var label = size.Label;
            using var bit = Create(size);
            using var read = ReadOnly(size);
            var path = Path.Combine(FileDirectory(), $"{size.Width}x{size.Height}.png");

            Measure(SectionFile, "Save", ClassBit, label, "", () =>
            {
                bit.Save(path);
                return new Item(null, new FileInfo(path).Length);
            }, s_fileIterations);

            Measure(SectionFile, "Save", ClassRead, label, "", () =>
            {
                read.Save(path);
                return new Item(null, new FileInfo(path).Length);
            }, s_fileIterations);

            // 形式ごとの符号化（出力の大きさも同じ計測で採る）
            foreach (var format in s_formats)
            {
                var formatPath = Path.Combine(FileDirectory(), $"{size.Width}x{size.Height}{format.Extension}");
                Measure(SectionFile, format.Method, ClassBit, label, "", () =>
                {
                    bit.Save(formatPath, format.Options);
                    return new Item(null, new FileInfo(formatPath).Length);
                }, s_fileIterations);

                Measure(SectionFile, format.Method, ClassRead, label, "", () =>
                {
                    read.Save(formatPath, format.Options);
                    return new Item(null, new FileInfo(formatPath).Length);
                }, s_fileIterations);
            }

            Measure(SectionFile, "Load", ClassBit, label, "", () => new Item(EsBitmap.Load(path)), s_fileIterations);
            Measure(SectionFile, "Load", ClassRead, label, "", () => new Item(EsReadOnlyBitmap.Load(path)), s_fileIterations);
        }
    }

    private static Item SumPixels(int width, int height, Func<int, (byte B, byte G, byte R, byte A)> read)
    {
        var total = 0L;
        for (var i = 0; i < width * height && i < 1_000_000; i++)
        {
            var pixel = read(i);
            total += pixel.B + pixel.G + pixel.R + pixel.A;
        }

        GC.KeepAlive(total);
        return new Item(null);
    }

    private static void MeasureScan(string section, string method, string className, string size, Func<ReadOnlySpan<byte>> pixels)
    {
        Measure(section, method, className, size, "", () =>
        {
            var total = 0L;
            foreach (var value in pixels())
            {
                total += value;
            }

            GC.KeepAlive(total);
            return new Item(null);
        });
    }

    /// <summary>1 ケースを計測する（ウォームアップ後、中央値と 1 回あたりのマネージド確保を採る）。</summary>
    /// <remarks>
    /// <b>ウォームアップを厚くする理由</b>: 実行時は呼び出しの回数で最適化の段階を上げるため、
    /// 回数が少ないと<b>最適化されていないコードの所要</b>を測ってしまい、同じ処理でも測定の順番で
    /// 数倍の差が出ます。 <see cref="WarmupFloor"/> 回を下限とすることで、計測は最適化後の値になります。
    /// </remarks>
    private static void Measure(
        string section, string method, string className, string size, string variant, Func<Item> body, int? iterations = null, int repeat = 1)
    {
        var count = iterations ?? s_iterations;

        for (var i = 0; i < s_warmup; i++)
        {
            RunRepeated(body, repeat);
        }

        var times = new double[count];
        var managed = 0L;
        var note = 0L;

        for (var i = 0; i < count; i++)
        {
            var before = GC.GetAllocatedBytesForCurrentThread();
            var started = Stopwatch.GetTimestamp();
            for (var r = 0; r < repeat; r++)
            {
                var item = body();
                note = item.Note;
                item.Dispose();
            }

            var elapsed = Stopwatch.GetElapsedTime(started);
            managed += GC.GetAllocatedBytesForCurrentThread() - before;
            times[i] = elapsed.TotalMilliseconds / repeat;
        }

        Array.Sort(times);
        s_cases[(section, method, className, size, variant)] =
            new Measured(times[times.Length / 2], managed / (count * repeat), note);
    }

    /// <summary>本体を指定の回数だけ呼び、結果を破棄する。</summary>
    private static void RunRepeated(Func<Item> body, int repeat)
    {
        for (var r = 0; r < repeat; r++)
        {
            body().Dispose();
        }
    }

    // ====================================================================
    // 表の組み立て
    // ====================================================================

    private static void Emit()
    {
        AppendEnvironment();
        AppendTargets();
        AppendMethodSection(4, "生成と複製", SectionCreation,
            ["Create", "CreateFilled", "FromPixelCopy", "Copy", "FromReadOnlyBitmap", "FromPixmap", "InstallPixels"]);
        AppendFromBitmapSection(4, 8);
        AppendMethodSection(5, "切り出しと 0 コピーのビュー", SectionCrop, ["Crop", "ToPixmap", "ToPixmap（入れ子）"]);
        AppendResizeSection(6);
        AppendReduceSection(7);
        AppendMethodSection(8, "画素の取得・設定・走査", SectionPixel,
            ["GetPixel", "SetPixel", "Clear", "ReadOnlyPixels", "Pixels"],
            "メソッドごとに、行を型・列を寸法とした 2 軸表を示します。 `GetPixel` と `SetPixel` は **1 回あたり 1,000,000 画素**を扱う時間、`Clear` / `ReadOnlyPixels` / `Pixels` は **1 回あたり全画素**を扱う時間です。");
        AppendMethodSection(9, "ファイル入出力（Windows 専用の符号化）", SectionFile,
            ["Save", .. s_formats.Select(static f => f.Method)]);
        AppendOutputSizeSection(9, s_formats.Length + 2);
        AppendMethodTables(9, SectionFile, ["Load"], subStart: s_formats.Length + 3);
    }

    private static void AppendEnvironment()
    {
        AddHeading("## 2. 計測環境");
        var environment = new (string Item, string Value)[]
        {
            ("計測日時", DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture)),
            ("OS", RuntimeInformation.OSDescription.Trim()),
            ("プロセッサ", string.Create(CultureInfo.InvariantCulture, $"{Environment.ProcessorCount} 論理コア / {RuntimeInformation.ProcessArchitecture}")),
            (".NET", Environment.Version.ToString()),
            ("サーバー GC", GCSettings.IsServerGC.ToString()),
            ("構成", "Release（最適化あり）"),
            ("ウォームアップ / 計測回数", string.Create(CultureInfo.InvariantCulture, $"{s_warmup} 回 / {s_iterations} 回（中央値。 ファイル入出力は {s_fileIterations} 回、画素の取得と設定は {s_pixelIterations} 回）")),
        };

        AddTable(["項目", "値"], environment.Select(e => new[] { e.Item, e.Value }));
    }

    private static void AppendTargets()
    {
        AddHeading("## 3. 計測の対象");
        AddText("計測するのは **公開 API だけ**です（内部型の名前を計測コードへ埋め込まないため、実装を変えても計測が壊れません）。 対象は次の 3 型で、すべての表が**どの型を計測したか**を行または節の軸に持ちます。");
        AddTable(["型", "何であるか", "計測に使う入口"], [
            [ClassBit, "書き換え可能な画像", "`Create` 系・`SetPixel`・`Pixels`・`Clear`・`SetReadOnly`"],
            [ClassRead, "読み取り専用の画像", "`Create` 系・`FromBitmap`・`Copy`・`Save`・`Load`"],
            [ClassView, "画像の一部を指す 0 コピーの読み取りビュー", "`ToPixmap`（生成の唯一の入口）・`Resize`・`ReduceColors`"],
        ]);

        AddText("**セルの書式**は「所要 (ms) / マネージド確保 (B)」です（所要は 0.000001 ms（1 ナノ秒）まで表示し、それ未満は `<0.000001` と書きます）。 `-` はその型にメソッドが無い（対称でない）ことを表します。 1 マイクロ秒を下回る操作は、タイマーの分解能に埋もれるため **1 回の計測につき 100 回呼び、1 回あたりへ換算**します。");

        AddText("**寸法の軸**は元の画像（`EsPixmap` はビュー）の寸法です。 縮小だけは**縮小先を長辺 320 に固定**し（型と寸法の比較を揃えるため）、縮小先を変えた表を別に示します。 減色は 256 色を既定とし、ディザの節は 16 色と 256 色を並べます。");

        AddText($"計測に使う画像は**折り返す勾配**です（B = X % {PatternPeriod}・G = Y % {PatternPeriod}・R = (X + Y) % {PatternPeriod}・A = 255。 決定的で、実行のたびに同じ画像になります）。 折り返すのは、画像の一部を切り出したビューと元画像とで**色の範囲を揃える**ためです（減色の費用は色の範囲で決まるため、折り返さない勾配では内容の違うもの同士を比べることになります）。 減色だけは内容の影響が大きいため、内容を変えた表を別に示します。");

        AddText("1 回だけ呼ぶ操作（`Dispose` / `ThrowIfDisposed`）と、実体ごとに 1 度しか呼べない操作（`SetReadOnly`）は、所要が他の処理に支配されるため計測しません。 `ToString` 系（`ToSizeText`）と、幅・高さ・行の間隔などのプロパティ（フィールドの読み取りだけ）も同様です。");        AddText($"画素の領域（`幅 × {EsBitmap.BytesPerPixel} × 高さ`）は**アンマネージド**であり、表の「マネージド確保」の列には現れません（ビューは `行間隔 × 高さ`）。");
        AddTable(
            Row("項目", s_sizes.Select(static s => s.Label)),
            new[] { Row("画素の領域 (B)", s_sizes.Select(PixelBytes)) });

        AddText("**確保は計測したスレッドの分だけ**です（内部が並列の場合は他スレッドの確保を含みません）。");
    }

    /// <summary>メソッドごとに「型 × 寸法」の 2 軸表を出力する。</summary>
    private static void AppendMethodSection(int number, string heading, string section, string[] methods, string? caption = null)
    {
        AddHeading($"## {number}. {heading}");
        AddText(caption ?? "メソッドごとに、行を型・列を寸法とした 2 軸表を示します。");
        AppendMethodTables(number, section, methods, subStart: 1);
    }

    /// <summary>節の見出しを伴わずに、メソッドごとの 2 軸表だけを出力する。</summary>
    private static void AppendMethodTables(int number, string section, string[] methods, int subStart)
    {
        for (var i = 0; i < methods.Length; i++)
        {
            AddHeading($"### {number}.{subStart + i} {methods[i]}");
            AddTable(
                Row("型", s_sizes.Select(static s => s.Label)),
                s_classNames.Select(cls => Row(cls, s_sizes.Select(s => Cell(section, methods[i], cls, s.Label, "")))));
        }
    }

    /// <summary>FromBitmap の凍結の有無を「状態 × 寸法」の 2 軸表で出力する。</summary>
    private static void AppendFromBitmapSection(int number, int sub)
    {
        AddHeading($"### {number}.{sub} FromBitmap（凍結の有無）");
        AddText($"書き換え可能な画像から読み取り専用の画像を作る唯一の入口です（`{ClassBit}` からは作れません）。 行を凍結の有無・列を寸法とした 2 軸表を示します。 凍結済みは 0 コピーで共有し、未凍結は複製します。");
        AddTable(
            Row("状態", s_sizes.Select(static s => s.Label)),
            new[]
            {
                Row(FrozenNo, s_sizes.Select(s => Cell(SectionCreation, "FromBitmap", ClassRead, s.Label, FrozenNo))),
                Row(FrozenYes, s_sizes.Select(s => Cell(SectionCreation, "FromBitmap", ClassRead, s.Label, FrozenYes))),
            });
    }

    /// <summary>Save の出力の大きさを「形式 × 寸法」の 2 軸表で出力する。</summary>
    private static void AppendOutputSizeSection(int number, int sub)
    {
        AddHeading($"### {number}.{sub} Save の出力の大きさ");
        AddText($"保存したファイルの大きさ (B) です（行を形式・列を寸法とした 2 軸表）。 `{ClassBit}` で測った値です（`{ClassRead}` も同じ画素を書くため同じ大きさになります）。 画像の内容は折り返す勾配であり、内容で圧縮の効きが変わります。 拡張子から判定する版と形式を指定した版は同じ大きさになります（同じ処理であることの確認になります）。");

        var rows = new List<string[]>
        {
            Row("PNG（`Save(path)` 既定）", s_sizes.Select(s => NoteOf(SectionFile, "Save", ClassBit, s.Label, ""))),
        };
        foreach (var format in s_formats)
        {
            rows.Add(Row(format.Label, s_sizes.Select(s => NoteOf(SectionFile, format.Method, ClassBit, s.Label, ""))));
        }

        AddTable(Row("形式", s_sizes.Select(static s => s.Label)), rows);
    }

    /// <summary>縮小を型ごとに「品質 × 寸法」の 2 軸表で出力する。</summary>
    private static void AppendResizeSection(int number)
    {
        AddHeading($"## {number}. 縮小（長辺 {ResizeMaxSize} へ収める）");
        AddText($"縮小先を**長辺 {ResizeMaxSize} に固定**し、型ごとに、行を品質・列を元画像の寸法とした 2 軸表を示します。");

        for (var i = 0; i < s_classNames.Length; i++)
        {
            var cls = s_classNames[i];
            AddHeading($"### {number}.{i + 1} {cls}");
            AddTable(
                Row("品質", s_sizes.Select(static s => s.Label)),
                s_qualities.Select(q => Row(q.ToString(), s_sizes.Select(s => Cell(SectionResize, "Resize", cls, s.Label, q.ToString())))));
        }

        // 縮小先の大小で品質ごとの費用の順序が変わるため、縮小先を変えた表も示す
        for (var i = 0; i < s_classNames.Length; i++)
        {
            var cls = s_classNames[i];
            AddHeading($"### {number}.{s_classNames.Length + i + 1} {cls}（縮小先を変えた場合）");
            AddText($"元画像は {s_sizes[0].Label} に固定し、行を縮小先・列を品質とした 2 軸表を示します。 長辺 {ResizeMaxSize} は品質の表と同じ計測です。");
            AddTable(
                Row("縮小先", s_qualities.Select(static q => q.ToString())),
                ResizeTargetRows(cls));
        }
    }

    /// <summary>縮小先ごとの行を作る（長辺 320 は品質の表の計測を使い回す）。</summary>
    private static IEnumerable<string[]> ResizeTargetRows(string cls)
    {
        yield return Row($"長辺 {ResizeMaxSize}", s_qualities.Select(q => Cell(SectionResize, "Resize", cls, s_sizes[0].Label, q.ToString())));
        foreach (var maxSize in s_resizeTargets)
        {
            yield return Row($"長辺 {maxSize}", s_qualities.Select(q => Cell(SectionResize, "Resize", cls, TargetKey(maxSize), q.ToString())));
        }
    }

    /// <summary>減色を型ごとに「品質 / ディザ × 寸法」の 2 軸表で出力する。</summary>
    private static void AppendReduceSection(int number)
    {
        AddHeading($"## {number}. 減色");
        AddText("型ごとに、行を品質またはディザ・列を元画像の寸法とした 2 軸表を示します。");

        var sub = 0;
        for (var i = 0; i < s_classNames.Length; i++)
        {
            var cls = s_classNames[i];
            AddHeading($"### {number}.{++sub} {cls}（品質）");
            AddTable(
                Row("品質（256 色）", s_sizes.Select(static s => s.Label)),
                s_qualities.Select(q => Row(q.ToString(), s_sizes.Select(s => Cell(SectionReduce, "ReduceColors", cls, s.Label, ReduceVariant(256, q, EsDitherMode.None))))));

            AddHeading($"### {number}.{++sub} {cls}（ディザ）");
            var ditherRows = new List<string[]>();
            foreach (var dither in new[] { EsDitherMode.None, EsDitherMode.FloydSteinberg })
            {
                foreach (var colors in new[] { 16, 256 })
                {
                    ditherRows.Add(Row(
                        DitherLabel(colors, dither),
                        s_sizes.Select(s => Cell(SectionReduce, "ReduceColors", cls, s.Label, ReduceVariant(colors, EsImageQuality.Mid, dither)))));
                }
            }

            AddTable(Row("指定（Mid）", s_sizes.Select(static s => s.Label)), ditherRows);
        }

        // 減色の費用は「画素が入った箱の数」で決まる（内容を変えた表も示す）
        var ditherNone = ReduceVariant(256, EsImageQuality.Mid, EsDitherMode.None);
        AddHeading($"### {number}.{++sub} 内容による違い（{s_sizes[0].Label}・Mid・256 色）");
        AddText("減色の費用は**画素が入った箱の数**で決まるため、内容を変えた表を示します（行を型・列を内容とした 2 軸表）。 平坦は箱が 1 つで下限の側、ノイズは箱が最大で上限の側です。");
        AddTable(
            Row("型", s_reduceContents.Select(static c => c.Label)),
            s_classNames.Select(cls => Row(cls, s_reduceContents.Select(c => Cell(SectionReduce, "ReduceColors", cls, c.Key, ditherNone)))));
    }

    /// <summary>先頭のセル 1 つと、寸法ごとのセルから 1 行を作る。</summary>
    private static string[] Row(string first, IEnumerable<string> rest) => [first, .. rest];

    private static string DitherLabel(int colors, EsDitherMode dither)
        => dither == EsDitherMode.None
            ? $"{colors.ToString(CultureInfo.InvariantCulture)} 色（ディザなし）"
            : $"{colors.ToString(CultureInfo.InvariantCulture)} 色（Floyd–Steinberg）";

    private static string ReduceVariant(int colors, EsImageQuality quality, EsDitherMode dither)
        => string.Create(CultureInfo.InvariantCulture, $"{colors}色 {quality} {DitherLabel(colors, dither)}");

    private static string Cell(string section, string method, string className, string size, string variant)
    {
        if (!s_cases.TryGetValue((section, method, className, size, variant), out var measured))
        {
            return "-";
        }

        return Format(measured.Milliseconds) + " / " + measured.Managed.ToString("N0", CultureInfo.InvariantCulture);
    }

    private static void AddHeading(string text)
    {
        s_report.Add(text);
        s_report.Add("");
    }

    private static void AddText(string text)
    {
        s_report.Add(text);
        s_report.Add("");
    }

    private static void AddTable(string[] columns, IEnumerable<string[]> rows)
    {
        s_report.Add("| " + string.Join(" | ", columns) + " |");
        s_report.Add("| " + string.Join(" | ", columns.Select(static _ => "---")) + " |");
        foreach (var row in rows)
        {
            s_report.Add("| " + string.Join(" | ", row) + " |");
        }

        s_report.Add("");
    }

    private static string Format(double milliseconds)
        => milliseconds switch
        {
            >= 1.0 => milliseconds.ToString("F3", CultureInfo.InvariantCulture),
            >= 0.001 => milliseconds.ToString("F4", CultureInfo.InvariantCulture),
            > 0.0 => milliseconds.ToString("F6", CultureInfo.InvariantCulture),
            _ => "<0.000001",
        };

    // ====================================================================
    // ヘルパー
    // ====================================================================

    /// <summary>計測に使う画像を生成する（内容はグラデーション。 平坦な画像では減色の箱が 1 つになり、品質の差が現れない）。</summary>
    private static EsBitmap Create(SizeCase size)
        => CreateWith(size, ContentGradient);

    private static EsReadOnlyBitmap ReadOnly(SizeCase size)
        => ReadOnlyWith(size, ContentGradient);

    /// <summary>指定の内容で書き換え可能な画像を生成する。</summary>
    private static EsBitmap CreateWith(SizeCase size, string content)
        => EsBitmap.CreateFilled(size.Width, size.Height, pixels => FillPattern(pixels, size.Width, size.Height, content));

    /// <summary>指定の内容で読み取り専用の画像を生成する。</summary>
    private static EsReadOnlyBitmap ReadOnlyWith(SizeCase size, string content)
        => EsReadOnlyBitmap.CreateFilled(size.Width, size.Height, pixels => FillPattern(pixels, size.Width, size.Height, content));

    /// <summary>画像の内容を書き込む（平坦 / グラデーション / ノイズ。 いずれも決定的で、実行のたびに同じ画像になる）。</summary>
    private static void FillPattern(Span<byte> pixels, int width, int height, string content)
    {
        var stride = width * EsBitmap.BytesPerPixel;
        if (content == ContentNoise)
        {
            var state = NoiseSeed;
            for (var i = 0; i < pixels.Length; i += EsBitmap.BytesPerPixel)
            {
                state = (state * 1664525u) + 1013904223u;
                pixels[i] = (byte)(state >> 24);
                pixels[i + 1] = (byte)(state >> 16);
                pixels[i + 2] = (byte)(state >> 8);
                pixels[i + 3] = 0xFF;
            }

            return;
        }

        if (content == ContentFlat)
        {
            pixels.Clear();
            return;
        }

        for (var y = 0; y < height; y++)
        {
            var row = pixels.Slice(y * stride, stride);
            for (var x = 0; x < width; x++)
            {
                var offset = x * EsBitmap.BytesPerPixel;
                row[offset] = (byte)(x % PatternPeriod);
                row[offset + 1] = (byte)(y % PatternPeriod);
                row[offset + 2] = (byte)((x + y) % PatternPeriod);
                row[offset + 3] = 0xFF;
            }
        }
    }

    /// <summary>縮小先を変えた計測の鍵（寸法の軸と同じ形の見出し）。</summary>
    private static string TargetKey(int maxSize)
        => string.Create(CultureInfo.InvariantCulture, $"長辺 {maxSize}");

    /// <summary>計測に付けた補助の値（Save は出力の大きさ）を返す。 計測が無い場合は <c>-</c>。</summary>
    private static string NoteOf(string section, string method, string className, string size, string variant)
        => s_cases.TryGetValue((section, method, className, size, variant), out var measured)
            ? measured.Note.ToString("N0", CultureInfo.InvariantCulture)
            : "-";

    /// <summary>寸法ごとの画素の領域のバイト数（幅 × 4 × 高さ。 アンマネージドであり、マネージド確保には現れない）。</summary>
    private static string PixelBytes(SizeCase size)
        => ((long)size.Width * EsBitmap.BytesPerPixel * size.Height).ToString("N0", CultureInfo.InvariantCulture);

    /// <summary>寸法の中央の 1/4 面積を切り出す矩形（切り出しの比較条件を揃える）。</summary>
    private static EsRectInt InnerRect(SizeCase size)
        => new(size.Width / 4, size.Height / 4, size.Width / 2, size.Height / 2);

    private static string FileDirectory()
        => Path.Combine(Path.GetTempPath(), "SaveEvidence.Measurement.Files");

    /// <summary>計測で書いたファイルを片付ける（次回の計測へ持ち越さない）。</summary>
    private static void CleanUpFiles()
    {
        var directory = FileDirectory();
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private readonly record struct SizeCase(string Label, int Width, int Height);

    private readonly record struct Measured(double Milliseconds, long Managed, long Note);

    private readonly struct Item(IDisposable? value, long note = 0) : IDisposable
    {
        public long Note { get; } = note;

        public void Dispose() => value?.Dispose();
    }
}
'@

[IO.File]::WriteAllText((Join-Path $workspace 'Program.cs'), $program, [Text.UTF8Encoding]::new($false))

Write-Output "== 計測 =="
Write-Output "workspace: $workspace"
Write-Output "configuration: $Configuration / iterations: $Iterations / warmup: $Warmup"

& dotnet build (Join-Path $workspace 'Measurement.csproj') -c $Configuration -v:q --nologo
if ($LASTEXITCODE -ne 0) {
    throw '計測プログラムのビルドに失敗しました。'
}

$assembly = Join-Path $workspace "bin/$Configuration/net10.0/Measurement.dll"
$measured = & dotnet $assembly $Iterations $Warmup
if ($LASTEXITCODE -ne 0) {
    throw '計測の実行に失敗しました。'
}

$measuredText = (($measured -join "`n") -replace "`r`n", "`n").TrimEnd()
if ([string]::IsNullOrWhiteSpace($measuredText)) {
    throw '計測結果が空です。'
}

if ($NoWrite) {
    Write-Output $measuredText
    Write-Output '（-NoWrite のため Result_Measurement.md は更新しない）'
}
else {
    $generated = "$startMarker`n`n$measuredText`n`n$endMarker"

    if (-not (Test-Path -LiteralPath $docPath)) {
        throw "Result_Measurement.md が見つかりません: $docPath"
    }

    $text = ([IO.File]::ReadAllText($docPath, [Text.UTF8Encoding]::new($false))) -replace "`r`n", "`n"
    $begin = $text.IndexOf($startMarker, [StringComparison]::Ordinal)
    $end = $text.IndexOf($endMarker, [StringComparison]::Ordinal)

    if ($begin -lt 0 -or $end -lt $begin) {
        throw "マーカーが見つかりません（$docPath へ '$startMarker' と '$endMarker' を置いてください）。"
    }

    $updated = $text.Substring(0, $begin) + $generated + $text.Substring($end + $endMarker.Length)
    [IO.File]::WriteAllText($docPath, ($updated -replace "`n", "`r`n"), [Text.UTF8Encoding]::new($false))
    Write-Output "更新: $docPath"
}

if (-not $KeepWorkspace) {
    Remove-Item -LiteralPath $workspace -Recurse -Force
}
