# EsUtil.Platform.Core

![NuGet](https://img.shields.io/nuget/v/EsUtil.Platform.Core.svg)
![Release](https://github.com/tomokuni/EsUtil/actions/workflows/release.yml/badge.svg)
![Build](https://github.com/tomokuni/EsUtil/actions/workflows/build.yml/badge.svg?branch=main)
![Platform](https://img.shields.io/badge/Platform-Windows%2010%20(1809)%2B-0078D6)
![.NET](https://img.shields.io/badge/.NET-10.0-512BD4)
![AOT](https://img.shields.io/badge/AOT-Ready-00695C)
![License](https://img.shields.io/badge/License-MIT-3DA639)

**対応 OS**: Windows 専用の型（Native.Win 配下・画像の保存/読み込み）は Windows 10 バージョン 1809（ビルド 17763）以降（`[SupportedOSPlatform("windows10.0.17763")]`）。 それ以外は指定なし（`net10.0`）。 ただし **Windows 以外での動作は未検証**

UI フレームワーク非依存の **共通基盤の型**（キーと入力・幾何・座標変換・画像・OS サービス抽象）を提供するライブラリです。

## 1. 概要

UI フレームワークに依存していた型を本プロジェクトへ切り出すことで、上位のアプリ・コントロールから、UI フレームワーク非依存のまま利用できます。

**実行時のプラットフォーム分岐を持ちません**。 OS サービスの Windows 実装（`EsUtil/Platform.Core/src/Native/Win`）はホストが生成して抽象型として注入します。 テストはモックを注入できるため、Win32 実行環境を必要としません。

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
| 窓口（ルート名前空間） | `EsInput` / `EsWindow` / `EsCapture` / `EsColor` / `EsErrors` / `EsCallbacks` / `EsImageCodec`（機能の型へ委譲する薄い入口。 `using EsUtil.Platform.Core;` だけで解釈・正規化・生成ができる） |
| キーと入力（`Input.*`） | `EsKey` / `EsKeyExtensions` / `HotKeyModifiers` / `HotKeySetting` / `HotKeyParser`（`"Ctrl + Shift + Q"` の解釈） |
| ウィンドウ（`Window.*`） | `EsPlatformHandle`（ネイティブ ハンドル） / `EsWindowState` / `EsWindowPlacementMath`（復元可否・はみ出しの補正・DIP ⇔ 物理ピクセル） |
| キャプチャ（`Capture.*`） | `CaptureType` |
| 小さな共通基盤（`Common.*`） | `EnumCanonical` / `BoundaryCatch` / `CallbackList<T>` |
| 幾何 | `EsPoint` / `EsSize` / `EsVector` / `EsRect` とその整数版、`EsThickness` / `EsRounding` / `EsRectInterval` / `EsAxis` / `EsRectIntJsonConverter`（設定ファイルの保存形式） |
| 座標変換 | `ICoordTransform` / `CoordTransform` / `ScrollMath` / `DpiScaler`（DIP ⇔ 物理ピクセル・ポイント換算・描画面からの係数解決） |
| 画像 | **2 型は対称です**（書き込みに関するものだけが `EsBitmap`）。 `EsReadOnlyBitmap`（**読み取り専用**。 受け取る・保持する側）/ `EsBitmap`（**書き換え可能**。 作る・書く側。 `Pixels`・`SetPixel`・`Clear`・`SetReadOnly` が追加）/ `EsPixmap`（一部を指す 0 コピーの読み取りビュー。 切り出しの複製を避け、**縮小・減色の対象にもなる**）。 変換は向きごとに 1 つ（`FromBitmap`（凍結済みなら 0 コピー）/ `FromReadOnlyBitmap`（複製）） |
| 画像の操作 | **両方の画像型が同じ名前のメンバーを持ちます**（切り出し `Crop`・縮小 `Resize`・減色 `ReduceColors`。 実装は内部の 1 か所）。 縮小の方法は `EsImageScalingMode`（WIC の 5 種 + 本ライブラリの 3 種。 既定は `WicFant`）で選びます。 `Wic` の縮小と減色は WIC が行い（`Es` の縮小だけが本ライブラリの計算）、ディザは `EsDitherMode`（`None` / `ErrorDiffusion`）で選びます（**Windows 専用**）。 `Resize` は縮小のみ（拡大は `NotSupportedException`） |
| 画像のファイル入出力 | `Save` / `Load`（**画像型のメンバー**。 `Load(string, int maxSize)` は **WIC のスケーラーでデコードしながら縮小する**（サムネイル用。 原寸のバッファを確保しない）。 `EsReadOnlyBitmap.Save` / `EsBitmap.Save` / `EsReadOnlyBitmap.Load` / `EsBitmap.Load`。 **形式は既定で拡張子が決め**、`EsImageSaveOptions.Format` で明示もできる（拡張子が無ければ補完する）。 **形式の一覧は OS のエンコーダーから作り**（固定の一覧を持たない）、試し書きが通った形式だけを `SupportedFileExtensions` が返す（この環境では 19 拡張子。 BMP / PNG / JPEG / GIF / TIFF / JPEG XR に加えて DDS / HEIF / JPEG XL など）。 保存のオプションは `EsImageSaveOptions`（**品質（JPEG / JPEG XR）と減色 `ColorCount` / `Dither`**）。 画素を画像型にせずに読み書きする場合は **`EsImageCodec`**（`ReadOnlySpan<byte>` + 寸法 + 行幅で受け渡す窓口。 画像型はここへ委譲する）を使う。 **EXIF の向き（`Orientation`）を適用してから縮小する**（回転した JPEG も正しい向きで読める）。 OS の WIC（`windowscodecs.dll` の COM）を使う Windows 専用で、外部パッケージは参照しない） |
| OS サービス抽象 | `ICaptureService` / `ICursorService` / `IWindowService` / `IElementFinder` / `ICursorOverrideGuard` / `IGlobalHotKeyService<T>` |
| Windows 実装 | `Native.Win.*`（Win32 / WIC の P/Invoke と COM。 ホストが生成して注入する） |
| クリップボード（Windows 専用） | `Native.Win.WinClipboard`（DIB の読み取り・テキストの読み書き・変更の検出。 開閉とロックの順序を単一所有する） / `Native.Win.WinVirtualScreen`（仮想スクリーンの矩形） |
| 補助 | `DrawingColorExtensions` / `EsImageFileExtensions`（一覧に並べる画像の拡張子の判定） |
| 色 | `Palette.ColorNames`（Windows 標準の色名の表と色名 ⇔ 色の相互参照） / `Palette.ColorEntry`（一覧と並び順） / `Palette.ColorString`（色名・16 進・不透明度 % の解析と整形） / `Palette.ColorValidation` / `Palette.ColorSortMode`（色名順・色相順）。 色を選ばせるコントロールと設定ファイルが同じ解釈を共有する |
| キャッシュ | `Caching.CapacityLimitedCache<TKey, TValue>`（生成にコストがかかる値をキー単位で共有する容量制限付きのキャッシュ。 **各コントロールは複製せず本型を使う**） |
| SVG | `Svg.SvgIconSource`（読み込み元: 埋め込みリソース・フォルダー・テキスト） / `SvgIcon` / `SvgPathParser` / `SvgPathFigure` / `SvgPathSegment` / `SvgPathRasterizer` |

各 UI 実装は、自身の型とコア型を相互変換する Interop 層を個別に提供します（本プロジェクトは UI 型を参照しないため、変換は利用側が持ちます）。

## 4. 依存

- 外部パッケージを参照しません（画像のファイル入出力は OS の WIC を直接呼びます）。 **例外はソース生成だけを目的とするパッケージ**（`Microsoft.Windows.CsWin32`。 `PrivateAssets="all"` のため実行時依存と配布物は増えません）。
- UI フレームワークを参照しません。
- 他のプロジェクトは本プロジェクトへ依存します（本プロジェクトは他のプロジェクトを参照しません）。

判定基準と設計判断は [`docs/SPEC_System.md`](docs/SPEC_System.md) を参照してください。

### 4.1 画像のファイル入出力が引き受ける副作用

画像のファイル入出力（`EsBitmap.Save` / `EsReadOnlyBitmap.Save` / `EsReadOnlyBitmap.Load` / `EsBitmap.Load`）は **Windows の WIC（`windowscodecs.dll` の COM）を直接呼びます**。 外部パッケージを参照せず、**プロセス全体に及ぶ副作用も持ちません**（所要は [`docs/Result_Measurement.md`](docs/Result_Measurement.md) が単一所有します）。

| 項目 | 内容 | 確認したこと |
| --- | --- | --- |
| 初期化 | 初回に `CoInitializeEx` と `CoCreateInstance`（`CLSID_WICImagingFactory`）を 1 回だけ呼ぶ | **スレッドは増えない**（GDI+ の `GdiplusStartup` が起こす表示設定の通知スレッドに相当するものが無い） |
| 既に COM が初期化済みのプロセス | `CoInitializeEx` の `RPC_E_CHANGED_MODE`（`0x80010106`）は**成功として続行する** | 変更モードのプロセスでも全形式の保存と読み込みが成功した |
| Native AOT | `[ComImport]` を使わず、`[GeneratedComInterface]`（ソース生成）で呼ぶ。 「`[LibraryImport]` の `out` にその型を取る」ため、生ポインターも手動ラップも要らない（`CoCreateInstance` も同じ形。 §`docs/SPEC_System.md` 7.3.1） | `[ComImport]` + `Marshal.GetObjectForIUnknown` は `NotSupportedException` になった（実測） |

**初期化を前倒しする公開 API はありません**（初期化は `WinWicFactory` が単一所有しますが `internal` のため本アセンブリ専用です）。 コストを払うのは最初の `Save` / `Load` です。

**WIC を選んだ理由**は、GDI+ が持っていた副作用（バックグラウンド スレッド +1・解放されない参照カウント・初回の遅延）を**引き受けずに済む**ことです。 代償は実装量です（COM インターフェイスの宣言を自前で持つため、`[ComImport]` を使う場合より宣言が増えます）。 判断の記録は [`docs/SPEC_System.md`](docs/SPEC_System.md) の「ファイル入出力の実装方式」を参照してください。

## 5. カスタマイズ

| 差し替えたいもの | 方法 |
| --- | --- |
| OS サービスの実装 | `Native.*` の抽象を実装し、ホストで注入する（Windows 以外・テスト用のモック） |
| 各 UI の型との相互変換 | Interop 層を自作する（UI 型とコア型の変換は利用側が持ちます） |
| 画像の描画 | 描画は各 UI が行う（本プロジェクトは画像の型と配置計算だけを提供する） |

## 6. ビルド

```powershell
dotnet build EsUtil/Platform.Core/src/EsUtil.Platform.Core.csproj
```

## 7. テスト

```powershell
dotnet test EsUtil/Platform.Core/tests/EsUtil.Platform.Core.Tests.csproj
```

## 8. ドキュメント

- [`docs/SPEC_API.md`](docs/SPEC_API.md) — 公開 API のリファレンスと使用例
- [docs/SPEC_System.md](docs/SPEC_System.md) — アーキテクチャ・依存の判定基準・設計判断
- [docs/Result_Measurement.md](docs/Result_Measurement.md) — 所要とメモリの実測値（数値の単一所有者）

## 9. ライセンス

[MIT License](LICENSE)
