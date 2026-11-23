# EsUtil.Ui.WinForms.Annotation API リファレンス

WinForms 版の注釈レイヤーが公開する API の一覧です。 操作と描画の規則は Avalonia UI 版と同じで、本アセンブリは GDI+ へ写す層です。 判断は [`EsUtil.Feature.Annotation`](../../../EsUtil/Feature.Annotation.Core/README.md) が単一所有します。

## 目次

- [1. `AnnotationTextEditor`（クラス）](#1-annotationtexteditorクラス)
- [2. `AnnotationLayerControl`（クラス）](#2-annotationlayercontrolクラス)
- [3. `AnnotationPalette`（静的クラス）](#3-annotationpalette静的クラス)
- [4. `AnnotationFonts`（静的クラス）](#4-annotationfonts静的クラス)

## 1. `AnnotationTextEditor`（クラス）

本文の表示・編集と書式バーを 1 つにまとめた `UserControl` です。 ホストはこれ 1 つを置きます。

| メンバー | 型 | 説明 |
| --- | --- | --- |
| `Layer` | `AnnotationLayerControl` | 画像・文書・選択を操作するレイヤー |
| `IsEditingText` | `bool` | 本文を編集中かどうか |
| `TextFormat` | `ColorTextFormat` | 書式バーの色の表記（保持だけする） |
| `ShowStylePanel()` / `CloseStylePanel()` | `void` | 外観・重なり順の編集パネルの表示と終了（右クリックと同じ） |

## 2. `AnnotationLayerControl`（クラス）

画像の表示・注釈の描画・入力の受付を担う `Control` です。 `Source` が `null` でも注釈だけを描きます。

| メンバー | 説明 |
| --- | --- |
| `Source` / `SourcePixelWidth` / `SourcePixelHeight` | 表示する画像と元画像の画素数 |
| `Document` / `SelectedId` / `Selected` | 注釈の文書と選択 |
| `DraftDefaults` | 新規作成時の外観・書式 |
| `ColorTextFormat` | 書式バーの色の表記 |
| `AddFrame(bounds)` / `AddBubble(bounds, text)` | 注釈の追加（追加した識別子を返す） |
| `DeleteSelected()` / `SetSelectedText(text)` | 削除と本文の置き換え |
| `ApplyTextStyle(...)` / `TransformTextStyle(...)` | 選択範囲の書式の差し替え・属性だけの変更 |
| `SelectedTextMatches(match)` | 選択範囲のすべての文字が条件を満たすか |
| `UpdateSelectedAppearance(...)` / `UpdateSelectedTail(...)` / `SetSelectedBodyPadding(...)` | 外観・しっぽ・本文の余白の変更 |
| `MoveSelectedToFront()` / `MoveSelectedToBack()` | 重なり順の変更 |
| `Undo()` / `Redo()` / `CanUndo` / `CanRedo` | 元に戻す・やり直す（1 操作 = 1 手） |
| `BeginTextEditing()` / `EndTextEditing()` / `IsEditingText` | 本文のその場編集の開始・終了（入力欄を重ねず、キャレットと選択範囲を描く） |
| `TextSelectionStart` / `TextSelectionLength` / `TextSelectionRange` | 本文の選択範囲（文字数） |
| `GetSelectedBodyRect()` | 本文の矩形（DIP。 書式バーの位置を決める） |
| `RenderComposedImage()` | 画像と注釈を 1:1 で合成した画像（保存用） |
| `DocumentChanged` / `SelectionChanged` / `EditingChanged` / `TextSelectionChanged` / `StyleEditorRequested` | 変更・選択・編集開始終了・編集パネルの要求の通知 |

**元に戻す・やり直す**: 文書を複製して積みます（`AnnotationHistory` が単一所有）。 ドラッグは押下から離すまでで 1 手です。 本文の編集は入力のたびに 1 手として積みます。

## 3. `AnnotationPalette`（静的クラス）

代表色（40 色。 4 つの UI 版で同じ色名・同じ値）を提供します。 WinForms には色選択コントロールが無いため、色のボタンは代表色のメニューと色ダイアログで選びます。

| メンバー | 説明 |
| --- | --- |
| `RepresentativeColors` | 代表色（色名と色） |

## 4. `AnnotationFonts`（静的クラス）

| メンバー | 説明 |
| --- | --- |
| `All` | 選べるフォントの一覧（`AnnotationDefaults.PreferredFontFamilies` を先頭に置く） |
