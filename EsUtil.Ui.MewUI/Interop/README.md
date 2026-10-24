# EsUtil.Ui.MewUI.Interop

[![NuGet](https://img.shields.io/nuget/v/EsUtil.Ui.MewUI.Interop.svg)](https://www.nuget.org/packages/EsUtil.Ui.MewUI.Interop)

## 1. 概要

MewUI と EsUtil の型（キー・幾何・色・ウィンドウ状態・画像）を相互変換する**ブリッジ**です。
UI フレームワーク非依存の値型と抽象は `EsUtil.Platform.Core` が、OS の操作はその Native 実装が担います。
本アセンブリは **UI 側の型との境界だけ**を持ちます（変換の規則と UI 固有の描画）。

## 2. 主な API

| 何 | 型 |
| --- | --- |
| `EsKeyInterop` | `EsKey` ↔ `Aprillz.MewUI.Key`（名前で解決し、未定義値は `None`） |
| `EsGeometryInterop` | `EsPoint` / `EsSize` / `EsRect` と整数版 ↔ MewUI の幾何型 |
| `EsWindowStateInterop` | `EsWindowState` ↔ MewUI のウィンドウ状態 |
| `EsPlatformHandleInterop` | `EsPlatformHandle` ↔ MewUI のウィンドウ ハンドル |
| `EsWindowPlacementMath`（コア） | 設定の物理ピクセル ↔ MewUI の DIP の換算 |
| `Imaging.ImageView` | 画像を描く表示ビュー（`Control` + `OnRender`） |
| `Imaging.ImageRenderer` | `EsReadOnlyBitmap` を描く層（変換は元画像の変更時だけ） |
| `Imaging.ImageRenderPlan` | 描画の計画（元矩形・先矩形）の純粋計算 |
| `Imaging.ImageBinding` | コアの画像を MewUI の `IImage` として保持する |
| `EsImageDrawMath`（コア） | 配置の矩形の純粋計算 |
| `EsImageDrawMode`（コア） | 配置の仕方（`Fill` / `Uniform`） |
| `Imaging.TextMeasure` | MewUI のテキスト エンジンで実寸を測る |

一覧は [`docs/SPEC_API.md`](docs/SPEC_API.md) を参照してください。

## 3. 使い方

```csharp
var view = new ImageView { Mode = EsImageDrawMode.Uniform };
view.Source = EsReadOnlyBitmap.Load(path, 320);
var key = Aprillz.MewUI.Key.A.ToEsKey();
```

## 4. 依存

`EsUtil.Platform.Core` と `Aprillz.MewUI.Core`（0.21.1 固定） を参照します。 規則は [`.github/依存関係.md`](../../.github/依存関係.md)。

- 描画バックエンド（Direct2D / GDI）はアプリが選びます。 本アセンブリは `IGraphicsContext` だけを使います。
- テキスト計測はグラフィックス バックエンドの登録が前提です（未登録では例外）。 テストは GDI バックエンドを登録します。

## 5. テスト

`tests/`（xUnit v3）。 変換の往復・未定義値の扱い・配置の計算・描画の計画を検証します。
検証の手順は [`.github/検証手順.md`](../../.github/検証手順.md)。

## 6. ドキュメント

- API リファレンス … [`docs/SPEC_API.md`](docs/SPEC_API.md)
- システム仕様書 … [`docs/SPEC_System.md`](docs/SPEC_System.md)
- 層の帰属 … [`../../EsUtil/README.md`](../../EsUtil/README.md)

## 7. ライセンス

MIT（リポジトリの [LICENSE](../../LICENSE) を参照）。
