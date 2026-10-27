# EsUtil.Feature.Thumbnail.Core API リファレンス

サムネイル一覧の**配置・選択・可視範囲**を解決する UI 非依存のコアが提供する公開 API のリファレンスです。  
使い方（概要・クイックスタート）は [`../README.md`](../README.md)、アーキテクチャと設計判断は [`SPEC_System.md`](SPEC_System.md) を参照してください。

## 目次 <!-- omit from toc -->

- [1. 前提条件](#1-前提条件)
- [2. 公開 API](#2-公開-api)
  - [2.1 名前空間 `EsUtil.Feature.Thumbnail`](#21-名前空間-esutilfeaturethumbnail)
  - [2.2 `ThumbnailLayout`（純粋計算）](#22-thumbnaillayout純粋計算)
  - [2.3 `ThumbnailLayoutResult`（配置の結果）](#23-thumbnaillayoutresult配置の結果)
  - [2.4 `ThumbnailOptions`（設定）](#24-thumbnailoptions設定)
  - [2.5 `ThumbnailSelection`（単一選択）](#25-thumbnailselection単一選択)
  - [2.6 `ThumbnailRange`（項目の範囲）](#26-thumbnailrange項目の範囲)
- [3. 使用例](#3-使用例)
- [4. 座標系](#4-座標系)
- [5. 制限事項](#5-制限事項)

## 1. 前提条件

- 本コアは UI フレームワークに依存しません（依存は `EsUtil.Platform.Core` のみ）。 UI 固有の型は利用者（各 UI のコントロール）が境界で変換します。
- 座標・寸法は `EsPoint` / `EsSize` / `EsRect`（`EsUtil.Platform.Core`）で扱います。 単位は **DIP**（デバイス非依存ピクセル）です。
- **色・フォントサイズ・影の濃さは含みません**（描画にだけ必要な設定であり、コアの計算・判断に入らないため）。 これらは提示層（`EsUtil.Feature.Thumbnail.Presenter` の `ThumbnailAppearance`）が単一所有します。
- 本コアは**画像を持ちません**。 受け取るのは「画像の高さ（DIP）」だけであり、画素には触れません。

## 2. 公開 API

可視性は public のみを記載します（`internal` は [`SPEC_System.md`](SPEC_System.md) の可視性ポリシーを参照）。  
公開面はすべてルート名前空間 `EsUtil.Feature.Thumbnail` に属します。

### 2.1 名前空間 `EsUtil.Feature.Thumbnail`

| 型 | 種別 | 説明 |
| --- | --- | --- |
| `ThumbnailLayout` | static class | 配置（折り返し・行の高さ・下端揃え）を解決する純粋計算 |
| `ThumbnailLayoutResult` | sealed class | 配置の結果（項目の外枠・画像・名前の矩形、内容の寸法、当たり判定、可視範囲） |
| `ThumbnailOptions` | sealed record | 計算に必要な設定（余白・行の寸法・列数の上限）と既定値・補正 |
| `ThumbnailSelection` | sealed class | 単一選択の状態（選択・解除・変更通知） |
| `ThumbnailRange` | readonly record struct | 項目の連続した範囲（両端を含む） |

### 2.2 `ThumbnailLayout`（純粋計算）

配置を解く唯一の入口です。 状態を持たず、同じ入力からは常に同じ結果を返します。

| メンバー | 種別 | 説明 |
| --- | --- | --- |
| `Resolve(double availableWidth, double imageWidth, ReadOnlySpan<double> imageHeights, ThumbnailOptions? options = null)` | static method | 一覧の配置を解決する。 `availableWidth` は一覧の幅、`imageWidth` は画像の幅、`imageHeights` は項目ごとの画像の高さ（DIP）。 `options` が null の場合は `ThumbnailOptions.Default` |
| `ImageHeight(double width, int pixelWidth, int pixelHeight)` | static method | 幅を固定して縦横比を保つ画像の高さを求める。 画像の寸法が不正（0 以下）な場合は `width` をそのまま返す（正方形） |

- `Resolve` は **`ArgumentOutOfRangeException`** を送出します（`imageHeights` に負の値が含まれる場合）。
- `availableWidth` が 0 以下の場合は **1 列**で並べます（幅の確定後に呼び直す）。
- `imageWidth` が `ThumbnailOptions.MinimumImageWidth` 未満の場合は引き上げます。
- `imageHeights` が空の場合は、列数 1・内容の寸法に余白だけを持つ結果を返します。

### 2.3 `ThumbnailLayoutResult`（配置の結果）

| メンバー | 種別 | 説明 |
| --- | --- | --- |
| `Empty` | static property | 項目が無い配置 |
| `ImageWidth` | property | 画像の幅（DIP） |
| `SlotWidth` | property | 項目の外枠の幅（DIP） |
| `Count` | property | 項目数 |
| `Columns` | property | 列数 |
| `Rows` | property | 行数（`Count` が 0 の場合は 0） |
| `ContentSize` | property | 内容の寸法（DIP。 一覧のスクロール範囲を決める） |
| `Slots` | property | 項目の外枠（ホバー帯）の矩形（並び順） |
| `Images` | property | 画像の矩形（並び順） |
| `Labels` | property | 名前の矩形（並び順） |
| `Slot(int index)` | method | 指定した項目の外枠の矩形。 **範囲外は `EsRect.Empty`** |
| `Image(int index)` | method | 指定した項目の画像の矩形。 **範囲外は `EsRect.Empty`** |
| `Label(int index)` | method | 指定した項目の名前の矩形。 **範囲外は `EsRect.Empty`** |
| `IndexAt(EsPoint position)` | method | 指定した位置にある項目の添字。 **どの項目にも当たらない場合は -1** |
| `VisibleRange(double offsetY, double viewportHeight)` | method | 見えている項目の範囲。 **項目が無い場合は `ThumbnailRange.Empty`** |

- `IndexAt` は**外枠（ホバー帯）で判定**します。 外枠は隙間なく並ぶため、`ItemSpacing` を空けた場合を除いて「どこにも当たらない」帯は生じません。
- `VisibleRange` は**行の下端で最初の行を決め、行の上端がビューポートの下端に達したら打ち切ります**（行の下端・上端は行の順に増えるため）。 行内は下端を揃えるため上端が揃わず、端の行だけは項目ごとに交差を判定します。
- `IndexAt` は外枠の下端で行を求め、**その行の列（`Columns` まで）だけ**を調べます。 下端は行の中で同じ値（行の高さ）で行の順に増えるため行を決められますが、**行内で上端は揃わない**（下端を揃える）ため上端では行を決められません。
- `VisibleRange` はビューポートと**交差する**項目を返します（一部だけ見えている項目を含む）。 高さが 0 以下の場合は、スクロール位置にある項目 1 件を返します（生成の優先度に使えるようにするため）。

### 2.4 `ThumbnailOptions`（設定）

不変の値オブジェクトです。 `Default` を基点に `with` 式で部分変更します。

| 項目 | 型 | 既定値 | 補正 |
| --- | --- | --- | --- |
| `ItemMargin` | `double` | 2 | 負の値は 0 |
| `ItemPadding` | `double` | 12 | 負の値は 0 |
| `ItemSpacing` | `double` | 0 | 負の値は 0 |
| `LabelHeight` | `double` | 30 | 負の値は 0 |
| `LabelHorizontalPadding` | `double` | 1 | 負の値は 0 |
| `SurroundExtent` | `double` | 10 | 負の値は 0 |
| `ContentPadding` | `double` | 2 | 負の値は 0 |
| `MinimumImageWidth` | `double` | 16 | 0 以下は既定値（16） |
| `MaximumColumns` | `int` | 0（無制限） | 0 以下は無制限 |
| `SlotInset`（導出） | `double` | — | `ItemMargin + ItemPadding` と `SurroundExtent` の大きい方 |

- `Default` は「すべての項目を既定値にした設定」です。
- **`SurroundExtent` は長さだけを持ちます**（濃さ・色は提示層の外観が単一所有する）。 影が切れない余白を確保するためにだけ使います。

### 2.5 `ThumbnailSelection`（単一選択）

| メンバー | 種別 | 説明 |
| --- | --- | --- |
| `Changed` | event | 選択が変わったときに発生する（同じ添字の再設定では発生しない） |
| `Index` | property | 選択中の添字（選択が無い場合は -1） |
| `HasSelection` | property | 選択があるかどうか |
| `Select(int index, int count)` | method | 選択を設定する。 負の値と範囲外の添字は**選択の解除**として扱う。 選択が変わった場合は true |
| `Clear()` | method | 選択を解除する。 選択が変わった場合は true |
| `Log` | property | ログの出力先（null は `NullLogger`）。 選択の変更を本クラスが記録する |
| `LogPrefix` | property | ログの接頭辞（null または空白は既定値 `[Thumbnail]` へ戻す） |

### 2.6 `ThumbnailRange`（項目の範囲）

| メンバー | 種別 | 説明 |
| --- | --- | --- |
| `ThumbnailRange(int first, int last)` | ctor | 範囲を生成する（両端を含む） |
| `Empty` | static property | 該当なしの範囲（`First` = 0 / `Last` = -1） |
| `First` | property | 先頭の添字（含む） |
| `Last` | property | 末尾の添字（含む） |
| `IsEmpty` | property | 該当が無いかどうか（`Last < First`） |
| `Count` | property | 含まれる項目数（該当が無い場合は 0） |

## 3. 使用例

```csharp
using EsUtil.Feature.Thumbnail;

// 画像の高さは幅を固定して縦横比から求める
var imageWidth = 96.0;
var heights = files.Select(f => ThumbnailLayout.ImageHeight(imageWidth, f.PixelWidth, f.PixelHeight)).ToArray();

// 幅が未確定（0 以下）なら 1 列で返る。 確定後に呼び直す
var layout = ThumbnailLayout.Resolve(availableWidth: 800, imageWidth, heights);

// 見えている項目だけを先に作る
var visible = layout.VisibleRange(scrollOffset, viewportHeight);
for (var i = visible.First; i <= visible.Last; i++)
{
    CreateTile(i, layout.Slot(i));
}

// 入力は一覧の位置から判定する（子要素のヒット テストに依存しない）
var index = layout.IndexAt(pointerPosition);
if (index >= 0)
{
    selection.Select(index, layout.Count);
}
```

## 4. 座標系

- すべて **DIP** です。 物理ピクセルへの変換は UI 層が行います。
- 矩形の原点は**一覧の内容（スクロールする面）の左上**です。 スクロール位置は含みません（`VisibleRange` の `offsetY` として呼び出し側が渡します）。
- 添字は**並び順**です（0 が先頭）。

## 5. 制限事項

- **幅が未確定（`availableWidth` が 0 以下）の場合は折り返しません**。 幅の確定前の呼び出しは暫定の結果になります。
- **行の高さは行ごとに異なります**（その行の最大アイテム高さ）。 全体で 1 つの行の高さに揃えることはできません（下端揃えで名前の位置を揃えるため）。
- **本コアは画像を保持しません**。 画像の差し替え・解放は UI 層とホストが行います。
- **選択は単一選択だけ**です（複数選択は提供しません）。
