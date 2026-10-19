<#
.SYNOPSIS
    ビルドの前処理として、ソースの指紋を検査し、変わったプロジェクトのパッケージの版を上げる。

.DESCRIPTION
    `Directory.Build.targets` の `CheckPackageVersions` が **ビルドの一部として** 実行する
    （`dotnet build` を叩くだけで走る。 スクリプトを別途実行する必要はない）。

    手順:
      1. `pack.ps1 -WhatIf` と同じ判定（ソースの指紋と `fingerprints.json` の比較）を行い、
         **変更のあったプロジェクトがあるか**を調べる。
      2. 変更が無ければ何もしない（終了コード 0）。
      3. 変更があれば `pack.ps1` を実行する（3 桁目を +1 し、ローカル フィードへ出力し、指紋を記録する）。
      4. **版はビルドの評価時に確定する**ため、更新した版をそのまま現在のビルドへ反映できない。
         呼び出し側（MSBuild）は終了コード 3 を受けて「もう一度ビルドしてください」と案内する。

    終了コード:
      0 = 変更なし（または他のプロジェクトが処理中）
      3 = 版を更新した（再ビルドが必要）
      4 = パッケージの更新に失敗した

.PARAMETER Configuration
    pack の構成（既定 Release）。

.PARAMETER Stamp
    ビルド 1 回につき 1 度だけ実行するためのマーカー ファイル。
    MSBuild がプロセス ID を含むパスを渡す（同じビルドの 2 つ目以降のプロジェクトは即座に終了する）。

.EXAMPLE
    pwsh -NoProfile -File buildScript/versionCheck.ps1

.EXAMPLE
    pwsh -NoProfile -File buildScript/versionCheck.ps1 -Stamp "$env:TEMP/esutil-check.stamp"
#>
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Release',
    [string]$Stamp = ''
)

$ErrorActionPreference = 'Stop'

# ビルド 1 回につき 1 度だけ実行する（同じビルドの他のプロジェクトは即座に抜ける）。
if ($Stamp -ne '') {
    if (Test-Path -LiteralPath $Stamp) { exit 0 }

    try {
        [IO.File]::WriteAllText($Stamp, '')
    }
    catch {
        # マーカーを作れなくても検査自体は続ける（多重実行は下のミューテックスが防ぐ）
    }
}

# 並列ビルドでも pack を 1 回だけにする（取得できなければ他のプロジェクトが処理している）。
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
    $packScript = Join-Path $PSScriptRoot 'pack.ps1'

    # 判定だけを行う（バージョン表の書き換えも pack もしない）。
    $plan = & pwsh -NoProfile -File $packScript -WhatIf 2>&1
    if (-not ($plan -match '変更あり')) { exit 0 }

    Write-Output 'ソースの指紋が変わったプロジェクトがあります。 パッケージの版を更新します。'
    $plan | Where-Object { $_ -match '変更あり' } | ForEach-Object { "  $($_.ToString().Trim())" }

    # 3 桁目を +1 してローカル フィードへ出力し、指紋を記録する（失敗時は版を据え置く）。
    $updated = & pwsh -NoProfile -File $packScript -Configuration $Configuration 2>&1
    $updated | ForEach-Object { "  $($_.ToString().TrimEnd())" }
    if ($LASTEXITCODE -ne 0) { exit 4 }

    exit 3
}
finally {
    if ($hasLock) { $mutex.ReleaseMutex() }

    $mutex.Dispose()
}
