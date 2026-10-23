# コントリビューション

本リポジトリへの変更を歓迎します。 変更の前に、この文書と[規約の索引](.github/copilot-instructions.md)を読んでください。

## 1. 前提

| 必要なもの | バージョン | 用途 |
| --- | --- | --- |
| .NET SDK | 10.0（[`global.json`](global.json)） | ビルド・テスト・パッケージ作成 |
| PowerShell | 7.4 以降（`pwsh`） | 検証スクリプト |
| OS | Windows 10 1809 以降（x64） | Win32 / WIC を呼ぶ層と UI コントロールのテスト |

UI コントロールは Avalonia UI / MewUI / WinUI 3 / WinForms の 4 実装を持ちます。 1 つを直したら、**同じ規則を他の UI にも適用**してください（共有している計算は `EsUtil.Feature.*`、変換は `EsUtil.Ui.<UI>.Interop` が持ちます）。

## 2. ビルドとテスト

```powershell
# 共通基盤と機能層
dotnet build EsUtil/EsUtil.slnx
dotnet test  EsUtil/EsUtil.slnx --no-build

# UI 層（ソリューションは UI ごとに 1 本）
dotnet build EsUtil.Ui.AvaloniaUI/EsUtil.Ui.AvaloniaUI.slnx
dotnet build EsUtil.Ui.MewUI/EsUtil.Ui.MewUI.slnx
dotnet build EsUtil.Ui.WinUI3/EsUtil.Ui.WinUI3.slnx
dotnet build EsUtil.Ui.WinForms/EsUtil.Ui.WinForms.slnx
```

**プロジェクト同士はパッケージ参照で結ばれています**（[`.github/依存関係.md`](.github/依存関係.md) §1）。 そのため、型を変えたら次の順で実行してください。

```powershell
# 1. 変更したプロジェクトのパッケージを作り直す（版の 3 桁目が +1 される）
pwsh -NoProfile -File buildScript/pack.ps1

# 2. 参照しているプロジェクトをビルドする
dotnet build EsUtil/EsUtil.slnx
```

順序を逆にすると「Interop にメンバーが無い」という紛らわしいエラーになります。

## 3. 検証（必須）

```powershell
pwsh -NoProfile -File buildScript/verify.ps1
```

ビルド（警告 0 / エラー 0）・全テスト・ローカル フィードの整合・NuGet の健全性・文書とコードの走査を 1 コマンドで実行します（不合格なら終了コード 1）。 合否基準は [`.github/検証手順.md`](.github/検証手順.md) が単一所有します。

`-Fix` を付けると、自動修正できる項目（markdownlint / `dotnet format`）を修正します。

## 4. 規約

規約は主題ごとに 1 文書へ分かれています（索引は [`.github/copilot-instructions.md`](.github/copilot-instructions.md)）。 よく参照するもの:

| 主題 | 文書 |
| --- | --- |
| 設計・記述スタイル・コメント | [`.github/コーディング規約.md`](.github/コーディング規約.md) |
| 命名と配置 | [`.github/命名規約.md`](.github/命名規約.md) |
| テスト | [`.github/テスト規約.md`](.github/テスト規約.md) |
| 層の依存関係 | [`.github/依存関係.md`](.github/依存関係.md) |
| 文書の役割 | [`.github/ドキュメント規約.md`](.github/ドキュメント規約.md) |
| コミット | [`.github/コミット規約.md`](.github/コミット規約.md) |

規約と実装が食い違う場合は、**規約を先に更新**してから実装を直します。

## 5. 変更の種類ごとの手順

### 5.1 型を追加する

1. `src/` の機能フォルダへ置く（`src/` 直下は機能の窓口だけ。 [命名規約](.github/命名規約.md)）。
2. 公開 API へ XML ドキュメント コメントを付ける（`CS1591` でビルドが失敗する）。
3. `tests/` の対応するフォルダへテストを追加する（`src/` を鏡映する）。
4. `docs/SPEC_API.md` と `README.md` の型一覧を更新する。
5. パッケージの版は人が上げない（`pack.ps1` が 3 桁目を +1 する）。

### 5.2 UI コントロールを追加する

1. `EsUtil.Feature.<機能>.Core`（UI 非依存の計算）と `EsUtil.Feature.<機能>.Presenter`（外観・描画計画）を先に作る。
2. `EsUtil.Ui.<UI>/<機能>` を 4 UI 分作り、`sample/` と `tests/` を同じ階層に置く。
3. 変換が必要なら `EsUtil.Ui.<UI>.Interop` へ追加する（**コントロール側へ複製しない**。 [依存関係](.github/依存関係.md) §4.1）。
4. 上の 3 表（ルート `README.md`・`EsUtil/README.md`・`.github/copilot-instructions.md`）へ追加する。

### 5.3 バグを直す

1. **失敗するテストを先に書く**（再現が取れたら直す）。
2. 直したら `verify.ps1` を通す。

## 6. コミット

- **日本語**で書き、1 行目に要約、3 行目以降に「何を」「なぜ」（[`.github/コミット規約.md`](.github/コミット規約.md)）。
- **1 コミット 1 主題**。 生成物（`bin` / `obj` / `.nupkg/`）を含めない。
- **`buildScript/fingerprints.json` はコミットする**（ソースの指紋の記録。 手で編集しない）。

## 7. 行動規範

相手の意図を汲み、根拠（実測値・再現手順）を示して議論してください。 個人への攻撃、差別的な表現、ハラスメントは受け付けません。
