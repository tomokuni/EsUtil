#Requires -Version 7.0
<#
.SYNOPSIS
画像（EsUtil.Platform.Core の Imaging）の所要とメモリを実測し、本スクリプトと同じフォルダの Result_Measurement.md の計測結果を差し替える。

.DESCRIPTION
一時プロジェクト（Release）を生成してこの場で実行し、公開 API だけを計測する。
計測値の所有者は本スクリプトと同じフォルダの Result_Measurement.md であり（.github/ドキュメント規約.md §1・§10）、
コードのコメント・仕様書・README へは数値を書かない。 再計測は本スクリプトの実行だけでよい。

計測する値（1 ケースあたり）:
    所要             … ウォームアップ後の中央値（ms）
    マネージド確保   … GC.GetAllocatedBytesForCurrentThread の増分の平均（計測したスレッド分のみ）
    非マネージド確保 … 生成した画像の画素のバイト数（現在の実装では画像ごとに 幅 x 4 x 高さ）
    出力ファイル     … 保存したファイルのバイト数（ファイル入出力の節のみ）

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
    private static readonly EsImageQuality[] s_qualities = [EsImageQuality.Low, EsImageQuality.Mid, EsImageQuality.High];
    private static readonly List<Row> s_rows = [];
    private static readonly StringBuilder s_report = new();

    private static int s_iterations = 20;
    private static int s_warmup = 3;

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

        Console.OutputEncoding = Encoding.UTF8;

        AppendEnvironment();
        MeasureCreationAndCopy();
        MeasureCropAndView();
        MeasureResize();
        MeasureReduceColors();
        MeasurePixelAccess();

        if (OperatingSystem.IsWindows())
        {
            MeasureFileIo();
            CleanUpFiles();
        }

        Console.Write(s_report.ToString());
        return 0;
    }    private static void AppendEnvironment()
    {
        s_report.AppendLine("## 2. 計測環境");
        s_report.AppendLine();
        s_report.AppendLine("| 項目 | 値 |");
        s_report.AppendLine("| --- | --- |");
        s_report.AppendLine("| 計測日時 | " + DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture) + " |");
        s_report.AppendLine("| OS | " + RuntimeInformation.OSDescription.Trim() + " |");
        s_report.AppendLine("| プロセッサ | " + Environment.ProcessorCount.ToString(CultureInfo.InvariantCulture) + " 論理コア / " + RuntimeInformation.ProcessArchitecture + " |");
        s_report.AppendLine("| .NET | " + Environment.Version.ToString() + " |");
        s_report.AppendLine("| サーバー GC | " + GCSettings.IsServerGC.ToString() + " |");
        s_report.AppendLine("| 構成 | Release（最適化あり） |");
        s_report.AppendLine("| ウォームアップ / 計測回数 | " + s_warmup.ToString(CultureInfo.InvariantCulture) + " 回 / " + s_iterations.ToString(CultureInfo.InvariantCulture) + " 回（中央値） |");
        s_report.AppendLine();
    }

    private static void MeasureCreationAndCopy()
    {
        var start = s_rows.Count;

        Measure("Create", "3840x2160（0 埋め）", () =>
        {
            var image = EsBitmap.Create(3840, 2160);
            return new Item(image, BytesOf(image));
        });

        Measure("CreateFilled", "3840x2160", () =>
        {
            var image = EsBitmap.CreateFilled(3840, 2160, static pixels => pixels.Clear());
            return new Item(image, BytesOf(image));
        });

        var copySource = new byte[3840 * 2160 * EsBitmap.BytesPerPixel];

        Measure("FromPixelCopy", "3840x2160", () =>
        {
            var image = EsBitmap.FromPixelCopy(3840, 2160, copySource);
            return new Item(image, BytesOf(image));
        });

        using var source = Create(3840, 2160, Pattern.Mixed);

        Measure("Copy", "EsBitmap 3840x2160", () =>
        {
            var image = source.Copy();
            return new Item(image, BytesOf(image));
        });

        using var read = EsReadOnlyBitmap.FromBitmap(source);
        Measure("Copy", "EsReadOnlyBitmap 3840x2160", () =>
        {
            var image = read.Copy();
            return new Item(image, BytesOf(image));
        });

        Measure("FromReadOnlyBitmap", "3840x2160（複製）", () =>
        {
            var image = EsBitmap.FromReadOnlyBitmap(read);
            return new Item(image, BytesOf(image));
        });

        Measure("FromBitmap", "未凍結の EsBitmap（複製）", () =>
        {
            var image = EsReadOnlyBitmap.FromBitmap(source);
            return new Item(image, BytesOf(image));
        });

        using (var frozen = source.Copy())
        {
            frozen.SetReadOnly();
            Measure("FromBitmap", "凍結済みの EsBitmap（共有）", () =>
            {
                var image = EsReadOnlyBitmap.FromBitmap(frozen);
                return new Item(image, 0);
            });
        }

        AddSection("3. 生成と複製", start, Columns.Dimensions);
    }

    private static void MeasureCropAndView()
    {
        var start = s_rows.Count;

        using var full = Create(3840, 2160, Pattern.Mixed);
        using var half = Create(1920, 1080, Pattern.Gradient);

        Measure("Crop", "3840x2160 → 1000x800（内側）", () =>
        {
            var image = full.Crop(new EsRectInt(1200, 700, 1000, 800));
            return new Item(image, BytesOf(image));
        });

        Measure("Crop", "3840x2160 → 3840x2160（全域）", () =>
        {
            var image = full.Crop(new EsRectInt(0, 0, 3840, 2160));
            return new Item(image, BytesOf(image));
        });

        Measure("Crop", "1920x1080 → 320x180", () =>
        {
            var image = half.Crop(new EsRectInt(800, 450, 320, 180));
            return new Item(image, BytesOf(image));
        });

        Measure("ToPixmap", "3840x2160 → 1000x800", () =>
        {
            var view = full.ToPixmap(new EsRectInt(1200, 700, 1000, 800));
            return new Item(view, 0);
        });

        Measure("ToPixmap", "1920x1080 → 320x180", () =>
        {
            var view = half.ToPixmap(new EsRectInt(800, 450, 320, 180));
            return new Item(view, 0);
        });

        Measure("ToPixmap", "ビュー 1000x800 → 320x180（入れ子）", () =>
        {
            using var parent = full.ToPixmap(new EsRectInt(1200, 700, 1000, 800));
            var view = parent.ToPixmap(new EsRectInt(100, 100, 320, 180));
            return new Item(view, 0);
        });

        using (var view = full.ToPixmap(new EsRectInt(1200, 700, 1000, 800)))
        {
            Measure("FromPixmap", "ビュー 1000x800（複製）", () =>
            {
                var image = EsBitmap.FromPixmap(view);
                return new Item(image, BytesOf(image));
            });

            Measure("GetPixel", "ビュー 1000x800 から 100 万回", () =>
            {
                var total = 0;
                for (var i = 0; i < 1_000_000; i++)
                {
                    var pixel = view.GetPixel(i % view.Width, (i / view.Width) % view.Height);
                    total += pixel.B + pixel.G + pixel.R + pixel.A;
                }

                GC.KeepAlive(total);
                return new Item(null, 0);
            });
        }

        AddSection("4. 切り出しと 0 コピーのビュー", start, Columns.Dimensions);
    }

    private static void MeasureResize()
    {
        var start = s_rows.Count;

        using var full = ReadOnly(3840, 2160, Pattern.Mixed);
        using var half = ReadOnly(1920, 1080, Pattern.Gradient);

        foreach (var quality in s_qualities)
        {
            Measure("Resize", "3840x2160 → 長辺 320（" + quality + "）", () =>
            {
                var image = full.Resize(320, quality);
                return new Item(image, BytesOf(image));
            });

            Measure("Resize", "3840x2160 → 長辺 1920（" + quality + "）", () =>
            {
                var image = full.Resize(1920, quality);
                return new Item(image, BytesOf(image));
            });

            Measure("Resize", "1920x1080 → 長辺 96（" + quality + "）", () =>
            {
                var image = half.Resize(96, quality);
                return new Item(image, BytesOf(image));
            });

            Measure("Resize", "1920x1080 → 長辺 320（" + quality + "）", () =>
            {
                var image = half.Resize(320, quality);
                return new Item(image, BytesOf(image));
            });
        }

        using (var writable = Create(3840, 2160, Pattern.Mixed))
        {
            Measure("Resize", "EsBitmap 3840x2160 → 長辺 320（Mid）", () =>
            {
                var image = writable.Resize(320, EsImageQuality.Mid);
                return new Item(image, BytesOf(image));
            });
        }

        using (var view = full.ToPixmap(new EsRectInt(1200, 700, 1000, 800)))
        {
            foreach (var quality in s_qualities)
            {
                Measure("Resize", "ビュー 1000x800 → 長辺 320（" + quality + "）", () =>
                {
                    var image = view.Resize(320, quality);
                    return new Item(image, BytesOf(image));
                });
            }
        }

        AddSection("5. 縮小（長辺の上限へ収める）", start, Columns.Dimensions);
    }

    private static void MeasureReduceColors()
    {
        var start = s_rows.Count;

        using var flat = ReadOnly(1920, 1080, Pattern.Flat);
        using var gradient = ReadOnly(1920, 1080, Pattern.Gradient);
        using var noise = ReadOnly(1920, 1080, Pattern.Noise);

        foreach (var quality in s_qualities)
        {
            MeasureReduce(flat, "平坦（箱が少ない）", 256, quality, EsDitherMode.None);
            MeasureReduce(gradient, "グラデーション（箱が多い）", 256, quality, EsDitherMode.None);
            MeasureReduce(gradient, "グラデーション（箱が多い）", 16, quality, EsDitherMode.None);
            MeasureReduce(noise, "ノイズ（箱が最大）", 256, quality, EsDitherMode.None);
        }

        MeasureReduce(noise, "ノイズ（箱が最大）", 16, EsImageQuality.Mid, EsDitherMode.None);
        MeasureReduce(flat, "平坦（箱が少ない）", 16, EsImageQuality.Mid, EsDitherMode.FloydSteinberg);
        MeasureReduce(gradient, "グラデーション（箱が多い）", 16, EsImageQuality.Mid, EsDitherMode.FloydSteinberg);
        MeasureReduce(gradient, "グラデーション（箱が多い）", 256, EsImageQuality.Mid, EsDitherMode.FloydSteinberg);
        MeasureReduce(noise, "ノイズ（箱が最大）", 256, EsImageQuality.Mid, EsDitherMode.FloydSteinberg);

        using (var full = ReadOnly(3840, 2160, Pattern.Mixed))
        using (var view = full.ToPixmap(new EsRectInt(1200, 700, 1000, 800)))
        {
            Measure("ReduceColors", "ビュー 1000x800 → 256 色（Mid）", () =>
            {
                var image = view.ReduceColors(256, EsImageQuality.Mid);
                return new Item(image, BytesOf(image));
            });
        }

        AddSection("6. 減色（Low / Mid / High とディザ）", start, Columns.Dimensions);
    }

    private static void MeasureReduce(
        EsReadOnlyBitmap source, string target, int colors, EsImageQuality quality, EsDitherMode dither)
    {
        var label = dither == EsDitherMode.None
            ? target + " → " + colors.ToString(CultureInfo.InvariantCulture) + " 色（" + quality + "）"
            : target + " → " + colors.ToString(CultureInfo.InvariantCulture) + " 色（" + quality + " / ディザ " + dither + "）";

        Measure("ReduceColors", label, () =>
        {
            var image = source.ReduceColors(colors, quality, dither);
            return new Item(image, BytesOf(image));
        });
    }

    private static void MeasurePixelAccess()
    {
        var start = s_rows.Count;

        using var full = Create(3840, 2160, Pattern.Mixed);

        Measure("GetPixel", "3840x2160 から 100 万回", () =>
        {
            var total = 0L;
            for (var i = 0; i < 1_000_000; i++)
            {
                var pixel = full.GetPixel(i % full.Width, (i / full.Width) % full.Height);
                total += pixel.B + pixel.G + pixel.R + pixel.A;
            }

            GC.KeepAlive(total);
            return new Item(null, 0);
        });

        Measure("SetPixel", "3840x2160 へ 100 万回", () =>
        {
            for (var i = 0; i < 1_000_000; i++)
            {
                full.SetPixel(i % full.Width, (i / full.Width) % full.Height, 0x11, 0x22, 0x33, 0xFF);
            }

            return new Item(null, 0);
        });

        Measure("ReadOnlyPixels", "3840x2160 の全域を走査", () =>
        {
            var total = 0L;
            foreach (var value in full.ReadOnlyPixels)
            {
                total += value;
            }

            GC.KeepAlive(total);
            return new Item(null, 0);
        });

        Measure("Clear", "3840x2160 を透明で塗る", () =>
        {
            full.Clear(0, 0, 0, 0);
            return new Item(null, 0);
        });

        AddSection("7. 画素の取得・設定・走査", start, Columns.Plain);
    }

    [SupportedOSPlatform("windows")]
    private static void MeasureFileIo()
    {
        var start = s_rows.Count;

        using var full = Create(3840, 2160, Pattern.Mixed);
        using var half = Create(1920, 1080, Pattern.Gradient);

        Measure("Save", "3840x2160 → PNG", () =>
        {
            full.Save(PathTo("4k.png"));
            return new Item(null, 0);
        });

        Measure("Save", "3840x2160 → BMP", () =>
        {
            full.Save(PathTo("4k.bmp"));
            return new Item(null, 0);
        });

        Measure("Save", "1920x1080 → PNG（256 色へ減色）", () =>
        {
            half.Save(PathTo("1080.png"), new EsImageSaveOptions { ColorCount = 256 });
            return new Item(null, 0);
        });

        Measure("Save", "1920x1080 → GIF（256 色へ減色）", () =>
        {
            half.Save(PathTo("1080.gif"), new EsImageSaveOptions { ColorCount = 256 });
            return new Item(null, 0);
        });

        Measure("Save", "1920x1080 → JPEG（品質 90）", () =>
        {
            half.Save(PathTo("1080.jpg"), new EsImageSaveOptions { JpegQuality = 90 });
            return new Item(null, 0);
        });

        Measure("Load", "PNG 3840x2160", () =>
        {
            var image = EsBitmap.Load(PathTo("4k.png"));
            return new Item(image, BytesOf(image));
        });

        Measure("Load", "PNG 1920x1080（256 色）", () =>
        {
            var image = EsBitmap.Load(PathTo("1080.png"));
            return new Item(image, BytesOf(image));
        });

        AddSection("8. ファイル入出力（Windows 専用の符号化）", start, Columns.FileSize);
    }

    private static string PathTo(string name)
    {
        var directory = Path.Combine(Path.GetTempPath(), "SaveEvidence.Measurement.Files");
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, name);
    }

    /// <summary>計測で書いたファイルを片付ける（次回の計測へ持ち越さない）。</summary>
    private static void CleanUpFiles()
    {
        var directory = Path.Combine(Path.GetTempPath(), "SaveEvidence.Measurement.Files");
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static void Measure(string operation, string target, Func<Item> body)
    {
        for (var i = 0; i < s_warmup; i++)
        {
            body().Dispose();
        }

        var times = new double[s_iterations];
        var managed = 0L;
        var bytes = 0L;

        for (var i = 0; i < s_iterations; i++)
        {
            var before = GC.GetAllocatedBytesForCurrentThread();
            var started = Stopwatch.GetTimestamp();
            var item = body();
            var elapsed = Stopwatch.GetElapsedTime(started);
            managed += GC.GetAllocatedBytesForCurrentThread() - before;
            bytes = item.Bytes;
            item.Dispose();
            times[i] = elapsed.TotalMilliseconds;
        }

        Array.Sort(times);
        s_rows.Add(new Row(operation, target, times[times.Length / 2], managed / s_iterations, bytes, OutputSize(target)));
    }

    private static long OutputSize(string target)
    {
        var name = target switch
        {
            "3840x2160 → PNG" => "4k.png",
            "3840x2160 → BMP" => "4k.bmp",
            "1920x1080 → PNG（256 色へ減色）" => "1080.png",
            "1920x1080 → GIF（256 色へ減色）" => "1080.gif",
            "1920x1080 → JPEG（品質 90）" => "1080.jpg",
            _ => null,
        };

        if (name is null)
        {
            return 0;
        }

        var path = PathTo(name);
        return File.Exists(path) ? new FileInfo(path).Length : 0;
    }

    private static void AddSection(string title, int start, Columns columns)
    {
        s_report.AppendLine("## " + title);
        s_report.AppendLine();

        switch (columns)
        {
            case Columns.Dimensions:
                s_report.AppendLine("| 操作 | 対象 | 所要 (ms) | マネージド確保 (B) | 画素のバイト数 (B) |");
                s_report.AppendLine("| --- | --- | --- | --- | --- |");
                break;

            case Columns.FileSize:
                s_report.AppendLine("| 操作 | 対象 | 所要 (ms) | マネージド確保 (B) | 出力ファイル (B) |");
                s_report.AppendLine("| --- | --- | --- | --- | --- |");
                break;

            default:
                s_report.AppendLine("| 操作 | 対象 | 所要 (ms) | マネージド確保 (B) |");
                s_report.AppendLine("| --- | --- | --- | --- |");
                break;
        }

        foreach (var row in s_rows.Skip(start))
        {
            var line = "| " + row.Operation + " | " + row.Target + " | " + Format(row.Milliseconds)
                + " | " + row.Managed.ToString("N0", CultureInfo.InvariantCulture);

            line += columns switch
            {
                Columns.Dimensions => " | " + row.Bytes.ToString("N0", CultureInfo.InvariantCulture) + " |",
                Columns.FileSize => " | " + row.Note.ToString("N0", CultureInfo.InvariantCulture) + " |",
                _ => " |",
            };

            s_report.AppendLine(line);
        }

        s_report.AppendLine();
    }

    private static string Format(double milliseconds)
        => milliseconds < 1.0
            ? milliseconds.ToString("F4", CultureInfo.InvariantCulture)
            : milliseconds.ToString("F3", CultureInfo.InvariantCulture);

    private static long BytesOf(EsBitmap image) => (long)image.Width * EsBitmap.BytesPerPixel * image.Height;

    private static long BytesOf(EsReadOnlyBitmap image) => (long)image.Width * EsReadOnlyBitmap.BytesPerPixel * image.Height;

    private static EsBitmap Create(int width, int height, Pattern pattern)
        => EsBitmap.CreateFilled(width, height, pixels => Fill(pixels, width, height, pattern));

    private static EsReadOnlyBitmap ReadOnly(int width, int height, Pattern pattern)
        => EsReadOnlyBitmap.CreateFilled(width, height, pixels => Fill(pixels, width, height, pattern));

    private static void Fill(Span<byte> pixels, int width, int height, Pattern pattern)
    {
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                byte b;
                byte g;
                byte r;

                switch (pattern)
                {
                    case Pattern.Flat:
                        {
                            var block = (((x / 32) + (y / 32)) % 2) == 0;
                            b = block ? (byte)0x30 : (byte)0x90;
                            g = 0x60;
                            r = block ? (byte)0x90 : (byte)0x30;
                            break;
                        }

                    case Pattern.Gradient:
                        {
                            b = (byte)(((long)x * 255) / (width - 1));
                            g = (byte)(((long)y * 255) / (height - 1));
                            r = (byte)((((long)x + y) * 255) / (width + height - 2));
                            break;
                        }

                    case Pattern.Noise:
                        {
                            var seed = Noise(x, y);
                            b = (byte)seed;
                            g = (byte)(seed >> 8);
                            r = (byte)(seed >> 16);
                            break;
                        }

                    default:
                        {
                            var seed = Noise(x, y);
                            var block = (((x / 96) + (y / 96)) % 3) == 0;
                            b = block ? (byte)0x28 : (byte)seed;
                            g = block ? (byte)0x50 : (byte)(seed >> 8);
                            r = block ? (byte)0x78 : (byte)(seed >> 16);
                            break;
                        }
                }

                var offset = ((y * width) + x) * EsBitmap.BytesPerPixel;
                pixels[offset] = b;
                pixels[offset + 1] = g;
                pixels[offset + 2] = r;
                pixels[offset + 3] = 0xFF;
            }
        }
    }

    private static uint Noise(int x, int y)
    {
        var seed = (uint)((y * 0x9E3779B1) ^ (x * 0x85EBCA6B));
        seed ^= seed << 13;
        seed ^= seed >> 17;
        seed ^= seed << 5;
        return seed;
    }

    private enum Pattern
    {
        Flat,
        Gradient,
        Noise,
        Mixed,
    }

    private enum Columns
    {
        Plain,
        Dimensions,
        FileSize,
    }

    private readonly record struct Row(
        string Operation, string Target, double Milliseconds, long Managed, long Bytes, long Note);

    private readonly struct Item : IDisposable
    {
        private readonly IDisposable? _value;

        public Item(IDisposable? value, long bytes)
        {
            _value = value;
            Bytes = bytes;
        }

        public long Bytes { get; }

        public void Dispose() => _value?.Dispose();
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

Write-Output $measuredText

if ($NoWrite) {
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
