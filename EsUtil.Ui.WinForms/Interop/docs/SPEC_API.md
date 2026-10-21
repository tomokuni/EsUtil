# EsUtil.Ui.WinForms.Interop API リファレンス

Windows Forms と EsUtil の型の相互変換が提供する公開 API です。 使い方は [../README.md](../README.md)、構造と設計判断は [SPEC_System.md](SPEC_System.md) を参照してください。

## 目次 <!-- omit from toc -->

- [1. 前提条件](#1-前提条件)
- [2. 公開 API](#2-公開-api)
  - [2.1 ルート名前空間 `EsUtil.Ui.WinForms.Interop`](#21-ルート名前空間-esutiluiwinformsinterop)
  - [2.2 部分名前空間](#22-部分名前空間)
- [3. 制限事項](#3-制限事項)
- [4. 例外](#4-例外)

## 1. 前提条件

- 対象フレームワークは `net10.0-windows` です（バージョンの範囲はプロジェクト ファイルが持つ）。
- UI フレームワーク非依存の値型（`EsKey` / `EsPoint` / `EsRect` / `EsReadOnlyBitmap` など）は `EsUtil.Platform.Core` が提供します。
- 変換は**拡張メソッド**として提供します（`using EsUtil.Ui.WinForms.Interop;` で使えます）。
- 単位は **DIP** です（物理ピクセルを扱う API は名前で区別します）。

## 2. 公開 API

### 2.1 ルート名前空間 `EsUtil.Ui.WinForms.Interop`

| 型 | 内容 |
| --- | --- |
| `EsKeyInterop` | `EsKey` ↔ `System.Windows.Forms.Keys` |
| `EsGeometryInterop` | `EsPoint` / `EsSize` / `EsRect` と整数版 ↔ `System.Drawing` の幾何型 |
| `EsWindowStateInterop` | `EsWindowState` ↔ `FormWindowState` |
| `EsPlatformHandleInterop` | `EsPlatformHandle` ↔ WinForms のウィンドウ ハンドル |
| `MonitorDpi` | モニター単位の DPI（`GetDpiForMonitor`） |
| `WindowPlacementMath` | 設定の物理ピクセル ↔ WinForms の DIP の換算 |

### 2.2 部分名前空間

| 型 | 内容 |
| --- | --- |
| `Imaging.EsBitmapDrawingInterop` | コアの画像 ↔ `System.Drawing.Bitmap` |
| `Imaging.ImageView` | 画像を描く表示ビュー（`Control` + `OnPaint`） |
| `Imaging.ImageRenderer` | コアの画像を GDI+ で描く |
| `Imaging.GdiFill` / `GdiText` / `GdiLabel` | GDI+ の塗り・文字・ラベル |
| `Imaging.ImageDrawMath` | 配置の矩形の純粋計算 |
| `Imaging.ImageDrawMode` | 配置の仕方（`Fit` / `Fill` など） |
| `Imaging.ClientPixelScale` | 実効倍率（物理ピクセルと DIP の比） |

## 3. 制限事項

- **描画は GDI+ の標準 API を使います**（Skia を使いません）。
- **モニター単位の DPI は WinForms の公開 API に無い**ため、`GetDpiForMonitor` を直接呼びます。

## 4. 例外

- 引数の null は `ArgumentNullException`。
- 未定義のキーは `None` を返します（例外にしません）。
