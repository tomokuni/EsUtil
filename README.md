# EsUtil

![Release](https://github.com/tomokuni/EsUtil/actions/workflows/release.yml/badge.svg)
![Build](https://github.com/tomokuni/EsUtil/actions/workflows/build.yml/badge.svg?branch=main)
![Platform](https://img.shields.io/badge/Platform-Windows%2010%2B-0078D6)
![.NET](https://img.shields.io/badge/.NET-10.0-512BD4)
![AOT](https://img.shields.io/badge/AOT-Ready-00695C)
![Avalonia](https://img.shields.io/badge/UI-Avalonia-8D46D6)
![MewUI](https://img.shields.io/badge/UI-MewUI-00BFFF)
![WinUI3](https://img.shields.io/badge/UI-WinUI%203-0078D4)
![WinForms](https://img.shields.io/badge/UI-WinForms-512BD4)
![License](https://img.shields.io/badge/License-MIT-3DA639)

複数の Windows デスクトップ アプリで使い回す **UI 非依存の共通基盤** と、**再利用可能な UI コントロール** を提供するライブラリ群です。
Avalonia UI / MewUI / WinUI 3 / WinForms の 4 つの UI 実装で、同じ計算結果と同じ見た目のコントロールを共有できます。

各プロジェクトは **NuGet パッケージ**として出力します。
リポジトリ内のプロジェクト同士は**ローカル フィード**（`.nupkg/`。 リポジトリへはコミットしません）で相互参照し、利用側のアプリは `PackageReference` で参照します。

**ドキュメントと例外メッセージは日本語です**（利用者・開発者ともに日本語を前提とします。 識別子とログの接頭辞は英語です）。

## 1. ソリューションとプロジェクト

リポジトリ直下のフォルダが**ソリューション フォルダ**であり、その直下のフォルダが**プロジェクト フォルダ**です（プロジェクトはソリューションの中にだけ置きます）。
**使い方（インストール・クイックスタート）は各プロジェクトの `README.md` が持ちます**（版は nuget.org のバッジで示します。 公開前は「not found」と表示されます）。

**層の帰属（Core / Presenter / UI のどれに置くか）は [`EsUtil/README.md`](EsUtil/README.md) が、参照の許可範囲は [`.github/依存関係.md`](.github/依存関係.md) が単一所有します**（本節は分類だけを示します）。

### 1.1 `EsUtil`（共通基盤・機能層）

UI に依存しない層です。 4 つの UI 版が共有します。

ソリューション: [`EsUtil.slnx`](EsUtil/EsUtil.slnx)

| プロジェクト | 内容 | 版 |
| --- | --- | --- |
| [`EsUtil.Platform.Core`](EsUtil/Platform.Core/README.md) | キーと入力・幾何・座標変換・画像・OS サービス抽象 | [![NuGet](https://img.shields.io/nuget/v/EsUtil.Platform.Core.svg)](https://www.nuget.org/packages/EsUtil.Platform.Core) |
| [`EsUtil.Platform.Core.Windows`](EsUtil/Platform.Core.Windows/README.md) | `System.Drawing.Bitmap` との相互変換（Windows 専用） | [![NuGet](https://img.shields.io/nuget/v/EsUtil.Platform.Core.Windows.svg)](https://www.nuget.org/packages/EsUtil.Platform.Core.Windows) |
| [`EsUtil.Feature.Loupe.Core`](EsUtil/Feature.Loupe.Core/README.md) | 拡大表示の UI 非依存の計算層 | [![NuGet](https://img.shields.io/nuget/v/EsUtil.Feature.Loupe.Core.svg)](https://www.nuget.org/packages/EsUtil.Feature.Loupe.Core) |
| [`EsUtil.Feature.Loupe.Presenter`](EsUtil/Feature.Loupe.Presenter/README.md) | 外観・描画計画・ラベル計画 | [![NuGet](https://img.shields.io/nuget/v/EsUtil.Feature.Loupe.Presenter.svg)](https://www.nuget.org/packages/EsUtil.Feature.Loupe.Presenter) |
| [`EsUtil.Feature.Marquee.Core`](EsUtil/Feature.Marquee.Core/README.md) | ドラッグ矩形の選択・移動・リサイズの計算層 | [![NuGet](https://img.shields.io/nuget/v/EsUtil.Feature.Marquee.Core.svg)](https://www.nuget.org/packages/EsUtil.Feature.Marquee.Core) |
| [`EsUtil.Feature.Marquee.Presenter`](EsUtil/Feature.Marquee.Presenter/README.md) | 外観・描画手順 | [![NuGet](https://img.shields.io/nuget/v/EsUtil.Feature.Marquee.Presenter.svg)](https://www.nuget.org/packages/EsUtil.Feature.Marquee.Presenter) |
| [`EsUtil.Feature.Thumbnail.Core`](EsUtil/Feature.Thumbnail.Core/README.md) | 折り返し配置・選択・可視範囲の計算層 | [![NuGet](https://img.shields.io/nuget/v/EsUtil.Feature.Thumbnail.Core.svg)](https://www.nuget.org/packages/EsUtil.Feature.Thumbnail.Core) |
| [`EsUtil.Feature.Thumbnail.Presenter`](EsUtil/Feature.Thumbnail.Presenter/README.md) | 外観・影・描画計画 | [![NuGet](https://img.shields.io/nuget/v/EsUtil.Feature.Thumbnail.Presenter.svg)](https://www.nuget.org/packages/EsUtil.Feature.Thumbnail.Presenter) |

### 1.2 `EsUtil.Ui.AvaloniaUI`（Avalonia UI 版）

ソリューション: [`EsUtil.Ui.AvaloniaUI.slnx`](EsUtil.Ui.AvaloniaUI/EsUtil.Ui.AvaloniaUI.slnx)

| プロジェクト | 内容 | 版 |
| --- | --- | --- |
| [`EsUtil.Ui.AvaloniaUI.Interop`](EsUtil.Ui.AvaloniaUI/Interop/README.md) | Avalonia UI の型とコアの型の相互変換・描画の補助 | [![NuGet](https://img.shields.io/nuget/v/EsUtil.Ui.AvaloniaUI.Interop.svg)](https://www.nuget.org/packages/EsUtil.Ui.AvaloniaUI.Interop) |
| [`EsUtil.Ui.AvaloniaUI.ColorComboBox`](EsUtil.Ui.AvaloniaUI/ColorComboBox/README.md) | 色選択（色名・16 進・ピッカー） | [![NuGet](https://img.shields.io/nuget/v/EsUtil.Ui.AvaloniaUI.ColorComboBox.svg)](https://www.nuget.org/packages/EsUtil.Ui.AvaloniaUI.ColorComboBox) |
| [`EsUtil.Ui.AvaloniaUI.Loupe`](EsUtil.Ui.AvaloniaUI/Loupe/README.md) | ルーペ（拡大鏡） | [![NuGet](https://img.shields.io/nuget/v/EsUtil.Ui.AvaloniaUI.Loupe.svg)](https://www.nuget.org/packages/EsUtil.Ui.AvaloniaUI.Loupe) |
| [`EsUtil.Ui.AvaloniaUI.Marquee`](EsUtil.Ui.AvaloniaUI/Marquee/README.md) | マーキー（ドラッグ矩形） | [![NuGet](https://img.shields.io/nuget/v/EsUtil.Ui.AvaloniaUI.Marquee.svg)](https://www.nuget.org/packages/EsUtil.Ui.AvaloniaUI.Marquee) |
| [`EsUtil.Ui.AvaloniaUI.Thumbnail`](EsUtil.Ui.AvaloniaUI/Thumbnail/README.md) | サムネイル一覧 | [![NuGet](https://img.shields.io/nuget/v/EsUtil.Ui.AvaloniaUI.Thumbnail.svg)](https://www.nuget.org/packages/EsUtil.Ui.AvaloniaUI.Thumbnail) |

### 1.3 `EsUtil.Ui.MewUI`（MewUI 版）

ソリューション: [`EsUtil.Ui.MewUI.slnx`](EsUtil.Ui.MewUI/EsUtil.Ui.MewUI.slnx)

| プロジェクト | 内容 | 版 |
| --- | --- | --- |
| [`EsUtil.Ui.MewUI.Interop`](EsUtil.Ui.MewUI/Interop/README.md) | MewUI の型とコアの型の相互変換・描画の補助 | [![NuGet](https://img.shields.io/nuget/v/EsUtil.Ui.MewUI.Interop.svg)](https://www.nuget.org/packages/EsUtil.Ui.MewUI.Interop) |
| [`EsUtil.Ui.MewUI.ColorComboBox`](EsUtil.Ui.MewUI/ColorComboBox/README.md) | 色選択（色名・16 進・ピッカー） | [![NuGet](https://img.shields.io/nuget/v/EsUtil.Ui.MewUI.ColorComboBox.svg)](https://www.nuget.org/packages/EsUtil.Ui.MewUI.ColorComboBox) |
| [`EsUtil.Ui.MewUI.Loupe`](EsUtil.Ui.MewUI/Loupe/README.md) | ルーペ（拡大鏡） | [![NuGet](https://img.shields.io/nuget/v/EsUtil.Ui.MewUI.Loupe.svg)](https://www.nuget.org/packages/EsUtil.Ui.MewUI.Loupe) |
| [`EsUtil.Ui.MewUI.Marquee`](EsUtil.Ui.MewUI/Marquee/README.md) | マーキー（ドラッグ矩形） | [![NuGet](https://img.shields.io/nuget/v/EsUtil.Ui.MewUI.Marquee.svg)](https://www.nuget.org/packages/EsUtil.Ui.MewUI.Marquee) |
| [`EsUtil.Ui.MewUI.Thumbnail`](EsUtil.Ui.MewUI/Thumbnail/README.md) | サムネイル一覧 | [![NuGet](https://img.shields.io/nuget/v/EsUtil.Ui.MewUI.Thumbnail.svg)](https://www.nuget.org/packages/EsUtil.Ui.MewUI.Thumbnail) |

### 1.4 `EsUtil.Ui.WinUI3`（WinUI 3 版）

ソリューション: [`EsUtil.Ui.WinUI3.slnx`](EsUtil.Ui.WinUI3/EsUtil.Ui.WinUI3.slnx)

| プロジェクト | 内容 | 版 |
| --- | --- | --- |
| [`EsUtil.Ui.WinUI3.Interop`](EsUtil.Ui.WinUI3/Interop/README.md) | WinUI 3 の型とコアの型の相互変換・描画の補助 | [![NuGet](https://img.shields.io/nuget/v/EsUtil.Ui.WinUI3.Interop.svg)](https://www.nuget.org/packages/EsUtil.Ui.WinUI3.Interop) |
| [`EsUtil.Ui.WinUI3.ColorComboBox`](EsUtil.Ui.WinUI3/ColorComboBox/README.md) | 色選択（色名・16 進・ピッカー） | [![NuGet](https://img.shields.io/nuget/v/EsUtil.Ui.WinUI3.ColorComboBox.svg)](https://www.nuget.org/packages/EsUtil.Ui.WinUI3.ColorComboBox) |
| [`EsUtil.Ui.WinUI3.Loupe`](EsUtil.Ui.WinUI3/Loupe/README.md) | ルーペ（拡大鏡） | [![NuGet](https://img.shields.io/nuget/v/EsUtil.Ui.WinUI3.Loupe.svg)](https://www.nuget.org/packages/EsUtil.Ui.WinUI3.Loupe) |
| [`EsUtil.Ui.WinUI3.Marquee`](EsUtil.Ui.WinUI3/Marquee/README.md) | マーキー（ドラッグ矩形） | [![NuGet](https://img.shields.io/nuget/v/EsUtil.Ui.WinUI3.Marquee.svg)](https://www.nuget.org/packages/EsUtil.Ui.WinUI3.Marquee) |
| [`EsUtil.Ui.WinUI3.Thumbnail`](EsUtil.Ui.WinUI3/Thumbnail/README.md) | サムネイル一覧 | [![NuGet](https://img.shields.io/nuget/v/EsUtil.Ui.WinUI3.Thumbnail.svg)](https://www.nuget.org/packages/EsUtil.Ui.WinUI3.Thumbnail) |

### 1.5 `EsUtil.Ui.WinForms`（WinForms 版）

ソリューション: [`EsUtil.Ui.WinForms.slnx`](EsUtil.Ui.WinForms/EsUtil.Ui.WinForms.slnx)

| プロジェクト | 内容 | 版 |
| --- | --- | --- |
| [`EsUtil.Ui.WinForms.Interop`](EsUtil.Ui.WinForms/Interop/README.md) | WinForms の型とコアの型の相互変換・描画の補助 | [![NuGet](https://img.shields.io/nuget/v/EsUtil.Ui.WinForms.Interop.svg)](https://www.nuget.org/packages/EsUtil.Ui.WinForms.Interop) |
| [`EsUtil.Ui.WinForms.Loupe`](EsUtil.Ui.WinForms/Loupe/README.md) | ルーペ（拡大鏡） | [![NuGet](https://img.shields.io/nuget/v/EsUtil.Ui.WinForms.Loupe.svg)](https://www.nuget.org/packages/EsUtil.Ui.WinForms.Loupe) |
| [`EsUtil.Ui.WinForms.Marquee`](EsUtil.Ui.WinForms/Marquee/README.md) | マーキー（ドラッグ矩形） | [![NuGet](https://img.shields.io/nuget/v/EsUtil.Ui.WinForms.Marquee.svg)](https://www.nuget.org/packages/EsUtil.Ui.WinForms.Marquee) |
| [`EsUtil.Ui.WinForms.Thumbnail`](EsUtil.Ui.WinForms/Thumbnail/README.md) | サムネイル一覧 | [![NuGet](https://img.shields.io/nuget/v/EsUtil.Ui.WinForms.Thumbnail.svg)](https://www.nuget.org/packages/EsUtil.Ui.WinForms.Thumbnail) |

色選択（`ColorComboBox`）は **WinForms 版だけありません**（WinForms には相当するコントロールが無く、作る予定もありません）。

## 2. ドキュメント

| 知りたいこと | 所有者 |
| --- | --- |
| 規約の索引と本リポジトリ固有の事実 | [`.github/copilot-instructions.md`](.github/copilot-instructions.md) |
| 参照の許可範囲（層構造）と依存関係 | [`.github/依存関係.md`](.github/依存関係.md) |
| ビルド・テスト・pack・検証の進め方 | [`.github/開発手順.md`](.github/開発手順.md) |
| 検証の手順と合否基準 | [`.github/検証手順.md`](.github/検証手順.md) |
| 変更履歴 | [`CHANGELOG.md`](CHANGELOG.md) |
| 各プロジェクトの仕様 | `<ソリューション フォルダ>/<プロジェクト フォルダ>/docs/SPEC_API.md` / `SPEC_System.md`（例: `EsUtil.Ui.AvaloniaUI/Loupe/docs/SPEC_API.md`） |

## 3. 対応フレームワークと Native AOT

| 区分 | 対象フレームワーク |
| --- | --- |
| 共通基盤・機能層（`EsUtil.Platform.Core*` / `EsUtil.Feature.*`） | `net10.0`（`Platform.Core` は OS 依存の実装を含むため Windows 10 1809 以降） |
| UI 層（`EsUtil.Ui.*`） | `net10.0` / `net10.0-windows10.0.17763.0`（UI フレームワークの要件に従う） |

- **Native AOT への対応は層によって異なります**（`IsAotCompatible=true` は「そのアセンブリを AOT で発行して動かせる」ことの宣言です。
  実測 2026-10-24: 各層のサンプルを `dotnet publish -p:PublishAot=true -r win-x64` で発行し、起動を確認）。

| 層 | Native AOT | 根拠（実測） |
| --- | --- | --- |
| 共通基盤・機能層（`EsUtil.Platform.Core*` / `EsUtil.Feature.*`） | **互換** | Win32 / WIC の呼び出しを **CsWin32 の生成物**（関数ポインター）だけで行う（AOT での動作を `Platform.Core` の仕様書が実測記録） |
| Avalonia UI 層（Loupe / Marquee / Thumbnail / ColorComboBox） | **互換** | サンプルを AOT で発行し、起動を確認 |
| MewUI 層（Loupe / Marquee / Thumbnail / ColorComboBox） | **互換** | 同上 |
| WinUI 3 層（Loupe / Marquee / Thumbnail / ColorComboBox） | **互換** | 同上（Windows App SDK 1.6 以降が Native AOT に対応） |
| WinForms 層 | **非対応** | **Windows Forms 自体がトリミング/AOT に対応しない**（`PublishAot` は `NETSDK1175` で失敗する） |

- **`IsAotCompatible` を設定するのは AOT で動くことを実測できた層だけです**（設定すると `IsTrimmable` のメタデータが埋め込まれ、消費者へ「トリム/AOT で使える」と宣言することになるため）。 WinForms 層は宣言も解析も付けません（AOT に対応しないため）。
- **WinUI 3 + Native AOT では managed コレクションを `ItemsSource` に渡せません**（`E_INVALIDARG`。 実測 2026-10-24: `ObservableCollection<string>` / `List<string>` / `string[]` のいずれも失敗し、`Items.Add(文字列)` は成功する。 CsWinRT が AOT で managed オブジェクトの COM ラッパーを作れないため）。 一覧へは 1 件ずつ `Items.Add` で渡してください。
- **WinUI 3 のサンプルは `EnableMsixTooling=true` を持ちます**（これが無いと `dotnet publish` が自分自身の `.pri` を発行せず、配布物が起動時に `0xC000027B` で終了します）。
- **言語**: C# 14 / .NET 10。
- **プラットフォーム**: Windows のみ（`EsUtil.Platform.Core/src/Native/Win` 配下が Win32 / WIC を呼びます。 UI 非依存の計算層は他 OS でも動きます）。
- シンボル パッケージ（`.snupkg`）を同梱し、SourceLink でリポジトリの該当コミットへ辿れます。

## 4. ライセンス

[MIT](LICENSE)
