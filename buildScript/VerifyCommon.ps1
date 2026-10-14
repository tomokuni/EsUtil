# 検証スクリプトが共有する土台（結果の記録と、ファイル・git の読み込みの使い回し）。
# 所有者は .github/検証手順.md であり、手順は buildScript/verify.ps1（入口）と
# buildScript/verifyDocs.ps1（文書とコードの走査。 入口から起動される）が実装する。
# 本ファイルは検査そのものを持たない（両者が同じ書式で結果を出し、同じ読み込みを使うためだけのもの）。

$script:Results = [System.Collections.Generic.List[object]]::new()

function Add-Result([string]$Name, [bool]$Ok, [string]$Detail) {
    $script:Results.Add([pscustomobject]@{ Name = $Name; Ok = $Ok; Kind = 'Check'; Detail = $Detail })
    $mark = if ($Ok) { 'OK  ' } else { 'FAIL' }
    Write-Output "$mark  $Name`t$Detail"
}

# 合否に含めない参考値（数値を見るためだけの項目）
function Add-Info([string]$Name, [string]$Detail) {
    $script:Results.Add([pscustomobject]@{ Name = $Name; Ok = $true; Kind = 'Info'; Detail = $Detail })
    Write-Output "INFO  $Name`t$Detail"
}

# ファイルの中身とリポジトリのファイル一覧は多数の検査が共有するため、1 回だけ読んで使い回す
# （実測: 検査ごとに `*.cs` を列挙して読み直すと 51 秒かかっていた。 ディスクからの読み直しを無くす）。
# 自動修正（markdownlint --fix / dotnet format）の後は Clear-FileCache で捨てる（古い内容で判定しないため）。
$script:LineCache = [System.Collections.Generic.Dictionary[string, string[]]]::new()
$script:TextCache = [System.Collections.Generic.Dictionary[string, string]]::new()
$script:ListCache = [System.Collections.Generic.Dictionary[string, object]]::new()

function Get-Lines([string]$Path) {
    $cached = $null
    if ($script:LineCache.TryGetValue($Path, [ref]$cached)) { return $cached }
    $value = [IO.File]::ReadAllLines($Path, [Text.UTF8Encoding]::new($false))
    $script:LineCache[$Path] = $value
    return $value
}

function Get-Text([string]$Path) {
    $cached = ''
    if ($script:TextCache.TryGetValue($Path, [ref]$cached)) { return $cached }
    $value = [IO.File]::ReadAllText($Path, [Text.UTF8Encoding]::new($false))
    $script:TextCache[$Path] = $value
    return $value
}

function Get-GitList([string[]]$GitArguments) {
    $key = $GitArguments -join ' '
    $cached = $null
    if ($script:ListCache.TryGetValue($key, [ref]$cached)) { return $cached }
    $value = @(& git @GitArguments 2>$null)
    $script:ListCache[$key] = $value
    return $value
}

function Clear-FileCache {
    $script:LineCache.Clear()
    $script:TextCache.Clear()
    $script:ListCache.Clear()
}
