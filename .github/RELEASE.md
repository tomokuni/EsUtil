# リリース手順（GitHub Actions）

本ドキュメントは **EsUtil のパッケージを公開する開発者**向けの手順です。
開発の進め方は [`開発手順.md`](開発手順.md)、検証の合否基準は [`検証手順.md`](検証手順.md)、
利用方法は [`../README.md`](../README.md) を参照してください。

**手順の所有者は本ドキュメント**です（ワークフローはその実装であり、手順を重複して書かない）。
リリースは **手動実行のみ**で、バージョンの**自動インクリメントは行いません**。

## 1. リリース手順

### 1.1 手順

1. **CHANGELOG の見出しを確定してコミットする**（`## 未リリース` → `## <版>（<公開日>）`。 §1.2）
2. **版を上げてコミットする**（`Directory.Build.props` の `VersionPrefix`。 §4.1）。 **桁は [`コミット規約.md`](コミット規約.md) §5 の表で決める**。 `dev` で上げ、`main` へ Pull Request で入れる
3. **Pull Request の `ci` が成功するまで待つ**（`DryRun` は自動で走るので結果も見る）
   - `ci` は検証のゲートであり**マージの必須チェック**（合否基準は [`検証手順.md`](検証手順.md)）
   - `DryRun` は **Pull Request で自動実行**され、**版・タグ・ブランチ・CHANGELOG を検証**する（公開もタグ作成もしない）。 **必須チェックではない**ため、失敗してもマージは妨げられない。 **公開前に気づくための早期発見**である
   - `release` は `DryRun` の実行を参照しない（同じ検査を `verify` で自分で行う。 失敗するのは公開より前）
   - **公開物の中身まで確かめたいとき**は、`dev` で `dry-run-pack` を実行する（配布物の作成と検証まで行い、公開とタグ付けはしない。 §1.3）
   - 配布物（パッケージ）と SBOM は **`pack.yml`** が作る（PR では `build.yml` の `package` ジョブ。 パッケージへ影響する変更のときだけ。 §3.2）
   - 前回のタグから**変更が無ければリリースしない**（`git diff` で判定）
4. **Pull Request を Merge commit でマージする**（検証したマージ結果と同じ親・同じツリーのコミットを `main` の HEAD にするため。 [`コミット規約.md`](コミット規約.md) §3）
5. `main` で **`release`** を実行する（§1.3）
   - `pack` が**この実行で配布物を作り、その場で検証**する（`pack.yml`。 3〜4 分。 他の実行のビルドは待たない）
   - `validate` が**配布物の過不足と同封物を確かめる**（公開の直前。 `dry-run-pack` でも同じ検証が走る）
   - `environment: release` の**承認**を経て、NuGet へ公開し、その後にタグと GitHub Release を作成する

**公開済みの版は不変**です。 同じ版を再公開しようとすると `release` は失敗します（`dry-run` / `dry-run-pack` は確認のため続行します）。

### 1.2 CHANGELOG の確定

**公開するコミットに `## 未リリース` を残しません**。 残っていると `dry-run` と `release` の両方が `verify` で失敗します（`CHANGELOG.md` の検査）。

- 見出しは **`## <版>（<公開日>）`**（例: `## 0.4.0（2026-11-02）`）。 日付は**公開する日**を書く。
- 版が含まれていないと**警告**が出る（失敗はしない）。
- **`## 未リリース` の節は、次の変更を書き始めるときに作る**（公開のコミットには含めない）。
- 節の中は `### 破壊的な変更` / `### 追加` / `### 変更` / `### 修正` で分ける（[`../CHANGELOG.md`](../CHANGELOG.md) の既存の並びに合わせる）。

### 1.3 操作（コマンド）

`gh` はリポジトリの権限で認証済みであることが前提です（`gh auth status`）。

