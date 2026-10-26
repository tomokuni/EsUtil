<#
.SYNOPSIS
    版グループ単位でパッケージを作り、ローカル フィード（.nupkg/）へ出力する。

.DESCRIPTION
    本リポジトリの通常のビルドは ProjectReference であり、pack は要らない
    （.github/依存関係.md §1）。 本スクリプトは**未公開の版を利用側のアプリと
    パッケージング検証で試すとき**だけ使う。

    手順:
      1. 版グループ（EsUtil / EsUtil.Ui.<UI> の 5 つ）ごとのソースの指紋を
         前回の記録（buildScript/local-versions.json）と比べる。
         **変わったグループだけ**ローカル識別子の番号（local.<n>）を +1 する。
      2. 依存順に `dotnet pack` する。 ローカル識別子は `-p:VersionSuffix=local.<n>` で渡す
         （**Directory.Build.props の VersionPrefix は書き換えない**。 公開する版は人が決める）。
      3. pack したパッケージの展開済みキャッシュ（グローバル パッケージ フォルダ）を削除する。
         同じ識別子のまま中身を更新しても NuGet はキャッシュを使うため、削除しないと
         後続のプロジェクトが**古い DLL** を参照する（実測で必要と確認した手順）。
      4. 同じパッケージの**古い版の .nupkg を削除する**（.nupkg/ には各パッケージ 1 版だけを置く）。
      5. **pack に成功したグループだけ**ローカル識別子と指紋を記録する
         （失敗したグループの番号は消費しない）。

    公開する版（VersionPrefix）にローカル識別子は含めない。 公開の手順は
    .github/workflows/release.yml が持つ（`-` を含む版を拒否する）。

.PARAMETER Configuration
    pack の構成（既定 Release）。

.PARAMETER Only
    対象の版グループを絞る（例: EsUtil.Ui.AvaloniaUI）。

.PARAMETER NoClean
    出力先（.nupkg/）の既存パッケージ（古い版）を削除しない（差分の確認用。 通常は指定しない）。

.PARAMETER NoBump
    ローカル識別子を +1 しない（記録の番号のまま pack する。 記録が無ければ 1）。

.PARAMETER WhatIf
    何も書き換えず、変更の有無と使う識別子を表示する。

.EXAMPLE
    pwsh -NoProfile -File buildScript/pack.ps1
.EXAMPLE
    pwsh -NoProfile -File buildScript/pack.ps1 -WhatIf
.EXAMPLE
    pwsh -NoProfile -File buildScript/pack.ps1 -Only EsUtil.Ui.AvaloniaUI
#>
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Release',
    [string]$Only = '',
    [switch]$NoClean,
    [switch]$NoBump,
    [switch]$WhatIf
)

$ErrorActionPreference = 'Stop'
$enc = [Text.UTF8Encoding]::new($false)
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
Set-Location $repoRoot

# ネイティブ コマンド（dotnet）の出力を UTF-8 として読む（規則の所有者は .github/検証手順.md §15）
. (Join-Path $PSScriptRoot 'VerifyCommon.ps1')

# ============================================================
# 版グループ（規則の所有者は .github/ビルド設定規約.md §4）
# ============================================================
# グループはプロジェクト名から導出する（Directory.Build.props の VersionPrefix と同じ規則）。
function Get-GroupName([string]$projectName) {
    if ($projectName -match '^EsUtil\.Ui\.(?<ui>AvaloniaUI|MewUI|WinUI3|WinForms)\.') {
        return "EsUtil.Ui.$($Matches['ui'])"
    }
    if ($projectName -match '^EsUtil\.(Platform|Feature)\.') { return 'EsUtil' }
    throw "版グループを判定できません: $projectName"
}

# グループの VersionPrefix（人が決める公開する版）を Directory.Build.props から読む。
function Get-VersionPrefix([string]$text, [string]$group) {
    $key = ($group -replace '\.', '_') + '_VersionPrefix'
    $m = [regex]::Match($text, ("<{0}>([^<]+)</{0}>" -f [regex]::Escape($key)))
    if (-not $m.Success) { throw "Directory.Build.props に $key がありません" }
    $value = $m.Groups[1].Value.Trim()
    if ($value -notmatch '^\d+\.\d+\.\d+$') { throw "Directory.Build.props の $key が 3 桁の数値ではありません: $value" }
    return $value
}

# ソリューション フォルダ（ルート直下の EsUtil*）の名前もグループ名と一致する
$groupRoots = @(Get-ChildItem -Path $repoRoot -Directory |
        Where-Object { $_.Name -like 'EsUtil*' } |
        Select-Object -ExpandProperty Name)

