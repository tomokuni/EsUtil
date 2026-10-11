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
| 画像の操作 | `EsBitmapExtensions`（切り出し `Crop`・縮小 `Resize`。 アルゴリズムは `DownscaleAlgorithm`（`Nearest` / `Bilinear` / `Box`）で選ぶ） |
| 画像のファイル入出力 | 画像型の `Save` / `Load`（`EsBitmapBase.Save` / `EsBitmap.Load` / `EsWritableBitmap.Load`。 **形式は拡張子が決める**。 BMP / PNG / JPEG / GIF / TIFF。 OS の GDI+ を使う Windows 専用で、外部パッケージは参照しない） |
| OS サービス抽象 | `ICaptureService` / `ICursorService` / `IWindowService` / `IElementFinder` / `ICursorOverrideGuard` / `IGlobalHotKeyService<T>` |
| Windows 実装 | `Native.Win.*`（Win32 / GDI+ の P/Invoke。 ホストが生成して注入する） |
| 補助 | `EsPlatformHandle` / `DrawingColorExtensions` |

各 UI 実装は、自身の型とコア型を相互変換する Interop 層を個別に提供します（本プロジェクトは UI 型を参照しないため、変換は利用側が持ちます）。

## 4. 依存

- 外部パッケージを参照しません（画像のファイル入出力は OS の `gdiplus.dll` を直接呼びます）。
- UI フレームワーク（Avalonia / MewUI / WinUI / WinForms）と SkiaSharp を参照しません（Skia への依存は Skia を使用する層が単一所有する）。
- 他のプロジェクトは本プロジェクトへ依存します（本プロジェクトは他のプロジェクトを参照しません）。

判定基準と設計判断は [`docs/SPEC_System.md`](docs/SPEC_System.md) を参照してください。

### 4.1 画像のファイル入出力が引き受ける副作用

画像型のファイル入出力（`EsBitmapBase.Save` / `EsBitmap.Load` / `EsWritableBitmap.Load`）は **Windows の `gdiplus.dll` を直接呼びます**。 これは依存を増やさない代わりに、**プロセス全体に及ぶ副作用**を伴います（実測）。

| 副作用 | 内容 | 実測 |
| --- | --- | --- |
| GDI+ の初期化 | 初回の `Save` / `Load` で `GdiplusStartup` を 1 回だけ呼ぶ。 **呼ばないと GDI+ の API は 1 つも動かない** | 未初期化の `GdipCreateBitmapFromScan0` は状態 18（`GdiplusNotInitialized`）で失敗する |
| **バックグラウンド スレッド +1** | GDI+ が表示設定の通知を受け取るために作る。 プロセス終了まで生存する | 起動直後 8 → 初期化後 **9** |
| **解放されない参照カウント** | `GdiplusShutdown` を呼ばない（**呼ぶと利用者の GDI+ を止める可能性がある**）。 プロセス終了時に OS が解放する | `Startup` 2 回 → `Shutdown` 1 回でも API は動作する（参照カウント式） |
| **初回の遅延** | 最初の `Save` / `Load` に初期化のコストが乗る | 初回 4.89 ms / 2 回目 0.53 ms |

**この副作用は本ライブラリ固有ではありません**。 `System.Drawing.Common.Bitmap` を使っても同じ初期化（`GdiplusStartup`）が走り、**同じ副作用**（GDI+ の有効化・バックグラウンド スレッド +1・解放されない参照カウント・初回の遅延）が起きます（実測: スレッド 8 → 9、初回 4.87 ms / 2 回目 0.06 ms）。 したがって **影響を受けるのは「GDI+ を他に使っていないプロセス」だけです**（既に GDI+ を使うアプリでは同じ初期化とスレッドが既に存在するため、追加の影響はありません）。

**初期化を前倒しする公開 API はありません**（初期化は `WinGdiPlusLifetime` が単一所有しますが `internal` のため本アセンブリ専用です）。 コストを払うのは最初の `Save` / `Load` です。

**副作用を避けたい場合**は、GDI+ ではなく **WIC**（`windowscodecs.dll` の COM）へ移す必要があります（プロセス全体の初期化もスレッドも不要）。 代償は実装量と速度です（実測: BMP で 6.8 倍遅い）。 判断の記録は [`docs/SPEC_System.md`](docs/SPEC_System.md) の「ファイル入出力の実装方式」を参照してください。

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