```powershell
# 版と CHANGELOG を確認する（ワーキング ツリーが汚れていないこと）
git status --short
Select-String -Path Directory.Build.props -Pattern 'VersionPrefix>'
Select-String -Path CHANGELOG.md -Pattern '^## '

# Pull Request を作り、ci と DryRun の両方が成功するまで待つ（DryRun は自動で走る）
gh pr create --base main --head dev --title "<要約>" --body "<本文>"
gh pr checks --watch

# マージする（公開物は release の実行が pack で作る）
gh pr merge --merge

# 任意: 公開物の中身まで確かめる（検証 + 配布物の作成と検証。 公開とタグ付けはしない）
gh workflow run release.yml --ref dev -f mode=dry-run-pack

# main を取り、release を実行する（マージ後の新しいコミットで実行する）
git switch main
git pull
gh workflow run release.yml --ref main -f mode=release

# 承認を待つ（environment: release。 Web か gh で承認する）
gh run list --workflow release.yml --limit 3

# 結果を確認する（タグ・Release・nuget.org）
git fetch --tags
git tag --list 'v*'
gh release list --limit 6
(Invoke-RestMethod 'https://api.nuget.org/v3-flatcontainer/esutil.platform.core/index.json').versions
```

- **dry-run は 2 段階**です（どちらも `release` の実行を要求しません）。
  - `-f mode=dry-run` … 検証だけ（版・タグ・ブランチ・CHANGELOG。 十数秒）
  - `-f mode=dry-run-pack` … 上記 + **配布物の作成・検証と、配布物の過不足の検証**（`pack` → `validate`。 3〜4 分）。 **公開とタグ付けはしません**
  - **Pull Request では `dry-run` が自動で走ります**（`dry-run-pack` は走りません）。 手で実行するのは、公開物の中身まで先に確かめたいときだけです
- **`release` は dry-run の実行を要求しません**（他の実行を照会しない。 `release` の `verify` が同じ検査を自分で行い、配布物もその実行の `pack` が作る）。 dry-run は**公開前に気づくための早期発見**であり、マージ後に別の Pull Request を先にマージした場合も、そのまま `release` を実行できます。
- **`release` は `main` でのみ実行できます**（`releaseBranches`）。 `dry-run` / `dry-run-pack` は `dryRunBranches`（`main` / `dev`）で実行できます。

## 2. 構成

### 2.1 ファイルの構成

リリースの設定と処理は、次のファイルが持ちます。 **同じ知識を複数箇所に置かない**ことが設計方針です。

| ファイル | 内容 |
| --- | --- |
| [`release-config.json`](release-config.json) | 製品名、**ソリューション ファイル**（`solutionFile` / `solutions`）、**リリースを許可するブランチ** |
| [`../Directory.Build.props`](../Directory.Build.props) | **公開する版**（`VersionPrefix` 1 行。 すべてのパッケージが同じ版）。 各 `.csproj` では指定しない |
| [`../global.json`](../global.json) | .NET SDK のバージョン。 ワークフローでは指定しない |
| [`../.config/dotnet-tools.json`](../.config/dotnet-tools.json) | SBOM を作るツール（CycloneDX）の版。 CI とローカルで同じものを使う |
| [`workflows/release.yml`](workflows/release.yml) | dry-run / 公開 / タグ付けの実装 |
| [`../buildScript/pack.ps1`](../buildScript/pack.ps1) | **ローカル フィード用**のパッケージ作成（未公開の版を試すときだけ使う。 公開はワークフローが行う） |
| [`scripts/generateSbom.ps1`](scripts/generateSbom.ps1) | SBOM（CycloneDX）の作成 |

**版はすべてのパッケージで 1 つ**です（`Directory.Build.props` の `VersionPrefix`）。 タグも Release も **1 件**（`v<版>`。 例: `v0.4.0`）になります。

### 2.2 リポジトリの設定（初回のみ）

| 設定 | 内容 |
| --- | --- |
| `Protect main`（ブランチ保護） | `main` への直接 push を禁止し、`ci` の成功を必須にする。 **`strict_required_status_checks_policy` を `true`** にし、Pull Request が最新の `main` に追随していないとマージできないようにする。 **マージ方式は Merge commit のみ**（Squash / Rebase を許可しない。 検証したマージ結果と同じ親・同じツリーのコミットを `main` の HEAD にするため）（[`rulesets/protect-main.json`](rulesets/protect-main.json)） |
| `release`（Environment） | **Required reviewers** を設定する。 公開ジョブはこの環境の承認が無いと動かない |
| NuGet の Trusted Publishing | ポリシーを「リポジトリ + ワークフロー ファイル（`release.yml`）+ `environment: release`」に紐づける。 **長期の API キーは持たない** |
| `NUGET_USER`（Secret） | Trusted Publishing のユーザー名 |

## 3. 全体の流れ

### 3.1 実行契機とコスト

