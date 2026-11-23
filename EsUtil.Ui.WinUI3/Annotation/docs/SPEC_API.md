# EsUtil.Ui.WinUI3.Annotation API リファレンス

WinUI 3 版の注釈レイヤーが公開する API の一覧です。 操作と描画の規則は Avalonia UI 版と同じで、本アセンブリは XAML の図形と文字へ写す層です。 判断（当たり判定・折り返し・描画計画）は [`EsUtil.Feature.Annotation`](../../../EsUtil/Feature.Annotation.Core/README.md) が単一所有します。

## 目次

- [1. `AnnotationTextEditor`（クラス）](#1-annotationtexteditorクラス)
- [2. `AnnotationLayerControl`（クラス）](#2-annotationlayercontrolクラス)
- [3. `AnnotationTextRunMeasure`（静的クラス）](#3-annotationtextrunmeasure静的クラス)
- [4. `AnnotationPalette`（静的クラス）](#4-annotationpalette静的クラス)
- [5. `AnnotationFonts`（静的クラス）](#5-annotationfonts静的クラス)

## 1. `AnnotationTextEditor`（クラス）

本文の表示・編集と書式バーを 1 つにまとめた `Grid` です。 ホストはこれ 1 つを置きます。

| メンバー | 型 | 説明 |
| --- | --- | --- |
| `Layer` | `AnnotationLayerControl` | 画像・文書・選択を操作するレイヤー |
| `IsEditingText` | `bool` | 本文を編集中かどうか |
| `TextFormat` | `ColorTextFormat` | 書式バーとフライアウトの色の表記（設定へ追随させる） |
| `Log` / `LogPrefix` | `ILogger` / `string?` | ログの出力先と接頭辞（コアと提示層で共有する） |
| `Presenter` | `AnnotationPresenter` | 描画計画を解決する提示層（ログと接頭辞を共有する） |
| `Core` | `AnnotationCore` | 文書・保存形式・ログの入口（ファサード。 アプリは `Core.Load` / `Core.Save` を使う） |
| `ShowStylePanel()` / `CloseStylePanel()` | `void` | 外観・重なり順の編集パネルの表示と終了（右クリックと同じ） |

## 2. `AnnotationLayerControl`（クラス）

画像の表示・注釈の描画・入力の受付を担う `Grid` です。 `Source` が `null` でも注釈だけを描きます。

| メンバー | 説明 |
| --- | --- |
| `MeasureText` | 本文の実寸を測る関数（既定は文字数からの見積もり。 実アプリは `AnnotationTextRunMeasure.Measure`） |
| `Source` / `SourcePixelWidth` / `SourcePixelHeight` | 表示する画像と元画像の画素数 |
| `Document` / `SelectedId` / `Selected` | 注釈の文書と選択 |
| `DraftDefaults` | 新規作成時の外観・書式 |
| `ColorTextFormat` | 書式バー・フライアウトの色の表記 |
| `Log` / `LogPrefix` | ログの出力先と接頭辞（コアと提示層で共有する） |
| `Presenter` | 描画計画を解決する提示層（ログと接頭辞を共有する） |
| `Core` | 文書・保存形式・ログの入口（ファサード。 アプリは `Core.Load` / `Core.Save` を使う） |
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
| `RenderComposedImage()` | 画像と注釈を 1:1 で合成した画像（保存用。 GDI+ で焼き込む） |
| `DocumentChanged` / `SelectionChanged` / `EditingChanged` / `TextSelectionChanged` / `StyleEditorRequested` | 変更・選択・編集開始終了・編集パネルの要求の通知 |

**元に戻す・やり直す**: 文書を複製して積みます（`AnnotationHistory` が単一所有）。 ドラッグは押下から離すまでで 1 手です。 本文の編集は入力のたびに 1 手として積みます。

## 3. `AnnotationTextRunMeasure`（静的クラス）

WinUI 3 のテキスト エンジン（`TextBlock`）で本文の実寸を測ります。 ホストが `AnnotationLayerControl.MeasureText` へ設定します（UI スレッドから呼ばれます）。

| メンバー | 説明 |
| --- | --- |
| `Measure(text, style)` | 実寸（元画像ピクセル）。 高さは斜体を外した書式で測る |

## 4. `AnnotationPalette`（静的クラス）

代表色（40 色）と、色選択コントロールへ渡すタブを用意します。

| メンバー | 説明 |
| --- | --- |
| `RepresentativeColors` | 代表色（40 色） |
| `CreatePaletteTab(columns)` | 書式バー用の「代表色」のタブ（ColorPicker は別のフラグで出す） |
| `Configure(selector, ...)` / `ConfigureAll(selector, ...)` | 出すタブの選択 |

## 5. `AnnotationFonts`（静的クラス）

| メンバー | 説明 |
| --- | --- |
| `All` | 選べるフォントの一覧（`AnnotationDefaults.PreferredFontFamilies` を先頭に置く） |
