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
| `OverlayWindowPlacer` | オーバーレイ ウィンドウを対象の矩形へ配置する |
| `EsWindowPlacementMath`（コア） | 設定の物理ピクセル ↔ WinUI 3 の DIP の換算 |

### 2.2 部分名前空間

| 型 | 内容 |
| --- | --- |
| `Imaging.TextMeasure` | 文字列の実寸（WinUI 3 のテキスト計測） |
| `Drawing.CompositionImageView` | コアの画像を Composition で描く表示ビュー。 `WaitForSourceReadyAsync()` は**サーフェスの読み込み完了**を待つ（`LoadedImageSurface` は段階的に復号するため、完了前に表示すると縮小された内容が見える） |
| `Drawing.RectShapeLayer` | 矩形（角丸を含む）を Composition の図形として描く |
| `Drawing.DrawList` / `DrawRect` / `DrawRoundRect` | 描く図形の一覧（UI 非依存の値） |
| `Drawing.BmpStreamBuilder` | BMP をメモリ上で組み立てる（ICO の中身） |

## 3. 制限事項

- **描画は Composition を使います**（Skia を使いません）。
- **ICO は WIC のエンコーダーで作れません**（WIC に ICO のエンコーダーが無い）。 `BmpStreamBuilder` が BMP を組み立て、複数サイズを 1 ファイルへ詰めます。

## 4. 例外

- 引数の null は `ArgumentNullException`。
- 未定義のキーは `None` を返します（例外にしません）。