# ============================================================
# 対象（本体プロジェクト）と依存順
# ============================================================
# テスト・サンプルは除く。 **依存順**（ProjectReference の EsUtil.* が先）に並べる。
# 並べ替えの根拠は .github/依存関係.md §3。 対象の追加は .csproj を置くだけでよく、本スクリプトの一覧は触らない。
function Get-PackageProjects {
    $all = @(Get-ChildItem -Path $repoRoot -Directory |
        Where-Object { $_.Name -like 'EsUtil*' } |
        ForEach-Object { Get-ChildItem -Path $_.FullName -Recurse -File -Filter '*.csproj' } |
        Where-Object { $_.FullName -notmatch '\\(obj|bin)\\' -and $_.Name -notmatch '\.(Tests|Sample)\.csproj$' })

    # 各プロジェクトが参照する同一リポジトリのプロジェクト（ファイル名 = プロジェクト名）
    $deps = @{}
    foreach ($file in $all) {
        $text = [IO.File]::ReadAllText($file.FullName)
        $deps[$file.BaseName] = @([regex]::Matches($text, '<ProjectReference\s+Include="[^"]*(?<p>EsUtil\.[^"\\]+)\.csproj"') |
            ForEach-Object { $_.Groups['p'].Value })
    }

    # 依存先が先に来る順へ並べる（依存関係の循環は規約違反のため、見つけたら止める）
    $ordered = [System.Collections.Generic.List[string]]::new()
    $visiting = [System.Collections.Generic.HashSet[string]]::new()
    $done = [System.Collections.Generic.HashSet[string]]::new()
    $visit = $null
    $visit = {
        param([string]$name)
        if ($done.Contains($name)) { return }
        if (-not $visiting.Add($name)) { throw "パッケージの依存関係が循環しています: $name" }

        foreach ($dep in $deps[$name]) {
            if ($deps.ContainsKey($dep)) { & $visit $dep }
        }

        [void]$visiting.Remove($name)
        [void]$done.Add($name)
        $ordered.Add($name)
    }

    # 名前順に走査して、同じ依存関係なら並びが安定するようにする
    foreach ($name in ($deps.Keys | Sort-Object)) { & $visit $name }

    $byName = @{}
    foreach ($file in $all) { $byName[$file.BaseName] = $file }

    foreach ($name in $ordered) {
        [pscustomobject]@{
            Name    = $name
            Group   = Get-GroupName $name
            Project = $byName[$name].FullName
        }
    }
}

