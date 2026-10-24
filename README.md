# EsUtil

![Release](https://github.com/tomokuni/EsUtil/actions/workflows/release.yml/badge.svg)
![Build](https://github.com/tomokuni/EsUtil/actions/workflows/build.yml/badge.svg?branch=main)
![Platform](https://img.shields.io/badge/Platform-Windows%2010%2B-0078D6)
![.NET](https://img.shields.io/badge/.NET-10.0-512BD4)
![Avalonia](https://img.shields.io/badge/UI-Avalonia-8D46D6)
![MewUI](https://img.shields.io/badge/UI-MewUI-00BFFF)
![WinUI3](https://img.shields.io/badge/UI-WinUI%203-0078D4)
![WinForms](https://img.shields.io/badge/UI-WinForms-512BD4)
![License](https://img.shields.io/badge/License-MIT-3DA639)

複数の Windows デスクトップ アプリで使い回す **UI 非依存の共通基盤** と、**再利用可能な UI コントロール** を提供するライブラリ群です。 Avalonia UI / MewUI / WinUI 3 / WinForms の 4 つの UI 実装で、同じ計算結果と同じ見た目のコントロールを共有できます。

各プロジェクトは **NuGet パッケージ**として出力します。 リポジトリ内のプロジェクト同士は**ローカル フィード**（`.nupkg/`。 リポジトリへはコミットしません）で相互参照し、利用側のアプリは `PackageReference` で参照します。

**ドキュメントと例外メッセージは日本語です**（利用者・開発者ともに日本語を前提とします。 識別子とログの接頭辞は英語です）。

## 1. ソリューションとプロジェクト

リポジトリ直下のフォルダが**ソリューション フォルダ**（`EsUtil/` と UI ごとの `EsUtil.Ui.<UI>/`）であり、その直下のフォルダが**プロジェクト フォルダ**です。 プロジェクトは**ソリューションの中**にだけ置きます（リポジトリ直下にプロジェクトを並べません）。

### 1.1 ソリューション

| ソリューション | フォルダ | プロジェクト数 | 内容 |
| --- | --- | --- | --- |
| `EsUtil` | [`EsUtil/`](EsUtil/README.md) | 8 | 共通基盤と機能層（**UI 非依存**。 4 つの UI 版が共有する） |
| `EsUtil.Ui.AvaloniaUI` | [`EsUtil.Ui.AvaloniaUI/`](EsUtil.Ui.AvaloniaUI/Interop/README.md) | 5 | Avalonia UI 版のコントロールと相互運用 |
| `EsUtil.Ui.MewUI` | [`EsUtil.Ui.MewUI/`](EsUtil.Ui.MewUI/Interop/README.md) | 5 | MewUI 版のコントロールと相互運用 |
| `EsUtil.Ui.WinUI3` | [`EsUtil.Ui.WinUI3/`](EsUtil.Ui.WinUI3/Interop/README.md) | 5 | WinUI 3 版のコントロールと相互運用 |
| `EsUtil.Ui.WinForms` | [`EsUtil.Ui.WinForms/`](EsUtil.Ui.WinForms/Interop/README.md) | 4 | WinForms 版のコントロールと相互運用 |

**層の帰属（Core / Presenter / UI のどれに置くか）は [`EsUtil/README.md`](EsUtil/README.md) が、参照の許可範囲は [`.github/依存関係.md`](.github/依存関係.md) が単一所有します**（本節は分類だけを示します）。

### 1.2 プロジェクト

| 区分 | プロジェクト | 内容 |
| --- | --- | --- |
| 共通基盤 | [`EsUtil.Platform.Core`](EsUtil/Platform.Core/README.md) | キーと入力・幾何・座標変換・画像・OS サービス抽象 |
| 共通基盤（Windows） | [`EsUtil.Platform.Core.Windows`](EsUtil/Platform.Core.Windows/README.md) | `System.Drawing.Bitmap` との相互変換（Windows 専用） |
| 機能（ルーペ） | [`EsUtil.Feature.Loupe.Core`](EsUtil/Feature.Loupe.Core/README.md) | 拡大表示の UI 非依存の計算層 |
| 機能（ルーペ） | [`EsUtil.Feature.Loupe.Presenter`](EsUtil/Feature.Loupe.Presenter/README.md) | 外観・描画計画・ラベル計画 |
| 機能（ルーペ） | [`EsUtil.Ui.AvaloniaUI.Loupe`](EsUtil.Ui.AvaloniaUI/Loupe/README.md) | Avalonia UI 版コントロール |
| 機能（ルーペ） | [`EsUtil.Ui.MewUI.Loupe`](EsUtil.Ui.MewUI/Loupe/README.md) | MewUI 版コントロール |
| 機能（ルーペ） | [`EsUtil.Ui.WinUI3.Loupe`](EsUtil.Ui.WinUI3/Loupe/README.md) | WinUI 3 版コントロール |
| 機能（ルーペ） | [`EsUtil.Ui.WinForms.Loupe`](EsUtil.Ui.WinForms/Loupe/README.md) | WinForms 版コントロール |
| 色選択 | [`EsUtil.Ui.AvaloniaUI.ColorComboBox`](EsUtil.Ui.AvaloniaUI/ColorComboBox/README.md) | Avalonia UI 版コントロール（色名・16 進・ピッカー） |
| 色選択 | [`EsUtil.Ui.MewUI.ColorComboBox`](EsUtil.Ui.MewUI/ColorComboBox/README.md) | MewUI 版コントロール |
| 色選択 | [`EsUtil.Ui.WinUI3.ColorComboBox`](EsUtil.Ui.WinUI3/ColorComboBox/README.md) | WinUI 3 版コントロール |
| 機能（マーキー） | [`EsUtil.Feature.Marquee.Core`](EsUtil/Feature.Marquee.Core/README.md) | ドラッグ矩形の選択・移動・リサイズの計算層 |
| 機能（マーキー） | [`EsUtil.Feature.Marquee.Presenter`](EsUtil/Feature.Marquee.Presenter/README.md) | 外観・描画手順 |
| 機能（マーキー） | [`EsUtil.Ui.AvaloniaUI.Marquee`](EsUtil.Ui.AvaloniaUI/Marquee/README.md) | Avalonia UI 版コントロール |
| 機能（マーキー） | [`EsUtil.Ui.MewUI.Marquee`](EsUtil.Ui.MewUI/Marquee/README.md) | MewUI 版コントロール |
| 機能（マーキー） | [`EsUtil.Ui.WinUI3.Marquee`](EsUtil.Ui.WinUI3/Marquee/README.md) | WinUI 3 版コントロール |
| 機能（マーキー） | [`EsUtil.Ui.WinForms.Marquee`](EsUtil.Ui.WinForms/Marquee/README.md) | WinForms 版コントロール |
| 機能（サムネイル一覧） | [`EsUtil.Feature.Thumbnail.Core`](EsUtil/Feature.Thumbnail.Core/README.md) | 折り返し配置・選択・可視範囲の計算層 |
| 機能（サムネイル一覧） | [`EsUtil.Feature.Thumbnail.Presenter`](EsUtil/Feature.Thumbnail.Presenter/README.md) | 外観・影・描画計画 |
| 機能（サムネイル一覧） | [`EsUtil.Ui.AvaloniaUI.Thumbnail`](EsUtil.Ui.AvaloniaUI/Thumbnail/README.md) | Avalonia UI 版コントロール |
| 機能（サムネイル一覧） | [`EsUtil.Ui.MewUI.Thumbnail`](EsUtil.Ui.MewUI/Thumbnail/README.md) | MewUI 版コントロール |
| 機能（サムネイル一覧） | [`EsUtil.Ui.WinUI3.Thumbnail`](EsUtil.Ui.WinUI3/Thumbnail/README.md) | WinUI 3 版コントロール |
| 機能（サムネイル一覧） | [`EsUtil.Ui.WinForms.Thumbnail`](EsUtil.Ui.WinForms/Thumbnail/README.md) | WinForms 版コントロール |
| 相互運用 | [`EsUtil.Ui.<UI>.Interop`](EsUtil.Ui.AvaloniaUI/Interop/README.md) | UI の型とコアの型の相互変換・描画の補助（**UI ごとに 1 つ**） |

各パッケージの版と一覧は [§6](#6-パッケージソリューション別) が持ちます。

## 2. 使い方

### 2.1 パッケージとして参照する

```powershell
# nuget.org から取得する場合（公開後）
dotnet add package EsUtil.Ui.AvaloniaUI.Marquee
```

リポジトリ内で開発する場合は、ローカル フィードを使います。 パッケージは `.nupkg/` に出力されます（**リポジトリへはコミットしません**。 クローン直後に 1 回 [`buildScript/pack.ps1`](buildScript/pack.ps1) を実行してください）。 利用側のリポジトリ（例: 画面キャプチャ アプリ）は次のどちらかで参照します。

- **プロジェクト参照**: 本リポジトリを同列に置き、`..\EsUtil\EsUtil\<プロジェクト>\src\<プロジェクト>.csproj`（UI 層は `..\EsUtil\EsUtil.Ui.<UI>\<機能>\src\<プロジェクト>.csproj`）を参照する（開発中は型の変更が即座に反映される）。
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
  <PackageReference Include="EsUtil.Ui.AvaloniaUI.Loupe" Version="0.1.0" />
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
dotnet build EsUtil/EsUtil.slnx
dotnet test EsUtil/EsUtil.slnx --no-build

# UI 層はソリューションごとに 1 本ずつ
dotnet build EsUtil.Ui.AvaloniaUI/EsUtil.Ui.AvaloniaUI.slnx
dotnet build EsUtil.Ui.MewUI/EsUtil.Ui.MewUI.slnx
dotnet build EsUtil.Ui.WinUI3/EsUtil.Ui.WinUI3.slnx
dotnet build EsUtil.Ui.WinForms/EsUtil.Ui.WinForms.slnx
```

### 2.4 ローカル フィード（`.nupkg/`）の管理

本フォルダは、本リポジトリのプロジェクトが**相互参照に使うパッケージ**の出力先です。

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
| 開発への参加（ビルド・テスト・規約） | [`CONTRIBUTING.md`](CONTRIBUTING.md) |
| 変更履歴 | [`CHANGELOG.md`](CHANGELOG.md) |
| 各プロジェクトの仕様 | `<ソリューション フォルダ>/<プロジェクト フォルダ>/docs/SPEC_API.md` / `SPEC_System.md`（例: `EsUtil.Ui.AvaloniaUI/Loupe/docs/SPEC_API.md`） |

## 5. 対応フレームワークと Native AOT

| 区分 | 対象フレームワーク |
| --- | --- |
| 共通基盤・機能層（`EsUtil.Platform.Core*` / `EsUtil.Feature.*`） | `net10.0`（`Platform.Core` は OS 依存の実装を含むため Windows 10 1809 以降） |
| UI 層（`EsUtil.Ui.*`） | `net10.0` / `net10.0-windows10.0.17763.0`（UI フレームワークの要件に従う） |

- **Native AOT への対応は層によって異なります**（`IsAotCompatible=true` は「そのアセンブリを AOT で発行して動かせる」ことの宣言です。 実測 2026-10-24: 各層のサンプルを `dotnet publish -p:PublishAot=true -r win-x64` で発行し、起動を確認）。

| 層 | Native AOT | 根拠（実測） |
| --- | --- | --- |
| 共通基盤・機能層（`EsUtil.Platform.Core*` / `EsUtil.Feature.*`） | **互換** | COM・P/Invoke を `[GeneratedComInterface]` / `[LibraryImport]`（ソース生成）だけで呼ぶ（AOT での動作を `Platform.Core` の仕様書が実測記録） |
| Avalonia UI 層（Loupe / Marquee / Thumbnail） | **互換** | サンプルを AOT で発行し、起動を確認 |
| MewUI 層（Loupe / Marquee / Thumbnail） | **互換** | 同上 |
| WinUI 3 層（Loupe / Marquee / Thumbnail） | **互換** | 同上（Windows App SDK 1.6 以降が Native AOT に対応） |
| WinForms 層 | **非対応** | **Windows Forms 自体がトリミング/AOT に対応しない**（`PublishAot` は `NETSDK1175` で失敗する） |

- **`IsAotCompatible` を設定するのは AOT で動くことを実測できた層だけです**（設定すると `IsTrimmable` のメタデータが埋め込まれ、消費者へ「トリム/AOT で使える」と宣言することになるため）。 WinForms 層は宣言も解析も付けません（AOT に対応しないため）。
- **WinUI 3 + Native AOT では managed コレクションを `ItemsSource` に渡せません**（`E_INVALIDARG`。 実測 2026-10-24: `ObservableCollection<string>` / `List<string>` / `string[]` のいずれも失敗し、`Items.Add(文字列)` は成功する。 CsWinRT が AOT で managed オブジェクトの COM ラッパーを作れないため）。 一覧へは 1 件ずつ `Items.Add` で渡してください。
- **WinUI 3 のサンプルは `EnableMsixTooling=true` を持ちます**（これが無いと `dotnet publish` が自分自身の `.pri` を発行せず、配布物が起動時に `0xC000027B` で終了します）。
- **言語**: C# 14 / .NET 10。
- **プラットフォーム**: Windows のみ（`EsUtil.Platform.Core/src/Native/Win` 配下が Win32 / WIC を呼びます。 UI 非依存の計算層は他 OS でも動きます）。
- シンボル パッケージ（`.snupkg`）を同梱し、SourceLink でリポジトリの該当コミットへ辿れます。

## 6. パッケージ（ソリューション別）

各パッケージは **NuGet パッケージ**として出力します。 版はバッジで示します（**公開後に解決します**。 公開前は「not found」と表示されます）。 版の値は [`Directory.Build.props`](Directory.Build.props) の表が単一所有します。

### 6.1 `EsUtil`（共通基盤・機能層）

ソリューション: [`EsUtil.slnx`](EsUtil/EsUtil.slnx)

| パッケージ | 版 |
| --- | --- |
| [`EsUtil.Platform.Core`](EsUtil/Platform.Core/README.md) | [![NuGet](https://img.shields.io/nuget/v/EsUtil.Platform.Core.svg)](https://www.nuget.org/packages/EsUtil.Platform.Core) |
| [`EsUtil.Platform.Core.Windows`](EsUtil/Platform.Core.Windows/README.md) | [![NuGet](https://img.shields.io/nuget/v/EsUtil.Platform.Core.Windows.svg)](https://www.nuget.org/packages/EsUtil.Platform.Core.Windows) |
| [`EsUtil.Feature.Loupe.Core`](EsUtil/Feature.Loupe.Core/README.md) | [![NuGet](https://img.shields.io/nuget/v/EsUtil.Feature.Loupe.Core.svg)](https://www.nuget.org/packages/EsUtil.Feature.Loupe.Core) |
| [`EsUtil.Feature.Loupe.Presenter`](EsUtil/Feature.Loupe.Presenter/README.md) | [![NuGet](https://img.shields.io/nuget/v/EsUtil.Feature.Loupe.Presenter.svg)](https://www.nuget.org/packages/EsUtil.Feature.Loupe.Presenter) |
| [`EsUtil.Feature.Marquee.Core`](EsUtil/Feature.Marquee.Core/README.md) | [![NuGet](https://img.shields.io/nuget/v/EsUtil.Feature.Marquee.Core.svg)](https://www.nuget.org/packages/EsUtil.Feature.Marquee.Core) |
| [`EsUtil.Feature.Marquee.Presenter`](EsUtil/Feature.Marquee.Presenter/README.md) | [![NuGet](https://img.shields.io/nuget/v/EsUtil.Feature.Marquee.Presenter.svg)](https://www.nuget.org/packages/EsUtil.Feature.Marquee.Presenter) |
| [`EsUtil.Feature.Thumbnail.Core`](EsUtil/Feature.Thumbnail.Core/README.md) | [![NuGet](https://img.shields.io/nuget/v/EsUtil.Feature.Thumbnail.Core.svg)](https://www.nuget.org/packages/EsUtil.Feature.Thumbnail.Core) |
| [`EsUtil.Feature.Thumbnail.Presenter`](EsUtil/Feature.Thumbnail.Presenter/README.md) | [![NuGet](https://img.shields.io/nuget/v/EsUtil.Feature.Thumbnail.Presenter.svg)](https://www.nuget.org/packages/EsUtil.Feature.Thumbnail.Presenter) |

### 6.2 `EsUtil.Ui.AvaloniaUI`（Avalonia UI 版）

ソリューション: [`EsUtil.Ui.AvaloniaUI.slnx`](EsUtil.Ui.AvaloniaUI/EsUtil.Ui.AvaloniaUI.slnx)

| パッケージ | 版 |
| --- | --- |
| [`EsUtil.Ui.AvaloniaUI.Interop`](EsUtil.Ui.AvaloniaUI/Interop/README.md) | [![NuGet](https://img.shields.io/nuget/v/EsUtil.Ui.AvaloniaUI.Interop.svg)](https://www.nuget.org/packages/EsUtil.Ui.AvaloniaUI.Interop) |
| [`EsUtil.Ui.AvaloniaUI.ColorComboBox`](EsUtil.Ui.AvaloniaUI/ColorComboBox/README.md) | [![NuGet](https://img.shields.io/nuget/v/EsUtil.Ui.AvaloniaUI.ColorComboBox.svg)](https://www.nuget.org/packages/EsUtil.Ui.AvaloniaUI.ColorComboBox) |
| [`EsUtil.Ui.AvaloniaUI.Loupe`](EsUtil.Ui.AvaloniaUI/Loupe/README.md) | [![NuGet](https://img.shields.io/nuget/v/EsUtil.Ui.AvaloniaUI.Loupe.svg)](https://www.nuget.org/packages/EsUtil.Ui.AvaloniaUI.Loupe) |
| [`EsUtil.Ui.AvaloniaUI.Marquee`](EsUtil.Ui.AvaloniaUI/Marquee/README.md) | [![NuGet](https://img.shields.io/nuget/v/EsUtil.Ui.AvaloniaUI.Marquee.svg)](https://www.nuget.org/packages/EsUtil.Ui.AvaloniaUI.Marquee) |
| [`EsUtil.Ui.AvaloniaUI.Thumbnail`](EsUtil.Ui.AvaloniaUI/Thumbnail/README.md) | [![NuGet](https://img.shields.io/nuget/v/EsUtil.Ui.AvaloniaUI.Thumbnail.svg)](https://www.nuget.org/packages/EsUtil.Ui.AvaloniaUI.Thumbnail) |

### 6.3 `EsUtil.Ui.MewUI`（MewUI 版）

ソリューション: [`EsUtil.Ui.MewUI.slnx`](EsUtil.Ui.MewUI/EsUtil.Ui.MewUI.slnx)

| パッケージ | 版 |
| --- | --- |
| [`EsUtil.Ui.MewUI.Interop`](EsUtil.Ui.MewUI/Interop/README.md) | [![NuGet](https://img.shields.io/nuget/v/EsUtil.Ui.MewUI.Interop.svg)](https://www.nuget.org/packages/EsUtil.Ui.MewUI.Interop) |
| [`EsUtil.Ui.MewUI.Loupe`](EsUtil.Ui.MewUI/Loupe/README.md) | [![NuGet](https://img.shields.io/nuget/v/EsUtil.Ui.MewUI.Loupe.svg)](https://www.nuget.org/packages/EsUtil.Ui.MewUI.Loupe) |
| [`EsUtil.Ui.MewUI.Marquee`](EsUtil.Ui.MewUI/Marquee/README.md) | [![NuGet](https://img.shields.io/nuget/v/EsUtil.Ui.MewUI.Marquee.svg)](https://www.nuget.org/packages/EsUtil.Ui.MewUI.Marquee) |
| [`EsUtil.Ui.MewUI.Thumbnail`](EsUtil.Ui.MewUI/Thumbnail/README.md) | [![NuGet](https://img.shields.io/nuget/v/EsUtil.Ui.MewUI.Thumbnail.svg)](https://www.nuget.org/packages/EsUtil.Ui.MewUI.Thumbnail) |
| [`EsUtil.Ui.MewUI.ColorComboBox`](EsUtil.Ui.MewUI/ColorComboBox/README.md) | [![NuGet](https://img.shields.io/nuget/v/EsUtil.Ui.MewUI.ColorComboBox.svg)](https://www.nuget.org/packages/EsUtil.Ui.MewUI.ColorComboBox) |

### 6.4 `EsUtil.Ui.WinUI3`（WinUI 3 版）

ソリューション: [`EsUtil.Ui.WinUI3.slnx`](EsUtil.Ui.WinUI3/EsUtil.Ui.WinUI3.slnx)

| パッケージ | 版 |
| --- | --- |
| [`EsUtil.Ui.WinUI3.Interop`](EsUtil.Ui.WinUI3/Interop/README.md) | [![NuGet](https://img.shields.io/nuget/v/EsUtil.Ui.WinUI3.Interop.svg)](https://www.nuget.org/packages/EsUtil.Ui.WinUI3.Interop) |
| [`EsUtil.Ui.WinUI3.Loupe`](EsUtil.Ui.WinUI3/Loupe/README.md) | [![NuGet](https://img.shields.io/nuget/v/EsUtil.Ui.WinUI3.Loupe.svg)](https://www.nuget.org/packages/EsUtil.Ui.WinUI3.Loupe) |
| [`EsUtil.Ui.WinUI3.Marquee`](EsUtil.Ui.WinUI3/Marquee/README.md) | [![NuGet](https://img.shields.io/nuget/v/EsUtil.Ui.WinUI3.Marquee.svg)](https://www.nuget.org/packages/EsUtil.Ui.WinUI3.Marquee) |
| [`EsUtil.Ui.WinUI3.Thumbnail`](EsUtil.Ui.WinUI3/Thumbnail/README.md) | [![NuGet](https://img.shields.io/nuget/v/EsUtil.Ui.WinUI3.Thumbnail.svg)](https://www.nuget.org/packages/EsUtil.Ui.WinUI3.Thumbnail) |
| [`EsUtil.Ui.WinUI3.ColorComboBox`](EsUtil.Ui.WinUI3/ColorComboBox/README.md) | [![NuGet](https://img.shields.io/nuget/v/EsUtil.Ui.WinUI3.ColorComboBox.svg)](https://www.nuget.org/packages/EsUtil.Ui.WinUI3.ColorComboBox) |

### 6.5 `EsUtil.Ui.WinForms`（WinForms 版）

ソリューション: [`EsUtil.Ui.WinForms.slnx`](EsUtil.Ui.WinForms/EsUtil.Ui.WinForms.slnx)

| パッケージ | 版 |
| --- | --- |
| [`EsUtil.Ui.WinForms.Interop`](EsUtil.Ui.WinForms/Interop/README.md) | [![NuGet](https://img.shields.io/nuget/v/EsUtil.Ui.WinForms.Interop.svg)](https://www.nuget.org/packages/EsUtil.Ui.WinForms.Interop) |
| [`EsUtil.Ui.WinForms.Loupe`](EsUtil.Ui.WinForms/Loupe/README.md) | [![NuGet](https://img.shields.io/nuget/v/EsUtil.Ui.WinForms.Loupe.svg)](https://www.nuget.org/packages/EsUtil.Ui.WinForms.Loupe) |
| [`EsUtil.Ui.WinForms.Marquee`](EsUtil.Ui.WinForms/Marquee/README.md) | [![NuGet](https://img.shields.io/nuget/v/EsUtil.Ui.WinForms.Marquee.svg)](https://www.nuget.org/packages/EsUtil.Ui.WinForms.Marquee) |
| [`EsUtil.Ui.WinForms.Thumbnail`](EsUtil.Ui.WinForms/Thumbnail/README.md) | [![NuGet](https://img.shields.io/nuget/v/EsUtil.Ui.WinForms.Thumbnail.svg)](https://www.nuget.org/packages/EsUtil.Ui.WinForms.Thumbnail) |

## 7. ライセンス

[MIT](LICENSE)
