# EsUtil

複数の Windows デスクトップ アプリで使い回す **UI 非依存の共通基盤** と、**再利用可能な UI コントロール** を提供するライブラリ群です。

本リポジトリの各プロジェクトは **NuGet パッケージ** として出力し、**ローカル フィード**（`.nupkg/`。 リポジトリへはコミットしません）で相互参照します（外部へは公開しません）。 利用側のアプリは `PackageReference` で参照します。

## 1. プロジェクト

| 区分 | プロジェクト | 内容 |
| --- | --- | --- |
| 共通基盤 | [`EsUtil.Platform.Core`](src/EsUtil.Platform.Core/README.md) | キーと入力・幾何・座標変換・画像・OS サービス抽象 |
| 共通基盤（Windows） | [`EsUtil.Platform.Core.Windows`](src/EsUtil.Platform.Core.Windows/README.md) | `System.Drawing.Bitmap` との相互変換（Windows 専用） |
| 機能（ルーペ） | [`EsUtil.Feature.Loupe.Core`](src/EsUtil.Feature.Loupe.Core/README.md) | 拡大表示の UI 非依存の計算層 |
| 機能（ルーペ） | [`EsUtil.Feature.Loupe.Presenter`](src/EsUtil.Feature.Loupe.Presenter/README.md) | 外観・描画計画・ラベル計画 |
| 機能（ルーペ） | [`EsUtil.Feature.Loupe.ControlAvaloniaUI`](src/EsUtil.Feature.Loupe.ControlAvaloniaUI/README.md) | Avalonia UI 版コントロール |
| 機能（ルーペ） | [`EsUtil.Feature.Loupe.ControlMewUI`](src/EsUtil.Feature.Loupe.ControlMewUI/README.md) | MewUI 版コントロール |
| 機能（マーキー） | [`EsUtil.Feature.Marquee.Core`](src/EsUtil.Feature.Marquee.Core/README.md) | ドラッグ矩形の選択・移動・リサイズの計算層 |
| 機能（マーキー） | [`EsUtil.Feature.Marquee.Presenter`](src/EsUtil.Feature.Marquee.Presenter/README.md) | 外観・描画手順 |
| 機能（マーキー） | [`EsUtil.Feature.Marquee.ControlAvaloniaUI`](src/EsUtil.Feature.Marquee.ControlAvaloniaUI/README.md) | Avalonia UI 版コントロール |
| 機能（マーキー） | [`EsUtil.Feature.Marquee.ControlMewUI`](src/EsUtil.Feature.Marquee.ControlMewUI/README.md) | MewUI 版コントロール |

層の帰属（Core / Presenter / UI のどれに置くか）と参照の許可範囲は [`.github/依存関係.md`](.github/依存関係.md) が単一所有します。

## 2. 使い方

### 2.1 ローカル フィードから参照する

パッケージは本リポジトリの `.nupkg/` に出力されます（**リポジトリへはコミットしません**。 クローン直後に 1 回 [`buildScript/pack.ps1`](buildScript/pack.ps1) を実行してください）。 利用側のリポジトリ（例: 画面キャプチャ アプリ）は次のどちらかで参照します。

- **プロジェクト参照**: 本リポジトリを同列に置き、`..\EsUtil\src\<プロジェクト>\src\<プロジェクト>.csproj` を参照する（開発中は型の変更が即座に反映される）。
- **パッケージ参照**: `NuGet.config` にローカル フィードを追加し、`PackageReference` で参照する。

```xml
<!-- 利用側の NuGet.config。 取得元はローカル フィードを優先する -->
<packageSources>
  <clear />
  <add key="esutil-local" value="..\EsUtil\.nupkg" />
  <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
</packageSources>
```

```xml
<!-- 利用側の .csproj。 バージョンはプロジェクトごとに異なる（実際の値は .nupkg/ のファイル名で確認する） -->
<ItemGroup>
  <PackageReference Include="EsUtil.Feature.Loupe.ControlAvaloniaUI" Version="0.1.0" />
</ItemGroup>
```

