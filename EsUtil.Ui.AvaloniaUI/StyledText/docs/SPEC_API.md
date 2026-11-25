# EsUtil.Ui.AvaloniaUI.StyledText API リファレンス

書式付き文字列を表示し、その場で編集する Avalonia のコントロールです。 折り返し・行の配置・キャレットの位置・削除の単位は `EsUtil.Feature.StyledText.Core` が単一所有します。

## 目次

- [1. `StyledTextControl`（クラス）](#1-styledtextcontrolクラス)
- [2. 編集の API](#2-編集の-api)
- [3. 制限事項](#3-制限事項)
- [4. 書式バー（`StyledTextFormatBar`）](#4-書式バーstyledtextformatbar)

## 1. `StyledTextControl`（クラス）

| メンバー | 説明 |
| --- | --- |
| `RichTextValue` | 表示・編集する本文（`RichText`。 run 単位の書式） |
| `MeasureText` | 文字列と書式から実寸を測る関数（既定は描画と同じエンジンで測る） |
| `LineHeightFactor` | 行間の倍率（0 以下・非有限は 1） |
| `RenderScale` | 元画像ピクセルから画面の DIP への倍率（0 以下・非有限は 1） |
| `Editor` | 本文の値と編集の状態（`TextEditor`。 キャレット・選択・以降の入力へ効かせる書式を含む）。 ホストと共有できる |
| `ShowFormatBarWhileEditing` | 編集中に書式バーを自動で表示するか（既定は `true`）。 ホストが独自にバーを出す場合は `false` |
| `ShouldShowFormatBar` | いま書式バーを表示すべきか（編集中・自動表示が有効・1 文字以上を選択） |
| `PreeditText` | IME の変換中の文字列（本文へは入れず、キャレットの位置へ描く） |
| `IsInputEnabled` | 入力を受け付けるか（既定は受け付ける）。 表示専用にする場合は `false`（ホストが入力とフォーカスを持つ場合） |
| `RichTextChanged` / `SelectionChanged` / `EditingChanged` | 本文・選択範囲・編集の開始と終了の通知 |

- **座標は元画像ピクセル**です。 `RenderScale` を掛けて DIP へ写します。
- **本文の値と編集の状態は `EsUtil.Feature.StyledText.Core` の `TextEditor` が単一所有**します。 編集は新しい `RichText` を `RichTextValue` へ書き戻し、`RichTextChanged` を発生させます。
- **編集器を 2 つ持たない**ため、ホストが本文と状態を持っている場合は `Editor` に同じ実体を設定します。
- **書式バーは 1 文字以上を選んだときだけ出す**（自動表示が有効でも、適用先が決まらない間は出さない）。

## 2. 編集の API

| メンバー | 説明 |
| --- | --- |
| `IsEditing` / `BeginEditing()` / `EndEditing()` | 編集の状態（開始時のキャレットは本文の末尾）。 本文を押すと編集が始まり、押した位置へキャレットを置く |
| `CaretIndex` / `SelectionStart` / `SelectionEnd` / `SelectionLength` / `HasSelection` | キャレットと選択 |
| `Select(start, length)` / `SelectAll()` | 選択範囲の指定（端は書記素クラスタの境界へ揃える） |
| `MoveCaret(index, extend)` | キャレットの移動（書記素クラスタの境界へ丸める） |
| `MoveCaretByCluster(direction, extend)` | 前後の 1 クラスタへ移動（選択は端へ畳む） |
| `MoveCaretByLine(delta, extend)` / `MoveCaretToLineEdge(first, extend)` | 上下の行・行の先頭と末尾 |
| `SetCaretFromPoint(point, extend)` | 位置（元画像ピクセル）に最も近い文字位置 |
| `ReplaceSelection(text)` / `ReplaceRange(start, end, text)` | 文字列の挿入・置き換え |
| `DeleteAtCaret(forward)` | 選択範囲、または前後の 1 クラスタの削除 |
| `ApplyTextStyle(style)` / `ApplyTextStyle(start, length, style)` | 選択範囲（指定範囲）の書式の差し替え |
| `TransformTextStyle(transform)` / `TransformTextStyle(start, length, transform)` | 1 属性だけの変換 |
| `SelectedTextStyle` / `SelectedTextMatches(match)` | 書式バーへ表示する値・押下状態 |
| `GetSelectedText()` / `CopyAsync()` / `CutAsync()` / `PasteAsync()` | 選択文字とクリップボード（`PasteAsync` は貼り付けた文字列を返し、無い場合は `null`） |
| `CaretRect` | キャレットの矩形（DIP。 IME の変換候補の位置） |
| `RefreshCaretBlink()` | キャレットの点滅を点灯から始め直す（ホストが入力を所有する場合に呼ぶ） |

- **選択が無いときの書式変更は以降の入力へ回します**（既存の文字は変えない）。
- **キー入力は同じ API を呼びます**（キーの割り当ては `EsUtil.Feature.StyledText.Core` の `TextEditKeyMap` が単一所有する）。

## 3. 制限事項

- **IME は制御できません**（変換候補の表示位置と内容は OS と入力方式が決める）。 本コントロールは位置と文字列を渡すだけです。
- **キャレットと選択範囲の色・太さは変更できません**（`EsUtil.Feature.StyledText.Core` の `TextEditorAppearance` が単一所有する。 4 つの UI 版で同じ見た目にするため）。
- **書式は run 単位**です（文字単位の任意の属性は持たない）。

## 4. 書式バー（`StyledTextFormatBar`）

本文を編集中、**1 文字以上を選んだとき**に本文の上へ出す**文字単位の書式**のバーです。 ホストは `View` をパネルへ載せ、本文の選択が変わったら `Refresh()` を呼び、選択が無くなったら閉じます（表示の条件は `EsUtil.Feature.StyledText.Presenter.Formatting.TextFormatBarVisibility` が単一所有し、`StyledTextControl.ShouldShowFormatBar` が公開する）。

| メンバー | 説明 |
| --- | --- |
| `View` / `PreferredWidth` | バーの画面要素と幅（DIP。 置く位置の計算に使う） |
| `TextFormat` | 色の入力欄へ出す表記（色名 + 不透明度 / カラーコード） |
| `Refresh()` | 現在の選択範囲の書式を表示へ反映する |
| `SetKeyHandler(forward)` / `SetFocusRequest(request)` | バーがフォーカスを持つ間のキー転送と、操作後に本文へフォーカスを戻す処理（**4 つの UI 版で同じ挙動**。 バー内の入力欄では転送しない） |
| `ITextFormatBarContext`（計算層） | バーが書式を読み書きする相手（`SelectedTextStyle` / `SelectedTextMatches` / `Transform` / `Apply` / `HasTextSelection`）。 ホストが自分の編集状態へつなぐ |
| `StyledTextFormatBarOptions` | フォントの一覧・色の選択（`ConfigureColorSelector` / `CreateColorButton` / `CreateColorSelector`）・色の表記 |

**差し替えの入口**: ホストは `IStyledTextFormatBarFactory` を実装し、`IStyledTextFormatBar`（`View` / `Refresh` / `TextFormat` / キーとフォーカスの設定）を返します。 既定は `StyledTextFormatBarFactory`（組み込みのバー）です。 差し替えはバーを出す前に行います（表示中の入れ替えは位置と操作の対象が食い違うため）。
