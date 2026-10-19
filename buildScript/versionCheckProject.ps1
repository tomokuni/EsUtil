<#
.SYNOPSIS
    プロジェクト単体のビルドの前処理として、そのプロジェクトのソースの指紋を検査し、変わっていれば版を上げる。

.DESCRIPTION
    `Directory.Build.targets` の `CheckPackageVersions` が、**プロジェクト単体のビルド**（IDE のビルド・
    `dotnet build <csproj>`・C# 拡張機能の fastbuild）で `GetAssemblyVersion` の前に実行する
    （ソリューションのビルドは `Directory.Solution.targets` が評価前に処理するため、本スクリプトは使わない）。

    手順:
      1. 記録（`fingerprints.json`）に無いプロジェクト（テスト・サンプル）は何もしない（終了コード 0）。
      2. `pack.ps1 -WhatIf` と同じ判定を行い、**そのプロジェクトに変更が無ければ**何もしない（終了コード 0）。
      3. 変更があれば、そのプロジェクトだけ `pack.ps1 -Only <名前>` で pack する
         （3 桁目を +1 し、ローカル フィードへ出力し、記録を更新する）。
      4. 更新後の版を `-VersionFile` へ書き出す（呼び出し側が `Version` を差し替え、
         **そのままビルドを続けられる**ようにする）。

    依存する側（このプロジェクトを参照するプロジェクト）の版は上げない。 パッケージの依存は
    「以上」の範囲で記述されるため、参照側のパッケージは新しい版を解決できる。

    終了コード:
      0 = 変更なし
      3 = 版を更新した（`-VersionFile` に新しい版を書き出した）
      4 = パッケージの更新に失敗した

.PARAMETER ProjectPath
    ビルド中のプロジェクトのパス（`$(MSBuildProjectFullPath)`）。

.PARAMETER Configuration
    pack の構成（既定 Release）。

.PARAMETER VersionFile
    更新後の版を書き出すファイル（変更が無い場合は空にする）。

.EXAMPLE
    pwsh -NoProfile -File buildScript/versionCheckProject.ps1 -ProjectPath EsUtil/Platform.Core/src/EsUtil.Platform.Core.csproj -VersionFile obj/.esutil-version.txt
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$ProjectPath,
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Release',
    [Parameter(Mandatory = $true)][string]$VersionFile
)

$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$fingerprintPath = Join-Path $PSScriptRoot 'fingerprints.json'
$propsPath = Join-Path $repoRoot 'Directory.Build.props'
$name = [IO.Path]::GetFileNameWithoutExtension($ProjectPath)

# 版を書き出す場所は毎回作り直す（前回の値を残さない）。
function Set-VersionFile([string]$value) {
    $dir = Split-Path -Parent $VersionFile
    if ($dir -ne '' -and -not (Test-Path -LiteralPath $dir)) {
        New-Item -ItemType Directory -Force -Path $dir | Out-Null
    }

    [IO.File]::WriteAllText($VersionFile, $value)
}

Set-VersionFile ''

if (-not (Test-Path -LiteralPath $fingerprintPath)) { exit 0 }

# 記録に無いプロジェクト（テスト・サンプル）は対象外。
$recorded = (Get-Content -LiteralPath $fingerprintPath -Raw | ConvertFrom-Json).fingerprints
if (-not $recorded.PSObject.Properties[$name]) { exit 0 }

$packScript = Join-Path $PSScriptRoot 'pack.ps1'

# 判定の結果はビルド中に何度も問い合わせられる（ビルドされたプロジェクトごとに 1 回）。
# 記録（fingerprints.json）の更新時刻を鍵にした一時ファイルへ結果を残し、2 回目以降は計算しない。
$repoBytes = [Text.Encoding]::UTF8.GetBytes($repoRoot.ToLowerInvariant())
$repoHash = [BitConverter]::ToString([System.Security.Cryptography.SHA256]::HashData($repoBytes)).Replace('-', '').Substring(0, 16)
$cachePath = Join-Path ([IO.Path]::GetTempPath()) "esutil-plan-$repoHash.txt"
$info = Get-Item -LiteralPath $fingerprintPath
$mark = '{0}|{1}' -f $info.LastWriteTimeUtc.Ticks, $info.Length

function Get-Plan {
    if (Test-Path -LiteralPath $cachePath) {
        $cached = [IO.File]::ReadAllText($cachePath)
        if ($cached.StartsWith($mark + "`n", [StringComparison]::Ordinal)) {
            return @($cached.Substring($mark.Length + 1) -split "`n" | Where-Object { $_ -ne '' })
        }
    }

    $result = & pwsh -NoProfile -File $packScript -WhatIf 2>&1
    if ($LASTEXITCODE -ne 0) {
        return @($result | ForEach-Object { "ERROR $($_.ToString().TrimEnd())" })
    }

    $lines = @($result | ForEach-Object { $_.ToString().TrimEnd() })
    try {
        [IO.File]::WriteAllText($cachePath, $mark + "`n" + ($lines -join "`n"))
    }
    catch {
        # 記録できなくても判定の結果は変わらない（次回もう一度計算するだけ）
    }

    return $lines
}

# 1 つのビルドで 1 回だけ実行する（並列ビルドの他のプロジェクトは待たずに抜ける）。
$mutex = [System.Threading.Mutex]::new($false, 'EsUtilVersionCheck')
$hasLock = $false
try {
    $hasLock = $mutex.WaitOne(0)
}
catch [System.Threading.AbandonedMutexException] {
    $hasLock = $true
}
catch {
    $hasLock = $false
}

if (-not $hasLock) { exit 0 }

try {
    # 判定だけを行う（バージョン表の書き換えも pack もしない。 結果はビルド中キャッシュする）。
    $plan = @(Get-Plan)
    if ($plan -match '^ERROR ') {
        $plan | ForEach-Object { "  $($_ -replace '^ERROR ', '')" }
        exit 4
    }

    if (-not ($plan -match ('変更あり\s+' + [regex]::Escape($name) + '\s'))) { exit 0 }

    Write-Output "ソースの指紋が変わったため、パッケージの版を更新します: $name"

    # 3 桁目を +1 してローカル フィードへ出力し、記録を更新する（失敗時は版を据え置く）。
    $updated = & pwsh -NoProfile -File $packScript -Only $name -Configuration $Configuration 2>&1
    $updated | ForEach-Object { "  $($_.ToString().TrimEnd())" }
    if ($LASTEXITCODE -ne 0) { exit 4 }

    # 表から更新後の版を読む（pack.ps1 は成功したプロジェクトだけ表を書き換える）。
    $key = ($name -replace '\.', '_') + '_Version'
    $text = [IO.File]::ReadAllText($propsPath)
    $match = [regex]::Match($text, ('<' + [regex]::Escape($key) + '>([^<]+)</' + [regex]::Escape($key) + '>'))
    if (-not $match.Success) { exit 4 }

    Set-VersionFile $match.Groups[1].Value.Trim()
    exit 3
}
finally {
    if ($hasLock) { $mutex.ReleaseMutex() }

    $mutex.Dispose()
}
