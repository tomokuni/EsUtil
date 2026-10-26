# パッケージング（nuget の正しさ）の検証。
#
# 通常のビルドは ProjectReference のため、パッケージの誤りはビルドでは検出されない
# （.github/依存関係.md §1）。 本スクリプトは**パッケージ参照だけ**を使う検証で、
# 配布できる状態かを確かめる（規則の所有者は .github/検証手順.md §9）。
#
#   1. 5 グループのパッケージが .nupkg/ に揃っているか（無ければ pack.ps1 を実行する）
#   2. 同封物（lib/<tfm>/<名前>.dll・XML ドキュメント・README.md）が入っているか
#   3. nuspec の依存が最小版（厳密一致ではない）で、公開する版に `-` が無いか
#   4. パッケージ参照だけの一時プロジェクトが復元・ビルド・起動できるか（共通基盤 + 機能層）
#   5. UI グループのパッケージ参照が復元できるか（依存の宣言が閉じているか）
#
# 一時プロジェクトは $env:TEMP へ置く（リポジトリの Directory.Build.* を継承させないため。 片付ける）。
#
# 使い方:
#   pwsh -NoProfile -File buildScript/verifyPackaging.ps1
#   pwsh -NoProfile -File buildScript/verifyPackaging.ps1 -SkipPack     # pack を省略する
#   pwsh -NoProfile -File buildScript/verifyPackaging.ps1 -KeepWork     # 一時フォルダを残す

[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Release',
    [switch]$SkipPack,
    [switch]$KeepWork
)

$ErrorActionPreference = 'Stop'
$enc = [Text.UTF8Encoding]::new($false)
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
Set-Location $repoRoot

. (Join-Path $PSScriptRoot 'VerifyCommon.ps1')

$feed = Join-Path $repoRoot '.nupkg'
$propsText = [IO.File]::ReadAllText((Join-Path $repoRoot 'Directory.Build.props'), $enc)
$recordPath = Join-Path $PSScriptRoot 'local-versions.json'

function Get-GroupName([string]$projectName) {
    if ($projectName -match '^EsUtil\.Ui\.(?<ui>AvaloniaUI|MewUI|WinUI3|WinForms)\.') {
        return "EsUtil.Ui.$($Matches['ui'])"
    }
    if ($projectName -match '^EsUtil\.(Platform|Feature)\.') { return 'EsUtil' }
    throw "版グループを判定できません: $projectName"
}

function Get-VersionPrefix([string]$group) {
    $key = ($group -replace '\.', '_') + '_VersionPrefix'
    $m = [regex]::Match($propsText, ("<{0}>([^<]+)</{0}>" -f [regex]::Escape($key)))
    if (-not $m.Success) { throw "Directory.Build.props に $key がありません" }
    return $m.Groups[1].Value.Trim()
}

$groups = @(Get-ChildItem -Path $repoRoot -Directory | Where-Object { $_.Name -like 'EsUtil*' } | Select-Object -ExpandProperty Name | Sort-Object)

# ============================================================
# 1. ローカル識別子（グループごとの local.<n>）
# ============================================================
$record = @{}
if (Test-Path -LiteralPath $recordPath) {
    $loaded = Get-Content -LiteralPath $recordPath -Raw -Encoding UTF8 | ConvertFrom-Json
    foreach ($property in $loaded.groups.PSObject.Properties) {
        $record[$property.Name] = [pscustomobject]@{
            suffix      = [int]$property.Value.suffix
            fingerprint = [string]$property.Value.fingerprint
        }
    }
}

# パッケージの一覧（テスト・サンプルは作らない）
$packages = @(Get-ChildItem -Path $repoRoot -Directory |
    Where-Object { $_.Name -like 'EsUtil*' } |
    ForEach-Object { Get-ChildItem -Path $_.FullName -Recurse -File -Filter '*.csproj' } |
    Where-Object { $_.FullName -notmatch '\\(obj|bin)\\' -and $_.Name -notmatch '\.(Tests|Sample)\.csproj$' } |
    ForEach-Object { [pscustomobject]@{ Name = $_.BaseName; Group = (Get-GroupName $_.BaseName) } } |
    Sort-Object Name)

