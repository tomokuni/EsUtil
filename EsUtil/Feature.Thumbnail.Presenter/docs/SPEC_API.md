# EsUtil.Feature.Thumbnail.Presenter API リファレンス

サムネイルの**外観・影・描画計画**を単一所有する UI 非依存の提示層が提供する公開 API のリファレンスです。  
使い方（概要・差し替えの例）は [`../README.md`](../README.md)、アーキテクチャと設計判断は [`SPEC_System.md`](SPEC_System.md) を参照してください。

## 目次 <!-- omit from toc -->

- [1. 前提条件](#1-前提条件)
- [2. 公開 API](#2-公開-api)
  - [2.1 名前空間 `EsUtil.Feature.Thumbnail`](#21-名前空間-esutilfeaturethumbnail)
  - [2.2 `ThumbnailPresenter`（提示のファサード）](#22-thumbnailpresenter提示のファサード)
  - [2.3 `ThumbnailAppearance`（外観）](#23-thumbnailappearance外観)
  - [2.4 `ThumbnailDrawPlan`（1 件分の描画計画）](#24-thumbnaildrawplan1-件分の描画計画)
  - [2.5 `ThumbnailLabelPlan`（名前の計画）](#25-thumbnaillabelplan名前の計画)
  - [2.6 `ThumbnailDrawLayer`（層の順序）](#26-thumbnaildrawlayer層の順序)
  - [2.7 `IThumbnailShadowPlan`（影の算法の差し替え点）](#27-ithumbnailshadowplan影の算法の差し替え点)
  - [2.8 `ThumbnailShadowPlan`（既定の影の計画）](#28-thumbnailshadowplan既定の影の計画)
  - [2.9 影の値型](#29-影の値型)
- [3. 使用例](#3-使用例)
- [4. 座標系と単位](#4-座標系と単位)
- [5. 制限事項](#5-制限事項)

## 1. 前提条件

- 本層は UI フレームワークに依存しません（依存は `EsUtil.Feature.Thumbnail.Core` と `EsUtil.Platform.Core` のみ）。 描画 API の呼び出しは UI 層が行います。
- 単位は **DIP** です。 影の帯だけは**物理ピクセル**で返します（画素単位の濃さを表すため）。
- 色は `uint` の `0xAARRGGBB` で表します（`0` は「色を指定しない」の意味になります。 項目ごとに意味が異なるため §2 で述べます）。

## 2. 公開 API

可視性は public のみを記載します。 公開面はすべてルート名前空間 `EsUtil.Feature.Thumbnail` に属します。

### 2.1 名前空間 `EsUtil.Feature.Thumbnail`

| 型 | 種別 | 説明 |
| --- | --- | --- |
| `ThumbnailPresenter` | class（継承可） | 提示のファサード（配置の解決・描画計画の解決・外観の保持） |
| `ThumbnailAppearance` | sealed record | 外観（色・名前の体裁・影の有無） |
| `ThumbnailDrawPlan` | readonly record struct | 1 件分の描画計画（矩形・層・状態・影・名前） |
| `ThumbnailLabelPlan` | readonly record struct | 名前の計画（文言・矩形・体裁） |
| `ThumbnailDrawLayer` | enum | 描く層とその順序 |
| `IThumbnailShadowPlan` | interface | 影の算法の差し替え点 |
| `ThumbnailShadowPlan` | sealed class | 既定の影の計画（実測値） |
| `ThumbnailShadowEdge` | sealed class | 1 辺分の影の計画 |
| `ThumbnailShadowSide` | enum | 影を落とす辺 |
| `ThumbnailShadowStop` | readonly record struct | グラデーションの停止点 |
| `ThumbnailShadowBand` | readonly record struct | 影を塗る 1 画素分の帯（物理ピクセル） |
| `ThumbnailShadowInsets` | readonly record struct | 影の余白（4 辺） |

### 2.2 `ThumbnailPresenter`（提示のファサード）

| メンバー | 種別 | 説明 |
| --- | --- | --- |
| `ThumbnailPresenter(ThumbnailOptions? options = null, ThumbnailAppearance? appearance = null)` | ctor | 生成（省略時は各 `Default`） |
| `Options` | property | 計算に必要な設定。 null を設定すると `ThumbnailOptions.Default` になる |
| `Appearance` | property | 外観。 null を設定すると `ThumbnailAppearance.Default` になる |
| `ResolveLayout(double availableWidth, double imageWidth, ReadOnlySpan<double> imageHeights)` | method | 配置を解決する。 **影の余白（`Insets`）が `SurroundExtent` より広い場合は余白を広げて解く** |
| `ResolveDrawPlan(ThumbnailLayoutResult layout, int index, string text, bool isCurrent, int selectedIndex, int hoveredIndex, bool isDark)` | virtual method | 1 件分の描画計画を解決する。 **添字が範囲外の場合は空の計画** |
| `ImageHeight(double width, int pixelWidth, int pixelHeight)` | static method | 縦横比を保つ画像の高さ（コアの `ThumbnailLayout.ImageHeight` へ委譲） |
| `Log` | property | ログの出力先（null は `NullLogger`）。 設定・外観の変更を本クラスが記録する。 初回の設定時に生成を 1 度だけ記録する |
| `LogPrefix` | property | ログの接頭辞（null または空白は既定値 `[Thumbnail]` へ戻す） |

**`ResolveDrawPlan` が決めること**:

- 背景色 … 選択中かつ `ShowSelectionBackground` なら選択の背景、そうでなくホバー中かつ `ShowHoverBackground` ならホバーの背景、それ以外は 0（描かない）。 **選択はホバーより優先**します。
- 影 … 強調（ホバー・選択）中かつ `HideShadowWhenHighlighted` なら `null`、それ以外は `Appearance.Shadow`。
- 名前 … `Label` の矩形と `LabelFontSize`。 **現在表示中の項目かつ `BoldCurrentLabel` なら太字**。 強調中は `HighlightedLabelArgb`（0 の場合は UI の標準色）。
- 層 … 背景と影の有無で 4 通りの並びから選ぶ（§2.6）。

`ResolveDrawPlan` は `ArgumentNullException` を送出します（`layout` が null の場合）。

### 2.3 `ThumbnailAppearance`（外観）

不変の値オブジェクトです。 `Default` を基点に `with` 式で部分変更します。

| 項目 | 型 | 既定値 | 補正・意味 |
| --- | --- | --- | --- |
| `LabelFontSize` | `double` | 11 | 0 以下は既定値。 文字サイズ（DIP） |
| `LabelVerticalPadding` | `double` | 1 | 負の値は 0。 名前の行の上下の余白（DIP） |
| `BoldCurrentLabel` | `bool` | `true` | 現在表示中の項目の名前を太字にする |
| `HoverBackgroundLightArgb` | `uint` | `0xFFDADADA` | 明色テーマのホバー背景 |
| `HoverBackgroundDarkArgb` | `uint` | `0xFF303030` | 暗色テーマのホバー背景 |
| `SelectionBackgroundLightArgb` | `uint` | `0x330078D4` | 明色テーマの選択背景（0 は描かない） |
| `SelectionBackgroundDarkArgb` | `uint` | `0x330078D4` | 暗色テーマの選択背景（0 は描かない） |
| `HighlightedLabelArgb` | `uint` | `0` | 強調中の名前の色（0 は UI の標準色） |
| `HideShadowWhenHighlighted` | `bool` | `false` | 強調中は影を描かない |
| `ShowHoverBackground` | `bool` | `true` | ホバーの背景を描く |
| `ShowSelectionBackground` | `bool` | `true` | 選択の背景を描く |
| `Shadow` | `IThumbnailShadowPlan?` | `ThumbnailShadowPlan.Default` | 影の計画（null は影を描かない） |

| メンバー | 種別 | 説明 |
| --- | --- | --- |
| `Default` | static property | すべての項目を既定値にした外観 |
| `HoverBackground(bool isDark)` | method | テーマに応じたホバー背景の色 |
| `SelectionBackground(bool isDark)` | method | テーマに応じた選択背景の色 |

- **等価性**: レコードの値等価性により、同値の外観を再代入しても UI の再描画は発生しません。

### 2.4 `ThumbnailDrawPlan`（1 件分の描画計画）

| メンバー | 型 | 説明 |
| --- | --- | --- |
| `Index` | `int` | 項目の添字 |
| `Slot` | `EsRect` | 項目の外枠（ホバー帯） |
| `Image` | `EsRect` | 画像の矩形 |
| `ShadowArea` | `EsRect` | 影を含む矩形（画像 + 影の余白） |
| `Label` | `ThumbnailLabelPlan` | 名前の計画 |
| `Layers` | `IReadOnlyList<ThumbnailDrawLayer>` | 描く層の順序（状態に応じて間引かれる） |
| `BackgroundArgb` | `uint` | 背景の色（0 は描かない） |
| `Shadow` | `IThumbnailShadowPlan?` | 影の計画（null は描かない） |
| `IsHovered` | `bool` | ホバー中かどうか |
| `IsSelected` | `bool` | 選択中かどうか |
| `IsCurrent` | `bool` | 現在表示中の画像かどうか |
| `IsHighlighted`（導出） | `bool` | `IsHovered ‖ IsSelected` |

- **UI は計画のとおりに描きます**（どの矩形へ何をどの順で描くかは計画が単一所有します）。
- 公開の `readonly record struct` であるため、ホストは `with` 式で部分変更した計画を返せます。

### 2.5 `ThumbnailLabelPlan`（名前の計画）

| メンバー | 型 | 説明 |
| --- | --- | --- |
| `Text` | `string` | 表示する名前 |
| `Bounds` | `EsRect` | 名前の行の矩形（一覧の内容の座標） |
| `FontSize` | `double` | 文字サイズ（DIP） |
| `IsBold` | `bool` | 太字にするかどうか |
| `ForegroundArgb` | `uint` | 文字色（0 は UI の標準色） |
| `VerticalPadding` | `double` | 上下の余白（DIP） |

### 2.6 `ThumbnailDrawLayer`（層の順序）

| 値 | 意味 |
| --- | --- |
| `Background` | 項目の背景（ホバー・選択の帯） |
| `Shadow` | 画像の周囲の影 |
| `Image` | 画像 |
| `Label` | 名前 |

**順序は `Background` → `Shadow` → `Image` → `Label`** です。 影は背景の上・画像の下へ描きます（画像が影を覆い、影の内側が画像の縁の混色の下地になります）。

### 2.7 `IThumbnailShadowPlan`（影の算法の差し替え点）

| メンバー | 種別 | 説明 |
| --- | --- | --- |
| `BlurRadius` | property | 影のぼかしの広がり（DIP） |
| `ColorArgb` | property | 影の色（透過度は停止点が上書きする） |
| `Insets` | property | 画像の周囲へ確保する余白（DIP）。 **レイアウトの項目の大きさへ反映される** |
| `Edges` | property | 4 辺の計画（要素として辺を描く UI が使う） |
| `CornerStops` | property | 4 隅のグラデーションの停止点（隅を 1 つの要素で塗る UI が使う） |
| `BuildDeviceBands(double x, double y, double width, double height, double scaling)` | method | 画像の矩形（DIP）から**1 画素幅の帯**（物理ピクセルの矩形と色）を作る |
| `Expand(EsRect image)` | default method | 影を含む矩形（画像 + 余白）を求める |

- **帯を塗る UI（MewUI / WinUI3 / WinForms）は `BuildDeviceBands` の結果を単色の矩形として塗ります**。 要素で描く UI（AvaloniaUI）は既定の実装に限り `BlurRadius` / `ColorArgb` を使えます。
- **座標系**: 入力は DIP、出力は物理ピクセルです。

### 2.8 `ThumbnailShadowPlan`（既定の影の計画）

| メンバー | 種別 | 説明 |
| --- | --- | --- |
| `ThumbnailShadowPlan()` | ctor | 既定値（実測プロファイル）で生成する |
| `ThumbnailShadowPlan(uint colorArgb, IReadOnlyList<double>? alphaProfile)` | ctor | 色と濃さのプロファイルを指定して生成する。 `alphaProfile` が null の場合は実測値。 **空の場合は `ArgumentException`** |
| `Default` | static property | 既定の計画（4 辺・4 隅・余白が確定済み） |
| `Edge(ThumbnailShadowSide side)` | method | 指定した辺の計画 |
| `AlphaAtDistance(double distance)` | method | 画像の端から `distance` DIP 外側の不透明度 |
| `ColorAtDistance(double distance)` | method | 画像の端から `distance` DIP 外側の色 |

- 濃さのプロファイルは **1 DIP 刻み**で、その長さがぼかしの広がり（`BlurRadius`）になります。
- 4 辺とも同じ濃さで外側へ薄くし、4 隅は**画像の隅の頂点からの斜めの距離**で濃さを決めます。

### 2.9 影の値型

| 型 | メンバー | 説明 |
| --- | --- | --- |
| `ThumbnailShadowEdge` | `Side` / `Extent` / `IsOuterAtStart` / `Stops` / `IsVertical` | 1 辺分の影（幅・濃さの変化・向き）。 生成は `ThumbnailShadowPlan` だけが行う（コンストラクターは `internal`） |
| `ThumbnailShadowSide` | `Left` / `Top` / `Right` / `Bottom` | 影を落とす辺 |
| `ThumbnailShadowStop` | `Offset`（0〜1）/ `Argb` | グラデーションの停止点 |
| `ThumbnailShadowBand` | `X` / `Y` / `Width` / `Height` / `Argb` | 影を塗る 1 画素分の帯（**物理ピクセル**） |
| `ThumbnailShadowInsets` | `Left` / `Top` / `Right` / `Bottom` | 影の余白（DIP） |

## 3. 使用例

```csharp
using EsUtil.Feature.Thumbnail;

var presenter = new ThumbnailPresenter(
    options: ThumbnailOptions.Default with { ItemSpacing = 4 },
    appearance: ThumbnailAppearance.Default with { HighlightedLabelArgb = 0xFF0078D4 });

// 影の算法を差し替える（ここでは影を描かない）
presenter.Appearance = presenter.Appearance with { Shadow = null };

var layout = presenter.ResolveLayout(availableWidth, imageWidth, heights);
var plan = presenter.ResolveDrawPlan(
    layout, index: 3, text: "capture-0003.png", isCurrent: true,
    selectedIndex: 3, hoveredIndex: 1, isDark: false);

// 層の順に描く（描画 API の呼び出しは UI が持つ）
foreach (var layer in plan.Layers)
{
    switch (layer)
    {
        case ThumbnailDrawLayer.Background:
            Fill(plan.Slot, plan.BackgroundArgb);
            break;
        case ThumbnailDrawLayer.Shadow:
            foreach (var band in plan.Shadow!.BuildDeviceBands(plan.Image.X, plan.Image.Y, plan.Image.Width, plan.Image.Height, scaling))
            {
                FillPixels(band);
            }

            break;
        case ThumbnailDrawLayer.Image:
            DrawImage(plan.Image);
            break;
        case ThumbnailDrawLayer.Label:
            DrawLabel(plan.Label);
            break;
    }
}
```

## 4. 座標系と単位

- 矩形（`Slot` / `Image` / `ShadowArea` / `Label.Bounds`）は**一覧の内容の左上を原点とする DIP** です（スクロール位置は UI が加えます）。
- `ThumbnailShadowBand` だけは**物理ピクセル**です（`BuildDeviceBands` の `scaling` で DIP から換算されます）。
- 色は `0xAARRGGBB` です。

## 5. 制限事項

- **本層は描画 API を持ちません**（キャンバスの抽象も作りません）。 帯を塗る・画像を描く・文字を置くのは UI 層です。
- **テキストの実寸測定は行いません**（`FontSize` と矩形を渡すだけです。 実寸は描画 API でしか測れません）。
- **影の濃さは実測値です**（`ThumbnailShadowPlan` の既定は AvaloniaUI の `BoxShadow` を写した値。 別の見た目にする場合は `IThumbnailShadowPlan` を差し替えます）。
- **`Edges` / `CornerStops` は要素で描く UI のための値です**（帯を塗る UI は使いません）。
