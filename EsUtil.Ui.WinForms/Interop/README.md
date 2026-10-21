# EsUtil.Ui.WinForms.Interop

## 1. 概要

Windows Forms と EsUtil の型（キー・幾何・色・ウィンドウ状態・画像）を相互変換する**ブリッジ**です。
UI フレームワーク非依存の値型と抽象は `EsUtil.Platform.Core` が、OS の操作はその Native 実装が担います。
本アセンブリは **UI 側の型との境界だけ**を持ちます（変換の規則と UI 固有の描画）。

## 2. 主な API

| 何 | 型 |
| --- | --- |
| `EsKeyInterop` | `EsKey` ↔ `System.Windows.Forms.Keys`（名前で解決し、未定義値は `None`） |
| `EsGeometryInterop` | `EsPoint` / `EsSize` / `EsRect` と整数版 ↔ `System.Drawing` の幾何型 |
| `EsWindowStateInterop` | `EsWindowState` ↔ `FormWindowState` |
| `EsPlatformHandleInterop` | `EsPlatformHandle` ↔ WinForms のウィンドウ ハンドル |
| `MonitorDpi` | モニター単位の DPI を照会する（`GetDpiForMonitor`） |
| `EsWindowPlacementMath`（コア） | 設定の物理ピクセル ↔ WinForms の DIP の換算 |
| `Imaging.EsBitmapDrawingInterop` | コアの画像 ↔ `System.Drawing.Bitmap` |
| `Imaging.ImageView` | 画像を描く表示ビュー（`Control` + `OnPaint`） |
| `Imaging.ImageRenderer` | コアの画像を GDI+ で描く |
| `Imaging.GdiFill` / `GdiText` / `GdiLabel` | GDI+ の塗り・文字・ラベル |
| `EsImageDrawMath`（コア） | 配置の矩形の純粋計算 |
| `EsImageDrawMode`（コア） | 配置の仕方（`Fill` / `Uniform`） |

一覧は [`docs/SPEC_API.md`](docs/SPEC_API.md) を参照してください。

## 3. 使い方

```csharp
var view = new ImageView { Mode = EsImageDrawMode.Uniform };
view.Source = EsReadOnlyBitmap.Load(path);
var key = System.Windows.Forms.Keys.A.ToEsKey();
```

## 4. 依存

`EsUtil.Platform.Core` と （`UseWindowsForms`。 追加のパッケージ参照は無し） を参照します。 規則は [`.github/依存関係.md`](../../.github/依存関係.md)。

- 描画は GDI+ の標準 API を使います（Skia を使いません）。

## 5. テスト

`tests/`（xUnit v3）。 変換の往復・未定義値の扱い・配置の計算・描画の計画を検証します。
検証の手順は [`.github/検証手順.md`](../../.github/検証手順.md)。

## 6. ドキュメント

- API リファレンス … [`docs/SPEC_API.md`](docs/SPEC_API.md)
- システム仕様書 … [`docs/SPEC_System.md`](docs/SPEC_System.md)
- 層の帰属 … [`../../EsUtil/README.md`](../../EsUtil/README.md)

## 7. ライセンス

MIT（リポジトリの [LICENSE](../../LICENSE) を参照）。
