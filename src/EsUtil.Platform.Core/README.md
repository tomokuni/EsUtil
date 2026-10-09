# EsUtil.Platform.Core

UI フレームワーク非依存の **共通基盤の型**（キーと入力・幾何・座標変換・画像・OS サービス抽象）を提供するライブラリです。

## 1. 概要

UI フレームワークに依存していた型を本プロジェクトへ切り出すことで、上位のアプリ・コントロールから、UI フレームワーク非依存のまま利用できます。

**実行時のプラットフォーム分岐を持ちません**。 OS サービスの Windows 実装（`src/EsUtil.Platform.Core/src/Native/Win`）はホストが生成して抽象型として注入します。 テストはモックを注入できるため、Win32 実行環境を必要としません。

## 2. 使い方

```csharp
using EsUtil.Platform.Core.Coord;
using EsUtil.Platform.Core.Geometry;

// 幾何（DIP 相当の値型。UI の座標型は境界で 1 行変換する）
var rect = EsRect.FromLTRB(10, 20, 110, 120);
var center = rect.Center;                             // EsPoint
var inside = rect.ClampToArea(new EsSize(200, 100));  // 領域内へクランプ

// 座標系の変換ポート（対象座標 = ローカル DIP × 倍率）
var port = CoordTransform.ClientToSource(() => 2.0);
var source = port.ToSource(new EsPoint(100, 100));
```

```csharp
// ホスト（Windows 前提）が OS サービスの既定実装を生成して注入する
services.AddSingleton<IGlobalHotKeyService<CaptureType>>(_ => new WinGlobalHotKeyService<CaptureType>());
```

## 3. 含まれるもの

| グループ | 型 |
| --- | --- |
| キーと入力 | `EsKey` / `EsKeyExtensions` / `EnumCanonical` / `EsWindowState` / `HotKeyModifiers` / `HotKeySetting` / `CaptureType` |
| 幾何 | `EsPoint` / `EsSize` / `EsVector` / `EsRect` とその整数版、`EsThickness` / `EsRounding` / `EsRectInterval` / `EsAxis` |
| 座標変換 | `ICoordTransform` / `CoordTransform` / `ScrollMath` / `DpiScaler` |
| 画像 | `EsBitmap`（読み取り専用の実体） / `EsWritableBitmap`（書き換え可能な実体） / `IEsBitmap`（読み取る権利。 寸法の文字列化 `ToSizeText` を含む） / `IEsWritableBitmap`（書き換える権利） |
| 画像の操作 | `EsBitmapExtensions`（切り出し `Crop`・縮小 `Resize`。 `IEsBitmap` を受ける拡張メソッド） |
| 画像のファイル入出力 | `Native.Imaging.EsBitmapFileExtensions`（`Save` / `Load`。 **形式は拡張子が決める**。 BMP / PNG / JPEG / GIF / TIFF。 `System.Drawing.Common` を使う Windows 専用） |
| OS サービス抽象 | `ICaptureService` / `ICursorService` / `IWindowService` / `IElementFinder` / `ICursorOverrideGuard` / `IGlobalHotKeyService<T>` |
| Windows 実装 | `Native.Win.*`（Win32 P/Invoke。 ホストが生成して注入する） |
| 補助 | `EsPlatformHandle` / `LoggerFallback` / `DrawingColorExtensions` |

各 UI 実装は、自身の型とコア型を相互変換する Interop 層を個別に提供します（本プロジェクトは UI 型を参照しないため、変換は利用側が持ちます）。

## 4. 依存

- 外部パッケージは Microsoft 公式のみです（`Microsoft.Extensions.Logging.Abstractions`、`src/EsUtil.Platform.Core/src/Native/Imaging` の `System.Drawing.Common`）。
- UI フレームワーク（Avalonia / MewUI / WinUI / WinForms）と SkiaSharp を参照しません（Skia への依存は Skia を使用する層が単一所有する）。
- 他のプロジェクトは本プロジェクトへ依存します（本プロジェクトは他のプロジェクトを参照しません）。

判定基準と設計判断は [`docs/SPEC_System.md`](docs/SPEC_System.md) を参照してください。

## 5. カスタマイズ

| 差し替えたいもの | 方法 |
| --- | --- |
| OS サービスの実装 | `Native.*` の抽象を実装し、ホストで注入する（Windows 以外・テスト用のモック） |
| 各 UI の型との相互変換 | Interop 層を自作する（UI 型とコア型の変換は利用側が持ちます） |
| 画像の描画 | 描画は各 UI が行う（本プロジェクトは画像の型と配置計算だけを提供する） |

## 6. ビルド

```powershell
dotnet build src/EsUtil.Platform.Core/src/EsUtil.Platform.Core.csproj
```

## 7. テスト

```powershell
dotnet test src/EsUtil.Platform.Core/tests/EsUtil.Platform.Core.Tests.csproj
```

## 8. ドキュメント

- [`docs/SPEC_API.md`](docs/SPEC_API.md) — 公開 API のリファレンスと使用例
- [`docs/SPEC_System.md`](docs/SPEC_System.md) — アーキテクチャ・依存の判定基準・設計判断

## 9. ライセンス

MIT License（全文は [`LICENSE`](LICENSE) を参照）。
