# EsUtil.Ui.MewUI.Thumbnail API リファレンス

MewUI 版のサムネイル一覧コントロールが提供する公開 API のリファレンスです。  
使い方（概要・クイックスタート）は [`../README.md`](../README.md)、構造と設計判断は [`SPEC_System.md`](SPEC_System.md) を参照してください。

## 目次 <!-- omit from toc -->

- [1. 前提条件](#1-前提条件)
- [2. 公開 API](#2-公開-api)
  - [2.1 名前空間 `EsUtil.Ui.MewUI.Thumbnail`](#21-名前空間-esutiluimewuithumbnail)
  - [2.2 `ThumbnailControl` のメンバー](#22-thumbnailcontrol-のメンバー)
  - [2.3 `ThumbnailActivatedEventArgs`（2 回押しの通知）](#23-thumbnailactivatedeventargs2-回押しの通知)
- [3. 使用例](#3-使用例)
- [4. 座標系と単位](#4-座標系と単位)
- [5. 制限事項](#5-制限事項)

## 1. 前提条件

- 対象フレームワークは `net10.0` で、`Aprillz.MewUI.Core` を参照します（MewUI は API が安定化中（0.x）のため、バージョンは本体のプロジェクト ファイルが固定します）。
- 画像は **`EsReadOnlyBitmap`**（コアの読み取り専用画像型）で受け取ります。 **ピクセルは複製しません**（非所有ビューとして描画します）。
- 配置・選択・見た目は `EsUtil.Feature.Thumbnail.Core` と `EsUtil.Feature.Thumbnail.Presenter` が単一所有します。
- 単位は **DIP** です。

## 2. 公開 API

可視性は public のみを記載します（`internal` は [`SPEC_System.md`](SPEC_System.md) の可視性ポリシーを参照）。  
公開面はすべて名前空間 `EsUtil.Ui.MewUI.Thumbnail` に属します。

### 2.1 名前空間 `EsUtil.Ui.MewUI.Thumbnail`

| 型 | 種別 | 説明 |
| --- | --- | --- |
| `ThumbnailControl` | sealed class（`Panel` を継承） | サムネイル一覧のコントロール（内側にスクロールを持つ） |
| `ThumbnailActivatedEventArgs` | sealed class（`EventArgs`） | 項目が 2 回押されたときの引数 |

**MewUI の `ScrollViewer` は sealed のため継承できません**（AvaloniaUI 版との差）。 そのため一覧は `Panel` を継承し、スクロールと内容を子として持ちます。

### 2.2 `ThumbnailControl` のメンバー

| メンバー | 種別 | 説明 |
| --- | --- | --- |
| `ThumbnailControl()` | ctor | 生成 |
| `ScrollChanged` | event | 一覧がスクロールされたときに発生する |
| `SelectionChanged` | event | 選択中の添字が変わったときに発生する |
| `ItemActivated` | event | 項目が 2 回押されたときに発生する（`ThumbnailActivatedEventArgs`） |
| `Presentation` | property（get） | 提示層（設定・外観・描画計画の解決）。 派生した提示を使う場合は `Options` / `Appearance` ではなく本プロパティを設定する |
| `Options` | property | 計算に必要な設定（既定は `ThumbnailOptions.Default`）。 設定すると自動で配置を解き直す |
| `Appearance` | property | 外観（既定は `ThumbnailAppearance.Default`）。 設定すると自動で描き直す |
| `IsDarkTheme` | property | 暗色テーマかどうか（既定は false）。 設定すると自動で描き直す |
| `ImageWidth` | property | 画像の幅（DIP。既定は 96）。 **0 以下の指定は既定の下限へ引き上げられる**。 設定すると自動で配置を解き直す |
| `ItemCount` | property（get） | 項目数 |
| `SelectedIndex` | property | 選択中の添字。 範囲外と -1 は選択の解除 |
| `VisibleRange` | property（get） | ビューポートと交差する項目の範囲。 項目が無い場合は空 |
| `SlotBounds(int index)` | method | 項目の外枠の矩形（内容の座標）。 範囲外は `EsRect.Empty` |
| `ClearItems()` | method | 一覧を空にする |
| `Add(EsReadOnlyBitmap? image, string caption, string? toolTip = null, bool isCurrent = false, object? tag = null)` | method | サムネイルを 1 件追加し、追加した添字を返す |
| `SetImage(int index, EsReadOnlyBitmap? image)` | method | 項目の画像を差し替える（null は画像を消す）。 範囲外は何もしない |
| `SetCaption(int index, string caption)` | method | 項目の名前を差し替える |
| `ScrollToIndex(int index)` | method | 項目を表示領域へスクロールする |
| `Refresh()` | method | 配置と見た目を解決し直す（通常は各 setter が行うため、明示的に解き直したいときに呼ぶ） |
| `Log` | property | ログの出力先（null は `NullLogger`）。 画像と項目数の変更を本コントロールが記録する。 同じロガーを提示層・コアへ渡す |
| `LogPrefix` | property | ログの接頭辞（null または空白は既定値 `[Thumbnail]` へ戻す）。 提示層・コアへ伝搬する |

- **`Add` は項目を末尾へ追加します**（画像が null でも項目は作られ、画像の領域だけが空になります）。
- **影は計画の帯を単色で塗ります**（`ThumbnailShadowPlan.BuildDeviceBands` の 1 画素の帯。 グラデーション ブラシの端の扱いに依存しません）。

### 2.3 `ThumbnailActivatedEventArgs`（2 回押しの通知）

| メンバー | 型 | 説明 |
| --- | --- | --- |
| `ThumbnailActivatedEventArgs(int index, object? tag)` | ctor | 生成 |
| `Index` | `int` | 項目の添字 |
| `Tag` | `object?` | 項目へ付けた任意の値（`Add` の `tag`） |

## 3. 使用例

```csharp
using EsUtil.Feature.Thumbnail;
using EsUtil.Ui.MewUI.Thumbnail;

var list = new ThumbnailControl
{
    ImageWidth = 96,
    IsDarkTheme = true,
    Options = ThumbnailOptions.Default with { ItemSpacing = 4 },
};

list.ItemActivated += (_, e) => Open((string)e.Tag!);
list.ScrollChanged += (_, _) => Prefetch(list.VisibleRange);

list.ClearItems();
foreach (var file in files)
{
    list.Add(placeholder, Path.GetFileName(file), toolTip: file, tag: file);
}

// 生成が終わった画像は添字で差し替える
list.SetImage(index, bitmap);

// 見えている項目を先に作る
var visible = list.VisibleRange;
```

## 4. 座標系と単位

- `SlotBounds` の戻り値は**一覧の内容（スクロールする面）の左上を原点とする DIP** です（スクロール位置を含みません）。
- `ImageWidth` と `Options` の長さは DIP です。

## 5. 制限事項

- **画像は `EsReadOnlyBitmap` だけ**です（MewUI の画像型やフレームワークのビットマップ型は受け取りません。 変換はホストが行います）。
- **画像の所有権はホストに残ります**（コントロールはピクセルを複製せず、非所有ビューとして描画します。 コントロールを破棄しても画像は解放されません）。
- **自動テストは未整備**です（ビルドの警告 0 と、利用側アプリの配布物の起動で確認しています）。
- **複数選択は提供しません**（単一選択だけです）。
- **`Options` / `Appearance` / `IsDarkTheme` / `ImageWidth` の変更は自動で反映されます**（各 setter が配置を解き直します）。