# ============================================================
# ソースの指紋（グループ単位）
# ============================================================
# グループのソリューション フォルダ配下の全ファイル（生成物を除く）から作る。
# グループは同時に公開する単位であるため、1 プロジェクトの変更でグループ全体を同じ識別子で作り直す。
function Get-GroupFingerprint([string]$group) {
    $root = Join-Path $repoRoot $group
    if (-not (Test-Path -LiteralPath $root)) { throw "版グループのフォルダがありません: $group" }
    $files = @(Get-ChildItem -Path $root -Recurse -File |
        Where-Object { $_.FullName -notmatch '\\(obj|bin)\\' } |
        Sort-Object FullName)
    $sb = [Text.StringBuilder]::new()
    foreach ($file in $files) {
        $relative = $file.FullName.Substring($repoRoot.Length + 1).Replace('\', '/')
        [void]$sb.Append($relative).Append(':').Append((Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash).Append("`n")
    }
    $bytes = [Text.Encoding]::UTF8.GetBytes($sb.ToString())
    return [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes))
}

$feed = Join-Path $repoRoot '.nupkg'
$propsPath = Join-Path $repoRoot 'Directory.Build.props'
# 記録は buildScript/ に置いてコミットする（出力先 `.nupkg/` はコミットしないため）。
# 識別子は番号だけを持つ（公開する版は Directory.Build.props の VersionPrefix が持つ）。
$recordPath = Join-Path $PSScriptRoot 'local-versions.json'

$propsText = [IO.File]::ReadAllText($propsPath, $enc)
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

$targets = if ($Only) {
    if ($groupRoots -notcontains $Only) { throw "版グループがありません: $Only（候補: $($groupRoots -join ', ')）" }
    @($Only)
}
else {
    @($groupRoots | Sort-Object)
}

$locals = Invoke-NativeText 'dotnet' @('nuget', 'locals', 'global-packages', '--list')
$globalPackages = (($locals.Text + $locals.ErrorText) -split "`r?`n" |
        Where-Object { $_ -match 'global-packages:' } |
        Select-Object -First 1) -replace '^global-packages:\s*', ''

$plan = [System.Collections.Generic.List[object]]::new()
foreach ($group in $targets) {
    $prefix = Get-VersionPrefix $propsText $group
    $fingerprint = Get-GroupFingerprint $group
    $previous = $record[$group]
    $isChanged = (-not $previous) -or ($previous.fingerprint -ne $fingerprint)

    $suffix = if ($previous) { $previous.suffix } else { 0 }
    if ($isChanged -and -not $NoBump) { $suffix += 1 }
    if ($suffix -lt 1) { $suffix = 1 }

    $plan.Add([pscustomobject]@{
            Group       = $group
            Prefix      = $prefix
            Suffix      = $suffix
            Version     = "$prefix-local.$suffix"
            Changed     = $isChanged
            Fingerprint = $fingerprint
        })
}

Write-Output "ローカル フィード: $feed"
Write-Output ''
Write-Output '===== 判定 ====='
foreach ($item in $plan) {
    $state = if ($item.Changed) { '変更あり' } else { '変更なし' }
    if ($item.Changed -and $NoBump) { $state = '変更あり（-NoBump のため据え置き）' }
    Write-Output ("{0,-28} {1,-24} 公開する版 {2} / ローカル {3}" -f $item.Group, $state, $item.Prefix, $item.Version)
}

if ($WhatIf) {
    Write-Output ''
    Write-Output '判定のみ（-WhatIf）: ローカル フィードの更新も記録の更新もしません。'
    exit 0
}

# ============================================================
# pack（依存順）
# ============================================================
# 取得元（NuGet.config）が `.nupkg/` を指すため、無いと復元が NU1301 で失敗する
if (-not (Test-Path -LiteralPath $feed)) { [void](New-Item -ItemType Directory -Path $feed) }

$projects = @(Get-PackageProjects)
$byGroup = @{}
foreach ($item in $plan) { $byGroup[$item.Group] = $item }

$failedNames = [System.Collections.Generic.HashSet[string]]::new()
$packed = 0

foreach ($project in $projects) {
    if (-not $byGroup.ContainsKey($project.Group)) { continue }
    $item = $byGroup[$project.Group]

    $arguments = @('pack', $project.Project, '-c', $Configuration, '-o', $feed, '--nologo',
        ('-p:VersionPrefix={0}' -f $item.Prefix),
        ('-p:VersionSuffix=local.{0}' -f $item.Suffix))
    $result = Invoke-NativeText 'dotnet' $arguments
    $text = $result.Text + $result.ErrorText

    $expected = Join-Path $feed ("{0}.{1}.nupkg" -f $project.Name, $item.Version)
    if ($result.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $expected)) {
        [void]$failedNames.Add($project.Name)
        Write-Output ("FAIL  {0} {1}" -f $project.Name, $item.Version)
        Write-Output $text
        continue
    }

    # 展開済みキャッシュを削除する（残すと後続が古い DLL を参照する。 実測）
    if ($globalPackages) {
        $cached = Join-Path $globalPackages ("{0}\{1}" -f $project.Name, $item.Version)
        if (Test-Path -LiteralPath $cached) { Remove-Item -LiteralPath $cached -Recurse -Force }
    }

    $packed += 1
    Write-Output ("OK    {0} {1}" -f $project.Name, $item.Version)
}

# ============================================================
# 古い版の削除と記録（成功したグループだけ）
# ============================================================
if (-not $NoClean -and (Test-Path -LiteralPath $feed)) {
    foreach ($project in $projects) {
        if (-not $byGroup.ContainsKey($project.Group)) { continue }
        $keep = "{0}.{1}.nupkg" -f $project.Name, $byGroup[$project.Group].Version
        $keepSymbols = "{0}.{1}.snupkg" -f $project.Name, $byGroup[$project.Group].Version
        $rxOwn = [regex]('^' + [regex]::Escape($project.Name) + '\.\d')
        Get-ChildItem -Path $feed -File |
            Where-Object { $rxOwn.IsMatch($_.Name) -and $_.Name -ne $keep -and $_.Name -ne $keepSymbols } |
            ForEach-Object {
                Write-Output ("削除  {0}" -f $_.Name)
                Remove-Item -LiteralPath $_.FullName -Force
            }
    }
}

foreach ($item in $plan) {
    $groupProjects = @($projects | Where-Object { $_.Group -eq $item.Group })
    $anyFailed = @($groupProjects | Where-Object { $failedNames.Contains($_.Name) }).Count -gt 0
    if ($anyFailed) { continue }
    $record[$item.Group] = [pscustomobject]@{ suffix = $item.Suffix; fingerprint = $item.Fingerprint }
}

$groups = [ordered]@{}
foreach ($group in ($record.Keys | Sort-Object)) {
    $groups[$group] = [ordered]@{ suffix = $record[$group].suffix; fingerprint = $record[$group].fingerprint }
}
$json = ([ordered]@{ groups = $groups } | ConvertTo-Json -Depth 5) -replace "`r?`n", "`n"
[IO.File]::WriteAllText($recordPath, $json + "`n", $enc)

Write-Output ''
if ($failedNames.Count -gt 0) {
    Write-Output ("失敗 {0} 件: {1}" -f $failedNames.Count, (@($failedNames) -join ', '))
    exit 1
}
Write-Output ("pack 完了: {0} パッケージ" -f $packed)
exit 0