**フル検証は Pull Request の 1 回だけ**です。 `main` へ入るのは Pull Request だけであり（ruleset）、Pull Request の検証は**マージ後に `main` へ載るツリーそのもの**（`refs/pull/<番号>/merge`）を対象にします。 同じツリーをもう一度検証しても結果は変わらないため、**`dev` への push と `main` への push では検証しません**。

**古い `main` に対する検証結果は使いません**。 ruleset の `strict_required_status_checks_policy` が `true` のため、Pull Request が最新の `main` に追随していないとマージできません。 追随すると `refs/pull/<番号>/merge` が作り直され、`ci` がその組み合わせで走り直されます（複数の Pull Request が古い `main` のまま順にマージされ、**実際に載る組み合わせが未検証**になるのを防ぐ）。

| 契機 | 実行する内容 | 理由 |
| --- | --- | --- |
| `dev` への push | なし | 手元の `verify.ps1` と、`main` 向け Pull Request の検証で足りる |
| `main` 向け Pull Request | フル検証（`build.yml`。 必須チェックは `ci`） | **唯一のゲート**。 最新の `main` とマージしたツリーを検証する（配布物・パッケージングの検証・SBOM は `pack.yml` が持つ。 §3.2） |
| `main` 向け Pull Request | **`dry-run` を自動実行**（`release.yml`。 版・タグ・ブランチ・CHANGELOG の検証だけ） | マージ後に手動で dry-run を回さない（公開物は `release` の実行で作り、そのまま公開する） |
| `main` 向け Pull Request（**lock が変わりうるファイルを触ったときだけ**） | `Locks`（`locks.yml`。 `packages.lock.json` の自動更新と `dev` への自動コミット） | 版やパッケージを変えると lock が古くなり `NU1004` で落ちるため。 文書・コードだけの Pull Request では**ジョブを起動しない**（実測 2026-11-06: 1 回 110 秒） |
| `main` への push（マージ） | なし | Pull Request と同じツリーのため重複する（最新の `main` への追随が前提） |
| `workflow_dispatch`（`build.yml`） | フル検証（配布物の作成と検証・SBOM を含む） | 任意のブランチを今すぐ確かめたいとき |
| Release `dry-run`（手動） | 版・タグ・ブランチ・CHANGELOG の検証 | `dev` などで先に確かめたいとき（配布物は作らない。 最も軽い） |
| Release `dry-run-pack`（手動） | 上記 + **配布物の作成と検証**（`pack.yml` と `verifyPackaging.ps1`）と配布物の過不足の検証 | **公開以外のすべてを公開前に確かめたいとき**（3〜4 分。 公開とタグ付けはしない） |
| Release `release`（手動） | **この実行で配布物を作り**（`pack.yml`）、検証したバイト列をそのまま公開し、タグ付けする | 検証したバイト列と公開するバイト列を一致させる（別の実行の成果物を探さない） |

- **Pull Request の `dry-run` は自動**です（`release.yml` の `pull_request`）。 版・CHANGELOG・タグ・ブランチの検証を PR のたびに行うため、**マージ後に dry-run を実行する必要はありません**。 配布物と SBOM は `pack.yml` が作り（PR では `build.yml` の `package` ジョブ、公開時は `release` の実行そのもの）、**パッケージへ影響する変更のときだけ**です。
- **作業ブランチで早く壊れを見つけたいとき**は、手元の `.github/verify/verify.ps1`（[`検証手順.md`](検証手順.md)）か、`build.yml` の `workflow_dispatch` を使います。
- **README の build バッジ**は `?event=pull_request` を指定します（既定は**既定ブランチの最新実行**であり、`main` の push では実行しないため、`dev` の古い push の結果が残って PR の失敗を表示できません）。

### 3.2 ジョブの流れ

```mermaid
flowchart LR
    A["pull_request（自動）<br/>workflow_dispatch（手動）"] --> B["release: verify<br/>CHANGELOG・版・タグ・ブランチ"]
    B -->|"release / dry-run-pack"| P["pack.yml（workflow_call）<br/>dotnet pack<br/>→ パッケージングの検証<br/>→ SBOM"]
    P --> V["validate<br/>配布物の過不足・同封物"]
    V -->|"release のみ<br/>同じ実行の成果物・承認（environment: release）"| H["publish<br/>NuGet へ公開"]
    H --> T["tag<br/>由来の証明 → タグ 1 本と Release 1 件"]
    W["build: verify<br/>ビルドとテスト（Release）"] -->|"パッケージへ影響する変更のみ"| Q["build: package<br/>→ pack.yml"]
```