$versions = @{}
$missing = [System.Collections.Generic.List[string]]::new()
foreach ($group in $groups) {
    $prefix = Get-VersionPrefix $group
    if ($prefix -match '-') {
        Add-Result '公開する版' $false "$group の VersionPrefix にローカル識別子が含まれています: $prefix"
    }
    $suffix = if ($record[$group]) { $record[$group].suffix } else { 0 }
    if ($suffix -lt 1) {
        $missing.Add("$group（記録が無い）")
        continue
    }
    $versions[$group] = "$prefix-local.$suffix"
}

foreach ($package in $packages) {
    if (-not $versions.ContainsKey($package.Group)) { continue }
    $path = Join-Path $feed ("{0}.{1}.nupkg" -f $package.Name, $versions[$package.Group])
    if (-not (Test-Path -LiteralPath $path)) { $missing.Add($package.Name) }
}

if ($missing.Count -gt 0 -and -not $SkipPack) {
    Write-Output "パッケージが揃っていないため pack.ps1 を実行します: $($missing.Count) 件"
    $packResult = Invoke-NativeText 'pwsh' @('-NoProfile', '-File', (Join-Path $PSScriptRoot 'pack.ps1'), '-Configuration', $Configuration)
    if ($packResult.ExitCode -ne 0) {
        Write-Output ($packResult.Text + $packResult.ErrorText)
        Add-Result 'パッケージの作成' $false "pack.ps1 が失敗しました（終了コード $($packResult.ExitCode)）"
        exit 1
    }
    # 記録が更新されたため読み直す
    $loaded = Get-Content -LiteralPath $recordPath -Raw -Encoding UTF8 | ConvertFrom-Json
    foreach ($property in $loaded.groups.PSObject.Properties) {
        $versions[$property.Name] = "$(Get-VersionPrefix $property.Name)-local.$($property.Value.suffix)"
    }
}

Add-Result '公開する版' (@($groups | Where-Object { (Get-VersionPrefix $_) -match '-' }).Count -eq 0) 'グループの VersionPrefix に `-` を含まない'
Add-Result 'パッケージの存在' ($missing.Count -eq 0) "不足 $($missing.Count) 件"
$missing | Select-Object -First 10 | ForEach-Object { "      $_" }

# ============================================================
# 2. 同封物と nuspec の依存
# ============================================================
Add-Type -AssemblyName System.IO.Compression.FileSystem

$contentIssues = [System.Collections.Generic.List[string]]::new()
$dependencyIssues = [System.Collections.Generic.List[string]]::new()

