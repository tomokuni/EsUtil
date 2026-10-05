# EsUtil.Feature.Loupe.Presenter API リファレンス

ルーペ（拡大鍵）の見た目を定める **コントロール化を前提としない UI 非依存層**が提供する公開 API のリファレンスです。 使い方（概要・クイックスタート・カスタマイズ）は [`../README.md`](../README.md)、アーキテクチャと設計判断は [`SPEC_System.md`](SPEC_System.md) を参照してください。

## 目次

- [1. 前提条件](#1-前提条件)
- [2. 公開 API](#2-公開-api)
  - [2.1 `EsUtil.Feature.Loupe`（提示・外観）](#21-esutilfeatureloupe提示外観)
  - [2.2 `LoupePresenter` のメンバー](#22-loupepresenter-のメンバー)
  - [2.3 `LoupeAppearance` のメンバー](#23-loupeappearance-のメンバー)
  - [2.4 `LoupeDrawPlan` のメンバー](#24-loupedrawplan-のメンバー)
  - [2.5 `LoupeDrawLayer` の値](#25-loupedrawlayer-の値)
  - [2.6 ラベル配置の解決（`internal`。 本リファレンスには載せない）](#26-ラベル配置の解決internal-本リファレンスには載せない)
  - [2.7 `LoupePresenterLogMessage`（`internal`。 本リファレンスには載せない）](#27-loupepresenterlogmessageinternal-本リファレンスには載せない)
- [3. 契約（利用者が守る・受け取る値）](#3-契約利用者が守る受け取る値)
  - [3.1 描画計画（`LoupeDrawPlan`）](#31-描画計画loupedrawplan)
  - [3.2 ラベル（本層の配置指示 → `LoupeLabelPlacement`）](#32-ラベル本層の配置指示--loupelabelplacement)
  - [3.3 描画の順序](#33-描画の順序)
- [4. 使用例](#4-使用例)
- [5. 座標系](#5-座標系)
- [6. 制限事項](#6-制限事項)

## 1. 前提条件

- 本層は UI フレームワークに依存しません（依存は `EsUtil.Feature.Loupe.Core` と `EsUtil.Platform.Core` のみ）。 **描画 API の抽象も持ちません**（描画計画というデータを返し、描画は各 UI が自 UI の API で行います）。
- **本層はコントロール化を前提とした処理を提供しません**（拡大元画像・再描画のスケジューリング・組み込み先の概念は、コントロール固有の値や UI の機構であるため UI 層が持ちます）。
- 座標は `EsPoint` / `EsSize` / `EsRect`（`EsUtil.Platform.Core`）、色は UI 非依存の `System.Drawing.Color` で扱います。
- 描画の単位は**物理ピクセル**です（`LoupeGeometry.ClientPixelSize` が作る正方形が描画面）。 DIP → 物理ピクセルの換算（表示サイズと枠線の太さ）は本層とコアが済ませているため、UI 層は換算しません。
- 中心座標ラベルは **DIP** で配置します（文言・基準点は本層が、実寸測定と要素の配置は UI 層が担います）。

## 2. 公開 API

### 2.1 `EsUtil.Feature.Loupe`（提示・外観）

**公開面**（ファサード `LoupePresenter` と、ファサードから返る・渡す型（`LoupeDrawPlan` / `LoupeDrawLayer` / `LoupeAppearance` / `LoupeLabelPlacement`））は `src/` 直下（ルート名前空間 `EsUtil.Feature.Loupe`）に属します。 **内部手順**（`internal`）は層フォルダ（`src/Presenter/`）へ置き、名前空間に層名を挟みます（`src/Presenter/Drawing/` → `EsUtil.Feature.Loupe.Presenter.Drawing`）。

| 型 | 種別 | 説明 |
| --- | --- | --- |
| `LoupePresenter` | class（`partial`） | 提示のファサード（コアのラップ・外観の保持・描画計画の解決）。 `sealed` ではない（派生で見た目を差し替えられる） |
| `LoupeAppearance` | record | 外観（色・フォント・角丸・枠線の太さ・表示可否） |
| `LoupeDrawPlan` | record struct | 1 フレーム分の描画計画（描画面・矩形・層の順序・ラベル配置・通知用の値） |
| `LoupeDrawLayer` | enum | 描画層（`Pasteboard` / `Image` / `Crosshair` / `Border` / `Label`） |
| `LoupeLabelPlacement` | record struct | ラベル要素の配置指示（背景矩形・内側余白・文言・フォントサイズ・色・角丸） |
| `EsUtil.Feature.Loupe.Presenter.Drawing.LoupeTextPlacement` | `internal` record struct | 描画層が UI 層へ渡すテキスト（文言・基準点・描画領域）。 **本層の `internal` の写像だけが使うため公開面ではない**（層フォルダの名前空間に残る） |
| `EsUtil.Feature.Loupe.Presenter.Logging.LoupePresenterLogMessage` | static partial class | 本層が出力するログ定義（生成・通過の記録・外観の変更。 内部手順でありホストは参照しない） |

**`LoupePresenter` は複数のファイルに分かれた `public partial class` です**（`LoupePresenter.cs` = ファサード本体、`DrawPlan` = 十字線・枠線の矩形の計算、`LabelPlan` = 実寸 → 配置指示の写像、`LabelTexts` = 文言と基準点の生成）。 **`static class` ではありません**（ファサードの状態と外観を保持するため）。

`LoupePresenter` の partial（`DrawPlan` = 十字線・枠線の矩形の計算、`LabelPlan` = 実寸 → 配置指示の写像、`LabelTexts` = 文言と基準点の生成）のメンバーと `EsUtil.Feature.Loupe.Presenter.Logging.LoupePresenterLogMessage` は `internal`（本層とテストのみが使う）であり、本リファレンスには載せません（内部手順は [`SPEC_System.md`](SPEC_System.md) の可視性ポリシーを参照してください）。

### 2.2 `LoupePresenter` のメンバー

ルーペの**提示の窓口**（適用パターン: Facade / Presenter）。 UI 層は本型だけを参照します。

| メンバー | 種別 | 説明 |
| --- | --- | --- |
| `LoupePresenter(ILogger? logger = null)` | ctor | 生成。 **ロガーを注入した場合は、その場で本層とコアの生成の記録を出力する**（注入しない場合は `Log` の初回設定時に出力する） |
| `Options` | property | コアの設定（計算に必要な値）を転送する（**本層を通過したことを Trace で記録する**） |
| `ClientToPixelScaling` / `CenterPoint` / `PositionOffset` | property | コアの環境値・拡大中心・オフセットを転送する（**本層を通過したことを Trace で記録する**） |
| `Log` | property | コアのロガーを転送する。 **コンストラクターで注入されていない場合、初回の設定時に本層の生成を 1 度だけ記録する** |
| `Appearance` | property | 外観（本層が単一所有する）。 **変化したときだけ**変更ログ（Debug）を出力する |
| `SetCoordTransform(ICoordTransform?)` | method | コアへ委譲する（**本層を通過したことを Trace で記録する**） |
| `ResolveLayout(EsSize)` | method | コアへ委譲する（表示サイズ + 位置。 毎フレーム呼ばれるためログは出力しない） |
| `ResolveDrawPlan(double displaySize, Func<string, double, EsSize> measureText)` | method（`virtual`） | 描画計画（`LoupeDrawPlan`）を解決する。 `measureText` が null なら `ArgumentNullException`。 毎フレーム呼ばれるためログは出力しない |

**ログの出力方針**: 生成（コンストラクター注入時、または `Log` の初回設定時に 1 度だけ。 Information）・本層を通過した値（Trace）・本層が単一所有する外観の変更（Debug）を出力します。 値の保持と変更の記録はコアが行うため、本層は「通過したこと」を Trace で残します（ホストの設定が本層を通ったかを確認できます。 詳細は [`SPEC_System.md`](SPEC_System.md) の「ログ」を参照）。

### 2.3 `LoupeAppearance` のメンバー

| プロパティ | 型 | 既定値 | 説明 |
| --- | --- | --- | --- |
| `PasteboardColor` | `Color` | `#FF535353` | 画像外（ペーストボード）の色 |
| `BorderColor` / `CrossColor` | `Color` | `#FFD3D3D3` / `#66FF0000` | 枠線 / 十字線の色 |
| `CoordLabelColor` / `CoordLabelBackgroundColor` | `Color` | 白 / `#99000000` | 中心座標ラベルの文字色 / 背景色 |
| `CoordLabelFontSize` / `CoordLabelCornerRadius` | `double` / `int` | `12.0` / `2` | 中心座標ラベルのフォントサイズ / 角丸（6〜48 / 0〜64 へ補正） |
| `CoordLabelOffset` | `double` | `2.0` | 中心座標ラベルをルーペの縁から離すオフセット（DIP。 左右・上下に同じ値を使う） |
| `BorderThickness` | `int` | `2` | 枠線の太さ（DIP。 1〜64 へ補正。 安全域は `LoupeCore.Limits` が単一所有する） |
| `ShowCrosshair` / `ShowBorder` / `ShowCoordLabel` | `bool` | `true` | 表示可否（描画にだけ必要。 透明色を代用にしない） |
| `ShowPasteboard` | `bool` | `true` | 画像外（ペーストボード）の表示可否。 非表示でも描画面を透明で塗るため、描画面を保持する UI でも前フレームの残像が残らない |

描画層が描画面を塗る色は `ResolvePasteboardColor()`（表示時は `PasteboardColor`、非表示時は透明）が単一所有します。 描画実装は返された色で描画面を塗るだけであり、分岐を持ちません（`Pasteboard` の層は常に含まれます）。

| メンバー | 説明 |
| --- | --- |
| `Default` | すべての項目を既定値にした外観 |
| `ShouldShowCoordLabel(bool hasCenterPoint)` | 中心座標ラベルを描画するかを判定（`ShowCoordLabel` かつ拡大中心あり） |

### 2.4 `LoupeDrawPlan` のメンバー

| メンバー | 説明 |
| --- | --- |
| `Appearance` | 外観（描画時に色・フォントを使うため保持する） |
| `ClientPixelRect` | 描画面の全体（物理ピクセルの正方形） |
| `MagnifiedImage` | 拡大画像の切り出しと描画の矩形（`LoupeMagnifiedImage`。 中心未設定は null） |
| `CrosshairPixelRects` / `BorderPixelRects` | 十字線（縦横 2 本）/ 枠線（4 辺）の矩形 |
| `Layers` | 描く層とその順序（先頭から処理する） |
| `Labels` | ラベルの配置指示（`LoupeLabelPlacement` の一覧。 空は表示なし） |
| `CaptureTopLeft` / `ZoomLevel` / `ClientPixelSize` | 描画完了通知（`Rendered`）の組み立てに使う値 |

### 2.5 `LoupeDrawLayer` の値

| 値 | 内容 |
| --- | --- |
| `Pasteboard` | ペーストボード（画像外の色）。 常に含む（描画面を置き換えて前フレームの残像を残さない） |
| `Image` | 拡大画像（切り出し領域を解決できた場合のみ） |
| `Crosshair` / `Border` | 十字線 / 枠線（表示可否が有効な場合のみ） |
| `Label` | 中心座標ラベル（ラベルがある場合のみ。 常に最上位） |

### 2.6 ラベル配置の解決（`internal`。 本リファレンスには載せない）

実寸 → ラベル要素の配置指示の写像は本層の内部手順です（入力の `LoupeTextPlacement` は `internal`、出力の `LoupeLabelPlacement` は公開しています）。

### 2.7 `LoupePresenterLogMessage`（`internal`。 本リファレンスには載せない）

本層が出力するログの定義（生成・通過の記録・外観の変更）です。 **外部公開しない**（呼び出すのは値を所有する本層だけで、ホストはロガーを設定するだけです）。 出力の方針は [`SPEC_System.md`](SPEC_System.md) の「ログ」を参照してください。

**拡大元画像（`Source`）と描画の例外のログは各 UI が所有します**（コントロール固有の値であるため。 接頭辞だけを揃える）。

## 3. 契約（利用者が守る・受け取る値）

### 3.1 描画計画（`LoupeDrawPlan`）

描画計画は**UI が見る唯一の描画値**です。 UI は `Layers` の順に自 UI の描画 API を呼び、`Labels` を要素として置くか文字として描くかを決めます。 **描く値はすべて解決済み**であり、UI は幾何計算（矩形の導出・寸法の換算）を行いません。

### 3.2 ラベル（本層の配置指示 → `LoupeLabelPlacement`）

- `Labels` の各要素は**配置矩形・内側余白・文言・フォントサイズ・色・角丸**を持ちます（背景の大きさは「実寸 + 内側余白 × 2」。 領域クランプは本層が済ませています）。
- **要素として置くか canvas へ描くかは UI 層が決めます**（要素は親より後に描かれるため常に最上位になる）。

### 3.3 描画の順序

`LoupeDrawLayer` の順序（`Pasteboard` → `Image` → `Crosshair` → `Border` → `Label`）は `LoupeDrawPlan.Layers` が単一所有します。 UI は順序を判断しません。

## 4. 使用例

```csharp
using EsUtil.Feature.Loupe;

// 外観（色・フォント・枠線の太さ・表示可否）を差し替える
var appearance = LoupeAppearance.Default with
{
    BorderColor = Color.FromArgb(0xFF, 0x33, 0x99, 0xFF),
    ShowCrosshair = false,
};

var presenter = new LoupePresenter(new LoupeCore(new LoupeOptions { RequestedSize = 160, ZoomLevel = 4 }))
{
    Appearance = appearance,
    CenterPoint = new EsPoint(1920, 1080),
};

// 描画計画（UI が見る唯一の描画値）を解決する
var plan = presenter.ResolveDrawPlan(displaySize: 160, measureText: MeasureText);

// UI は Layers の順に自 UI の描画 API を呼び、Labels を要素として配置する
foreach (var layer in plan.Layers) { /* Pasteboard → Image → Crosshair → Border → Label */ }
```

## 5. 座標系

| 座標系 | 用途 |
| --- | --- |
| 物理ピクセル | 描画面・矩形（`LoupeDrawPlan` の矩形） |
| DIP | ラベル（`LoupeLabelPlacement.DipRect` / `PaddingX` / `PaddingY`） |

## 6. 制限事項

- **描画は行いません**（描画計画というデータを返すだけです。 描画は各 UI が自 UI の API で行います）。
- 描画 API の抽象（キャンバスの抽象）は持ちません（UI フレームワークごとの描画モデルの差を吸収しきれないため）。
- 中間ビットマップの非同期生成と世代番号管理は、中間ビットマップを持つ UI にだけ必要な処理のため、その UI 実装が所有します。