**公開が成功した後にだけタグを打ちます**（公開できなかった版にタグを残さないため）。 **ジョブの実行条件は既定の `success()` に任せます**（`!cancelled()` も `needs.*.result` の手書きもしません）。 ただし **`publish` だけは明示の `if` を持ちます**: `dry-run-pack` でも `pack` と `validate` が走るため、既定の `success()` だけでは **`dry-run-pack` が公開まで進んでしまう**からです（`mode == 'release'` と `count != '0'` と `validate` の成功を条件にします）。 `pack` は `release` / `dry-run-pack` のとき走り、`validate` はその後に、`publish` は `release` のときだけ、`tag` は `publish` の後に置くため、**スキップされた `needs` を持つジョブは自動的にスキップされます**（`dry-run` でも、リリースする変更が無いときも、公開もタグ付けも走りません）。

**配布物は [`workflows/pack.yml`](workflows/pack.yml) が単一所有します**（`build.yml` の `package` ジョブも `release.yml` の `pack` ジョブも、このワークフローを `workflow_call` で呼びます）。 作成の手順（Pull Request では `dotnet pack --no-build` で `verify` のビルドを使い、変更の無かったソリューションだけ作り足す。 公開時は `verify` のビルドが無いため `dotnet pack` がビルドも行う）も、パッケージングの検証（`verifyPackaging.ps1 -SkipPack -Feed artifacts -Version <版>`）も、SBOM も 1 か所にあります。

**`release` は自分の実行で配布物を作ります**（`pack.yml` を呼ぶ）。 以前は `build.yml` の実行を API で走査して同じツリーの成果物を探し、最大 5 分待ち、無ければフォールバックで作り直していました（条件式の誤りと待機の費用がレビュー指摘の温床でした。 2026-11-07 に撤去）。 **別の実行を探さない**ため、**待機・フォールバック・出所の判定という仕組みが丸ごと不要**になり、**検証したバイト列と公開するバイト列が同一**になります。 代償は `release` 1 回あたり 3〜4 分の作成です（Pull Request では `build.yml` が 1 回だけ作り、**2 回 pack しません**）。

**`ci`（必須チェック）は `package` を待ちません**（`ci` の `needs` に `package` を入れない）。 そのため**マージ直後に `release` を始めても `build.yml` の `package` を待つ必要はありません**（`release` が自分で作る）。

**パッケージング（nuget の正しさ）の検証は `pack.yml` が持ちます**（`build.yml` の `package` ジョブと `release.yml` の `pack` ジョブの両方。 **`ci` の needs には入れません**＝必須チェックを遅らせない）。 検証は**全パッケージが揃ったフィード**を要求するため、変更の無かったソリューションのパッケージはそのジョブで 1 本ずつ作ります（ビルドし直すのはその分だけ）。 検証は `verifyPackaging.ps1 -SkipPack -Feed artifacts -Version <版>`（同封物・nuspec の依存・パッケージ参照での復元/ビルド/起動）。 **パッケージへ影響する変更のときだけ**実行します（workflow や `.editorconfig` の変更では実行しない）。 **SBOM のツールの版（`.config/dotnet-tools.json`）や配布する単位（`.github/release-config.json`）だけの変更でも実行します**（検証するソリューションが無いため、`pack.yml` が全ソリューションをこの実行で作ります。 そのとき `verify` はスキップされるので、`package` の条件は既定の `success()` に任せず「成功またはスキップ」を明示しています）。

**`pack.yml` は NuGet のグローバル パッケージ フォルダをキャッシュしません**（同じ ID・版の EsUtil パッケージが残っていると、検証の復元が**別のビルドのパッケージ**を再利用し、新しい nupkg の欠陥を検出できない。 実測 2026-11-06。 復元は 40 秒程度であり、正しさと引き換えにしない）。

**`pack` はソリューションを 1 本ずつ実行します**（UI ソリューションは共通基盤を `ProjectReference` で参照するが共通基盤をソリューションへ含まないため、同時に動かすと共有の `obj/Release` を奪い合って `GenerateDepsFile` が失敗する。 実測 2026-11-05: CI で再現）。 手元の実測でも、直列が 19 秒であるのに対し、ソリューションごとに出力を隔離した並列は参照先の再ビルドが増えて遅い。 `build.yml` の `verify` はマトリクスで分かれているため、**この制約は `pack.yml` の `pack` ジョブに当てはまります**。

