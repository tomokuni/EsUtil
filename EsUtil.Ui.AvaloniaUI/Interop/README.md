# EsUtil.Ui.AvaloniaUI.Interop

![NuGet](https://img.shields.io/nuget/v/EsUtil.Ui.AvaloniaUI.Interop.svg)
![Release](https://github.com/tomokuni/EsUtil/actions/workflows/release.yml/badge.svg)
![Build](https://github.com/tomokuni/EsUtil/actions/workflows/build.yml/badge.svg?branch=main)
![Platform](https://img.shields.io/badge/Platform-Portable-6E6E6E)
![.NET](https://img.shields.io/badge/.NET-10.0-512BD4)
![AOT](https://img.shields.io/badge/AOT-Ready-00695C)
![Avalonia](https://img.shields.io/badge/UI-Avalonia-8D46D6)
![License](https://img.shields.io/badge/License-MIT-3DA639)

**対応 OS**: 指定なし（`net10.0`）。 ただし **Windows 以外での動作は未検証**（UI フレームワークの対応 OS に従う。 参照する `EsUtil.Platform.Core` には Windows 専用の型が含まれる）

## 1. 概要

Avalonia UI と EsUtil の型（キー・幾何・色・ウィンドウ状態・画像）を相互変換する**ブリッジ**です。
UI フレームワーク非依存の値型と抽象は `EsUtil.Platform.Core` が、OS の操作はその Native 実装が担います。
本アセンブリは **UI 側の型との境界だけ**を持ちます（変換の規則と UI 固有の描画）。

## 2. 主な API

| 何 | 型 |
| --- | --- |
| `EsKeyInterop` | `EsKey` ↔ `Avalonia.Input.Key`（名前で解決し、未定義値は `None`） |
| `EsGeometryInterop` | `EsPoint` / `EsSize` / `EsVector` / `EsRect` と整数版 ↔ Avalonia の幾何型 |
| `EsWindowStateInterop` | `EsWindowState` ↔ `Avalonia.Controls.WindowState` |
| `EsPlatformHandleInterop` | `EsPlatformHandle` ↔ Avalonia のプラットフォーム ハンドル |
| `ColorInterop` | `System.Drawing.Color` ↔ `Avalonia.Media.Color` |
| `ColorExtensions` | 名前付き色と不透明度から `Color` を作る（C# 14 の静的拡張メンバー） |
| `CursorOverrideGuardExtensions` | `ICursorOverrideGuard` を `TopLevel` へ結び付ける |
| `Coord.ScreenConverter` | コントロール ローカルの DIP ↔ 画面物理ピクセル |
| `Imaging.EsBitmapAdapter` | コアの画像を Avalonia の `IImage` として描く |
| `Imaging.EsBitmapExtensions` | コアの画像 ↔ `Bitmap` / `WriteableBitmap` |
| `Imaging.SolidColorBrushCache` | 色 → `SolidColorBrush` の変換と容量管理 |
| `Imaging.TextMeasure` | Avalonia のテキスト計測で実寸を測る |

一覧は [`docs/SPEC_API.md`](docs/SPEC_API.md) を参照してください。

## 3. 使い方

```csharp
var a = new Avalonia.Point(10, 20).ToEsPoint();
var p = a.ToAvaloniaPoint();
var key = Avalonia.Input.Key.A.ToEsKey();
var w = Avalonia.Controls.WindowState.Normal.ToEsWindowState();
var brush = SolidColorBrushCache.GetOrCreate(System.Drawing.Color.Red);
```

## 4. 依存

`EsUtil.Platform.Core` と `Avalonia` を参照します。 規則は [`.github/依存関係.md`](../../.github/依存関係.md)。

- `Avalonia.Skia`（描画エンジン）はアプリが選びます。 本アセンブリは型の変換だけを行うため `Avalonia` 本体のみを参照します。

## 5. テスト

`tests/`（xUnit v3）。 変換の往復・未定義値の扱い・配置の計算・描画の計画を検証します。
検証の手順は [`.github/検証手順.md`](../../.github/検証手順.md)。

## 6. ドキュメント

- API リファレンス … [`docs/SPEC_API.md`](docs/SPEC_API.md)
- システム仕様書 … [`docs/SPEC_System.md`](docs/SPEC_System.md)
- 層の帰属 … [`../../EsUtil/README.md`](../../EsUtil/README.md)

## 7. ライセンス

[MIT License](LICENSE)
