<#
.SYNOPSIS
    すべてのソリューションの `packages.lock.json` を、現在の `Directory.Packages.props` に合わせて作り直す。

.DESCRIPTION
    `packages.lock.json` は復元結果を固定する（規則は `.github/ビルド設定規約.md` §8）。 版を変えたときは
    このスクリプトで lock を更新し、**コミットする**（更新しないと CI の `-p:RestoreLockedMode=true` が
    `NU1004` で失敗する）。

    Dependabot は `Directory.Packages.props` を書き換えますが、**lock は一部のプロジェクトしか更新しません**
    （Dependabot の制限。 実測: アナライザーの更新で 76 個中 3 個だけ更新された）。 そのため Dependabot の
    Pull Request は、そのブランチでこのスクリプトを実行しないと CI が失敗します。

    ソリューションの一覧は `.github/release-config.json` の `solutions` が単一所有する（本文へ列挙しない）。

.EXAMPLE
    pwsh -NoProfile -File buildScript/updateLocks.ps1
#>
[CmdletBinding()]
param(
    # 既定は release-config.json の solutions（全ソリューション）
    [string[]]$Solution = @()
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
Set-Location $repoRoot

. (Join-Path $PSScriptRoot 'VerifyCommon.ps1')

if (-not $Solution -or $Solution.Count -eq 0) { $Solution = @((Get-RepoConfig $repoRoot).solutions) }
if ($Solution.Count -eq 0) { throw '.github/release-config.json の solutions が空です' }

foreach ($sln in $Solution) {
    Write-Host "lock を更新しています: $sln"
    # --force-evaluate: 復元結果が最新でも評価し直す（lock の要求範囲だけが変わった場合に必要）
    & dotnet restore $sln --force-evaluate
    if ($LASTEXITCODE -ne 0) { throw "lock の更新に失敗しました（$sln、終了コード $LASTEXITCODE）" }
}

# lock と Directory.Packages.props の食い違いを、CI と同じ条件（ロック済みモード）で確かめる
foreach ($sln in $Solution) {
    & dotnet restore $sln -p:RestoreLockedMode=true
    if ($LASTEXITCODE -ne 0) { throw "ロック済みモードの復元に失敗しました（$sln、終了コード $LASTEXITCODE）" }
}

$changed = @(git status --porcelain -- '*/packages.lock.json' 'packages.lock.json')
Write-Host "lock を更新しました（変更 $($changed.Count) 件）。 内容を確認してコミットしてください。"
$changed | ForEach-Object { "  $_" }