**SBOM は `pack.yml` がソリューションごとに 1 ファイル作ります**（`generateSbom.ps1`）。 **復元は 1 回だけ直列に行い、解析は 5 本を並列に**走らせます（ツールの復元も 1 回だけ）。 **並列のまま復元させません**: 共有プロジェクトの `obj/` を取り合って失敗し、復元の費用も増えます（実測 2026-11-05: 5 本がそれぞれ復元し直して 140 秒）。 解析は `-SkipPackageRestore`（`--disable-package-restore`）で行います（実測 2026-11-06: 1 本あたり復元込み 11.8 秒 → 復元なし 1.0 秒）。 復元が無いと components が 0 件になって失敗します（`generateSbom.ps1` が 0 件を不合格にする）。

**公開するバイト列は、その実行で作って検証したものだけです**（別の実行の成果物を探しません）。 公開の直前に次を確かめます（[`workflows/release.yml`](workflows/release.yml)）。

| 確認 | 内容 |
| --- | --- |
| 配布物の中身 | リポジトリの全パッケージが**過不足なく同じ版**で入っていること。 XML ドキュメントと `README.md` が同封されていること（公開の直前に検証する） |
| 作成と検証の一致 | 公開するのは、**同じ実行の `pack.yml` が作った直後に `verifyPackaging.ps1` が検証したバイト列**であること（`actions/download-artifact` に `run-id` を渡さない） |
| 由来の証明 | 公開の後に `actions/attest-build-provenance` で、どのワークフロー・どのコミットが作ったかを証明する（§4.2） |

- **他の実行の成果物を使わないため、fork の Pull Request の成果物や保存期間の失効を気にしません**（`pack.yml` は呼び出し元の実行の中で走る）。 公開できるのは `releaseBranches`（`main`）での `release` だけです。

## 4. 細かい説明

### 4.1 バージョンの指定

版は **`Directory.Build.props` にコミットした値**を使います（ワークフローは書き換えません）。
入力 `version` は確認用であり、ツリーの値と一致しなければ失敗します。

```xml
<VersionPrefix>0.4.1</VersionPrefix>
```

- **形式は 3 桁の数値**（`<主>.<副>.<修正>`）。
- **ローカル識別子（`-`）を含めない**（公開する版にだけ適用する規則。 ローカルで試すときは `buildScript/pack.ps1` が `local.<n>` を付ける）。
- **すべてのパッケージが同じ版**になる（版グループは持たない）。

### 4.2 Release の添付ファイル

Release には、**ソリューションごとの SBOM**（`<ソリューション>.<版>.sbom.cdx.json`。 5 件）がまとめて添付されます。
パッケージ本体は nuget.org が配布元であり、SBOM は「どの依存のどの版を使ったか」を公開物から辿るために付けます。

**公開したパッケージには由来の証明（build provenance）を付けます**（`actions/attest-build-provenance`）。
どのワークフロー・どのコミットが作ったパッケージかを後から検証できます。

### 4.3 失敗した場合の復旧

| 失敗した箇所 | 状態 | 復旧 |
| --- | --- | --- |
| `verify`（dry-run / `dry-run-pack` / `release` の失敗） | 何も作っていない | 原因（版・CHANGELOG・ブランチ）を直して再実行する |
| `package`（`build.yml` 失敗） | 成果物が無い（公開していない） | 原因を直して再実行する（`package` は**パッケージングの検証と SBOM の作成も兼ねる**。 §3.2）。 `release` は自分の実行で作るため、公開への影響は無い |
| `pack` / `validate`（作成・検証の失敗） | 何も公開していない | 原因を直して `release`（または `dry-run-pack`）を再実行する（作成からやり直す） |
| `publish`（一部だけ公開） | 公開済みの版は取り消せない | 残りを再実行する（公開済みの版は HEAD で確認して**省略**する） |
| `tag`（タグ作成の失敗） | 公開済み・タグ無し | `release` を再実行する（公開は省略され、タグだけが作られる） |

**版を公開した後に中身を直すことはできません**。 修正は**版を上げて**行います。

### 4.4 ローカルでの確認

