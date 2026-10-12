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

    private static readonly SizeCase[] s_sizes =
    [
        new("3840x2160", 3840, 2160),
        new("1920x1080", 1920, 1080),
        new("640x360", 640, 360),
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
            Measure(SectionCreation, "FromBitmap", ClassRead, label, "", () => new Item(EsReadOnlyBitmap.FromBitmap(bit)));

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
            ["Create", "CreateFilled", "FromPixelCopy", "Copy", "FromReadOnlyBitmap", "FromBitmap", "FromPixmap", "InstallPixels"]);
        AppendMethodSection(5, "切り出しと 0 コピーのビュー", SectionCrop, ["Crop", "ToPixmap", "ToPixmap（入れ子）"]);
        AppendResizeSection(6);
        AppendReduceSection(7);
        AppendMethodSection(8, "画素の取得・設定・走査", SectionPixel,
            ["GetPixel", "SetPixel", "Clear", "ReadOnlyPixels", "Pixels"],
            "メソッドごとに、行を型・列を寸法とした 2 軸表を示します。 `GetPixel` と `SetPixel` は **1 回あたり 1,000,000 画素**を扱う時間、`Clear` / `ReadOnlyPixels` / `Pixels` は **1 回あたり全画素**を扱う時間です。");
        AppendMethodSection(9, "ファイル入出力（Windows 専用の符号化）", SectionFile, ["Save", "Load"]);
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

        AddText("**セルの書式**は「所要 (ms) / マネージド確保 (B)」です（所要は 0.000001 ms（1 ナノ秒）まで表示します）。 `-` はその型にメソッドが無い（対称でない）ことを表します。 1 マイクロ秒を下回る操作は、タイマーの分解能に埋もれるため **1 回の計測につき 100 回呼び、1 回あたりへ換算**します。");
        AddText("**寸法の軸**は元の画像（`EsPixmap` はビュー）の寸法です。 縮小だけは**縮小先を長辺 320 に固定**します（型と寸法の比較を揃えるため）。 減色は 256 色を既定とし、ディザの節は 16 色と 256 色を並べます。");
        AddText("1 回だけ呼ぶ操作（`Dispose` / `ThrowIfDisposed`）と、実体ごとに 1 度しか呼べない操作（`SetReadOnly`）は、所要が他の処理に支配されるため計測しません。 `ToString` 系（`ToSizeText`）と、幅・高さ・行の間隔などのプロパティ（フィールドの読み取りだけ）も同様です。");
        AddText("**確保は計測したスレッドの分だけ**です（内部が並列の場合は他スレッドの確保を含みません）。");
    }

    /// <summary>メソッドごとに「型 × 寸法」の 2 軸表を出力する。</summary>
    private static void AppendMethodSection(int number, string heading, string section, string[] methods, string? caption = null)
    {
        AddHeading($"## {number}. {heading}");
        AddText(caption ?? "メソッドごとに、行を型・列を寸法とした 2 軸表を示します。");

        for (var i = 0; i < methods.Length; i++)
        {
            AddHeading($"### {number}.{i + 1} {methods[i]}");
            AddTable(
                Row("型", s_sizes.Select(static s => s.Label)),
                s_classNames.Select(cls => Row(cls, s_sizes.Select(s => Cell(section, methods[i], cls, s.Label, "")))));
        }
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
            _ => milliseconds.ToString("F6", CultureInfo.InvariantCulture),
        };

    // ====================================================================
    // ヘルパー
    // ====================================================================

    private static EsBitmap Create(SizeCase size)
        => EsBitmap.CreateFilled(size.Width, size.Height, static p => p.Clear());

    private static EsReadOnlyBitmap ReadOnly(SizeCase size)
        => EsReadOnlyBitmap.CreateFilled(size.Width, size.Height, static p => p.Clear());

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

$program = $program.Replace('            var path = Path.Combine(FileDirectory(), $"{size.Width}x{size.Height}.png");',
    '            Directory.CreateDirectory(FileDirectory());' + "`n" + '            var path = Path.Combine(FileDirectory(), $"{size.Width}x{size.Height}.png");')

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