foreach ($package in $packages) {
    if (-not $versions.ContainsKey($package.Group)) { continue }
    $path = Join-Path $feed ("{0}.{1}.nupkg" -f $package.Name, $versions[$package.Group])
    if (-not (Test-Path -LiteralPath $path)) { continue }

    $zip = [IO.Compression.ZipFile]::OpenRead($path)
    try {
        $names = @($zip.Entries | ForEach-Object { $_.FullName })

        # DLL と XML ドキュメント（利用側の IntelliSense に使う）
        if (-not ($names | Where-Object { $_ -match ("^lib/[^/]+/{0}\.dll$" -f [regex]::Escape($package.Name)) })) {
            $contentIssues.Add("$($package.Name): lib/<tfm>/$($package.Name).dll がありません")
        }
        if (-not ($names | Where-Object { $_ -match ("^lib/[^/]+/{0}\.xml$" -f [regex]::Escape($package.Name)) })) {
            $contentIssues.Add("$($package.Name): lib/<tfm>/$($package.Name).xml がありません")
        }
        if ($names -notcontains 'README.md') {
            $contentIssues.Add("$($package.Name): README.md がありません")
        }

        # nuspec の依存（EsUtil.* は最小版で書かれる。 厳密一致 `[x]` にしない）
        $nuspecEntry = $zip.Entries | Where-Object { $_.FullName -match '\.nuspec$' } | Select-Object -First 1
        if ($nuspecEntry) {
            $reader = [IO.StreamReader]::new($nuspecEntry.Open(), $enc)
            try { $nuspec = $reader.ReadToEnd() } finally { $reader.Dispose() }
            foreach ($m in [regex]::Matches($nuspec, '<dependency id="(?<id>EsUtil\.[^"]+)" version="(?<v>[^"]+)"')) {
                $id = $m.Groups['id'].Value
                $version = $m.Groups['v'].Value
                # 依存は最小版（`1.2.3`）で書く。 範囲指定（`[1.2.3,)`）にすると解決の幅が広がり、
                # 厳密一致（`[1.2.3]`）にすると依存先の更新で参照側の再公開が必要になる。
                if ($version -match '^[\[\(]') {
                    $dependencyIssues.Add("$($package.Name): $id の版が範囲指定です（最小版で書く）: $version")
                }
            }
        }
        else {
            $contentIssues.Add("$($package.Name): nuspec がありません")
        }
    }
    finally {
        $zip.Dispose()
    }
}

Add-Result 'パッケージの同封物' ($contentIssues.Count -eq 0) "不足 $($contentIssues.Count) 件"
$contentIssues | Select-Object -First 10 | ForEach-Object { "      $_" }
Add-Result 'nuspec の依存' ($dependencyIssues.Count -eq 0) "範囲指定・ローカル識別子 $($dependencyIssues.Count) 件"
$dependencyIssues | Select-Object -First 10 | ForEach-Object { "      $_" }

# ============================================================
# 3. パッケージ参照だけでの復元・ビルド・起動
# ============================================================
$work = Join-Path ([IO.Path]::GetTempPath()) ("esutil-packaging-" + [guid]::NewGuid().ToString('N'))
[void](New-Item -ItemType Directory -Path $work)

