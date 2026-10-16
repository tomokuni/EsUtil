@{
    # PSScriptAnalyzer の設定（VS Code の PowerShell 拡張が読む。 .vscode/settings.json の
    # `powershell.scriptAnalysis.settingsPath` が本ファイルを指す）。
    #
    # 目的: リポジトリの方針と衝突する規則だけを理由付きで除外し、それ以外は既定のまま検出する。

    Severity = @('Error', 'Warning')

    ExcludeRules = @(
        # 本リポジトリは UTF-8（BOM なし）で統一している（.editorconfig の charset = utf-8。
        # 既存の .cs / .md / .ps1 はすべて BOM なし）。
        # PowerShell 7 以降は BOM なしの UTF-8 を既定で読むため、BOM を要求する本規則は採用しない。
        'PSUseBOMForUnicodeEncodedFile'
    )
}
