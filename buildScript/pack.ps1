<#
.SYNOPSIS
    ローカル フィード（.nupkg/）へパッケージを出力し、**pack に成功した**プロジェクトのバージョンを +1 し、ソースの指紋を記録する。

.DESCRIPTION
    本リポジトリのプロジェクトは相互参照に ProjectReference ではなく PackageReference を使うため、
    **依存順に pack しないと後続のプロジェクトが復元できない**。

    手順（各段は前の段のパッケージが無いと復元できない）:
      1. プロジェクトごとのソースの指紋（ファイル内容のハッシュ）を前回の記録（buildScript/fingerprints.json）と比べる。
         **変わったプロジェクトだけ**バージョンの 3 桁目（一番右）を +1 し、変わっていないプロジェクトは据え置く
         （判定の一覧は必ず表示する。 規則は .github/ビルド設定規約.md §4）。
         **記録が無いプロジェクトは「変更なし」として扱う**（初回と `.nupkg/` を削除した後に版が一斉に上がらないようにする）。
      2. 一覧の順に `dotnet pack` する。 変更のあったプロジェクトの版は `-p:` で渡す
         （**バージョン表はまだ書き換えない**。 pack に失敗したときに版だけが上がるのを防ぐため）。
      3. pack したパッケージの展開済みキャッシュ（グローバル パッケージ フォルダ）を削除する。
         同じバージョンのまま中身を更新しても NuGet はキャッシュを使うため、削除しないと
         後続のプロジェクトが**古い DLL** を参照する（実測で必要と確認した手順）。
      4. 同じパッケージの**古い版の .nupkg を削除する**（.nupkg/ には各パッケージ 1 版だけを置く）。
      5. **pack に成功したプロジェクトだけ**バージョン表（Directory.Build.props）を新しい版へ書き換える
         （**失敗したプロジェクトの版は据え置く**。 失敗のたびに番号が増えるのを防ぐ）。
      6. **pack に成功したプロジェクトだけ**ソースの指紋を buildScript/fingerprints.json へ記録する
         （**版は記録しない**。 版の値は Directory.Build.props の表 1 箇所だけが持つ）。
         `verify.ps1` はこの記録と現在のソースを比べ、「ソースを直したが pack していない」状態を検出する。

    パッケージは外部へ公開しない（ローカル保存のみ）。 出力先の `.nupkg/` はリポジトリへコミットしない。

    バージョンの 1・2 桁目は人が Directory.Build.props の表で上げる（本スクリプトは 3 桁目だけを触る）。

.PARAMETER Configuration
    pack の構成（既定 Release）。

.PARAMETER NoClean
    出力先（.nupkg/）の既存パッケージ（古い版）を削除しない（差分の確認用。 通常は指定しない）。

.PARAMETER NoBump
    バージョンを +1 しない（バージョン表の値のまま pack する）。
    人が 1・2 桁目を上げた版を確定して出したいとき、**変更を戻して表の版へ戻したいとき**、
    最初の記録を作るときに使う。

.PARAMETER Only
    指定したパッケージ（プロジェクト名）だけを pack する（プロジェクト単体のビルドの前処理が使う）。
    他のプロジェクトも判定は行うが、pack も版の更新も記録もしない。

.PARAMETER WhatIf
    何も書き換えずに判定（変更あり / 変更なし）だけを表示する（バージョン表の更新も pack もしない）。

.EXAMPLE
    pwsh -NoProfile -File buildScript/pack.ps1

.EXAMPLE
    pwsh -NoProfile -File buildScript/pack.ps1 -WhatIf

.EXAMPLE
    pwsh -NoProfile -File buildScript/pack.ps1 -NoBump
#>
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Release',
    [switch]$NoClean,
    [switch]$NoBump,
    [switch]$WhatIf,
    [string]$Only = ''
)

$ErrorActionPreference = 'Stop'
$enc = [Text.UTF8Encoding]::new($false)
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
Set-Location $repoRoot

# ネイティブ コマンド（dotnet）の出力を UTF-8 として読む（規則の所有者は .github/検証手順.md §15）
. (Join-Path $PSScriptRoot 'VerifyCommon.ps1')

