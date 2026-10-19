<#
.SYNOPSIS
    ビルドの前処理として、ソースの指紋を検査し、変わったプロジェクトのパッケージの版を上げる。

.DESCRIPTION
    `Directory.Build.targets` の `CheckPackageVersions` が **ビルドの一部として** 実行する
    （素の `dotnet build` でも IDE のビルドでも、MSBuild が走れば自動で呼ばれる）。

    手順:
      1. 記録（`fingerprints.json`）の更新時刻を前回の検査時の値と比べる。 **変わっていれば** 次へ進む
         （毎回のビルドで指紋を計算しないための早期復帰。 ここで抜ければビルドへの負荷はほぼ 0）。
      2. 記録が変わっていれば `pack.ps1 -WhatIf` と同じ判定（ソースの指紋との比較）を行い、
         **変更のあったプロジェクトがあるか**を調べる。
      3. 変更があれば `pack.ps1` を実行する（3 桁目を +1 し、ローカル フィードへ出力し、記録を更新する）。
      4. **版はビルドの評価時に確定する**ため、更新した版をそのまま現在のビルドへは反映できない。
         呼び出し側（MSBuild）は終了コード 3 を受けて「もう一度ビルドしてください」と案内する。

    終了コード:
      0 = 変更なし
      3 = 版を更新した（再ビルドが必要）
      4 = パッケージの更新に失敗した

.PARAMETER Configuration
    pack の構成（既定 Release）。

.EXAMPLE
    pwsh -NoProfile -File buildScript/versionCheck.ps1
#>
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$fingerprintPath = Join-Path $PSScriptRoot 'fingerprints.json'
if (-not (Test-Path -LiteralPath $fingerprintPath)) { exit 0 }

# 検査済みの記録を覚える場所（リポジトリへ置かない。 リポジトリのパスごとに分ける）。
$repoBytes = [Text.Encoding]::UTF8.GetBytes($repoRoot.ToLowerInvariant())
$repoHash = [System.Security.Cryptography.SHA256]::HashData($repoBytes)
$stampName = 'esutil-versioncheck-' + ([BitConverter]::ToString($repoHash).Replace('-', '').Substring(0, 16)) + '.stamp'
$stampPath = Join-Path ([IO.Path]::GetTempPath()) $stampName

# 記録の更新時刻と長さ（同じ内容で書き直された場合も「変わった」とみなす。 判定は内容の比較が行う）。
$info = Get-Item -LiteralPath $fingerprintPath
$mark = '{0}|{1}' -f $info.LastWriteTimeUtc.Ticks, $info.Length

if (Test-Path -LiteralPath $stampPath) {
    if ([IO.File]::ReadAllText($stampPath) -eq $mark) { exit 0 }
}

function Set-Stamp([string]$value) {
    try {
        [IO.File]::WriteAllText($stampPath, $value)
    }
    catch {
        # 記録できなくても検査の結果は変わらない（次回もう一度判定するだけ）
    }
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
    $packScript = Join-Path $PSScriptRoot 'pack.ps1'

    # 判定だけを行う（バージョン表の書き換えも pack もしない）。
    $plan = & pwsh -NoProfile -File $packScript -WhatIf 2>&1
    if ($LASTEXITCODE -ne 0) {
        $plan | ForEach-Object { "  $($_.ToString().TrimEnd())" }
        exit 4
    }

    $changed = @($plan | Where-Object { $_ -match '変更あり' })
    if ($changed.Count -eq 0) {
        $after = Get-Item -LiteralPath $fingerprintPath
        Set-Stamp ('{0}|{1}' -f $after.LastWriteTimeUtc.Ticks, $after.Length)
        exit 0
    }

    Write-Output 'ソースの指紋が変わったプロジェクトがあります。 パッケージの版を更新します。'
    $changed | ForEach-Object { "  $($_.ToString().Trim())" }

    # 3 桁目を +1 してローカル フィードへ出力し、記録を更新する（失敗時は版を据え置く）。
    $updated = & pwsh -NoProfile -File $packScript -Configuration $Configuration 2>&1
    $updated | ForEach-Object { "  $($_.ToString().TrimEnd())" }
    if ($LASTEXITCODE -ne 0) { exit 4 }

    $after = Get-Item -LiteralPath $fingerprintPath
    Set-Stamp ('{0}|{1}' -f $after.LastWriteTimeUtc.Ticks, $after.Length)
    exit 3
}
finally {
    if ($hasLock) { $mutex.ReleaseMutex() }

    $mutex.Dispose()
}
