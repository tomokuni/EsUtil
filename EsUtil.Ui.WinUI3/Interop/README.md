# EsUtil.Ui.WinUI3.Interop

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
| `WindowPlacementMath` | 設定の物理ピクセル ↔ WinUI 3 の DIP の換算 |
| `Imaging.ClientPixelScale` | 物理ピクセルと DIP の比から実効倍率を求める |
| `Imaging.TextMeasure` | WinUI 3 のテキスト計測で実寸を測る |
| `Drawing.CompositionImageView` | コアの画像を Composition で描く表示ビュー |
| `Drawing.RectShapeLayer` | 矩形（角丸を含む）を Composition の図形として描く |
| `Drawing.DrawList` / `DrawRect` / `DrawRoundRect` | 描く図形の一覧（UI 非依存の値） |
| `Drawing.BmpStreamBuilder` | BMP をメモリ上で組み立てる（ICO の中身） |

一覧は [`docs/SPEC_API.md`](docs/SPEC_API.md) を参照してください。

## 3. 使い方

```csharp
var view = new CompositionImageView();
view.Source = EsReadOnlyBitmap.Load(path);
var key = Windows.System.VirtualKey.A.ToEsKey();
```

## 4. 依存

`EsUtil.Platform.Core` と `Microsoft.WindowsAppSDK` を参照します。 規則は [`.github/依存関係.md`](../../.github/依存関係.md)。

- 描画は Composition を使います（Skia を使いません）。

## 5. テスト

`tests/`（xUnit v3）。 変換の往復・未定義値の扱い・配置の計算・描画の計画を検証します。
検証の手順は [`.github/検証手順.md`](../../.github/検証手順.md)。

## 6. ドキュメント

- API リファレンス … [`docs/SPEC_API.md`](docs/SPEC_API.md)
- システム仕様書 … [`docs/SPEC_System.md`](docs/SPEC_System.md)
- 層の帰属 … [`../../EsUtil/README.md`](../../EsUtil/README.md)

## 7. ライセンス

MIT（リポジトリの [LICENSE](../../LICENSE) を参照）。