# 対象（本体プロジェクト）はソリューション フォルダ（ルート直下の EsUtil*）配下の .csproj から求める
# （テスト・サンプルは除く）。 **依存順**（PackageReference の EsUtil.* が先）に並べる。
# 並べ替えの根拠は .github/依存関係.md §3。 対象の追加は .csproj を置くだけでよく、本スクリプトの一覧は触らない。
function Get-PackageProjects {
    $all = @(Get-ChildItem -Path $repoRoot -Directory |
        Where-Object { $_.Name -like 'EsUtil*' } |
        ForEach-Object { Get-ChildItem -Path $_.FullName -Recurse -File -Filter '*.csproj' } |
        Where-Object { $_.FullName -notmatch '\\(obj|bin)\\' -and $_.Name -notmatch '\.(Tests|Sample)\.csproj$' })

    # 各プロジェクトが参照するパッケージ（EsUtil.*）の名前
    $deps = @{}
    foreach ($file in $all) {
        $text = [IO.File]::ReadAllText($file.FullName)
        $deps[$file.BaseName] = @([regex]::Matches($text, '<PackageReference\s+Include="(EsUtil\.[^"]+)"') |
            ForEach-Object { $_.Groups[1].Value })
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
        $byName[$name].FullName.Substring($repoRoot.Length + 1).Replace('\', '/')
    }
}

$projects = @(Get-PackageProjects)

$feed = Join-Path $repoRoot '.nupkg'
$propsPath = Join-Path $repoRoot 'Directory.Build.props'
# 記録は buildScript/ に置いてコミットする（版は持たない）。 出力先（`.nupkg/`）はコミットしないため、
# 記録を出力先へ置くと「出力先を消すと版が一斉に上がる」ことになる（記録が無い＝変更なしの規則で緩和済み）。
$fingerprintPath = Join-Path $PSScriptRoot 'fingerprints.json'

# バージョンの表の名前（MSBuild のプロパティ名にドットは使えない。 実測 MSB5016）。
# パッケージ ID のドットを `_` に置換した名前に `_Version` を付ける（例: EsUtil.Feature.Loupe.Core_Version）。
function Get-VersionKey([string]$name) { return (($name -replace '\.', '_') + '_Version') }

# 表から 1 プロジェクト分のバージョンを読む（値は 3 桁の数値だけを許可する）。
function Get-TableVersion([string]$text, [string]$name) {
    $key = Get-VersionKey $name
    $m = [regex]::Match($text, ("<{0}>([^<]+)</{0}>" -f [regex]::Escape($key)))
    if (-not $m.Success) { throw "Directory.Build.props のバージョン表に $key がありません" }
    $value = $m.Groups[1].Value.Trim()
    if ($value -notmatch '^\d+\.\d+\.\d+$') { throw "Directory.Build.props の $key が 3 桁の数値ではありません: $value" }
    return $value
}

# 3 桁目（一番右）だけを +1 する（1・2 桁目は人が上げる値なので触らない）。
function Get-BumpedVersion([string]$version) {
    $parts = $version.Split('.')
    return ('{0}.{1}.{2}' -f $parts[0], $parts[1], ([int]$parts[2] + 1))
}

$propsText = [IO.File]::ReadAllText($propsPath, $enc)
$recorded = if (Test-Path -LiteralPath $fingerprintPath) { Get-Content -LiteralPath $fingerprintPath -Raw | ConvertFrom-Json } else { $null }

$locals = Invoke-NativeText 'dotnet' @('nuget', 'locals', 'global-packages', '--list')
$globalPackages = (($locals.Text + $locals.ErrorText) -split "`r?`n" |
        Where-Object { $_ -match 'global-packages:' } |
        Select-Object -First 1) -replace '^global-packages:\s*', ''
$failures = [System.Collections.Generic.List[string]]::new()

Write-Output "ローカル フィード: $feed"
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
$bumped = 0

# 1. 判定（変更のあったプロジェクトだけ 3 桁目を +1 する）。
#    前回 pack したときの指紋と現在のソースを比べる（「その版としてフィードへ出していない変更があるか」で決まる）。
#    記録が無いプロジェクト（初回・出力先を消した後）は「変更なし」として扱う。
$plan = [System.Collections.Generic.List[object]]::new()
foreach ($project in $projects) {
    $name = [IO.Path]::GetFileNameWithoutExtension($project)
    if (-not (Test-Path -LiteralPath (Join-Path $repoRoot $project))) {
        throw "対象のプロジェクトが見つかりません: $project"
    }

    $table = Get-TableVersion $propsText $name
    $fingerprint = Get-ProjectFingerprint $project
    $before = ''
    if ($recorded -and $recorded.fingerprints) {
        $entry = $recorded.fingerprints.PSObject.Properties[$name]
        if ($entry) { $before = $entry.Value }
    }
    $isChanged = $before -ne '' -and $before -ne $fingerprint
    $version = if ($isChanged -and -not $NoBump) { Get-BumpedVersion $table } else { $table }

    if ($isChanged) { $bumped++ }
    # 記録は前回の値を引き継ぎ、pack に成功したプロジェクトだけ後で上書きする
    # （失敗したプロジェクトの記録を残し、「直したが pack していない」状態を verify.ps1 が検出できるようにする）
    if ($before -ne '') { $fingerprints[$name] = $before }
    $plan.Add([pscustomobject]@{ Project = $project; Name = $name; Table = $table; Version = $version; Changed = $isChanged; Fingerprint = $fingerprint })
}

# -Only: プロジェクト単体のビルドの前処理。 そのプロジェクトだけを pack の対象にする
# （依存する側の版は上げない。 パッケージの依存は「以上」の範囲で記述されるため、参照側は新しい版を解決できる）。
if ($Only -ne '') {
    $plan = [System.Collections.Generic.List[object]]@($plan | Where-Object { $_.Name -eq $Only })
    if ($plan.Count -eq 0) { throw "pack の対象に $Only はありません" }
}

Write-Output 'バージョンの判定（変更のあったプロジェクトだけ 3 桁目を +1 する。 実際に上がるのは pack に成功したときのみ）:'
foreach ($item in $plan) {
    $label = if ($item.Changed) { '変更あり' } else { '変更なし' }
    $to = if ($item.Version -ne $item.Table) { "$($item.Table) → $($item.Version)" }
    elseif ($item.Changed) { "$($item.Table)（-NoBump のため据え置き）" }
    else { "$($item.Table)（据え置き）" }
    Write-Output ("  {0}  {1,-42}  {2}" -f $label, $item.Name, $to)
}

Write-Output ''
if ($WhatIf) {
    Write-Output "判定のみ（-WhatIf）: 変更 $bumped 件 / 全体 $($plan.Count) 件。 バージョン表の更新も pack もしません。"
    exit 0
}

# 2. pack へ渡す版の上書きを組み立てる（**表はまだ書き換えない**）。
#    表を先に書き換えると、pack が失敗したときにも版だけが上がる（内容を戻すたびに番号が増える）。
#    そのため pack の間だけ `-p:` で「今回出す版」を渡し、表の更新は pack の成功後（5.）に行う。
#    依存先の版も一緒に渡す（後続のプロジェクトは参照先の新しい版で復元する必要がある）。
$versionOverrides = @()
foreach ($item in $plan) {
    if ($item.Version -ne $item.Table) {
        $versionOverrides += ('-p:{0}={1}' -f (Get-VersionKey $item.Name), $item.Version)
    }
}

# pack が起動するビルドでは、本スクリプトを呼ぶ前処理（Directory.Build.targets の CheckPackageVersions）を
# 止める（pack 自身が版を決めているため、入れ子で pack を呼ぶと循環する）。
$versionOverrides += '-p:EsUtilVersionCheck=false'

# 3. 依存順に pack する（各段は前の段のパッケージが無いと復元できない）。
New-Item -ItemType Directory -Force -Path $feed | Out-Null
$packed = [System.Collections.Generic.List[object]]::new()
foreach ($item in $plan) {
    $name = $item.Name
    $version = $item.Version

    $pack = Invoke-NativeText 'dotnet' (@('pack', $item.Project, '-c', $Configuration, '-o', $feed, '--nologo') + $versionOverrides)
    $text = $pack.Text + $pack.ErrorText
    if ($pack.ExitCode -ne 0) {
        Write-Output "FAIL  $name"
        Write-Output $text
        $failures.Add($name)
        continue
    }

    # 出力されたファイル名が <パッケージ ID>.<バージョン>.nupkg であることを確かめる
    # （バージョンの綴りが SDK に解釈されない事故を検出する）
    if (-not (Test-Path -LiteralPath (Join-Path $feed "$name.$version.nupkg"))) {
        Write-Output "FAIL  $name（出力されたパッケージの名前が $name.$version.nupkg ではありません）"
        Write-Output $text
        $failures.Add($name)
        continue
    }

    # 展開済みキャッシュを捨てる（同じバージョンで中身が変わっても後続が新しい DLL を拾うようにする）
    $cached = Join-Path $globalPackages "$name\$version"
    if (Test-Path -LiteralPath $cached) {
        Remove-Item -LiteralPath $cached -Recurse -Force
    }

    # 古い版を残さない（.nupkg/ には各パッケージ 1 版だけを置く）。
    # 新しいパッケージができた後に行う（先に消すと、途中で失敗したときにフィードが空になる）。
    # 名前は「パッケージ ID + 3 桁のバージョン」だけを対象にする（`EsUtil.Platform.Core` が
    # `EsUtil.Platform.Core.Windows` のパッケージを巻き込まないようにする）。
    if (-not $NoClean) {
        $rxOwn = [regex]("^" + [regex]::Escape($name) + "\.\d+\.\d+\.\d+\.nupkg$")
        Get-ChildItem -Path $feed -File | Where-Object { $rxOwn.IsMatch($_.Name) -and $_.Name -ne "$name.$version.nupkg" } |
            Remove-Item -Force
    }

    $packed.Add($item)
    $fingerprints[$name] = $item.Fingerprint
    Write-Output "OK    $name  $version"
}

# 5. バージョン表を書き換える（**pack に成功したプロジェクトだけ**。 失敗したプロジェクトは据え置き、
#    次の実行で改めて +1 する。 値を持つのは本表だけであり、3 桁目だけを触る）。
if (-not $NoBump -and $bumped -gt 0) {
    $updated = 0
    foreach ($item in $packed) {
        if ($item.Version -eq $item.Table) { continue }
        $key = Get-VersionKey $item.Name
        $propsText = [regex]::Replace($propsText, ("<{0}>[^<]+</{0}>" -f [regex]::Escape($key)), ("<{0}>$($item.Version)</{0}>" -f $key))
        $updated++
    }

    if ($updated -gt 0) {
        [IO.File]::WriteAllText($propsPath, $propsText, $enc)
        Write-Output "バージョン表を更新: $updated 件（Directory.Build.props）"
        Write-Output ''
    }
}

# 6. ソースの指紋を記録する（**pack に成功したプロジェクトだけ**を上書きする。 失敗したプロジェクトは
#    前回の記録のまま残す＝「直したが pack していない」状態を verify.ps1 が不合格にする）。
if ($packed.Count -gt 0) {
    # 指紋は「フィードに入った版のソース」を記録する（失敗を「最新」と記録しない）。
    # 記録は指紋だけを持つ（版の値は Directory.Build.props の表 1 箇所だけが持つ）。
    # 並びはプロジェクト名の順に固定する（pack の対象順に依存させない。 差分を読めるようにする）。
    # 改行は作業ツリーの規約（CRLF）に揃える（`ConvertTo-Json` の出力と混ざると `w/mixed` になる。 実測）。
    $sorted = [ordered]@{}
    foreach ($key in ([string[]]@($fingerprints.Keys) | Sort-Object -CaseSensitive)) {
        $sorted[$key] = $fingerprints[$key]
    }

    $json = [pscustomobject]@{ fingerprints = $sorted } |
        ConvertTo-Json -Depth 3
    $json = ($json -replace "`r`n", "`n") -replace "`n", "`r`n"
    [IO.File]::WriteAllText($fingerprintPath, $json + "`r`n", $enc)
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
