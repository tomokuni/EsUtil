# EsUtil

![Release](https://github.com/tomokuni/EsUtil/actions/workflows/release.yml/badge.svg)
![Build](https://github.com/tomokuni/EsUtil/actions/workflows/build.yml/badge.svg?event=pull_request)
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

各プロジェクトは **NuGet パッケージ**として出力します。 公開済みのパッケージは [nuget.org のプロフィール](https://www.nuget.org/profiles/tomokuni) から一覧できます（各パッケージのリンクは §1 の表にあります）。

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
| [`EsUtil.Feature.Loupe.Core`](EsUtil/Feature.Loupe.Core/README.md) | 拡大表示の計算層（Core）+ 外観・描画計画（Presenter） | [![NuGet](https://img.shields.io/nuget/v/EsUtil.Feature.Loupe.Core.svg)](https://www.nuget.org/packages/EsUtil.Feature.Loupe.Core) |
| [`EsUtil.Feature.Marquee.Core`](EsUtil/Feature.Marquee.Core/README.md) | ドラッグ矩形の計算層（Core）+ 外観・描画計画（Presenter） | [![NuGet](https://img.shields.io/nuget/v/EsUtil.Feature.Marquee.Core.svg)](https://www.nuget.org/packages/EsUtil.Feature.Marquee.Core) |
| [`EsUtil.Feature.Thumbnail.Core`](EsUtil/Feature.Thumbnail.Core/README.md) | 折り返し配置の計算層（Core）+ 外観・影・描画計画（Presenter） | [![NuGet](https://img.shields.io/nuget/v/EsUtil.Feature.Thumbnail.Core.svg)](https://www.nuget.org/packages/EsUtil.Feature.Thumbnail.Core) |
| [`EsUtil.Feature.ColumnPacking.Core`](EsUtil/Feature.ColumnPacking.Core/README.md) | 複数列の配置（最大列高さの最小化）の計算層 | [![NuGet](https://img.shields.io/nuget/v/EsUtil.Feature.ColumnPacking.Core.svg)](https://www.nuget.org/packages/EsUtil.Feature.ColumnPacking.Core) |
| [`EsUtil.Feature.ZenHan.Core`](EsUtil/Feature.ZenHan.Core/README.md) | 全角・半角・ひらがな・カタカナ・記号の相互変換 | [![NuGet](https://img.shields.io/nuget/v/EsUtil.Feature.ZenHan.Core.svg)](https://www.nuget.org/packages/EsUtil.Feature.ZenHan.Core) |

### 1.2 `EsUtil.Ui.AvaloniaUI`（Avalonia UI 版）

ソリューション: [`EsUtil.Ui.AvaloniaUI.slnx`](EsUtil.Ui.AvaloniaUI/EsUtil.Ui.AvaloniaUI.slnx)

| プロジェクト | 内容 | 版 |
| --- | --- | --- |
| [`EsUtil.Ui.AvaloniaUI.Interop`](EsUtil.Ui.AvaloniaUI/Interop/README.md) | Avalonia UI の型とコアの型の相互変換・描画の補助 | [![NuGet](https://img.shields.io/nuget/v/EsUtil.Ui.AvaloniaUI.Interop.svg)](https://www.nuget.org/packages/EsUtil.Ui.AvaloniaUI.Interop) |
| [`EsUtil.Ui.AvaloniaUI.ColorSelector`](EsUtil.Ui.AvaloniaUI/ColorSelector/README.md) | 色選択（色名・16 進・ピッカー） | [![NuGet](https://img.shields.io/nuget/v/EsUtil.Ui.AvaloniaUI.ColorSelector.svg)](https://www.nuget.org/packages/EsUtil.Ui.AvaloniaUI.ColorSelector) |
| [`EsUtil.Ui.AvaloniaUI.Loupe`](EsUtil.Ui.AvaloniaUI/Loupe/README.md) | ルーペ（拡大鏡） | [![NuGet](https://img.shields.io/nuget/v/EsUtil.Ui.AvaloniaUI.Loupe.svg)](https://www.nuget.org/packages/EsUtil.Ui.AvaloniaUI.Loupe) |
| [`EsUtil.Ui.AvaloniaUI.Marquee`](EsUtil.Ui.AvaloniaUI/Marquee/README.md) | マーキー（ドラッグ矩形） | [![NuGet](https://img.shields.io/nuget/v/EsUtil.Ui.AvaloniaUI.Marquee.svg)](https://www.nuget.org/packages/EsUtil.Ui.AvaloniaUI.Marquee) |
| [`EsUtil.Ui.AvaloniaUI.Thumbnail`](EsUtil.Ui.AvaloniaUI/Thumbnail/README.md) | サムネイル一覧 | [![NuGet](https://img.shields.io/nuget/v/EsUtil.Ui.AvaloniaUI.Thumbnail.svg)](https://www.nuget.org/packages/EsUtil.Ui.AvaloniaUI.Thumbnail) |
| [`EsUtil.Ui.AvaloniaUI.ColumnPacking`](EsUtil.Ui.AvaloniaUI/ColumnPacking/README.md) | 複数列の配置（パネル） | [![NuGet](https://img.shields.io/nuget/v/EsUtil.Ui.AvaloniaUI.ColumnPacking.svg)](https://www.nuget.org/packages/EsUtil.Ui.AvaloniaUI.ColumnPacking) |

### 1.3 `EsUtil.Ui.MewUI`（MewUI 版）

ソリューション: [`EsUtil.Ui.MewUI.slnx`](EsUtil.Ui.MewUI/EsUtil.Ui.MewUI.slnx)

| プロジェクト | 内容 | 版 |
| --- | --- | --- |
| [`EsUtil.Ui.MewUI.Interop`](EsUtil.Ui.MewUI/Interop/README.md) | MewUI の型とコアの型の相互変換・描画の補助 | [![NuGet](https://img.shields.io/nuget/v/EsUtil.Ui.MewUI.Interop.svg)](https://www.nuget.org/packages/EsUtil.Ui.MewUI.Interop) |
| [`EsUtil.Ui.MewUI.ColorSelector`](EsUtil.Ui.MewUI/ColorSelector/README.md) | 色選択（色名・16 進・ピッカー） | [![NuGet](https://img.shields.io/nuget/v/EsUtil.Ui.MewUI.ColorSelector.svg)](https://www.nuget.org/packages/EsUtil.Ui.MewUI.ColorSelector) |
| [`EsUtil.Ui.MewUI.Loupe`](EsUtil.Ui.MewUI/Loupe/README.md) | ルーペ（拡大鏡） | [![NuGet](https://img.shields.io/nuget/v/EsUtil.Ui.MewUI.Loupe.svg)](https://www.nuget.org/packages/EsUtil.Ui.MewUI.Loupe) |
| [`EsUtil.Ui.MewUI.Marquee`](EsUtil.Ui.MewUI/Marquee/README.md) | マーキー（ドラッグ矩形） | [![NuGet](https://img.shields.io/nuget/v/EsUtil.Ui.MewUI.Marquee.svg)](https://www.nuget.org/packages/EsUtil.Ui.MewUI.Marquee) |
| [`EsUtil.Ui.MewUI.Thumbnail`](EsUtil.Ui.MewUI/Thumbnail/README.md) | サムネイル一覧 | [![NuGet](https://img.shields.io/nuget/v/EsUtil.Ui.MewUI.Thumbnail.svg)](https://www.nuget.org/packages/EsUtil.Ui.MewUI.Thumbnail) |
| [`EsUtil.Ui.MewUI.ColumnPacking`](EsUtil.Ui.MewUI/ColumnPacking/README.md) | 複数列の配置（パネル） | [![NuGet](https://img.shields.io/nuget/v/EsUtil.Ui.MewUI.ColumnPacking.svg)](https://www.nuget.org/packages/EsUtil.Ui.MewUI.ColumnPacking) |

### 1.4 `EsUtil.Ui.WinUI3`（WinUI 3 版）

ソリューション: [`EsUtil.Ui.WinUI3.slnx`](EsUtil.Ui.WinUI3/EsUtil.Ui.WinUI3.slnx)

| プロジェクト | 内容 | 版 |
| --- | --- | --- |
| [`EsUtil.Ui.WinUI3.Interop`](EsUtil.Ui.WinUI3/Interop/README.md) | WinUI 3 の型とコアの型の相互変換・描画の補助 | [![NuGet](https://img.shields.io/nuget/v/EsUtil.Ui.WinUI3.Interop.svg)](https://www.nuget.org/packages/EsUtil.Ui.WinUI3.Interop) |
| [`EsUtil.Ui.WinUI3.ColorSelector`](EsUtil.Ui.WinUI3/ColorSelector/README.md) | 色選択（色名・16 進・ピッカー） | [![NuGet](https://img.shields.io/nuget/v/EsUtil.Ui.WinUI3.ColorSelector.svg)](https://www.nuget.org/packages/EsUtil.Ui.WinUI3.ColorSelector) |
| [`EsUtil.Ui.WinUI3.Loupe`](EsUtil.Ui.WinUI3/Loupe/README.md) | ルーペ（拡大鏡） | [![NuGet](https://img.shields.io/nuget/v/EsUtil.Ui.WinUI3.Loupe.svg)](https://www.nuget.org/packages/EsUtil.Ui.WinUI3.Loupe) |
| [`EsUtil.Ui.WinUI3.Marquee`](EsUtil.Ui.WinUI3/Marquee/README.md) | マーキー（ドラッグ矩形） | [![NuGet](https://img.shields.io/nuget/v/EsUtil.Ui.WinUI3.Marquee.svg)](https://www.nuget.org/packages/EsUtil.Ui.WinUI3.Marquee) |
| [`EsUtil.Ui.WinUI3.Thumbnail`](EsUtil.Ui.WinUI3/Thumbnail/README.md) | サムネイル一覧 | [![NuGet](https://img.shields.io/nuget/v/EsUtil.Ui.WinUI3.Thumbnail.svg)](https://www.nuget.org/packages/EsUtil.Ui.WinUI3.Thumbnail) |
| [`EsUtil.Ui.WinUI3.ColumnPacking`](EsUtil.Ui.WinUI3/ColumnPacking/README.md) | 複数列の配置（パネル） | [![NuGet](https://img.shields.io/nuget/v/EsUtil.Ui.WinUI3.ColumnPacking.svg)](https://www.nuget.org/packages/EsUtil.Ui.WinUI3.ColumnPacking) |

### 1.5 `EsUtil.Ui.WinForms`（WinForms 版）

ソリューション: [`EsUtil.Ui.WinForms.slnx`](EsUtil.Ui.WinForms/EsUtil.Ui.WinForms.slnx)

| プロジェクト | 内容 | 版 |
| --- | --- | --- |
| [`EsUtil.Ui.WinForms.Interop`](EsUtil.Ui.WinForms/Interop/README.md) | WinForms の型とコアの型の相互変換・描画の補助 | [![NuGet](https://img.shields.io/nuget/v/EsUtil.Ui.WinForms.Interop.svg)](https://www.nuget.org/packages/EsUtil.Ui.WinForms.Interop) |
| [`EsUtil.Ui.WinForms.Loupe`](EsUtil.Ui.WinForms/Loupe/README.md) | ルーペ（拡大鏡） | [![NuGet](https://img.shields.io/nuget/v/EsUtil.Ui.WinForms.Loupe.svg)](https://www.nuget.org/packages/EsUtil.Ui.WinForms.Loupe) |
| [`EsUtil.Ui.WinForms.Marquee`](EsUtil.Ui.WinForms/Marquee/README.md) | マーキー（ドラッグ矩形） | [![NuGet](https://img.shields.io/nuget/v/EsUtil.Ui.WinForms.Marquee.svg)](https://www.nuget.org/packages/EsUtil.Ui.WinForms.Marquee) |
| [`EsUtil.Ui.WinForms.Thumbnail`](EsUtil.Ui.WinForms/Thumbnail/README.md) | サムネイル一覧 | [![NuGet](https://img.shields.io/nuget/v/EsUtil.Ui.WinForms.Thumbnail.svg)](https://www.nuget.org/packages/EsUtil.Ui.WinForms.Thumbnail) |

色選択（`ColorSelector`）は **WinForms 版だけありません**（WinForms には相当するコントロールが無く、作る予定もありません）。

## 2. ドキュメント

| 知りたいこと | 所有者 |
| --- | --- |
| 規約の索引と本リポジトリ固有の事実 | [`.github/copilot-instructions.md`](.github/copilot-instructions.md) |
| 参照の許可範囲（層構造）と依存関係 | [`.github/依存関係.md`](.github/依存関係.md) |
| ビルド・テスト・pack・検証の進め方 | [`.github/開発手順.md`](.github/開発手順.md) |
| 検証の手順と合否基準 | [`.github/検証手順.md`](.github/検証手順.md) |
| リリースの手順（公開・タグ・SBOM・失敗時の復旧） | [`.github/RELEASE.md`](.github/RELEASE.md) |
| 変更履歴 | [`CHANGELOG.md`](CHANGELOG.md) |
| 各プロジェクトの仕様 | `<ソリューション フォルダ>/<プロジェクト フォルダ>/docs/SPEC_API.md` / `SPEC_System.md`（例: `EsUtil.Ui.AvaloniaUI/Loupe/docs/SPEC_API.md`） |

## 3. 対応フレームワークと Native AOT

**対象フレームワークと、実行に必要な Windows の下限**です（値の所有者は各 `.csproj` の `<TargetFramework>` / `<SupportedOSPlatformVersion>`。 パッケージの `README.md` も同じ値を書きます）。 **本リポジトリの動作 OS は Windows だけです**（すべてのパッケージが Windows 向けの対象フレームワークです）。

| 区分 | 対象フレームワーク | 対象プロジェクト |
| --- | --- | --- |
| 共通基盤 | `net10.0-windows10.0.17763.0` | `EsUtil.Platform.Core` / `EsUtil.Platform.Core.Windows` |
| 機能層 | `net10.0-windows10.0.17763.0` | `EsUtil.Feature.*` |
| Avalonia UI 層 | `net10.0-windows10.0.17763.0` | `EsUtil.Ui.AvaloniaUI.*` |
| MewUI 層 | `net10.0-windows10.0.17763.0` | `EsUtil.Ui.MewUI.*` |
| WinUI 3 層 | `net10.0-windows10.0.22621.0`（`SupportedOSPlatformVersion` = `10.0.19041.0`） | `EsUtil.Ui.WinUI3.*` |
| WinForms 層 | `net10.0-windows10.0.17763.0` | `EsUtil.Ui.WinForms.*` |

| 区分 | Windows の下限 |
| --- | --- |
| 共通基盤・機能層 | **Windows 10 バージョン 1809（ビルド 17763）以降** |
| Avalonia UI 層・MewUI 層・WinForms 層 | Windows 10 バージョン 1809（ビルド 17763）以降 |
| WinUI 3 層 | **Windows 10 バージョン 2004（ビルド 19041）以降**（Windows App Runtime 2.6 以降。 対象フレームワークの 22621 は API 面であり、実行の下限ではない。 ランタイムが無い場合は `EsUtil.Ui.WinUI3.Interop` の `WindowsAppRuntimeInventory` で判定し、`WindowsAppRuntimeBootstrap` で導入できる） |

- **検証は Windows でだけ行っています**。 対象フレームワークが Windows のため、**Windows 以外では動作しません**（実行時にも Win32 / WIC を呼びます）。

### 3.1 依存パッケージの下限（.NET 10）

**公開パッケージの依存下限は .NET 10 です**。 利用側に要求する .NET の版は、対象フレームワーク（`net10.0-windows10.0.17763.0`）と nuspec の依存が決めます。

| 依存 | nuspec に書かれる下限 | 理由 |
| --- | --- | --- |
| `Microsoft.Extensions.Logging.Abstractions` / `Microsoft.Extensions.Logging` | **10.0.0** | 本リポジトリが対象とする `net10.0` で動く最小の版。 上限にすると .NET 10 の利用者へ新しい版を強制する |
| `System.Drawing.Common` | **10.0.0** | 同上（本リポジトリは色の値型だけを使う） |

- **`PackageReference` の版は nuspec の依存下限になります**（範囲指定にしないため。 [`ビルド設定規約.md`](.github/ビルド設定規約.md) §4）。 したがって**ビルドに使う版も 10.0.0** にしています。 .NET 10 より新しい版へ上げると、**.NET 10 の利用者が `NU1605`（ダウングレード）で復元できなくなります**（機能上は新しい版でも動きますが、要求する版を上げない方針です）。
- **利用側が新しい版を参照しても問題ありません**。 NuGet の統合により、利用側の版（例: .NET 11 のアプリが参照する 11.0.0）で解決されます。 実測 2026-11-13: `.NET 10` / `.NET 11` の両ランタイムで、ログ出力と `System.Drawing` の利用が動作しました。
- **ビルドには .NET 11 SDK を使います**（[`global.json`](global.json) が単一所有。 対象フレームワークは `net10.0` のまま）。 利用者の実行環境に要求するのは上の 2 つだけであり、**SDK の版は公開物に影響しません**。

### 3.2 Native AOT

| 層 | Native AOT | 根拠（実測） |
| --- | --- | --- |
| 共通基盤・機能層 | **互換** | Win32 / WIC の呼び出しを **CsWin32 の生成物**（関数ポインター）だけで行う |
| Avalonia UI 層 | **互換** | サンプルを AOT で発行し、起動を確認 |
| MewUI 層 | **互換** | 同上 |
| WinUI 3 層 | **互換** | 同上（Windows App SDK 2.6 以降が Native AOT に対応。 2026-11-05 にサンプルを `-p:PublishAot=true -r win-x64` で発行して確認） |
| WinForms 層 | **非対応** | **Windows Forms 自体がトリミング/AOT に対応しない**（`PublishAot` は `NETSDK1175` で失敗する） |

- **言語**: C# 14 / .NET 10。
- **プラットフォーム**: Windows のみ（すべてのパッケージが Windows 向けの対象フレームワーク。 `EsUtil.Platform.Core/src/Native/Win` 配下が Win32 / WIC を呼びます）。 `EsUtil.Platform.Core.Native` の I/F は**差し替え（DIP）とテストのため**に残しており、他の OS 向け実装を提供するものではありません。

## 4. ライセンス

[MIT License](LICENSE)