```powershell
# 検証（ビルド・テスト・パッケージング・走査。 合否基準は .github/検証手順.md）
pwsh -NoProfile -File .github/verify/verify.ps1

# 未公開の版をローカル フィード（.nupkg/）へ出す（利用側のアプリで試すとき）
pwsh -NoProfile -File buildScript/pack.ps1

# SBOM だけを作る（既定は復元から行う。 `pack.ps1` や `verify.ps1` の直後なら解析だけで済む）
pwsh -NoProfile -File .github/scripts/generateSbom.ps1 -Output sbom
pwsh -NoProfile -File .github/scripts/generateSbom.ps1 -Output sbom -SkipPackageRestore
```

### 4.5 依存関係の更新（Dependabot）

[`dependabot.yml`](dependabot.yml) が GitHub Actions と NuGet パッケージの更新を Pull Request で通知します。

- **NuGet の対象は版の単一所有（`Directory.Packages.props`）のフォルダだけ**（`directory: "/"`）。 版は Central Package Management で 1 ファイルが持つため、プロジェクトごとに列挙する必要はありません。 列挙すると **Dependabot がフォルダごとに依存を解決して実行が長引きます**（実測 2026-11-02: 85 フォルダで 12〜56 分。 時間切れで Pull Request が作られないことがありました）。 登録漏れは `verify.ps1` が不合格にします（規則は [`検証手順.md`](検証手順.md) §8）。
- **メジャー更新の一部は自動で作らない**（`xunit.v3` / `xunit.analyzers`）: テストの書き方と実行基盤が変わるため、人が判断します。
- **MewUI（0.x）は固定して手動で更新する**: 破壊的変更でビルドが通らなくなるためです。
- **lock の更新は CI が行います**（[`workflows/locks.yml`](workflows/locks.yml)）。 `dev` からの Pull Request で `packages.lock.json` が古ければ、bot が `.github/scripts/updateLocks.ps1` を実行し、**`dev` へ自動でコミット**します（開発者の手作業はありません）。 **走るのは lock が変わりうるファイルを触った Pull Request だけ**です（文書だけの Pull Request では走りません）。 対象は**配布するプロジェクト（`src`）だけ**です（テスト・サンプルは lock を持たない。 版を上げるたびに 44 ファイルの機械的な差分が出るため。 [`ビルド設定規約.md`](ビルド設定規約.md) §8）。
  - Dependabot の Pull Request も同じ仕組みで lock が更新されます（版を書き換えるのは `Directory.Packages.props` だけ）。
  - 手元で先に直したいときは `pwsh -NoProfile -File .github/scripts/updateLocks.ps1`。

### 4.6 注意点

- **公開は取り消せません**（nuget.org の版は不変）。 版を上げる前に `dry-run` で**版・CHANGELOG・タグ**を確かめ、**公開物の中身まで確かめたいときは `dry-run-pack`** を使います（`release` と同じ `pack` と `validate` が走ります）。
- **`main` 以外では公開できません**（`releaseBranches`）。 `dry-run` / `dry-run-pack` は `dev` でも実行できます。
- **タグはワークフローだけが作ります**（人が打たない。 `v<版>` は公開した版の記録であり、リリースの単位）。
- **SBOM とパッケージは同じコミットから作ります**（`pack.yml` が同じ実行・同じ `github.sha` から両方を作る）。
- **公開するコミットに `## 未リリース` を残しません**（`verify` が失敗する。 §1.2）。
- **検証は Pull Request の 1 回だけです**（push では実行しない。 §3.1）。 マージ後に検証をやり直しても、同じツリーの同じ結果になるためです。 前提として、Pull Request は**最新の `main` に追随**してからマージします（ruleset の `strict_required_status_checks_policy`）。
- **配布物の名前は `nupkg-<ツリー>` / `sbom-<ツリー>`** です（同じ実行の中で受け渡す識別子であり、**他の実行を探すためには使いません**）。
- **公開するバイト列は、その実行で作って検証したものだけ**です（`release` は自分の実行で `pack.yml` を呼ぶ。 §3.2）。

## 関連ドキュメント

- [`検証手順.md`](検証手順.md) … 検証の合否基準（所有者）
- [`ビルド設定規約.md`](ビルド設定規約.md) … 版と単一所有の規則
- [`開発手順.md`](開発手順.md) … 日常の開発の進め方
- [`../CHANGELOG.md`](../CHANGELOG.md) … 利用者に影響する変更
