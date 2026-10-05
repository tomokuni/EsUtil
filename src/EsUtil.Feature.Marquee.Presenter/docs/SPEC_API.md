# EsUtil.Feature.Marquee.Presenter API リファレンス

マーキー選択（ドラッグで矩形を選び、移動・リサイズできる操作）の見た目を定める **コントロール化を前提としない UI 非依存層**が提供する公開 API のリファレンスです。 使い方（概要・クイックスタート・カスタマイズ）は [`../README.md`](../README.md)、アーキテクチャと設計判断は [`SPEC_System.md`](SPEC_System.md) を参照してください。

## 目次

- [1. 前提条件](#1-前提条件)
- [2. 公開 API](#2-公開-api)
  - [2.1 `EsUtil.Feature.Marquee`（提示・外観）](#21-esutilfeaturemarquee提示外観)
  - [2.2 `MarqueePresenter` のメンバー](#22-marqueepresenter-のメンバー)
  - [2.3 `MarqueeDrawPlan` のメンバー](#23-marqueedrawplan-のメンバー)
  - [2.4 `MarqueeAppearance` のメンバー（外観の単一値）](#24-marqueeappearance-のメンバー外観の単一値)
  - [2.5 ラベル配置の解決（`internal`。 本リファレンスには載せない）](#25-ラベル配置の解決internal-本リファレンスには載せない)
  - [2.6 `MarqueeLogMessage`（`internal`。 本リファレンスには載せない）](#26-marqueelogmessageinternal-本リファレンスには載せない)
- [3. 契約（利用者が守る・受け取る値）](#3-契約利用者が守る受け取る値)
  - [3.1 描画の呼び分け](#31-描画の呼び分け)
  - [3.2 ラベル要素（`MarqueeLabelPlacement`）](#32-ラベル要素marqueelabelplacement)
  - [3.3 付加情報ラベルの体裁](#33-付加情報ラベルの体裁)
- [4. 使用例](#4-使用例)
- [5. 制限事項](#5-制限事項)

## 1. 前提条件

- **描画は本層が行いません**。 本層は描画計画というデータを返し、描画は各 UI の描画実装が自 UI の API で行います（UI フレームワークごとの描画モデルの差は描画 API の抽象では吸収しきれないため、抽象を持ちません）。
- **本層はコントロール化を前提とした処理を提供しません**（ホイール操作・ビューポート連携・入力の解釈・組み込み先の概念は、コントロールを何かへ組み込んで初めて必要になるため UI 層が持ちます）。
- 描画計画の座標は**ローカル DIP**です（原点 (0, 0)・サイズは描画領域）。 対象 → DIP の変換はコアが済ませています。
- 実寸測定（`measureText`）は描画 API 依存のため UI 層が関数として渡します。
- 本層は外観（`MarqueeAppearance`）を単一所有します（描画にだけ必要な設定のため）。 寸法・対話パラメータはコア（`MarqueeOptions`）が単一所有します。
- **本層が単一所有する設定（外観・表示スケーリング係数）の変更は本層が記録します**（`MarqueeLogMessage`。 値を所有する主体が記録する）。 **コアへ伝搬するだけの値も、本層を通過したことを Trace で記録します**。

## 2. 公開 API

### 2.1 `EsUtil.Feature.Marquee`（提示・外観）

**公開面**（ファサード `MarqueePresenter` と、ファサードから返る・渡す型（`MarqueeDrawPlan` / `MarqueeDrawLayer` / `MarqueeAppearance` / `MarqueeLabelPlacement`））は `src/` 直下（ルート名前空間 `EsUtil.Feature.Marquee`）に属します。 **内部手順**（`internal`）は層フォルダ（`src/Presenter/`）へ置き、名前空間に層名を挟みます（`src/Presenter/Drawing/` → `EsUtil.Feature.Marquee.Presenter.Drawing`）。

| 型 | 種別 | 説明 |
| --- | --- | --- |
| `MarqueePresenter` | class（`sealed` でない） | 提示のファサード。 コアを内部に保持し、設定・環境値・入力の転送と、外観の保持、描画計画の解決を行う。 **継承して `ResolveDrawPlan` を上書きできることが役割**（独自の見た目を提供する） |
| `MarqueeDrawPlan` | readonly record struct | 1 フレーム分の描画計画（外観・描画面・注目矩形・暗転領域・枠線矩形・内側の塗りと枠線・ハンドル・マーカー・層の順序・ラベル配置）。 **UI が見る唯一の描画値** |
| `MarqueeDrawLayer` | enum | 描画層（`Decoration` / `Handles` / `Marker` / `Label`）。 **層にある ⇔ 描く値がある** |
| `MarqueeAppearance` | sealed record | **外観の単一所有者**（色・ラベルの体裁・十字マーカーの線の太さ・カーソル座標ラベルのオフセット・枠線の太さ・表示可否）。 `Default` を基点に `with` 式で部分変更する |
| `MarqueeLabelPlacement` | readonly record struct | ラベル要素の配置指示（背景矩形・内側余白・文言・フォントサイズ・色） |
| `EsUtil.Feature.Marquee.Presenter.Drawing.MarqueeTextPlacement` | `internal` readonly record struct | 描画層が UI 層へ渡すテキスト（文言・基準点・クランプ領域）。 **本層の `internal` の写像だけが使うため公開面ではない**（層フォルダの名前空間に残る） |

**`MarqueePresenter` は複数のファイルに分かれた `public partial class` です**（`MarqueePresenter.cs` = ファサード本体、`DrawPlan` = 装飾矩形の計算、`LabelPlan` = 実寸 → 配置指示の写像、`LabelTexts` = 文言と基準点の生成）。 **`static class` ではありません**（ファサードの状態と外観を保持するため）。 partial のメンバー（`ResolveLabelPlacements` など）と `EsUtil.Feature.Marquee.Presenter.Logging.MarqueeLogMessage` は `internal`（本層とテストのみが使う）であり、本リファレンスには載せません（内部手順は [`SPEC_System.md`](SPEC_System.md) の可視性ポリシーを参照してください）。

### 2.2 `MarqueePresenter` のメンバー

| メンバー | 説明 |
| --- | --- |
| `MarqueePresenter(IMarqueeEffects effects, ICursorService? cursor = null, MarqueeOptions? options = null, ILogger? logger = null)` | コアのファサードを内部に生成する。 `effects` は null 不可（null なら `ArgumentNullException`）。 `cursor` は null 可（カーソル操作を行わない）。 `logger` は null 可（**注入するとその場で本層とコアの生成を記録する**。 後から `Log` でも設定できる） |
| `Appearance` | 外観（`MarqueeAppearance`）。 既定は `MarqueeAppearance.Default`。 設定すると次の描画から反映される（`set`） |
| `ResolveDrawPlan(EsSize clientSize, Func<string, double, EsSize> measureText)` | 1 フレーム分の描画計画を解決する（`virtual`）。 処理は「幾何の解決 → 装飾矩形の導出 → ラベルの文言と配置 → 層の順序」の順。 `measureText` が null なら `ArgumentNullException` |
| `DisplayScaleScaling` | サイズラベルの DisplayScale 表示に使う係数（0 以下は `ClientToSourceScaling` と同値） |
| `SizeLabelOverrideProvider` | サイズラベルの文言プロバイダ（null または空文字を返す場合は自動生成） |
| `SizeLabelDisplayScalingProvider` | 表示スケールを矩形ごとに解決するプロバイダ（null の場合は `DisplayScaleScaling`） |
| `Options` | 選択の設定（寸法・対話パラメータ）。 コアへ委譲する |
| `ClientToSourceScaling` / `HighlightSourceRect` | 環境値・ホストが与える実行時状態。 コアへ委譲する |
| `Log` | ロガー。 null は `NullLogger` へ吸収される。 **コンストラクターで未注入の場合は初回の設定時に本層とコアの生成を 1 度だけ記録する** |
| `Subscribe` | 通知（`MarqueeNotice`）の購読（`IDisposable`。 解除は `Dispose`）。 UI 層は自 UI の公開イベントへ転送する |
| `HasSelection` / `SelectionSourceRect` / `CreateSnapshot(EsPoint?)` | 状態の読み取り |
| `SetCoordTransform(ICoordTransform?)` / `ToSource(EsPoint)` / `ToDip(EsPoint)` | 座標変換（コアへ委譲） |
| `Press(EsPoint, int)` / `Move(EsPoint)` / `Release(EsPoint)` / `KeyDown(EsKey, EsPoint?)` / `EndNudge()` | 入力の転送（コアへ委譲） |
| `SetSelection(EsRect)` / `ResetSelection()` / `RequestConfirm()` / `RequestCancel()` | 外部操作の転送（コアへ委譲） |

### 2.3 `MarqueeDrawPlan` のメンバー

| メンバー | 型 | 説明 |
| --- | --- | --- |
| `Appearance` | `MarqueeAppearance` | 解決に使った外観（そのまま保持する。 UI は色・フォントサイズをここから引く） |
| `ClientRect` | `EsRect` | 描画面の全体（原点 (0, 0)・描画領域のサイズ） |
| `FocusDipRect` | `EsRect?` | 注目矩形（DIP。 選択もハイライトも無い場合は null） |
| `LabelAnchor` | `EsPoint?` | ラベルの基準点（注目矩形の左上から**矩形の線幅 + 1 DIP** 内側へ寄せた点。 注目矩形が無い場合は null（サイズラベルと付加情報ラベルが同じ値を使う） |
| `OverlayDipRects` | `IReadOnlyList<EsRect>` | 暗転して塗る領域。 描かない設定または注目矩形が無い場合は空 |
| `OuterBorderDipRects` | `IReadOnlyList<EsRect>` | 外枠の矩形（4 辺）。 描かない設定または注目矩形が無い場合は空 |
| `InnerFillDipRect` | `EsRect?` | 注目矩形の内側の塗り。 塗らない設定または注目矩形が無い場合は null |
| `InnerBorderDipRects` | `IReadOnlyList<EsRect>` | 注目矩形の内側の枠線（4 辺）。 描かない設定または注目矩形が無い場合は空 |
| `Handles` | `MarqueeHandles?` | ドラッグハンドル（null は描かない） |
| `Marker` | `MarqueeMarker?` | 始点マーカー（null は描かない） |
| `Layers` | `IReadOnlyList<MarqueeDrawLayer>` | 描く層とその順序。 先頭から順に処理する。 テキストの層（`Label`）はラベルがある場合のみ末尾（常に最上位） |
| `Labels` | `IReadOnlyList<MarqueeLabelPlacement>` | ラベル要素の配置指示（実寸を反映済み）。 位置を持たない場合は空（表示可否は各指示の `IsVisible` が持つ） |
| `HasText` | `bool` | ラベルを持つ場合は true（`Labels.Count > 0` と同値） |
| `HasDecoration` | `bool` | 装飾（暗転領域・外枠・内側の塗り・内枠）のいずれかを持つ場合は true |

### 2.4 `MarqueeAppearance` のメンバー（外観の単一値）

| 分類 | メンバー | 型 | 既定値 |
| --- | --- | --- | --- |
| 色 | `DefaultBorderColor`（static readonly） | `DrawingColor` | `#FFA9A9A9`（ハンドル枠と外枠が共有する） |
| 色 | `HandleColor` | `DrawingColor` | 白（全成分 0 を指定した場合は白へ解決される） |
| 色 | `HandleBorderColor` | `DrawingColor` | `#FFA9A9A9` |
| 色 | `MarkerColor` | `DrawingColor` | 白 |
| 色 | `SizeLabelColor` / `SizeLabelBackgroundColor` | `DrawingColor` | 白 / 黒 60% |
| 色 | `CoordLabelColor` / `CoordLabelBackgroundColor` | `DrawingColor` | 白 / 黒 60% |
| 色 | `OverlayColor` | `DrawingColor` | 黒 50%（`#80000000`） |
| 色 | `OuterBorderColor` | `DrawingColor` | `#FFA9A9A9` |
| 色 | `InnerFillColor` / `InnerBorderColor` | `DrawingColor` | 透明 / 透明 |
| 体裁 | `SizeLabelFontSizeDefault` / `CoordLabelFontSizeDefault` | `double`（const） | 14.0 / 12.0 |
| 体裁 | `SizeLabelFontSize` / `CoordLabelFontSize` | `double` | 6〜48 へ補正済み。 非数は既定値 |
| 体裁 | `LabelFontSizeMin` / `LabelFontSizeMax` | `double`（const） | 6.0 / 48.0 |
| 体裁 | `LabelPadding` / `LabelPaddingDefault` | `double` | 4.0（ラベル共通の内側余白。 行送りも本値から導く） |
| 体裁 | `CoordLabelOffset` / `CoordLabelOffsetDefault` | `double` | 8.0（カーソル座標ラベルをカーソルから離すオフセット（DIP）。 0 はカーソルに重なる） |
| 体裁 | `MarkerThickness` / `MarkerThicknessDefault` | `double` | 2.0（十字マーカーの線の太さ（DIP）。 0.5〜64 へ補正済み） |
| 表示可否 | `ShowHandles` / `ShowMarker` / `ShowSizeLabel` / `ShowCoordLabel` | `bool` | `true` |
| 表示可否 | `ShowOverlay` / `ShowOuterBorder` | `bool` | `true` |
| 表示可否 | `ShowFullOverlay` | `bool` | `false`（注目矩形が無くても全面を暗転する） |
| 表示可否 | `ShowInnerFill` / `ShowInnerBorder` | `bool` | `false` |
| 太さ | `OuterBorderThickness` / `InnerBorderThickness` | `double` | 2.0（0.5〜64 へ補正済み。 安全域はコアの `MarqueeCore.Limits` が単一所有する） |

色は UI 非依存の `System.Drawing.Color` です（各 UI の色型への変換は各 UI の描画境界で行う）。
**描画の可否は色のアルファでは決まりません**（本型の明示フラグが単一所有します）。

| 述語 | 条件 |
| --- | --- |
| `ShouldShowSizeLabel(bool hasFocusRect)` | 表示の設定が有効かつ注目矩形がある（基準点が矩形の左上であり、文言も矩形の寸法から作るため） |
| `ShouldShowCoordLabel()` | 表示の設定が有効（基準点はカーソル位置であり、位置はコアの幾何が常に持つ） |
| `ShouldDrawOverlay(bool hasFocusRect)` | 注目矩形がある、または全面暗転（`ShowFullOverlay`）が有効 |

### 2.5 ラベル配置の解決（`internal`。 本リファレンスには載せない）

実寸 → ラベル要素の配置指示の写像（`ResolveLabelPlacements`）は本層の内部手順です（入力の `MarqueeTextPlacement` は `internal`、出力の `MarqueeLabelPlacement` は公開しています）。

### 2.6 `MarqueeLogMessage`（`internal`。 本リファレンスには載せない）

本層が出力するログの定義（生成・ロガー設定・本層が単一所有する設定の変更・コアへの転送）です。 **外部公開しない**（呼び出すのは値を所有する本層だけで、ホストはロガーを設定するだけです）。 出力の方針は [`SPEC_System.md`](SPEC_System.md) の「ログ」を参照してください。

- 書式はコアの設定変更ログ（`[Marquee]`）と本文を揃え、接頭辞だけを本プロジェクトにします（どの層まで値が伝搬したかをログから追えます）。
- 色は `#AARRGGBB`（`DrawingColorExtensions.ToHexText`）、数値はカルチャに依存しない表記です。
- 外部公開しません（呼び出すのは値を所有する本層だけであり、ホストはロガーを設定するだけです）。

## 3. 契約（利用者が守る・受け取る値）

### 3.1 描画の呼び分け

| 描く対象 | 呼ぶ API |
| --- | --- |
| 図形（装飾・ハンドル・始点マーカー） | `MarqueeDrawPlan.Layers` の順に自 UI の描画 API を呼ぶ |
| テキスト（サイズ・カーソル座標） | `MarqueeDrawPlan.Labels`（`MarqueeLabelPlacement`。 実寸を反映済み）を要素へ写す、または自 UI の描画 API で文字として描く |
| 付加情報ラベル（操作・情報）の配置 | コントロール固有の付加情報であるため本層は関知しない（組み込み先のホストが文言と基準点を決める） |

### 3.2 ラベル要素（`MarqueeLabelPlacement`）

| 項目 | 内容 |
| --- | --- |
| `DipRect` | 要素を置く矩形（背景の大きさ = 文字の実寸 + 内側余白 × 2。 領域クランプ適用済み） |
| `PaddingX` / `PaddingY` | 要素の内側余白（外観の値。 要素の `Padding` にそのまま写す） |
| `Text` / `FontSize` / `TextColor` / `BackgroundColor` | 文言・フォントサイズ・色は外観（`MarqueeAppearance`）と本層（文言の生成）が与える（UI は判断しない） |
| 角丸 | 持たない（選択矩形のラベルは矩形である） |

### 3.3 付加情報ラベルの体裁

- 背景は基準点（`anchor`）を左上とし、文字の実寸へ内側余白（`padding`）を四方へ加えた大きさになります。
- 文字は同じ余白だけ内側へ入ります（**背景の四方の余白が等しい**）。
- 縦積みの行送りは「行の高さ + 内側余白 × 2」です（背景の下端と次の行の上端が一致します）。
- 文言・表示可否はホストが判断します（本層は文言を生成しません）。

## 4. 使用例

```csharp
using EsUtil.Feature.Marquee;

// 外観（色・フォント・枠線の太さ・表示可否）を差し替える
var appearance = MarqueeAppearance.Default with
{
    OuterBorderColor = Color.FromArgb(0xFF, 0x33, 0x99, 0xFF),
    ShowOverlay = false,
};

var presenter = new MarqueePresenter(new MarqueeCore(effects))
{
    Appearance = appearance,
    DisplayScaleScaling = 1.0,
};

// 描画計画（UI が見る唯一の描画値）を解決する
var plan = presenter.ResolveDrawPlan(new EsSize(800, 600), MeasureText);

// UI は Layers の順に自 UI の描画 API を呼び、Labels を要素として配置する
foreach (var layer in plan.Layers) { /* Decoration → Handles → Marker → Label */ }
```

## 5. 制限事項

- 描画そのものは提供しません（描画計画というデータまでを返し、描画は各 UI が自 UI の API で行う）。
- 文字の折り返し・複数行・文字揃えは扱いません（1 行の左揃えのみ。 カーソル座標ラベルは左端・上端、サイズラベルは注目矩形の左上から枠線の幅（+ 1 DIP）だけ内側へ寄せた点）。
- 破線・クリップ・不透明度の指定は扱いません（描画計画は矩形と色までの情報を持つ）。
- ホイール操作（どの入力がズーム・パンか）は提供しません（コントロール化を前提とした処理のため UI 層が持ちます）。
