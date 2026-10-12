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

<#
.SYNOPSIS
本文の見出しから目次の行（`##` 以下。 `##` は 0 文字、`###` は 2 文字の字下げ）を作る。

.DESCRIPTION
アンカーは記号を除去して小文字化し、空白をハイフンへ変えたもの（.github/検証手順.md §11）。
`verify.ps1` の体裁検査と同じ規則である（本ドキュメントは SPEC_*.md ではないため、その検査の対象外）。
#>
function Get-TocLines {
    param([string] $Text)

    $result = [System.Collections.Generic.List[string]]::new()
    $inFence = $false
    foreach ($line in ($Text -split "`n")) {
        if ($line.StartsWith('```')) { $inFence = -not $inFence; continue }
        if ($inFence) { continue }
        if ($line -match '^(#{1,6}) (.+)$') {
            $level = $Matches[1].Length
            if ($level -lt 2 -or $level -gt 4) { continue }
            $heading = $Matches[2]
            if ($level -eq 2 -and $heading -match '^目次( <!-- omit from toc -->)?$') { continue }
            $slug = [regex]::Replace($heading, '[^\p{L}\p{M}\p{Nd}\p{Pc}\- ]', '')
            $slug = $slug.ToLowerInvariant() -replace '\s', '-'
            $result.Add((' ' * (($level - 2) * 2)) + "- [$heading](#$slug)")
        }
    }

    return , $result
}

