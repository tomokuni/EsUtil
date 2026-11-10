# EsUtil.Ui.WinUI3.Interop API リファレンス

WinUI 3 と EsUtil の型の相互変換が提供する公開 API です。 使い方は [../README.md](../README.md)、構造と設計判断は [SPEC_System.md](SPEC_System.md) を参照してください。

## 目次 <!-- omit from toc -->

- [1. 前提条件](#1-前提条件)
- [2. 公開 API](#2-公開-api)
  - [2.1 ルート名前空間 `EsUtil.Ui.WinUI3.Interop`](#21-ルート名前空間-esutiluiwinui3interop)
  - [2.2 部分名前空間](#22-部分名前空間)
- [3. 制限事項](#3-制限事項)
- [4. 例外](#4-例外)

## 1. 前提条件

- 対象フレームワークは `net10.0-windows10.0.22621.0` です（バージョンの範囲はプロジェクト ファイルが持つ）。
- UI フレームワーク非依存の値型（`EsKey` / `EsPoint` / `EsRect` / `EsReadOnlyBitmap` など）は `EsUtil.Platform.Core` が提供します。
- 変換は**拡張メソッド**として提供します（`using EsUtil.Ui.WinUI3.Interop;` で使えます）。
- 単位は **DIP** です（物理ピクセルを扱う API は名前で区別します）。

## 2. 公開 API

### 2.1 ルート名前空間 `EsUtil.Ui.WinUI3.Interop`

| 型 | 内容 |
| --- | --- |
| `EsKeyInterop` | `EsKey` ↔ `Windows.System.VirtualKey` |
| `EsGeometryInterop` | `EsPoint` / `EsSize` / `EsRect` と整数版 ↔ WinRT の幾何型 |
| `EsWindowStateInterop` | `EsWindowState` ↔ WinUI 3 のウィンドウ状態 |
| `EsPlatformHandleInterop` | `EsPlatformHandle` ↔ WinUI 3 のウィンドウ ハンドル |
| `OverlayWindowPlacer` | オーバーレイ ウィンドウを対象の矩形へ配置する（枠の計測先 `IWindowFrameMetrics` を差し替えられる） |
| `EsWindowPlacementMath`（コア） | 設定の物理ピクセル ↔ WinUI 3 の DIP の換算 |

### 2.2 部分名前空間

| 型 | 内容 |
| --- | --- |
| `Imaging.TextMeasure` | 文字列の実寸（WinUI 3 のテキスト計測） |
| `Drawing.SolidBrushCache` | ARGB ごとに `SolidColorBrush` を使い回す（図形の層が毎フレーム数十個の矩形へ色を設定するため。 UI スレッド専用） |
| `Drawing.CompositionImageView` | コアの画像を Composition で描く表示ビュー。 `WaitForSourceReadyAsync()` は**サーフェスの読み込み完了**を待つ（`LoadedImageSurface` は段階的に復号するため、完了前に表示すると縮小された内容が見える） |
| `Drawing.RectShapeLayer` | 矩形（角丸を含む）を Composition の図形として描く |
| `Drawing.DrawList` / `DrawRect` / `DrawRoundRect` | 描く図形の一覧（UI 非依存の値） |
| `Drawing.BmpStreamBuilder` | BMP を組み立てる（`Write` はファイル全体の配列を作らずにストリームへ書く） |
| `Bootstrapping.WindowsAppRuntimeBootstrap` | Windows App Runtime の初期化と導入（Bootstrapper API）。 版は参照している `Microsoft.WindowsAppSDK` が決める |
| `Bootstrapping.WindowsAppRuntimeInventory` | インストール済みの版（`GetInstalledVersions` は新しい順、`TryGetInstalledVersion` は最新。 このプロセスのアーキテクチャのみ）と `RequiredVersion`（必要とする版）を返す。 `IsRequiredVersionInstalled` で導入の有無を判定する。 **`TryGetLoadedVersion` は「実際に読み込んでいる版」**を返す（インストール済みの一覧とは別。 食い違うと描画の不具合になる） |
| `Bootstrapping.WindowsAppRuntimeBootstrapOptions` / `WindowsAppRuntimeInstallMode` | 導入の方法（`Download`（既定）/ `BootstrapUi`（案内 UI）/ `None`）・取得先・制限時間・案内 UI の差し替え（`Prompt`）・**要求する版（`RequiredVersion`）** |
| `Bootstrapping.WindowsAppRuntimeBootstrap` の版の決定 | `GetRequiredVersion` が「アプリが指定した版」と「本アセンブリの下限」の**高い方**を返す。 `IsRequiredVersionSpecified` で**指定の有無**を判定できる（未指定は本アセンブリの版にフォールバックするため、アプリは検出して警告を記録する） |
| `Bootstrapping.WindowsAppRuntimeInstallPrompt` / `WindowsAppRuntimeInstallPromptRequest` / `WindowsAppRuntimeInstallChoice` | 案内 UI の契約（必要とする版・インストール済みの版・ダウンロード ページを受け取り、自動インストール / ページの表示 / 終了 を返す） |

## 3. 制限事項

- **描画は Composition を使います**。
- **ICO は WIC のエンコーダーで作れません**（WIC に ICO のエンコーダーが無い）。 `BmpStreamBuilder` が BMP を組み立て、複数サイズを 1 ファイルへ詰めます。
- **ランタイムの導入には通信を使います**（`WindowsAppRuntimeInstallMode.Download`）。 通信を使わない場合は `BootstrapUi`（案内 UI を出す）か `None`（初期化だけを試す）を選びます。
- **`BootstrapUi` の案内は 3 択です**（自動インストール / ダウンロードページを表示 / 終了）。 案内には**ダウンロード ページの URL** と、インストール済みの版を表示します。 **URL はクリックするとブラウザーで開きます**（「ダウンロードページを表示」と同じ動作。 ダイアログは閉じません）。 「ダウンロードページを表示」を選ぶとページを開いて**もう一度案内を出します**（手動で導入したあと選び直せます）。
- **案内はタスク ダイアログで表示します**（ComCtl32 v6 が必要。 アプリの `app.manifest` で `Microsoft.Windows.Common-Controls` を宣言してください）。 宣言が無いアプリでは**メッセージ ボックス**へ落ち、本文に同じ 3 択（はい / いいえ / キャンセル）を表示します。 自前の UI を使う場合は `WindowsAppRuntimeBootstrapOptions.Prompt` を設定します。
- **導入を始める前に状態を調べられます**（`WindowsAppRuntimeInventory`）。 利用者へ確認してから導入する場合は、`IsRequiredVersionInstalled` が false のときだけ `TryInstall` / `TryInstallAsync` を呼びます（`TryEnsureAvailable` は `Download` のとき確認なしに、`BootstrapUi` のとき案内の選択に従って導入します）。
- **`GetInstalledVersions` はパッケージ ID を持たないアプリからも呼べます**が、`Windows.Management.Deployment.PackageManager` を使える環境に限ります（ポリシーで禁止されている場合は空を返します）。
- **ランタイムの版は既定では参照しているパッケージが決めます**（本アセンブリは `2.6` 以降の `Microsoft.WindowsAppSDK` を参照します。 必要とする版は `WindowsAppRuntimeInventory.RequiredVersion`）。
- **アプリが本アセンブリより新しい `Microsoft.WindowsAppSDK` でビルドする場合は `WindowsAppRuntimeBootstrapOptions.RequiredVersion` を必ず指定します**。 本アセンブリへ焼き込まれた版（ビルド時のパッケージの版）はアプリ側の版に追随しないため、指定しないと**アプリのマネージド アセンブリより古いネイティブ ランタイムで初期化が成功**します（実測 2026-11-10: 2.6.5 でビルドしたアプリが 2.6.4 のランタイムで初期化を通り、文字のグリフだけが描かれない。 レイアウト・画像・アイコン フォントは正常であった）。 指定した版は Bootstrapper の最小版・インストーラーの取得先・案内の表示へ一貫して使われます。

## 4. 例外

- 引数の null は `ArgumentNullException`。
- 未定義のキーは `None` を返します（例外にしません）。
- **ランタイムを用意できなかった場合は例外にせず `false`** を返します（通信の遮断・ポリシー・時間切れ）。
