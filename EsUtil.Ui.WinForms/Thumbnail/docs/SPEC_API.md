# EsUtil.Ui.WinForms.Thumbnail API リファレンス

WinForms 版のサムネイル一覧コントロールが提供する公開 API のリファレンスです。  
使い方（概要・クイックスタート）は [`../README.md`](../README.md)、構造と設計判断は [`SPEC_System.md`](SPEC_System.md) を参照してください。

## 目次 <!-- omit from toc -->

- [1. 前提条件](#1-前提条件)
- [2. 公開 API](#2-公開-api)
  - [2.1 名前空間 `EsUtil.Ui.WinForms.Thumbnail`](#21-名前空間-esutiluiwinformsthumbnail)
  - [2.2 `ThumbnailControl` のメンバー](#22-thumbnailcontrol-のメンバー)
  - [2.3 `ThumbnailActivatedEventArgs`（2 回押しの通知）](#23-thumbnailactivatedeventargs2-回押しの通知)
- [3. 使用例](#3-使用例)
- [4. 座標系と単位](#4-座標系と単位)
- [5. 制限事項](#5-制限事項)

## 1. 前提条件

- 対象フレームワークは `net10.0-windows10.0.17763.0` で、`UseWindowsForms` を有効にします。
- 画像は **`System.Drawing.Bitmap`** で受け取ります。 **渡した画像の所有権はコントロールへ移ります**（差し替え時と破棄時に解放します）。
- 配置・選択・見た目は `EsUtil.Feature.Thumbnail.Core` と `EsUtil.Feature.Thumbnail.Presenter` が単一所有します。 本コントロールは入力の受付・スクロール・描画（GDI+）だけを行います。
- 単位は **DIP** です（`AutoScaleMode` は `Dpi` を前提とします）。

## 2. 公開 API

可視性は public のみを記載します（`internal` は [`SPEC_System.md`](SPEC_System.md) の可視性ポリシーを参照）。  
公開面はすべて名前空間 `EsUtil.Ui.WinForms.Thumbnail` に属します。

### 2.1 名前空間 `EsUtil.Ui.WinForms.Thumbnail`

| 型 | 種別 | 説明 |
| --- | --- | --- |
| `ThumbnailControl` | sealed class（`ScrollableControl` を継承） | サムネイル一覧のコントロール（内容を自分で描く） |
| `ThumbnailActivatedEventArgs` | sealed class（`EventArgs`） | 項目が 2 回押されたときの引数 |

### 2.2 `ThumbnailControl` のメンバー

| メンバー | 種別 | 説明 |
| --- | --- | --- |
| `ThumbnailControl()` | ctor | 生成 |
| `SelectionChanged` | event | 選択中の添字が変わったときに発生する |
| `ItemActivated` | event | 項目が 2 回押されたときに発生する（`ThumbnailActivatedEventArgs`） |
| `Options` | property | 計算に必要な設定（既定は `ThumbnailOptions.Default`）。 変更すると配置を解き直す |
| `Appearance` | property | 外観（既定は `ThumbnailAppearance.Default`）。 変更すると描き直す |
| `IsDarkTheme` | property | 暗色テーマかどうか（既定は false）。 変更すると描き直す |
| `ImageWidth` | property | 画像の幅（DIP。既定は 96）。 **0 以下の指定は既定の下限へ引き上げられる** |
| `ItemCount` | property（get） | 項目数 |
| `SelectedIndex` | property | 選択中の添字。 範囲外と -1 は選択の解除 |
| `VisibleRange` | property（get） | ビューポートと交差する項目の範囲。 項目が無い場合は空 |
| `ItemBounds(int index)` | method | 項目の外枠の矩形（内容の座標。 `System.Drawing.Rectangle`）。 範囲外は空の矩形 |
| `ClearItems()` | method | 一覧を空にする（**保持している画像を解放する**） |
| `Add(Bitmap? image, string caption, string? toolTip = null, bool isCurrent = false, object? tag = null)` | method | サムネイルを 1 件追加し、追加した添字を返す（**画像の所有権は本コントロールへ移る**） |
| `SetImage(int index, Bitmap? image)` | method | 項目の画像を差し替える（**所有権は本コントロールへ移り、直前の画像は解放される**）。 範囲外は何もしない |
| `SetCaption(int index, string caption)` | method | 項目の名前を差し替える |
| `ScrollToIndex(int index)` | method | 項目を表示領域へスクロールする |
| `RefreshLayout()` | method | 配置を解き直して描き直す |
| `Log` | property | ログの出力先（null は `NullLogger`）。 画像と項目数の変更を本コントロールが記録する。 同じロガーを提示層・コアへ渡す |
| `LogPrefix` | property | ログの接頭辞（null または空白は既定値 `[Thumbnail]` へ戻す）。 提示層・コアへ伝搬する |

- **`ItemBounds` は `Rectangle`（整数）を返します**（GDI+ の描画にそのまま渡せるようにするため）。 DIP と物理ピクセルの換算はコントロールが行います。
- **`Add` は項目を末尾へ追加します**（画像が null でも項目は作られ、画像の領域だけが空になります）。
- **`Presentation` は公開しません**（提示層はコントロールが内部で保持し、差し替えは `Options` / `Appearance` で行います）。
- **スクロールの通知はありません**（`AutoScroll` に任せ、必要なら `Scroll` イベントを使います）。

### 2.3 `ThumbnailActivatedEventArgs`（2 回押しの通知）

| メンバー | 型 | 説明 |
| --- | --- | --- |
| `ThumbnailActivatedEventArgs(int index, object? tag)` | ctor | 生成 |
| `Index` | `int` | 項目の添字 |
| `Tag` | `object?` | 項目へ付けた任意の値（`Add` の `tag`） |

## 3. 使用例

```csharp
using EsUtil.Feature.Thumbnail;
using EsUtil.Ui.WinForms.Thumbnail;

var list = new ThumbnailControl
{
    ImageWidth = 96,
    IsDarkTheme = true,
    Dock = DockStyle.Fill,
    Options = ThumbnailOptions.Default with { ItemSpacing = 4 },
};

list.ItemActivated += (_, e) => Open((string)e.Tag!);
list.SelectionChanged += (_, _) => UpdatePreview(list.SelectedIndex);

list.ClearItems();
foreach (var file in files)
{
    list.Add(placeholder, Path.GetFileName(file), toolTip: file, tag: file);
}

// 生成が終わった画像は添字で差し替える（渡した画像の所有権はコントロールへ移る）
list.SetImage(index, bitmap);
```

## 4. 座標系と単位

- `ItemBounds` の戻り値は**一覧の内容（スクロールする面）の左上を原点とする DIP** です（スクロール位置を含みません）。
- `ImageWidth` と `Options` の長さは DIP です。

## 5. 制限事項

- **画像は `System.Drawing.Bitmap` だけ**です（コアの画像型は受け取りません。 変換はホストが行います）。
- **画像の所有権はコントロールへ移ります**（渡した画像をホストが解放しないでください。 二重解放になります）。
- **単体テストは `tests/` にあります**（検証の範囲と手順は [`../../.github/検証手順.md`](../../../.github/検証手順.md) を参照）。
- **複数選択は提供しません**（単一選択だけです）。
- **`Options` / `Appearance` の変更は自動で配置を解き直します**（`ImageWidth` も同じ）。 明示的に解き直したいときは `RefreshLayout()` を呼びます。
