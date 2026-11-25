# EsUtil.Ui.MewUI.Annotation API リファレンス

MewUI 版の注釈レイヤーが公開する API の一覧です。 操作と描画の規則は Avalonia UI 版（[`../../../EsUtil.Ui.AvaloniaUI/Annotation/docs/SPEC_API.md`](../../../EsUtil.Ui.AvaloniaUI/Annotation/docs/SPEC_API.md)）と同じで、本アセンブリは MewUI の描画 API へ写す層です。 判断（当たり判定・折り返し・描画計画）は [`EsUtil.Feature.Annotation`](../../../EsUtil/Feature.Annotation.Core/README.md) が単一所有します。

## 目次

- [1. `AnnotationEditorPanel`（クラス）](#1-annotationeditorpanelクラス)
- [2. `AnnotationLayerControl`（クラス）](#2-annotationlayercontrolクラス)
- [3. `AnnotationEditorControls`（クラス）](#3-annotationeditorcontrolsクラス)
- [4. `AnnotationPalette`（静的クラス）](#4-annotationpalette静的クラス)
- [5. 書式バー（`StyledTextFormatBar`）](#5-書式バーstyledtextformatbar)

## 1. `AnnotationEditorPanel`（クラス）

本文の表示・その場編集と書式バーを 1 つにまとめた `Panel` です。 ホストはこれ 1 つを置きます。

| メンバー | 型 | 説明 |
| --- | --- | --- |
| `Layer` | `AnnotationLayerControl` | 画像・文書・選択を操作するレイヤー |
| `IsEditingText` | `bool` | 本文を編集中かどうか（書式バーは 1 文字以上を選んでいる間だけ出る） |
| `ShowFormatBarWhileEditing` | property | 編集中に書式バーを自動で表示するか（既定は `true`。 ホストが独自にバーを出す場合は `false`） |
| `IsFormatBarOpen` | property | 書式バーを表示中かどうか（編集中で 1 文字以上を選んでいる間だけ `true`） |
| `TextFormat` | `ColorTextFormat` | 書式バーとフライアウトの色の表記（設定へ追随させる） |
| `Log` / `LogPrefix` | `ILogger` / `string?` | ログの出力先と接頭辞（コアと提示層で共有する） |
| `CopyTextSelection()` / `CutTextSelection()` / `PasteText()` | `void` | 本文の選択範囲をクリップボードとやり取りする（キーの割り当てと同じ処理） |
| `FormatBarFactory` / `FormatBarOptions` | property | 書式バーの生成（`IStyledTextFormatBarFactory`）と構成（フォント・色・表記） |
| `ShowStylePanel()` / `CloseStylePanel()` / `IsStylePanelOpen` | `void` / `bool` | 外観・重なり順の編集パネルの表示・終了・表示状態（右クリックと同じ） |

- 描画計画の提示層とファサードは `Layer.Presenter` / `Layer.Core` で参照します。

## 2. `AnnotationLayerControl`（クラス）

画像の表示・注釈の描画・入力の受付を担う `Panel` です。 `Source` が `null` でも注釈だけを描きます。

| メンバー | 説明 |
| --- | --- |
| `Source` / `SourcePixelWidth` / `SourcePixelHeight` | 表示する画像と元画像の画素数 |
| `ShowImage` | 画像をレイヤーが描くか（`false` の場合は注釈だけを描く） |
| `PreviewImageRect` / `ImageRectProvider` / `SetPreviewImageRect(rect)` | 画像の表示矩形（`ImageRectProvider` が優先。 動的なズーム・パンへ追従させる） |
| `HitTestAt(point)` / `ClearSelection()` | 位置が注釈に当たるか / 選択の解除 |
| `Document` / `SelectedId` / `Selected` | 注釈の文書と選択 |
| `DraftDefaults` | 新規作成時の外観・書式（既存の注釈は各注釈の値で描く） |
| `HandleSize` / `HandleHitRadius` | 選択枠のハンドルの一辺の大きさ・掴む半径（DIP） |
| `ColorTextFormat` | 書式バー・フライアウトの色の表記 |
| `Log` / `LogPrefix` | ログの出力先と接頭辞（コアと提示層で共有する） |
| `Presenter` | 描画計画を解決する提示層（ログと接頭辞を共有する） |
| `Core` | 文書・保存形式・ログの入口（ファサード。 アプリは `Core.Load` / `Core.Save` を使う） |
| `AddFrame(bounds)` / `AddBubble(bounds, text)` | 注釈の追加（追加した識別子を返す） |
| `DeleteSelected()` / `SetSelectedText(text)` | 削除と本文の置き換え |
| `ApplyTextStyle(...)` / `TransformTextStyle(...)` | 選択範囲の書式の差し替え・属性だけの変更 |
| `SelectedTextStyle` / `SelectedTextMatches(match)` | 選択範囲の書式（書式バーの現在値）・すべての文字が条件を満たすか（押下状態） |
| `UpdateSelectedAppearance(...)` / `UpdateSelectedTail(...)` / `SetSelectedBodyPadding(...)` | 外観・しっぽ・本文の余白の変更 |
| `BringSelectedToFront()` / `SendSelectedToBack()` | 重なり順の変更 |
| `Undo()` / `Redo()` / `CanUndo` / `CanRedo` | 元に戻す・やり直す（1 操作 = 1 手） |
| `ShowStyleEditor()` | 外観・重なり順の編集パネルを要求する（`StyleEditorRequested` を発火する） |
| `BeginTextEditing()` / `EndTextEditing()` / `IsEditingText` | 本文のその場編集の開始・終了 |
| `CaretIndex` / `SelectionStart` / `SelectionEnd` / `SelectionLength` / `HasTextSelection` / `TextSelectionRange` | キャレットと選択範囲（文字数）。 `TextSelectionRange` は選択位置の写し（長さ 0 は選択なし） |
| `SelectText(start, length)` | 本文の選択範囲を指定する（ホストが本文の選択範囲を写す） |
| `GetSelectedBodyRect()` / `GetSelectedBoundsRect()` | 選択中の本文・注釈の矩形（コアの `EsRect`・DIP）。 本文編集コントロールを重ねる位置と書式バーの位置を決める |
| `RenderComposedImage()` | 画像と注釈を 1:1 で合成した画像（保存用。 選択枠・キャレットは描かない。 生成できなければ `InvalidOperationException`） |
| `InvalidateRender()` | 今の状態で再描画する（ホストが画像の位置や表示倍率を変えたときに呼ぶ） |
| `DocumentChanged` / `SelectionChanged` / `EditingChanged` / `TextSelectionChanged` / `StyleEditorRequested` | 変更・選択・編集開始終了・編集パネルの要求の通知 |

**元に戻す・やり直す**: 変更前の文書をそのまま積みます（`AnnotationHistory` が単一所有。 文書は不変）。 ドラッグは押下から離すまでで 1 手です。 元に戻した後も**選択は識別子で引き継ぎます**。

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

## 5. 書式バー（`StyledTextFormatBar`）

本文を編集中に本文の上へ出す書式バーは **`EsUtil.Ui.MewUI.StyledText` の `StyledTextFormatBar`** が提供します（文字単位の書式）。 注釈はバーを持たず、代表色の並びを渡して使います。

| メンバー | 種別 | 説明 |
| --- | --- | --- |
| `FormatBarFactory` | property | 書式バーを作る処理（`IStyledTextFormatBarFactory`）。 **独自の GUI へ差し替える入口**である |
| `FormatBarOptions` | property | バーの構成（フォントの一覧・色の選択・色の表記） |

バーの API と差し替えの規則は [`../../StyledText/docs/SPEC_API.md`](../../StyledText/docs/SPEC_API.md) が所有します。