### 2.2 パッケージを作り直す

```powershell
# 判定一覧を出し、変更のあったプロジェクトのバージョンの 3 桁目を +1 してから pack する
pwsh -NoProfile -File buildScript/pack.ps1

# 何も書き換えずに「変更あり / 変更なし」の判定だけを見る
pwsh -NoProfile -File buildScript/pack.ps1 -WhatIf
```

**依存順に pack する必要があります**（本リポジトリのプロジェクトは相互参照に `PackageReference` を使うため、前段のパッケージが無いと復元できない）。 手順は [`buildScript/pack.ps1`](buildScript/pack.ps1) が単一所有し、同じバージョンのままで中身を更新したときに古い DLL を拾わないよう、展開済みキャッシュも削除します。

**バージョンはプロジェクトごとに異なります**（値は [`Directory.Build.props`](Directory.Build.props) の表が単一所有します）。 ソースが変わったプロジェクトだけ 3 桁目（一番右）が +1 され、変わらないプロジェクトは据え置きです。 1・2 桁目（`Major` / `Minor`）は人が表を書き換えて上げます（詳しくは [`.github/ビルド設定規約.md`](.github/ビルド設定規約.md) §4）。

### 2.3 ソースからのビルドとテスト

```powershell
dotnet build EsUtil.slnx
dotnet test EsUtil.slnx --no-build
```

### 2.4 ローカル フィード（`.nupkg/`）の管理

本フォルダは、本リポジトリのプロジェクトが**相互参照に使うパッケージ**の出力先です。

- **外部へは公開しません**（nuget.org へは push しない。 [`buildScript/pack.ps1`](buildScript/pack.ps1) は `dotnet pack` までを行う）。
- **リポジトリへコミットしません**（[`.gitignore`](.gitignore) が `.nupkg/` を無視する）。 **クローン直後は `pack.ps1` を 1 回実行する**まで復元できません。
- **バージョンはプロジェクトごとに異なります**（値は [`Directory.Build.props`](Directory.Build.props) のバージョン表が単一所有し、**変更のあったプロジェクトだけ `pack.ps1` が 3 桁目を +1** します）。
- **同じパッケージの版は 1 つだけ**置きます（`pack.ps1` が古い版の `.nupkg` を削除します）。
- 供給元は [`buildScript/pack.ps1`](buildScript/pack.ps1) です（依存順に `dotnet pack` し、各段の後に古いキャッシュを捨てる）。
- 取得元の設定はルートの [`NuGet.config`](NuGet.config) が持ちます（`local` を最優先にする）。
- **`.nupkg` を手で置かないでください**（再現できなくなる）。 更新は `pack.ps1` で行います。
- **ソースの指紋の記録は [`buildScript/fingerprints.json`](buildScript/fingerprints.json) です**（**コミットします**。 版は持たず、`pack.ps1` が書き、`verify.ps1` が現在のソースと照合します）。 **手で編集しないでください**。

## 3. 検証

```powershell
pwsh -NoProfile -File buildScript/verify.ps1
```

ビルド（0 警告 / 0 エラー）・テスト（全件合格）・ローカル フィードの整合・NuGet の健全性・文書とコードの走査を 1 コマンドで実行し、合否を要約します（不合格があれば終了コード 1）。 手順と合否基準は [`.github/検証手順.md`](.github/検証手順.md) が単一所有します。

## 4. ドキュメント

| 知りたいこと | 所有者 |
| --- | --- |
| 規約の索引と本リポジトリ固有の事実 | [`.github/copilot-instructions.md`](.github/copilot-instructions.md) |
| 参照の許可範囲（層構造） | [`.github/依存関係.md`](.github/依存関係.md) |
| 検証の手順と合否基準 | [`.github/検証手順.md`](.github/検証手順.md) |
| 各プロジェクトの仕様 | `src/<プロジェクト>/docs/SPEC_API.md` / `SPEC_System.md` |

## 5. ライセンス

[MIT](LICENSE)