try {
    # 取得元は一時フィードと nuget.org だけにする（リポジトリの NuGet.config を使わない）
    $nugetConfig = @"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="feed" value="$($feed.Replace('\', '/'))" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" protocolVersion="3" />
  </packageSources>
</configuration>
"@
    $nugetConfigPath = Join-Path $work 'NuGet.config'
    [IO.File]::WriteAllText($nugetConfigPath, $nugetConfig, $enc)

    $groupByName = @{}
    foreach ($package in $packages) { $groupByName[$package.Name] = $package.Group }

    function New-ConsumerProject([string]$directory, [string]$name, [string[]]$packageIds, [bool]$isExe) {
        $itemGroup = ($packageIds | ForEach-Object {
                $id = $_
                "    <PackageReference Include=`"$id`" Version=`"[$($versions[$groupByName[$id]])]`" />"
            }) -join "`n"

        $outputType = if ($isExe) { '<OutputType>Exe</OutputType>' } else { '' }
        $csproj = @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0-windows10.0.22621.0</TargetFramework>
    <SupportedOSPlatformVersion>10.0.17763.0</SupportedOSPlatformVersion>
    $outputType
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <RestorePackagesWithLockFile>false</RestorePackagesWithLockFile>
  </PropertyGroup>
  <ItemGroup>
$itemGroup
  </ItemGroup>
</Project>
"@
        $projectDir = Join-Path $work $directory
        [void](New-Item -ItemType Directory -Path $projectDir -Force)
        [IO.File]::WriteAllText((Join-Path $projectDir "$name.csproj"), $csproj, $enc)
        return $projectDir
    }

    # 共通基盤 + 機能層（UI 非依存）は復元・ビルド・起動まで確かめる
    $corePackages = @($packages | Where-Object { $_.Group -eq 'EsUtil' } | Select-Object -ExpandProperty Name)
    $coreDir = New-ConsumerProject 'core' 'Consumer' $corePackages $true
    $program = @"
using EsUtil.Feature.ZenHan;

var han = ZenHanConverter.ToHan("ＡＢＣ１２３");
if (han != "ABC123") { throw new InvalidOperationException(`$"変換結果が想定と違います: {han}"); }
Console.WriteLine(`$"OK {han}");
"@
    [IO.File]::WriteAllText((Join-Path $coreDir 'Program.cs'), $program, $enc)

    $restore = Invoke-NativeText 'dotnet' @('restore', (Join-Path $coreDir 'Consumer.csproj'), '--configfile', $nugetConfigPath)
    $built = Invoke-NativeText 'dotnet' @('build', (Join-Path $coreDir 'Consumer.csproj'), '-c', $Configuration, '--nologo')
    $buildText = $built.Text + $built.ErrorText
    Add-Result 'パッケージ参照での復元' ($restore.ExitCode -eq 0) "終了コード $($restore.ExitCode)"
    if ($restore.ExitCode -ne 0) { Write-Output (($restore.Text + $restore.ErrorText) | Select-Object -First 20) }
    Add-Result 'パッケージ参照でのビルド' ($built.ExitCode -eq 0) "終了コード $($built.ExitCode) / 警告 $(@([regex]::Matches($buildText, 'warning ')).Count)"
    if ($built.ExitCode -ne 0) { Write-Output ($buildText | Select-Object -First 30) }

    if ($built.ExitCode -eq 0) {
        $run = Invoke-NativeText 'dotnet' @('run', '--project', (Join-Path $coreDir 'Consumer.csproj'), '-c', $Configuration, '--no-build')
        $runText = $run.Text + $run.ErrorText
        $ok = $run.ExitCode -eq 0 -and $runText -match 'OK ABC123'
        Add-Result 'パッケージ参照での起動' $ok "終了コード $($run.ExitCode)"
        if (-not $ok) { Write-Output ($runText | Select-Object -First 20) }
    }
    else {
        Add-Result 'パッケージ参照での起動' $false 'ビルドが失敗したため実行しない'
    }

    # UI グループは復元まで確かめる（依存の宣言が閉じているか。 UI のビルドは各 UI の SDK 設定を要する）
    $uiIssues = [System.Collections.Generic.List[string]]::new()
    foreach ($group in ($groups | Where-Object { $_ -ne 'EsUtil' })) {
        $ids = @($packages | Where-Object { $_.Group -eq $group } | Select-Object -ExpandProperty Name)
        if ($ids.Count -eq 0) { continue }
        $dir = New-ConsumerProject ($group.Replace('.', '-').ToLowerInvariant()) 'Consumer' $ids $false
        $build = Invoke-NativeText 'dotnet' @('restore', (Join-Path $dir 'Consumer.csproj'), '--configfile', $nugetConfigPath)
        if ($build.ExitCode -ne 0) {
            $uiIssues.Add(("{0}（終了コード {1}）" -f $group, $build.ExitCode))
            Write-Output (($build.Text + $build.ErrorText) | Select-Object -First 10)
        }
    }
    Add-Result 'UI グループの復元' ($uiIssues.Count -eq 0) "失敗 $($uiIssues.Count) 件"
    $uiIssues | ForEach-Object { "      $_" }
}
finally {
    if ($KeepWork) { Write-Output "作業フォルダを残しました: $work" }
    else { Remove-Item -LiteralPath $work -Recurse -Force -ErrorAction SilentlyContinue }
}

$failed = @($script:Results | Where-Object { -not $_.Ok })
if ($failed.Count -gt 0) {
    Write-Output ''
    Write-Output "パッケージングの検証に失敗しました: $($failed.Count) 件"
    exit 1
}
Write-Output ''
Write-Output 'パッケージングの検証はすべて合格しました'
exit 0
