# EsUtil.Ui.AvaloniaUI.Interop API リファレンス

Avalonia UI と EsUtil の型の相互変換が提供する公開 API です。 使い方は [../README.md](../README.md)、構造と設計判断は [SPEC_System.md](SPEC_System.md) を参照してください。

## 目次 <!-- omit from toc -->

- [1. 前提条件](#1-前提条件)
- [2. 公開 API](#2-公開-api)
  - [2.1 ルート名前空間 `EsUtil.Ui.AvaloniaUI.Interop`](#21-ルート名前空間-esutiluiavaloniauiinterop)
  - [2.2 部分名前空間](#22-部分名前空間)
- [3. 制限事項](#3-制限事項)
- [4. 例外](#4-例外)

## 1. 前提条件

- 対象フレームワークは `net10.0` です（バージョンの範囲はプロジェクト ファイルが持つ）。
- UI フレームワーク非依存の値型（`EsKey` / `EsPoint` / `EsRect` / `EsReadOnlyBitmap` など）は `EsUtil.Platform.Core` が提供します。
- 変換は**拡張メソッド**として提供します（`using EsUtil.Ui.AvaloniaUI.Interop;` で使えます）。
- 単位は **DIP** です（物理ピクセルを扱う API は名前で区別します）。

## 2. 公開 API

### 2.1 ルート名前空間 `EsUtil.Ui.AvaloniaUI.Interop`

| 型 | 内容 |
| --- | --- |
| `EsKeyInterop` | `EsKey` ↔ `Avalonia.Input.Key`（`ToAvaloniaKey` / `ToEsKey`） |
| `EsGeometryInterop` | `EsPoint` / `EsSize` / `EsVector` / `EsRect` と整数版 ↔ Avalonia の幾何型 |
| `EsWindowStateInterop` | `EsWindowState` ↔ `Avalonia.Controls.WindowState` |
| `EsPlatformHandleInterop` | `EsPlatformHandle` ↔ Avalonia のプラットフォーム ハンドル |
| `ColorInterop` | `System.Drawing.Color` ↔ `Avalonia.Media.Color`（`ToAvaloniaColor` / `ToDrawingColor`） |
| `ColorExtensions` | `Color.FromAColor(alpha, baseColor)`（C# 14 の静的拡張メンバー） |
| `CursorOverrideGuardExtensions` | `Attach(this ICursorOverrideGuard, TopLevel)` |

### 2.2 部分名前空間

| 型 | 内容 |
| --- | --- |
| `Coord.ScreenConverter` | コントロール ローカルの DIP ↔ 画面物理ピクセル |
| `Imaging.EsBitmapAdapter` | コアの画像を `IImage` として描く（`Draw` で任意の `DrawingContext` へ描ける） |
| `Imaging.EsBitmapExtensions` | `ToAvaloniaBitmap` / `ToWriteableBitmap` / `ToEsBitmap` |
| `Imaging.SolidColorBrushCache` | `GetOrCreate`（色 → `SolidColorBrush`。 容量制限付き） |
| `Imaging.TextMeasure` | 文字列の実寸（Avalonia のテキスト計測） |

## 3. 制限事項

- **描画にはアプリ側の描画エンジンが要ります**（`Avalonia.Skia`）。 本アセンブリは `Avalonia` 本体だけを参照します。
- **変換は値の一致だけを保証します**（`EsKey` の別名は代表名へ正規化されるため、Avalonia 側の元の名前は復元できません）。

## 4. 例外

- 引数の null は `ArgumentNullException`。
- 未定義のキーは `None` を返します（例外にしません）。
- 画像として解釈できないデータは、コアの変換が例外を投げます（本アセンブリは握りつぶしません）。