<#
.SYNOPSIS
本文の「## 目次」に並んでいる行を取り出す（無い場合は空）。
#>
function Get-TocBlock {
    param([string] $Text)

    $result = [System.Collections.Generic.List[string]]::new()
    $lines = $Text -split "`n"
    $inToc = $false
    foreach ($line in $lines) {
        if (-not $inToc) {
            if ($line -match '^## 目次( <!-- omit from toc -->)?$') { $inToc = $true }
            continue
        }

        if ($line -match '^\s*- ') { $result.Add($line); continue }
        if ($line.Trim() -eq '' -and $result.Count -eq 0) { continue }
        break
    }

    return , $result
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

    /// <summary>速度比を示す下限（基準の所要 (ms) がこれ未満の表では速度比を示さない）。</summary>
    /// <remarks>ばらつきが比率を支配するためです（計測のばらつきは所要の 5〜10 % 程度あります）。</remarks>
    private const double RatioFloorMilliseconds = 0.001;

    /// <summary>減色の品質の段階を畳むかどうかの境目（最大と最小の所要の比）。</summary>
    /// <remarks>
    /// 速度差がこれ以下なら、品質の段階を選ぶ意味が無いため表の行を畳みます。
    /// 実測では 16 色の差が 1.0〜1.05 倍、256 色の差が 1.14〜1.20 倍であるため、その間の 1.10 を境目にします
    /// （境目を実測の近くへ置くと、実行のたびに表の形が変わります）。
    /// </remarks>
    private const double QualityCollapseRatio = 1.10;

    /// <summary>低い周波数（なだらかな変化）の一致度を測るときの平均の大きさ（画素）。</summary>
    /// <remarks>
    /// <b>1 画素ごとの差では見えないものを測る理由</b>: ディザは<b>わざと 1 画素ごとの差を増やして</b>、
    /// なだらかな変化に出る縞（階調の飛び）を散らします。 そのため 1 画素ごとの差（PSNR）は下がりますが、
    /// <b>見た目は良くなります</b>。 目のぼけを模して <c>n×n</c> の平均で比べると、差の平均が打ち消し合うため
    /// <b>ディザありのほうが一致します</b>。 この 2 つを並べることで「ディザが何と引き換えに何を得るか」が読めます。
    /// </remarks>
    private const int LowFrequencyBlock = 4;

    /// <summary>低い周波数の一致度を記録するときの節の名（同じ鍵で別の値を測るため節を分ける）。</summary>
    private const string LowFrequencySuffix = "（低周波）";

    /// <summary>計測に使う画像を生成する。</summary>
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

    /// <summary>減色の出力の品質を測る色数。</summary>
    private static readonly int[] s_qualityColors = [256, 16];

    private static readonly string[] s_classNames = [ClassBit, ClassRead, ClassView];
    private static readonly string[] s_sizeLabels = [.. s_sizes.Select(static s => s.Label)];
    private static readonly EsImageQuality[] s_qualities = [EsImageQuality.Low, EsImageQuality.Mid, EsImageQuality.High];
    private static readonly string[] s_qualityLabels = [.. s_qualities.Select(static q => q.ToString())];
    private static readonly Dictionary<(string, string, string, string, string), Measured> s_cases = [];
    private static readonly Dictionary<(string, string, string, string, string), double> s_values = [];
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

            // 品質の基準は面積平均（最も正確な縮小）の結果とする
            using var reference = read.Resize(ResizeMaxSize, EsImageQuality.High);

            foreach (var quality in s_qualities)
            {
                var variant = quality.ToString();
                Measure(SectionResize, "Resize", ClassBit, label, variant, () => new Item(bit.Resize(ResizeMaxSize, quality)));
                Measure(SectionResize, "Resize", ClassRead, label, variant, () => new Item(read.Resize(ResizeMaxSize, quality)));
                Measure(SectionResize, "Resize", ClassView, label, variant, () => new Item(view.Resize(ResizeMaxSize, quality)));

                RecordQuality(SectionResize, "Resize", ClassBit, label, variant, () => reference.ReadOnlyPixels, reference.Width, reference.Height, reference.Stride, () => bit.Resize(ResizeMaxSize, quality));
                RecordQuality(SectionResize, "Resize", ClassRead, label, variant, () => reference.ReadOnlyPixels, reference.Width, reference.Height, reference.Stride, () => read.Resize(ResizeMaxSize, quality));
                RecordQuality(SectionResize, "Resize", ClassView, label, variant, () => reference.ReadOnlyPixels, reference.Width, reference.Height, reference.Stride, () => view.Resize(ResizeMaxSize, quality));
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

    /// <summary>減色を型・色数・ディザ・品質・寸法ごとに計測する。</summary>
    private static void MeasureReduceColors()
    {
        using var source = Create(new SizeCase("source", 3840, 2160));

        foreach (var size in s_sizes)
        {
            var label = size.Label;
            using var bit = Create(size);
            using var read = ReadOnly(size);
            using var view = source.ToPixmap(new EsRectInt(0, 0, size.Width, size.Height));

            // 時間の表は品質（256 色・ディザなし）とディザ（16 / 256 色・Mid）を使い、品質の表は
            // ディザ × 品質の全組み合わせを使うため、全組み合わせを測る
            foreach (var colors in s_qualityColors)
            {
                foreach (var dither in new[] { EsDitherMode.None, EsDitherMode.FloydSteinberg })
                {
                    foreach (var quality in s_qualities)
                    {
                        var variant = ReduceVariant(colors, quality, dither);
                        Measure(SectionReduce, "ReduceColors", ClassBit, label, variant, () => new Item(bit.ReduceColors(colors, quality, dither)));
                        Measure(SectionReduce, "ReduceColors", ClassRead, label, variant, () => new Item(read.ReduceColors(colors, quality, dither)));
                        Measure(SectionReduce, "ReduceColors", ClassView, label, variant, () => new Item(view.ReduceColors(colors, quality, dither)));

                        RecordQuality(SectionReduce, "ReduceColors", ClassBit, label, variant, () => bit.ReadOnlyPixels, bit.Width, bit.Height, bit.Stride, () => bit.ReduceColors(colors, quality, dither));
                        RecordQuality(SectionReduce, "ReduceColors", ClassRead, label, variant, () => read.ReadOnlyPixels, read.Width, read.Height, read.Stride, () => read.ReduceColors(colors, quality, dither));
                        RecordQuality(SectionReduce, "ReduceColors", ClassView, label, variant, () => view.ReadOnlyPixels, view.Width, view.Height, view.Stride, () => view.ReduceColors(colors, quality, dither));
                    }
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

            Measure(SectionFile, "Save", ClassBit, label, "", () => new Item(null, OutputSize(bit, path)), s_fileIterations);
            Measure(SectionFile, "Save", ClassRead, label, "", () => new Item(null, OutputSize(read, path)), s_fileIterations);

            // 形式ごとの符号化（出力の大きさも同じ計測で採る）
            foreach (var format in s_formats)
            {
                var formatPath = Path.Combine(FileDirectory(), $"{size.Width}x{size.Height}{format.Extension}");
                Measure(SectionFile, format.Method, ClassBit, label, "", () => new Item(null, OutputSize(bit, formatPath, format.Options)), s_fileIterations);
                Measure(SectionFile, format.Method, ClassRead, label, "", () => new Item(null, OutputSize(read, formatPath, format.Options)), s_fileIterations);
            }

            Measure(SectionFile, "Load", ClassBit, label, "", () => new Item(EsBitmap.Load(path)), s_fileIterations);
            Measure(SectionFile, "Load", ClassRead, label, "", () => new Item(EsReadOnlyBitmap.Load(path)), s_fileIterations);
        }
    }

    /// <summary>保存して出力の大きさ (B) を返す。</summary>
    [SupportedOSPlatform("windows")]
    private static long OutputSize(EsBitmap image, string path, EsImageSaveOptions? options = null)
    {
        if (options is null)
        {
            image.Save(path);
        }
        else
        {
            image.Save(path, options.Value);
        }

        return new FileInfo(path).Length;
    }

    /// <summary>保存して出力の大きさ (B) を返す。</summary>
    [SupportedOSPlatform("windows")]
    private static long OutputSize(EsReadOnlyBitmap image, string path, EsImageSaveOptions? options = null)
    {
        if (options is null)
        {
            image.Save(path);
        }
        else
        {
            image.Save(path, options.Value);
        }

        return new FileInfo(path).Length;
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

    /// <summary>1 ケースを計測する（ウォームアップ後、中央値を採る）。</summary>
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
        var note = 0L;

        for (var i = 0; i < count; i++)
        {
            var started = Stopwatch.GetTimestamp();
            for (var r = 0; r < repeat; r++)
            {
                var item = body();
                note = item.Note;
                item.Dispose();
            }

            times[i] = Stopwatch.GetElapsedTime(started).TotalMilliseconds / repeat;
        }

        Array.Sort(times);
        s_cases[(section, method, className, size, variant)] = new Measured(times[times.Length / 2]);

        // 表に付ける値（Save は出力の大きさ (B)）は最後の 1 回の値を採る
        if (note > 0)
        {
            s_values[(section, method, "", size, variant)] = note;
        }
    }

    /// <summary>本体を指定の回数だけ呼び、結果を破棄する。</summary>
    private static void RunRepeated(Func<Item> body, int repeat)
    {
        for (var r = 0; r < repeat; r++)
        {
            body().Dispose();
        }
    }

    /// <summary>出力の品質（基準との PSNR (dB)）を求めて記録する。</summary>
    /// <remarks>
    /// <b>所要の計測と混ぜない理由</b>: PSNR の計算は全画素を走査するため、計測の本体へ混ぜると
    /// <b>所要が PSNR の分だけ増えます</b>。<br/>
    /// <b>行の間隔を受け取る理由</b>: ビュー（<c>EsPixmap</c>）は元画像の行の間隔を引き継ぐため、
    /// 画素の範囲の長さが生成した画像と一致しません（間隔を使わずに比べると範囲外を読みます）。
    /// </remarks>
    private static void RecordQuality(
        string section, string method, string className, string size, string variant,
        Func<ReadOnlySpan<byte>> reference, int width, int height, int referenceStride,
        Func<EsBitmap> produce)
    {
        using var produced = produce();
        var referencePixels = reference();
        s_values[(section, method, className, size, variant)] =
            Psnr(width, height, referencePixels, referenceStride, produced.ReadOnlyPixels, produced.Stride);

        // 1 画素ごとの差では、ディザの効果（縞の解消）が見えないため、低い周波数の一致度も測る
        s_values[(section + LowFrequencySuffix, method, className, size, variant)] =
            PsnrLowFrequency(width, height, referencePixels, referenceStride, produced.ReadOnlyPixels, produced.Stride);
    }

    /// <summary>2 つの画素の PSNR (dB) を求める（透明度は比べない）。</summary>
    private static double Psnr(int width, int height, ReadOnlySpan<byte> reference, int referenceStride, ReadOnlySpan<byte> target, int targetStride)
    {
        var rowBytes = width * EsBitmap.BytesPerPixel;
        var sum = 0.0;
        var count = 0;
        for (var y = 0; y < height; y++)
        {
            var referenceRow = reference.Slice(y * referenceStride, rowBytes);
            var targetRow = target.Slice(y * targetStride, rowBytes);
            for (var x = 0; x < rowBytes; x += EsBitmap.BytesPerPixel)
            {
                for (var channel = 0; channel < 3; channel++)
                {
                    var difference = referenceRow[x + channel] - targetRow[x + channel];
                    sum += (double)difference * difference;
                    count++;
                }
            }
        }

        return ToDecibels(sum / count);
    }

    /// <summary><see cref="LowFrequencyBlock"/> の平均で見た PSNR (dB) を求める（目のぼけを模した比べ方）。</summary>
    /// <remarks>
    /// 最小の 2 乗平均で測るため、1 画素ごとの差が大きくても<b>平均すると打ち消し合えば</b>値は高くなります
    /// （ディザの狙いがこれです）。
    /// </remarks>
    private static double PsnrLowFrequency(int width, int height, ReadOnlySpan<byte> reference, int referenceStride, ReadOnlySpan<byte> target, int targetStride)
    {
        var sum = 0.0;
        var count = 0;
        for (var y = 0; y < height; y += LowFrequencyBlock)
        {
            var rows = Math.Min(LowFrequencyBlock, height - y);
            var referenceRow = reference.Slice(y * referenceStride);
            var targetRow = target.Slice(y * targetStride);
            for (var x = 0; x < width; x += LowFrequencyBlock)
            {
                var columns = Math.Min(LowFrequencyBlock, width - x);
                for (var channel = 0; channel < 3; channel++)
                {
                    var referenceSum = 0;
                    var targetSum = 0;
                    for (var row = 0; row < rows; row++)
                    {
                        var offset = (row * EsBitmap.BytesPerPixel) + (x * EsBitmap.BytesPerPixel) + channel;
                        for (var column = 0; column < columns; column++)
                        {
                            referenceSum += referenceRow[offset + (column * EsBitmap.BytesPerPixel)];
                            targetSum += targetRow[offset + (column * EsBitmap.BytesPerPixel)];
                        }
                    }

                    var average = (referenceSum - targetSum) / (double)(rows * columns);
                    sum += average * average;
                    count++;
                }
            }
        }

        return ToDecibels(sum / count);
    }

    /// <summary>最小の 2 乗平均を PSNR (dB) へ変える（差が無い場合は無限大）。</summary>
    private static double ToDecibels(double mse)
        => mse <= 0 ? double.PositiveInfinity : 10.0 * Math.Log10(255.0 * 255.0 / mse);

    // ====================================================================
    // 表の組み立て
    // ====================================================================

    private static void Emit()
    {
        // 桁を揃える詰め物の空白は MD060（表の桁揃え）が禁じるため、計測結果の範囲だけ無効化する
        // （.github/検証手順.md §11）
        s_report.Add("<!-- markdownlint-disable MD060 -->");
        s_report.Add("");
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
        s_report.Add("<!-- markdownlint-enable MD060 -->");
    }

    private static void AppendEnvironment()
    {
        AddHeading("## 2. 計測環境");
        var environment = new (string Item, string Value)[]
        {
            ("計測日時", DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture)),
            ("OS", RuntimeInformation.OSDescription.Trim()),
            ("プロセッサ", ProcessorName()),
            ("論理コア", string.Create(CultureInfo.InvariantCulture, $"{Environment.ProcessorCount} / {RuntimeInformation.ProcessArchitecture}")),
            (".NET", Environment.Version.ToString()),
            ("サーバー GC", GCSettings.IsServerGC.ToString()),
            ("構成", "Release（最適化あり）"),
            ("ウォームアップ / 計測回数", string.Create(CultureInfo.InvariantCulture, $"{s_warmup} 回 / {s_iterations} 回（中央値。 ファイル入出力は {s_fileIterations} 回、画素の取得と設定は {s_pixelIterations} 回）")),
        };

        AddPlainTable(["項目", "値"], environment.Select(static e => new[] { e.Item, e.Value }));
    }

    /// <summary>プロセッサの名前を返す（計測スクリプトが Win32_Processor から渡す）。</summary>
    private static string ProcessorName()
    {
        var name = Environment.GetEnvironmentVariable("SAVEEVIDENCE_CPU_NAME");
        return string.IsNullOrWhiteSpace(name) ? "不明（結果の所有者は文書ではなく計測環境です）" : name.Trim();
    }

    private static void AppendTargets()
    {
        AddHeading("## 3. 計測の対象");
        AddText("計測するのは **公開 API だけ**です（内部型の名前を計測コードへ埋め込まないため、実装を変えても計測が壊れません）。 対象は次の 3 型で、すべての表が**どの型を計測したか**を行または節の軸に持ちます。");
        AddPlainTable(["型", "何であるか", "計測に使う入口"], [
            [ClassBit, "書き換え可能な画像", "`Create` 系・`SetPixel`・`Pixels`・`Clear`・`SetReadOnly`"],
            [ClassRead, "読み取り専用の画像", "`Create` 系・`FromBitmap`・`Copy`・`Save`・`Load`"],
            [ClassView, "画像の一部を指す 0 コピーの読み取りビュー", "`ToPixmap`（生成の唯一の入口）・`Resize`・`ReduceColors`"],
        ]);

        AddText("**所要の表**は、行を型・品質・内容など、列を寸法として、行の 1 番目（表ごとに定めた基準）を 100% とした**速度比**を足します。");
        AddText("所要はセルごとに単位 `ms` を付け、**列の中で桁を揃えて右揃え**にします（0.000001 ms（1 ナノ秒）まで表示し、それ未満は `<0.000001` と書きます）。 `-` はその型にメソッドが無い（対称でない）ことを表します。 1 マイクロ秒を下回る操作は、タイマーの分解能に埋もれるため **1 回の計測につき 100 回呼び、1 回あたりへ換算**します。 **値はコードスパンで囲まず**、桁を揃える詰め物の空白はそのまま書きます（`MD060` を計測結果の範囲だけ無効化しています。 [`.github/検証手順.md`](../../../.github/検証手順.md) §11）。");
        AddText($"**速度比**は同じ寸法の 1 行目との比です（`基準の所要 ÷ その行の所要`。 100% より大きいほど速い）。 速度比の列は、寸法の列と同じ順に並べます。 基準の所要が {RatioFloorMilliseconds} ms 未満の表では示しません（ばらつきが比率を支配するため）。");

        AddText("**寸法の軸**は元の画像（`EsPixmap` はビュー）の寸法です。 縮小だけは**縮小先を長辺 320 に固定**し（型と寸法の比較を揃えるため）、縮小先を変えた表を別に示します。 減色は 256 色を既定とし、色数とディザの表を別に示します。");

        AddText($"計測に使う画像は**折り返す勾配**です（B = X % {PatternPeriod}・G = Y % {PatternPeriod}・R = (X + Y) % {PatternPeriod}・A = 255。 決定的で、実行のたびに同じ画像になります）。 折り返すのは、画像の一部を切り出したビューと元画像とで**色の範囲を揃える**ためです（減色の費用は色の範囲で決まるため、折り返さない勾配では内容の違うもの同士を比べることになります）。 減色だけは内容の影響が大きいため、内容を変えた表を別に示します。");

        AddText("**品質の表**は、出力がどれだけ元の画素に近いかを **PSNR (dB)** で示します（大きいほど近い）。 縮小は**面積平均の結果を基準**とし（最も正確な縮小）、減色は**元画像を基準**とします。 1 画素ごとの差では**ディザの狙い（縞を散らすこと）が見えない**ため、減色は**縞の出にくさ**（" + LowFrequencyBlock + "×" + LowFrequencyBlock + " の平均で見た PSNR）も示します（§7.9・§7.11）。");

        AddText("画素の領域（`幅 × 4 × 高さ`）は**アンマネージド**であり、所要の表には現れません（ビューは `行間隔 × 高さ`）。");
        AddValueGrid("項目", s_sizeLabels, ["画素の領域 (B)"], [s_sizes.Select(PixelBytes).ToArray()]);

        AddText("1 回だけ呼ぶ操作（`Dispose` / `ThrowIfDisposed`）と、実体ごとに 1 度しか呼べない操作（`SetReadOnly`）は、所要が他の処理に支配されるため計測しません。 `ToString` 系（`ToSizeText`）と、幅・高さ・行の間隔などのプロパティ（フィールドの読み取りだけ）も同様です。");
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
            AddTimeGrid(
                "型",
                s_sizeLabels,
                s_classNames,
                [.. s_classNames.Select(cls => s_sizes.Select(s => Time(section, methods[i], cls, s.Label)).ToArray())],
                (row, column) => Time(section, methods[i], s_classNames[row], s_sizes[column].Label));
        }
    }

    /// <summary>FromBitmap の凍結の有無を「状態 × 寸法」の 2 軸表で出力する。</summary>
    private static void AppendFromBitmapSection(int number, int sub)
    {
        AddHeading($"### {number}.{sub} FromBitmap（凍結の有無）");
        AddText($"書き換え可能な画像から読み取り専用の画像を作る唯一の入口です（`{ClassBit}` からは作れません）。 行を凍結の有無・列を寸法とした 2 軸表を示します。 凍結済みは 0 コピーで共有し、未凍結は複製します。");
        string[] variants = [FrozenNo, FrozenYes];
        AddTimeGrid(
            "状態",
            s_sizeLabels,
            variants,
            [.. variants.Select(v => s_sizes.Select(s => Time(SectionCreation, "FromBitmap", ClassRead, s.Label, v)).ToArray())],
            (row, column) => Time(SectionCreation, "FromBitmap", ClassRead, s_sizes[column].Label, variants[row]));
    }

    /// <summary>Save の出力の大きさを「形式 × 寸法」の 2 軸表で出力する。</summary>
    private static void AppendOutputSizeSection(int number, int sub)
    {
        AddHeading($"### {number}.{sub} Save の出力の大きさ");
        AddText($"保存したファイルの大きさ (B) です（行を形式・列を寸法とした 2 軸表）。 `{ClassBit}` で測った値です（`{ClassRead}` も同じ画素を書くため同じ大きさになります）。 画像の内容は折り返す勾配であり、内容で圧縮の効きが変わります。 拡張子から判定する版と形式を指定した版は同じ大きさになります（同じ処理であることの確認になります）。");

        var labels = new List<string> { "PNG（`Save(path)` 既定）" };
        labels.AddRange(s_formats.Select(static f => f.Label));
        var values = new List<string[]>
        {
            s_sizes.Select(s => Size(SectionFile, "Save", s.Label)).ToArray(),
        };
        values.AddRange(s_formats.Select(f => s_sizes.Select(s => Size(SectionFile, f.Method, s.Label)).ToArray()));
        AddValueGrid("形式", s_sizeLabels, [.. labels], [.. values]);
    }

    /// <summary>縮小先を変えた行の所要を返す（長辺 320 は品質の表（元画像 3840x2160）の計測を使い回す）。</summary>
    private static double? ResizeTargetTime(string className, int maxSize, EsImageQuality quality)
        => maxSize == ResizeMaxSize
            ? Time(SectionResize, "Resize", className, s_sizes[0].Label, quality.ToString())
            : Time(SectionResize, "Resize", className, TargetKey(maxSize), quality.ToString());

    /// <summary>縮小を型ごとに「品質 × 寸法」の 2 軸表で出力する。</summary>
    private static void AppendResizeSection(int number)
    {
        AddHeading($"## {number}. 縮小（長辺 {ResizeMaxSize} へ収める）");
        AddText($"縮小先を**長辺 {ResizeMaxSize} に固定**し、型ごとに、行を品質・列を元画像の寸法とした 2 軸表を示します。");

        var sub = 0;
        for (var i = 0; i < s_classNames.Length; i++)
        {
            var cls = s_classNames[i];
            AddHeading($"### {number}.{++sub} {cls}");
            AddTimeGrid(
                "品質",
                s_sizeLabels,
                s_qualities.Select(static q => q.ToString()).ToArray(),
                [.. s_qualities.Select(q => s_sizes.Select(s => Time(SectionResize, "Resize", cls, s.Label, q.ToString())).ToArray())],
                (row, column) => Time(SectionResize, "Resize", cls, s_sizes[column].Label, s_qualities[row].ToString()));
        }

        // 縮小先の大小で品質ごとの費用の順序が変わるため、縮小先を変えた表も示す
        for (var i = 0; i < s_classNames.Length; i++)
        {
            var cls = s_classNames[i];
            AddHeading($"### {number}.{++sub} {cls}（縮小先を変えた場合）");
            AddText($"元画像は {s_sizes[0].Label} に固定し、行を縮小先・列を品質とした 2 軸表を示します。 長辺 {ResizeMaxSize} は品質の表と同じ計測です。");
            var targets = new List<int> { ResizeMaxSize };
            targets.AddRange(s_resizeTargets);
            AddTimeGrid(
                "縮小先",
                s_qualityLabels,
                targets.Select(TargetLabel).ToArray(),
                [.. targets.Select(t => s_qualities.Select(q => ResizeTargetTime(cls, t, q)).ToArray())],
                (row, column) => ResizeTargetTime(cls, targets[row], s_qualities[column]));
        }

        // 品質は出力がどれだけ面積平均（最も正確な縮小）に近いかで測る
        var qualitySub = sub + 1;
        AddHeading($"### {number}.{qualitySub} 出力の品質（品質 × 寸法）");
        AddText($"縮小先を長辺 {ResizeMaxSize} に固定し、**面積平均（{EsImageQuality.High}）の結果を基準**とした PSNR (dB) です（大きいほど基準に近い）。 行を品質・列を元画像の寸法とした 2 軸表を示します。 " + QualityTypeNote(SectionResize, "Resize", s_sizes[0].Label, EsImageQuality.Low.ToString()));
        AddValueGrid(
            "品質",
            s_sizeLabels,
            s_qualityLabels,
            [.. s_qualities.Select(q => s_sizes.Select(s => Decibels(SectionResize, "Resize", ClassBit, s.Label, q.ToString())).ToArray())],
            (row, column) => Time(SectionResize, "Resize", ClassBit, s_sizes[column].Label, s_qualities[row].ToString()));
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
            AddTimeGrid(
                "品質（256 色）",
                s_sizeLabels,
                s_qualityLabels,
                [.. s_qualities.Select(q => s_sizes.Select(s => Time(SectionReduce, "ReduceColors", cls, s.Label, ReduceVariant(256, q, EsDitherMode.None))).ToArray())],
                (row, column) => Time(SectionReduce, "ReduceColors", cls, s_sizes[column].Label, ReduceVariant(256, s_qualities[row], EsDitherMode.None)));

            AddHeading($"### {number}.{++sub} {cls}（ディザ）");
            var ditherRows = new List<(string Label, int Colors, EsDitherMode Dither)>();
            foreach (var dither in new[] { EsDitherMode.None, EsDitherMode.FloydSteinberg })
            {
                foreach (var colors in s_qualityColors.Reverse())
                {
                    ditherRows.Add((DitherLabel(colors, dither), colors, dither));
                }
            }

            AddTimeGrid(
                "指定（Mid）",
                s_sizeLabels,
                ditherRows.Select(static r => r.Label).ToArray(),
                [.. ditherRows.Select(r => s_sizes.Select(s => Time(SectionReduce, "ReduceColors", cls, s.Label, ReduceVariant(r.Colors, EsImageQuality.Mid, r.Dither))).ToArray())],
                (row, column) => Time(SectionReduce, "ReduceColors", cls, s_sizes[column].Label, ReduceVariant(ditherRows[row].Colors, EsImageQuality.Mid, ditherRows[row].Dither)));
        }

        // 減色の費用は「画素が入った箱の数」で決まる（内容を変えた表も示す）
        var ditherNone = ReduceVariant(256, EsImageQuality.Mid, EsDitherMode.None);
        AddHeading($"### {number}.{++sub} 内容による違い（{s_sizes[0].Label}・Mid・256 色）");
        AddText("減色の費用は**画素が入った箱の数**で決まるため、内容を変えた表を示します（行を型・列を内容とした 2 軸表）。 平坦は箱が 1 つで下限の側、ノイズは箱が最大で上限の側です。");
        AddTimeGrid(
            "型",
            s_reduceContents.Select(static c => c.Label).ToArray(),
            s_classNames,
            [.. s_classNames.Select(cls => s_reduceContents.Select(c => Time(SectionReduce, "ReduceColors", cls, c.Key, ditherNone)).ToArray())],
            (row, column) => Time(SectionReduce, "ReduceColors", s_classNames[row], s_reduceContents[column].Key, ditherNone));

        // 品質は元画像との差で測る（ディザは縞を減らす代わりに 1 画素ごとの差を増やす）
        foreach (var colors in s_qualityColors)
        {
            AppendReduceQuality(number, ref sub, colors);
        }
    }

    /// <summary>減色の出力の品質を「ディザ × 品質 × 寸法」の 2 軸表で出力する（低周波の一致度も続けて示す）。</summary>
    private static void AppendReduceQuality(int number, ref int sub, int colors)
    {
        // 品質の段階で速度が変わらないなら、選ぶ意味が無いため品質の行を畳む
        var collapsed = IsQualityCollapsed(SectionReduce, "ReduceColors", colors, EsDitherMode.None)
            && IsQualityCollapsed(SectionReduce, "ReduceColors", colors, EsDitherMode.FloydSteinberg);

        AddHeading($"### {number}.{++sub} 出力の品質（{colors} 色）");
        var dithers = new[] { EsDitherMode.None, EsDitherMode.FloydSteinberg };
        var rows = new List<(string Label, EsImageQuality Quality, EsDitherMode Dither)>();
        foreach (var dither in dithers)
        {
            if (collapsed)
            {
                // 速度差が無い場合は最も高い品質だけを示す（品質の名前も書かない）
                rows.Add((DitherOnlyLabel(dither), EsImageQuality.High, dither));
            }
            else
            {
                foreach (var quality in s_qualities)
                {
                    rows.Add(($"{DitherOnlyLabel(dither)}・{quality}", quality, dither));
                }
            }
        }

        AddText($"**元画像を基準**とした PSNR (dB) です（大きいほど元に近い）。 行をディザ" + (collapsed ? "・列を元画像の寸法" : "と品質・列を元画像の寸法") + "とした 2 軸表を示します。 " + QualityTypeNote(SectionReduce, "ReduceColors", s_sizes[0].Label, ReduceVariant(colors, rows[0].Quality, rows[0].Dither)));
        if (collapsed)
        {
            AddText($"品質の段階（Low / Mid / High）による所要の**最大と最小の比が {QualityCollapseRatio:F2} 倍以内**だったため、**品質の行を畳んでいます**（同じ所要で品質が最も高い {EsImageQuality.High} だけを示します）。");
        }
        else
        {
            AddText($"品質の段階（Low / Mid / High）による所要の**最大と最小の比が {QualityCollapseRatio:F2} 倍を超える**ため、**品質ごとに示しています**（所要は同じ条件の表を参照）。");
        }

        AddValueGrid(
            "指定",
            s_sizeLabels,
            rows.Select(static r => r.Label).ToArray(),
            [.. rows.Select(r => s_sizes.Select(s => Decibels(SectionReduce, "ReduceColors", ClassBit, s.Label, ReduceVariant(colors, r.Quality, r.Dither))).ToArray())],
            (row, column) => Time(SectionReduce, "ReduceColors", ClassBit, s_sizes[column].Label, ReduceVariant(colors, rows[row].Quality, rows[row].Dither)));

        // 1 画素ごとの差では、ディザの狙い（縞を散らすこと）が見えないため、低い周波数の一致度も示す
        sub++;
        var pair = new[] { EsDitherMode.None, EsDitherMode.FloydSteinberg };
        AddHeading($"### {number}.{sub} 縞の出にくさ（{colors} 色・{EsImageQuality.Mid}）");
        AddText($"**元画像を基準**とし、{LowFrequencyBlock}×{LowFrequencyBlock} の平均で見た PSNR (dB) です（大きいほど元のなだらかな変化に一致し、**縞が出にくい**）。 行をディザ・列を元画像の寸法とした 2 軸表を示します。 1 画素ごとの差（上の表）ではディザが不利に出ますが、**目のぼけを模したこの比べ方ではディザが有利に出ます**（ディザは差をとなりへ散らすため、平均すると打ち消し合います）。");
        AddValueGrid(
            "指定",
            s_sizeLabels,
            pair.Select(DitherOnlyLabel).ToArray(),
            [.. pair.Select(d => s_sizes.Select(s => Decibels(SectionReduce + LowFrequencySuffix, "ReduceColors", ClassBit, s.Label, ReduceVariant(colors, EsImageQuality.Mid, d))).ToArray())],
            (row, column) => Time(SectionReduce, "ReduceColors", ClassBit, s_sizes[column].Label, ReduceVariant(colors, EsImageQuality.Mid, pair[row])));
    }

    /// <summary>品質の段階による所要の差が小さいかどうか（最も大きい寸法で比べる）。</summary>
    private static bool IsQualityCollapsed(string section, string method, int colors, EsDitherMode dither)
    {
        var times = s_qualities
            .Select(q => Time(section, method, ClassBit, s_sizes[0].Label, ReduceVariant(colors, q, dither)))
            .ToArray();
        if (times.Any(static t => t is null or <= 0))
        {
            return false;
        }

        var values = times.Select(static t => t!.Value).ToArray();
        return values.Max() / values.Min() <= QualityCollapseRatio;
    }

    /// <summary>3 型の品質が同じかどうかを確かめ、表の読み方を返す。</summary>
    private static string QualityTypeNote(string section, string method, string size, string variant)
    {
        var values = s_classNames
            .Select(cls => Decibels(section, method, cls, size, variant))
            .ToArray();
        return values.All(static v => v != "-") && values.Distinct().Count() == 1
            ? "値は 3 型で同じでした（同じ実装を共有しているためです）。"
            : "値は型ごとに違うため、書き換え可能な画像の値を示します。";
    }

    // ====================================================================
    // 表と値の書式
    // ====================================================================

    /// <summary>所要 (ms) の表を出力する（行の 1 番目を基準とした速度比の列を足す）。</summary>
    private static void AddTimeGrid(
        string rowHeader, string[] columnLabels, string[] rowLabels, double?[][] milliseconds, Func<int, int, double?> timeOf)
        => AddGridTable(rowHeader, columnLabels, rowLabels, Durations(milliseconds), timeOf);

    /// <summary>所要 (ms) の行を書式化する。</summary>
    private static string[][] Durations(double?[][] milliseconds)
        => [.. milliseconds.Select(row => row.Select(Duration).ToArray())];

    /// <summary>品質 (dB) などの値の表を出力する（速度比の列を足す）。</summary>
    private static void AddValueGrid(
        string rowHeader, string[] columnLabels, string[] rowLabels, string[][] cells, Func<int, int, double?>? timeOf = null)
        => AddGridTable(rowHeader, columnLabels, rowLabels, cells, timeOf);

    /// <summary>「行の見出し × 列」の表を出力する。 行の 1 番目を基準とした速度比の列を足す。</summary>
    /// <param name="rowHeader">行の見出しの列名。</param>
    /// <param name="columnLabels">列の見出し。</param>
    /// <param name="rowLabels">行の見出し。</param>
    /// <param name="cells">行ごとの書式化済みの値（列の順は <paramref name="columnLabels"/> と同じ）。</param>
    /// <param name="timeOf">速度比を求めるための所要 (ms)。 <c>null</c> の場合は速度比の列を足さない。</param>
    private static void AddGridTable(
        string rowHeader, string[] columnLabels, string[] rowLabels, string[][] cells, Func<int, int, double?>? timeOf)
    {
        var right = new List<int>();
        string[] header = [rowHeader, .. columnLabels];
        var table = new List<string[]> { header };
        for (var c = 1; c <= columnLabels.Length; c++)
        {
            right.Add(c);
        }

        if (timeOf is not null)
        {
            table[0] = [.. table[0], "速度比"];
            right.Add(columnLabels.Length + 1);
        }

        for (var row = 0; row < rowLabels.Length; row++)
        {
            var line = new List<string> { rowLabels[row] };
            line.AddRange(cells[row]);
            if (timeOf is not null)
            {
                line.Add(SpeedCell(timeOf, row, columnLabels.Length));
            }

            table.Add([.. line]);
        }

        AddTable([.. table], [.. right]);
    }

    /// <summary>行の 1 番目を基準とした速度比のセルを作る（列と同じ順に並べる）。</summary>
    private static string SpeedCell(Func<int, int, double?> timeOf, int row, int columns)
    {
        var parts = new List<string>();
        for (var column = 0; column < columns; column++)
        {
            var baseline = timeOf(0, column);
            var value = timeOf(row, column);
            parts.Add(baseline is not null && value is not null && baseline >= RatioFloorMilliseconds && value > 0
                ? string.Create(CultureInfo.InvariantCulture, $"{baseline.Value / value.Value * 100:N0}%")
                : "-");
        }

        return string.Join(" / ", parts);
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

    /// <summary>値を右揃え・桁揃えにせずに表を出す（値が文字列の表）。</summary>
    private static void AddPlainTable(string[] columns, IEnumerable<string[]> rows)
        => AddTable([columns, .. rows], []);

    /// <summary>表を出力する（<paramref name="rightAligned"/> の列は右揃えにし、桁を揃える）。</summary>
    /// <param name="rows">1 行目は見出し。</param>
    /// <param name="rightAligned">右揃え・桁揃えにする列（0 起点）。</param>
    private static void AddTable(List<string[]> rows, List<int> rightAligned)
    {
        foreach (var column in rightAligned)
        {
            var width = rows.Skip(1).Max(row => row[column].Length);
            for (var i = 1; i < rows.Count; i++)
            {
                if (rows[i][column] != "-")
                {
                    rows[i][column] = rows[i][column].PadLeft(width);
                }
            }
        }

        s_report.Add("| " + string.Join(" | ", rows[0]) + " |");
        s_report.Add("| " + string.Join(
            " | ",
            rows[0].Select((_, i) => rightAligned.Contains(i) ? "---:" : "---")) + " |");
        for (var i = 1; i < rows.Count; i++)
        {
            s_report.Add("| " + string.Join(" | ", rows[i]) + " |");
        }

        s_report.Add("");
    }

    /// <summary>所要 (ms) を書式化する（<c>null</c> は <c>-</c>）。</summary>
    private static string Duration(double? milliseconds)
        => milliseconds is null ? "-" : $"{Format(milliseconds.Value)} ms";

    /// <summary>3 桁区切りの大きさを書式化する（<c>null</c> は <c>-</c>）。</summary>
    private static string Size(string section, string method, string size)
        => s_values.TryGetValue((section, method, "", size, ""), out var value)
            ? value.ToString("N0", CultureInfo.InvariantCulture)
            : "-";

    /// <summary>PSNR (dB) を書式化する（記録が無い場合は <c>-</c>）。</summary>
    private static string Decibels(string section, string method, string className, string size, string variant)
    {
        if (!s_values.TryGetValue((section, method, className, size, variant), out var value))
        {
            return "-";
        }

        return double.IsPositiveInfinity(value)
            ? "∞ dB"
            : string.Create(CultureInfo.InvariantCulture, $"{value:N2} dB");
    }

    /// <summary>所要 (ms) の表示を、桁に応じて刻みを変える（1 ナノ秒まで読めるようにする）。</summary>
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

    /// <summary>計測した所要 (ms) を返す（計測が無い場合は <c>null</c>）。</summary>
    private static double? Time(string section, string method, string className, string size, string variant = "")
        => s_cases.TryGetValue((section, method, className, size, variant), out var measured) ? measured.Milliseconds : null;

    /// <summary>計測に使う画像を生成する（内容はグラデーション）。</summary>
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

    /// <summary>縮小先の見出し。</summary>
    private static string TargetLabel(int maxSize)
        => string.Create(CultureInfo.InvariantCulture, $"長辺 {maxSize}");

    /// <summary>寸法ごとの画素の領域のバイト数（幅 × 4 × 高さ。 アンマネージドであり、所要の表には現れない）。</summary>
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

    private static string ReduceVariant(int colors, EsImageQuality quality, EsDitherMode dither)
        => string.Create(CultureInfo.InvariantCulture, $"{colors}色 {quality} {dither}");

    private static string DitherLabel(int colors, EsDitherMode dither)
        => dither == EsDitherMode.None
            ? string.Create(CultureInfo.InvariantCulture, $"{colors} 色（ディザなし）")
            : string.Create(CultureInfo.InvariantCulture, $"{colors} 色（Floyd–Steinberg）");

    private static string DitherOnlyLabel(EsDitherMode dither)
        => dither == EsDitherMode.None ? "ディザなし" : "ディザあり（Floyd–Steinberg）";

    private readonly record struct SizeCase(string Label, int Width, int Height);

    private readonly record struct Measured(double Milliseconds);

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

# プロセッサの名前は CIM から取り、計測プログラムへ環境変数で渡す（.NET からは取れないため）
$cpuName = '不明'
try {
    $processor = Get-CimInstance -ClassName Win32_Processor -ErrorAction Stop | Select-Object -First 1
    if ($processor -and $processor.Name) {
        $cpuName = $processor.Name.Trim()
    }
}
catch {
    Write-Output "警告: プロセッサの名前を取得できませんでした（$($_.Exception.Message)）"
}

$previousCpuName = $env:SAVEEVIDENCE_CPU_NAME
$env:SAVEEVIDENCE_CPU_NAME = $cpuName
try {
    $measured = & dotnet $assembly $Iterations $Warmup
    if ($LASTEXITCODE -ne 0) {
        throw '計測の実行に失敗しました。'
    }
}
finally {
    $env:SAVEEVIDENCE_CPU_NAME = $previousCpuName
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

    # 目次は本文の見出しと一致していること。
    # 本ドキュメントは SPEC_*.md ではないため verify.ps1 の体裁検査の対象外であり、
    # 節番号は本スクリプトが決めるため、ずれても markdownlint（MD051）では見つからない
    # （MD051 は既存のリンクだけを見る）。
    $tocExpected = Get-TocLines $updated
    $tocActual = Get-TocBlock $updated
    $difference = -1
    if ($tocExpected.Count -ne $tocActual.Count) {
        $difference = [Math]::Min($tocExpected.Count, $tocActual.Count)
    }
    else {
        for ($i = 0; $i -lt $tocExpected.Count; $i++) {
            if ($tocExpected[$i] -ne $tocActual[$i]) { $difference = $i; break }
        }
    }

    if ($difference -ge 0) {
        $expected = if ($difference -lt $tocExpected.Count) { $tocExpected[$difference] } else { '（無し）' }
        $actual = if ($difference -lt $tocActual.Count) { $tocActual[$difference] } else { '（無し）' }
        throw ("目次が本文の見出しと一致しません（{0} 件目。 期待 {1} / 実際 {2}）。 " +
            "{3} の「## 目次」を直してください（本文は更新済みです）。 件数は 期待 {4} / 実際 {5}。") -f
            ($difference + 1), $expected, $actual, $docPath, $tocExpected.Count, $tocActual.Count
    }
}

if (-not $KeepWorkspace) {
    Remove-Item -LiteralPath $workspace -Recurse -Force
}
