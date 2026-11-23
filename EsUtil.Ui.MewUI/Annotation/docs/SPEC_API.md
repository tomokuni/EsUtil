# EsUtil.Ui.MewUI.Annotation API リファレンス

MewUI 版の注釈レイヤーが公開する API の一覧です。 操作と描画の規則は Avalonia UI 版（[`../../../EsUtil.Ui.AvaloniaUI/Annotation/docs/SPEC_API.md`](../../../EsUtil.Ui.AvaloniaUI/Annotation/docs/SPEC_API.md)）と同じで、本アセンブリは MewUI の描画 API へ写す層です。 判断（当たり判定・折り返し・描画計画）は [`EsUtil.Feature.Annotation`](../../../EsUtil/Feature.Annotation.Core/README.md) が単一所有します。

## 目次

- [1. `AnnotationTextEditor`（クラス）](#1-annotationtexteditorクラス)
- [2. `AnnotationLayerControl`（クラス）](#2-annotationlayercontrolクラス)
- [3. `AnnotationEditorControls`（クラス）](#3-annotationeditorcontrolsクラス)
- [4. `AnnotationPalette`（静的クラス）](#4-annotationpalette静的クラス)
- [5. `AnnotationFonts`（静的クラス）](#5-annotationfonts静的クラス)

## 1. `AnnotationTextEditor`（クラス）

本文の表示・その場編集と書式バーを 1 つにまとめた `Panel` です。 ホストはこれ 1 つを置きます。

| メンバー | 型 | 説明 |
| --- | --- | --- |
| `Layer` | `AnnotationLayerControl` | 画像・文書・選択を操作するレイヤー |
| `IsEditingText` | `bool` | 本文を編集中かどうか |
| `TextFormat` | `ColorTextFormat` | 書式バーとフライアウトの色の表記（設定へ追随させる） |
| `Log` / `LogPrefix` | `ILogger` / `string?` | ログの出力先と接頭辞（コアと提示層で共有する） |
| `Presenter` | `AnnotationPresenter` | 描画計画を解決する提示層（ログと接頭辞を共有する） |
| `ShowStylePanel()` / `CloseStylePanel()` | `void` | 外観・重なり順の編集パネルの表示と終了（右クリックと同じ） |

## 2. `AnnotationLayerControl`（クラス）

画像の表示・注釈の描画・入力の受付を担う `Panel` です。 `Source` が `null` でも注釈だけを描きます。

| メンバー | 説明 |
| --- | --- |
| `Source` / `SourcePixelWidth` / `SourcePixelHeight` | 表示する画像と元画像の画素数 |
| `Document` / `SelectedId` / `Selected` | 注釈の文書と選択 |
| `DraftDefaults` | 新規作成時の外観・書式（既存の注釈は各注釈の値で描く） |
| `ColorTextFormat` | 書式バー・フライアウトの色の表記 |
| `Log` / `LogPrefix` | ログの出力先と接頭辞（コアと提示層で共有する） |
| `Presenter` | 描画計画を解決する提示層（ログと接頭辞を共有する） |
| `AddFrame(bounds)` / `AddBubble(bounds, text)` | 注釈の追加（追加した識別子を返す） |
| `DeleteSelected()` / `SetSelectedText(text)` | 削除と本文の置き換え |
| `ApplyTextStyle(...)` / `TransformTextStyle(...)` | 選択範囲の書式の差し替え・属性だけの変更 |
| `SelectedTextMatches(match)` | 選択範囲のすべての文字が条件を満たすか（書式バーの押下状態） |
| `UpdateSelectedAppearance(...)` / `UpdateSelectedTail(...)` / `SetSelectedBodyPadding(...)` | 外観・しっぽ・本文の余白の変更 |
| `MoveSelectedToFront()` / `MoveSelectedToBack()` | 重なり順の変更 |
| `Undo()` / `Redo()` / `CanUndo` / `CanRedo` | 元に戻す・やり直す（1 操作 = 1 手） |
| `ShowStyleEditor()` | 外観・重なり順の編集パネルを要求する（`StyleEditorRequested` を発火する） |
| `BeginTextEditing()` / `EndTextEditing()` / `IsEditingText` | 本文のその場編集の開始・終了 |
| `CaretIndex` / `SelectionStart` / `SelectionEnd` / `HasTextSelection` | キャレットと選択範囲 |
| `GetSelectedBodyRect()` | 本文の矩形（元画像ピクセル。 書式バーの位置を決める） |
| `RenderComposedImage()` | 画像と注釈を 1:1 で合成した画像（保存用。 選択枠・キャレットは描かない） |
| `DocumentChanged` / `SelectionChanged` / `EditingChanged` / `TextSelectionChanged` / `StyleEditorRequested` | 変更・選択・編集開始終了・編集パネルの要求の通知 |

**元に戻す・やり直す**: 文書を複製して積みます（`AnnotationHistory` が単一所有）。 ドラッグは押下から離すまでで 1 手です。 元に戻した後も**選択は識別子で引き継ぎます**。

## 3. `AnnotationEditorControls`（クラス）

編集 UI の行の部品（見出し・項目名・色・数値・選択肢・装飾の切り替え）を単一所有します。 書式バーと外観パネルが共有します。

| メンバー | 説明 |
| --- | --- |
| `Heading(text)` / `Label(text)` | 見出しと項目名 |
| `ColorRow(...)` / `CreateSelector(...)` | 色の行と色選択コントロールの生成（代表色のタブは `AnnotationPalette` が決める） |
| `NumberRow(...)` | 数値の行（範囲・刻み・整数の指定） |
| `ChoiceRow(...)` | 選択肢の行 |
| `Toggle(...)` | 装飾の切り替え（太字・斜体） |
| `ToHex(color)` | 設定が持つ色の文字列（`#AARRGGBB`）への変換 |

## 4. `AnnotationPalette`（静的クラス）

代表色（40 色）と、色選択コントロールへ渡すタブを用意します。

| メンバー | 説明 |
| --- | --- |
| `RepresentativeColors` | 代表色（40 色） |
| `CreatePaletteTab(columns)` | ■ だけを並べた「代表色」のタブ |
| `Configure(selector, ...)` / `ConfigureAll(selector, ...)` | 出すタブの選択（書式バーはテーマ・色名を出さず、設定画面はすべて出す） |

## 5. `AnnotationFonts`（静的クラス）

| メンバー | 説明 |
| --- | --- |
| `All` | 選べるフォントの一覧（`AnnotationDefaults.PreferredFontFamilies` を先頭に置く） |
