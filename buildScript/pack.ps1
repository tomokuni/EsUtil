<#
.SYNOPSIS
    ローカル フィード（nupkg/）へパッケージを出力し、ソースの指紋を記録する。

.DESCRIPTION
    本リポジトリのプロジェクトは相互参照に ProjectReference ではなく PackageReference を使うため、
    **依存順に pack しないと後続のプロジェクトが復元できない**。

    手順（各段は前の段のパッケージが無いと復元できない）:
      1. 一覧の順に `dotnet pack` する。
      2. pack したパッケージの展開済みキャッシュ（グローバル パッケージ フォルダ）を削除する。
         同じバージョンのまま中身を更新しても NuGet はキャッシュを使うため、削除しないと
         後続のプロジェクトが**古い DLL** を参照する（実測で必要と確認した手順）。
      3. パッケージの元になったソースの指紋（ファイル内容のハッシュ）を nupkg/fingerprints.json へ記録する。
         `verify.ps1` はこの指紋と現在のソースを比べ、「ソースを直したが pack していない」状態を検出する。

    パッケージは外部へ公開しない（ローカル保存のみ）。

.PARAMETER Configuration
    pack の構成（既定 Release）。

.PARAMETER NoClean
    出力先（nupkg/）の既存パッケージを削除しない（差分の確認用。 通常は指定しない）。

.EXAMPLE
    pwsh -NoProfile -File buildScript/pack.ps1

.EXAMPLE
    pwsh -NoProfile -File buildScript/pack.ps1 -Configuration Debug
#>
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Release',
    [switch]$NoClean
)

$ErrorActionPreference = 'Stop'
$enc = [Text.UTF8Encoding]::new($false)
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
Set-Location $repoRoot

# 依存順の一覧（規則の所有者は .github/依存関係.md §3。 表を変えるときは本一覧も追随させる）。
$projects = @(
    'src/EsUtil.Platform.Core/src/EsUtil.Platform.Core.csproj',
    'src/EsUtil.Platform.Core.Windows/src/EsUtil.Platform.Core.Windows.csproj',
    'src/EsUtil.Feature.Loupe.Core/src/EsUtil.Feature.Loupe.Core.csproj',
    'src/EsUtil.Feature.Marquee.Core/src/EsUtil.Feature.Marquee.Core.csproj',
    'src/EsUtil.Feature.Loupe.Presenter/src/EsUtil.Feature.Loupe.Presenter.csproj',
    'src/EsUtil.Feature.Marquee.Presenter/src/EsUtil.Feature.Marquee.Presenter.csproj',
    'src/EsUtil.Feature.Loupe.ControlAvaloniaUI/src/AvaloniaUI/EsUtil.Feature.Loupe.ControlAvaloniaUI.csproj',
    'src/EsUtil.Feature.Loupe.ControlMewUI/src/MewUI/EsUtil.Feature.Loupe.ControlMewUI.csproj',
    'src/EsUtil.Feature.Marquee.ControlAvaloniaUI/src/AvaloniaUI/EsUtil.Feature.Marquee.ControlAvaloniaUI.csproj',
    'src/EsUtil.Feature.Marquee.ControlMewUI/src/MewUI/EsUtil.Feature.Marquee.ControlMewUI.csproj'
)

$feed = Join-Path $repoRoot 'nupkg'
New-Item -ItemType Directory -Force -Path $feed | Out-Null

# バージョンは Directory.Build.props が単一所有する（複数箇所へ書かない）。
$propsText = [IO.File]::ReadAllText((Join-Path $repoRoot 'Directory.Build.props'), $enc)
$version = [regex]::Match($propsText, '<Version>([^<]+)</Version>').Groups[1].Value
if (-not $version) { throw 'Directory.Build.props に Version がありません' }

$globalPackages = (& dotnet nuget locals global-packages --list 2>&1 | Select-Object -First 1) -replace '^global-packages:\s*', ''
$failures = [System.Collections.Generic.List[string]]::new()

Write-Output "ローカル フィード: $feed"
Write-Output "バージョン: $version"
Write-Output ''

# プロジェクトのソース（配下の全ファイル。 生成物は除く）から指紋を作る。
# 「パッケージを作り直さずにソースを直す」状態を verify.ps1 が検出できるようにする。
function Get-ProjectFingerprint([string]$projectPath) {
    $dir = Split-Path -Parent (Join-Path $repoRoot $projectPath)
    $files = @(Get-ChildItem -Path $dir -Recurse -File |
        Where-Object { $_.FullName -notmatch '\\(obj|bin)\\' } |
        Sort-Object { $_.FullName })
    $sha = [System.Security.Cryptography.SHA256]::Create()
    try {
        $builder = [System.Text.StringBuilder]::new()
        foreach ($file in $files) {
            $relative = $file.FullName.Substring($repoRoot.Length + 1).Replace('\', '/')
            $hash = [BitConverter]::ToString($sha.ComputeHash([IO.File]::ReadAllBytes($file.FullName))).Replace('-', '')
            [void]$builder.Append($relative).Append(':').Append($hash).Append("`n")
        }

        return [BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($builder.ToString()))).Replace('-', '')
    }
    finally {
        $sha.Dispose()
    }
}

$fingerprints = [ordered]@{}
foreach ($project in $projects) {
    $name = [IO.Path]::GetFileNameWithoutExtension($project)
    if (-not (Test-Path -LiteralPath (Join-Path $repoRoot $project))) {
        throw "対象のプロジェクトが見つかりません: $project"
    }

    if (-not $NoClean) {
        # 同じバージョンの古いファイルを残さない（残すと pack の出力先が曖昧になる）
        Get-ChildItem -Path $feed -Filter "$name.$version.nupkg" -File -ErrorAction SilentlyContinue |
            Remove-Item -Force
    }

    $text = (& dotnet pack $project -c $Configuration -o $feed --nologo 2>&1 | Out-String)
    if ($LASTEXITCODE -ne 0) {
        Write-Output "FAIL  $name"
        Write-Output $text
        $failures.Add($name)
        continue
    }

    # 展開済みキャッシュを捨てる（同じバージョンで中身が変わっても後続が新しい DLL を拾うようにする）
    $cached = Join-Path $globalPackages "$name\$version"
    if (Test-Path -LiteralPath $cached) {
        Remove-Item -LiteralPath $cached -Recurse -Force
    }

    $fingerprints[$name] = Get-ProjectFingerprint $project
    Write-Output "OK    $name"
}

if ($failures.Count -eq 0) {
    # 指紋は pack が成功したときだけ更新する（失敗を「最新」と記録しない）。
    # 改行は作業ツリーの規約（CRLF）に揃える（`ConvertTo-Json` の出力と混ざると `w/mixed` になる。 実測）。
    $json = [pscustomobject]@{ version = $version; fingerprints = $fingerprints } |
        ConvertTo-Json -Depth 3
    $json = ($json -replace "`r`n", "`n") -replace "`n", "`r`n"
    [IO.File]::WriteAllText((Join-Path $feed 'fingerprints.json'), $json + "`r`n", $enc)
}

Write-Output ''
if ($failures.Count -gt 0) {
    Write-Output "失敗: $($failures -join ', ')"
    exit 1
}

$packages = @(Get-ChildItem -Path $feed -Filter '*.nupkg' -File | Sort-Object Name)
foreach ($package in $packages) { Write-Output ("  {0}  {1:N0} B" -f $package.Name, $package.Length) }
Write-Output ''
Write-Output "完了: $($packages.Count) パッケージ"
