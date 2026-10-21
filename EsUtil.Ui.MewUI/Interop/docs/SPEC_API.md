# EsUtil.Ui.MewUI.Interop API リファレンス

MewUI と EsUtil の型の相互変換が提供する公開 API です。 使い方は [../README.md](../README.md)、構造と設計判断は [SPEC_System.md](SPEC_System.md) を参照してください。

## 目次 <!-- omit from toc -->

- [1. 前提条件](#1-前提条件)
- [2. 公開 API](#2-公開-api)
  - [2.1 ルート名前空間 `EsUtil.Ui.MewUI.Interop`](#21-ルート名前空間-esutiluimewuiinterop)
  - [2.2 部分名前空間](#22-部分名前空間)
- [3. 制限事項](#3-制限事項)
- [4. 例外](#4-例外)

## 1. 前提条件

- 対象フレームワークは `net10.0` です（バージョンの範囲はプロジェクト ファイルが持つ）。
- UI フレームワーク非依存の値型（`EsKey` / `EsPoint` / `EsRect` / `EsReadOnlyBitmap` など）は `EsUtil.Platform.Core` が提供します。
- 変換は**拡張メソッド**として提供します（`using EsUtil.Ui.MewUI.Interop;` で使えます）。
- 単位は **DIP** です（物理ピクセルを扱う API は名前で区別します）。

## 2. 公開 API

### 2.1 ルート名前空間 `EsUtil.Ui.MewUI.Interop`

| 型 | 内容 |
| --- | --- |
| `EsKeyInterop` | `EsKey` ↔ `Aprillz.MewUI.Key` |
| `EsGeometryInterop` | `EsPoint` / `EsSize` / `EsRect` と整数版 ↔ MewUI の幾何型 |
| `EsWindowStateInterop` | `EsWindowState` ↔ MewUI のウィンドウ状態 |
| `EsPlatformHandleInterop` | `EsPlatformHandle` ↔ MewUI のウィンドウ ハンドル |
| `EsWindowPlacementMath`（コア） | 設定の物理ピクセル ↔ MewUI の DIP の換算 |

### 2.2 部分名前空間

| 型 | 内容 |
| --- | --- |
| `Imaging.ImageView` | 画像を描く表示ビュー（`Control` + `OnRender`） |
| `Imaging.ImageRenderer` | `EsReadOnlyBitmap` を描く層 |
| `Imaging.ImageRenderPlan` | 描画の計画（元矩形・先矩形。 internal） |
| `Imaging.ImageBinding` | コアの画像を `IImage` として保持する（internal） |
| `EsImageDrawMath`（コア） | 配置の矩形の純粋計算 |
| `EsImageDrawMode`（コア） | 配置の仕方（`Fill` / `Uniform`） |
| `Imaging.TextMeasure` | 文字列の実寸（MewUI のテキスト エンジン） |

## 3. 制限事項

- **テキスト計測はグラフィックス バックエンドの登録が前提です**（未登録では例外）。
- **GDI バックエンドの表面はアルファを保持しません**（実測）。 透過の確認はテストで行いません。

## 4. 例外

- 引数の null は `ArgumentNullException`。
- 未定義のキーは `None` を返します（例外にしません）。
- グラフィックス バックエンドが未登録の場合は、MewUI が投げる例外をそのまま伝えます。
