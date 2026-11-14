# EsUtil.Ui.WinUI3.Interop

![NuGet](https://img.shields.io/nuget/v/EsUtil.Ui.WinUI3.Interop.svg)
![Release](https://github.com/tomokuni/EsUtil/actions/workflows/release.yml/badge.svg)
![Build](https://github.com/tomokuni/EsUtil/actions/workflows/build.yml/badge.svg?branch=main)
![Platform](https://img.shields.io/badge/Platform-Windows%20(20348)%2B-0078D6)
![.NET](https://img.shields.io/badge/.NET-10.0-512BD4)
![AOT](https://img.shields.io/badge/AOT-Ready-00695C)
![WinUI3](https://img.shields.io/badge/UI-WinUI%203-0078D4)
![License](https://img.shields.io/badge/License-MIT-3DA639)

**対応 OS**: Windows Server 2022 / Windows 11（ビルド 20348）以降
**Windows App Runtime**: 2.0.1 以降（無い場合は Bootstrapper で導入できます。 §3）

## 1. 概要

WinUI 3 と EsUtil の型（キー・幾何・色・ウィンドウ状態・画像）を相互変換する**ブリッジ**です。
UI フレームワーク非依存の値型と抽象は `EsUtil.Platform.Core` が、OS の操作はその Native 実装が担います。
本アセンブリは **UI 側の型との境界だけ**を持ちます（変換の規則と UI 固有の描画）。

## 2. 主な API

| 何 | 型 |
| --- | --- |
| `EsKeyInterop` | `EsKey` ↔ `Windows.System.VirtualKey`（名前で解決し、未定義値は `None`） |
| `EsGeometryInterop` | `EsPoint` / `EsSize` / `EsRect` と整数版 ↔ WinRT の幾何型 |
| `EsWindowStateInterop` | `EsWindowState` ↔ WinUI 3 のウィンドウ状態 |
| `EsPlatformHandleInterop` | `EsPlatformHandle` ↔ WinUI 3 のウィンドウ ハンドル |
| `OverlayWindowPlacer` | 撮影用オーバーレイ ウィンドウを対象の矩形へ配置する |
| `EsWindowPlacementMath`（コア） | 設定の物理ピクセル ↔ WinUI 3 の DIP の換算 |
| `Imaging.TextMeasure` | WinUI 3 のテキスト計測で実寸を測る |
| `Drawing.CompositionImageView` | コアの画像を Composition で描く表示ビュー |
| `Drawing.RectShapeLayer` | 矩形（角丸を含む）を Composition の図形として描く |
| `Drawing.DrawList` / `DrawRect` / `DrawRoundRect` | 描く図形の一覧（UI 非依存の値） |
| `Drawing.BmpStreamBuilder` | BMP を組み立てる（`Write` はファイル全体の配列を作らずにストリームへ書く） |
| `Bootstrapping.WindowsAppRuntimeBootstrap` | Windows App Runtime を用意する（無ければ公式インストーラーで導入し、Bootstrapper で初期化する） |
| `Bootstrapping.WindowsAppRuntimeInventory` | インストール済みの Windows App Runtime の版と、EsUtil が必要とする版（`RequiredVersion`）を調べる |
| `Bootstrapping.WindowsAppRuntimeInstallPrompt` ほか | 案内 UI の差し替え（`WindowsAppRuntimeBootstrapOptions.Prompt`）に使う契約（選択肢は自動インストール / ダウンロードページを表示 / 終了） |

一覧は [`docs/SPEC_API.md`](docs/SPEC_API.md) を参照してください。

## 3. 使い方

```csharp
var view = new CompositionImageView();
view.Source = EsReadOnlyBitmap.Load(path);
var key = Windows.System.VirtualKey.A.ToEsKey();
```

### 3.1 Windows App Runtime が無い環境で起動する

**前提**: 既定のアンパッケージ アプリは、Windows App SDK の**自動初期化**（モジュール読み込み時に動く Bootstrapper）が `Main` より先に走ります。 ランタイムが無い PC では**そこへ到達する前に失敗する**ため、本クラスへ導入を任せる場合は `.csproj` で自動初期化を止めてください。

```xml
<!-- 導入の判断を EsUtil.Ui.WinUI3.Interop の Bootstrapper へ任せる（既定の自動初期化を止める） -->
<WindowsAppSdkBootstrapInitialize>false</WindowsAppSdkBootstrapInitialize>
```

そのうえで、**WinUI の型へ触れる前**（`Application` の生成より前）に 1 回呼びます。

```csharp
using EsUtil.Ui.WinUI3.Interop.Bootstrapping;

// 無ければ公式インストーラーを取得して黙って導入し、Bootstrapper で初期化する（既定）
if (!await WindowsAppRuntimeBootstrap.TryEnsureAvailableAsync())
{
    // 導入できなかった（通信の遮断・ポリシー等）。 利用者へ案内する
}

// 終了時（実際に初期化したときだけ解除される。 自己完結の配置では何もしない）
WindowsAppRuntimeBootstrap.Shutdown();
```

- **導入の方法**は `WindowsAppRuntimeBootstrapOptions.InstallMode` で選びます。
  - `Download`（既定） … 案内を出さずに、公式のインストーラーを取得して黙って実行します。
  - `BootstrapUi` … **案内 UI を出し、選択に従います**（§3.3）。
  - `None` … 初期化だけを試します。
- **版**は参照している `Microsoft.WindowsAppSDK` のパッケージが決めます（ライブラリ側に版を書きません）。
- **自己完結の配置**（`WindowsAppSDKSelfContained=true`）と **MSIX** では初期化は不要であり、呼んでも成功します（`Shutdown` は何もしません）。
- 自動初期化を**止めない**場合（既定）は、本クラスを呼ぶ必要はありません（`Main` より前に SDK が初期化します）。

### 3.2 版を調べてから、利用者へ確認してインストールする

`WindowsAppRuntimeInventory` で状態を調べ、必要だと分かってから導入します（確認なしにインストールを始めません）。

```csharp
using EsUtil.Ui.WinUI3.Interop.Bootstrapping;

var required = WindowsAppRuntimeInventory.RequiredVersion;                  // 例: 2.6.4
var installed = WindowsAppRuntimeInventory.GetInstalledVersions();          // 新しい順（このプロセスのアーキテクチャのみ）
if (WindowsAppRuntimeInventory.TryGetInstalledVersion(out var latest))     // 最新の 1 件（1 件も無ければ false）
{
    ShowInfo($"Windows App Runtime: {latest}（必要: {required} 以降）");    // MessageBox 等（アプリの実装）
}

if (!WindowsAppRuntimeInventory.IsRequiredVersionInstalled()
    && ShowConfirm($"Windows App Runtime {required} 以降が必要です。インストールしますか？")) // MessageBox 等（アプリの実装）
{
    await WindowsAppRuntimeBootstrap.TryInstallAsync();                     // 導入だけを行う
}

// 導入後に初期化する（TryEnsureAvailable は「初期化 → 必要なら導入 → 初期化」を 1 回で行う）
WindowsAppRuntimeBootstrap.TryInitialize(out _);
```

- `GetInstalledVersions()` は**パッケージ ID を持たないアプリからも**呼べます（`Windows.Management.Deployment.PackageManager` を使う）。
- 調べられないとき（ポリシー・COM の初期化失敗）は**空**を返します（例外にしません）。
- `IsRequiredVersionInstalled` が false でも、**自己完結の配置と MSIX** は動作します（判断は `WindowsAppRuntimeBootstrap.IsInitialized` と組み合わせます）。

### 3.3 案内 UI を出して、選択に従わせる（`InstallMode.BootstrapUi`）

`InstallMode = BootstrapUi` にすると、ランタイムが見つからないときに**次の案内**を出し、選ばれたとおりに処理します。

```text
Windows App Runtime 2.0.1 以降が必要です。          ← 主文
このアプリは WinUI 3 を使うため、Windows App Runtime が必要です。
インストール済みの版: 2.5.1.0
ダウンロード ページ（インストーラー）:
https://learn.microsoft.com/windows/apps/windows-app-sdk/downloads

[自動インストールする] [ダウンロードページを表示する] [インストールせずにアプリを終了する]
```

| 選択肢 | 動作 |
| --- | --- |
| 自動インストールする | 公式のインストーラーを取得して黙って実行し、初期化し直す（`WindowsAppRuntimeBootstrapOptions.InstallerSource` で取得先を差し替え可） |
| ダウンロードページを表示する | ダウンロード ページを既定のブラウザーで開き、**もう一度案内を出す**（手動で導入したあと選び直せる） |
| インストールせずにアプリを終了する | 何もせずに `false` を返す（アプリは終了する） |

本文の URL は**クリックするとブラウザーで開きます**（「ダウンロードページを表示する」と同じ動作。 ダイアログは閉じません）。

```csharp
var options = WindowsAppRuntimeBootstrapOptions.Default with
{
    InstallMode = WindowsAppRuntimeInstallMode.BootstrapUi,
};

if (!await WindowsAppRuntimeBootstrap.TryEnsureAvailableAsync(options))
{
    return; // アプリを終了する
}
```

- **案内の実装**は `WindowsAppRuntimeBootstrapOptions.Prompt` で差し替えられます（自前の WinUI ダイアログを使う場合）。
  既定は Win32 のタスク ダイアログです。
- **タスク ダイアログ**は ComCtl32 v6 で動くため、アプリの `app.manifest` で **`Microsoft.Windows.Common-Controls` を宣言**してください。
  宣言が無いアプリでは**メッセージ ボックスへ落ち**、本文に同じ 3 択（はい / いいえ / キャンセル）を表示します。
  宣言の例は `EsUtil.Ui.WinUI3/*/sample/app.manifest` にあります。

## 4. 依存

`EsUtil.Platform.Core` と `Microsoft.WindowsAppSDK` を参照します。 規則は [`.github/依存関係.md`](../../.github/依存関係.md)。

- 描画は Composition を使います。

## 5. テスト

`tests/`（xUnit v3）。 変換の往復・未定義値の扱い・配置の計算・描画の計画を検証します。
検証の手順は [`.github/検証手順.md`](../../.github/検証手順.md)。

## 6. ドキュメント

- API リファレンス … [`docs/SPEC_API.md`](docs/SPEC_API.md)
- システム仕様書 … [`docs/SPEC_System.md`](docs/SPEC_System.md)
- 層の帰属 … [`../../EsUtil/README.md`](../../EsUtil/README.md)

## 7. ライセンス

[MIT License](LICENSE)
